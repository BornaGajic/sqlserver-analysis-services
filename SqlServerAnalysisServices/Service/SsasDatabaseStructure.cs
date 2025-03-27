using Dapper;
using SqlServerAnalysisServices.Model;
using System.Data.Common;
using System.Runtime.Caching;
using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Extensions;

namespace SqlServerAnalysisServices.Service;

internal class SsasDatabaseStructure : ISsasDatabaseStructure
{
    private static readonly MemoryCache _cache = new MemoryCache($"{nameof(SsasDatabaseStructure)}");

    public SsasDatabaseStructure(string databaseName, Ssas parent)
    {
        DatabaseName = databaseName;
        Parent = parent;
    }

    public SsasDatabaseDescription Description
    {
        get
        {
            var properties = Properties();

            return new SsasDatabaseDescription
            {
                Id = properties.Id,
                Name = properties.Name,
                LastProcessed = properties.LastProcessedUtc,
                Model = properties.Model,
                Size = properties.Size,
                Tables = TableDescriptions()
            };
        }
    }

    internal string DatabaseName { get; }
    internal Ssas Parent { get; }

    public SsasDatabase Properties()
    {
        using var connection = Parent.GetConnection();
        connection.Open();
        connection.ChangeDatabase(DatabaseName);

        using var server = Parent.GetServer();

        var database = server.Databases.FindByName(DatabaseName);
        database.Refresh();

        return new SsasDatabase
        {
            Id = database.ID,
            Name = database.Name,
            Model = database.Model.Name,
            Size = database.EstimatedSize,
            LastProcessedUtc = database.LastProcessed.ToUniversalTime(),
            LastSchemaUpdateUtc = database.LastSchemaUpdate.ToUniversalTime()
        };
    }

    private ICollection<SsasTableDescription> TableDescriptions(CancellationToken cancellation = default)
    {
        var tablesCacheKey = $"{nameof(TableDescriptions)}:{DatabaseName}";
        var tableDescriptions = _cache.Get(tablesCacheKey) as ICollection<SsasTableDescription>;

        if (tableDescriptions is null)
        {
            using var connection = Parent.GetConnection();
            connection.Open();
            connection.ChangeDatabase(DatabaseName);

            using var server = Parent.GetServer();

            var database = server.Databases.FindByName(DatabaseName);
            database.Refresh();

            var tableRowCounts = connection.Query<SsasTableRowCount>(new CommandDefinition(SsasTableRowCount.TableRowCountQuery, cancellationToken: cancellation));
            var tableSizes = connection.Query<SsasTableSize>(new CommandDefinition(SsasTableSize.TableSizeQuery, cancellationToken: cancellation));
            var partitionSizes = connection.Query<SsasPartitionSize>(new CommandDefinition(SsasPartitionSize.PartitionSizeQuery, cancellationToken: cancellation));
            var partitionRowCounts = connection.Query<SsasPartitionRowCount>(new CommandDefinition(SsasPartitionRowCount.PartitionRowCountQuery, cancellationToken: cancellation));
            var partitionDataSources = connection
                .Query<SsasPartitionDataSources>(new CommandDefinition(SsasPartitionDataSources.Query, cancellationToken: cancellation))
                .GroupBy(pds => pds.PartitionName)
                .ToDictionary(
                    k => k.Key,
                    v => v.Select(pds => pds.DataSourceId).ToList()
                );
            var dataSources = connection
                .Query<SsasDataSource>(new CommandDefinition(SsasDataSource.Query, cancellationToken: cancellation))
                .Select(ds =>
                {
                    var newConnectionStringBuilder = new DbConnectionStringBuilder();
                    var connectionStringBuilder = new DbConnectionStringBuilder
                    {
                        ConnectionString = ds.ConnectionString ?? ""
                    };

                    if (
                        connectionStringBuilder.ConnectionString != ""
                        && connectionStringBuilder.ContainsKey("data source")
                        && connectionStringBuilder.ContainsKey("initial catalog")
                    )
                    {
                        newConnectionStringBuilder["Data Source"] = connectionStringBuilder["data source"];
                        newConnectionStringBuilder["Initial Catalog"] = connectionStringBuilder["initial catalog"];
                    }


                    return ds with
                    {
                        ModifiedTime = ds.ModifiedTime.ToUniversalTime(),
                        ConnectionString = newConnectionStringBuilder.ConnectionString
                    };
                })
                .ToList();

            tableDescriptions = database.Model.Tables
                .WithCancellation(cancellation)
                .Select(table => new SsasTableDescription
                {
                    Id = table.Name,
                    Name = table.Name,
                    ModifiedTime = table.ModifiedTime.ToUniversalTime(),
                    StructureModifiedTime = table.StructureModifiedTime.ToUniversalTime(),
                    RowCount = tableRowCounts.Single(row => row.TableName == table.Name).RowCount,
                    Size = tableSizes.Concat(partitionSizes.OfType<SsasTableSize>()).Where(row => row.TableName == table.Name).Sum(row => row.Size),
                    Partitions = table.Partitions
                        .WithCancellation(cancellation)
                        .Select(partition => new SsasPartitionDescription
                        {
                            Id = partition.Name,
                            Name = partition.Name,
                            Description = partition.Description,
                            ModifiedTime = partition.ModifiedTime.ToUniversalTime(),
                            RefreshedTime = partition.RefreshedTime.ToUniversalTime(),
                            DataSources = partitionDataSources.TryGetValue(partition.Name, out var dataSourceIds)
                                ? dataSources.IntersectBy(dataSourceIds, dataSource => dataSource.Id).OfType<SsasDataSourceDescription>().ToList()
                                : [],
                            RowCount = partitionRowCounts
                                .Where(row =>
                                    row.TableName == table.Name
                                    && row.PartitionName == partition.Name
                                )
                                .Sum(row => row.RowCount),
                            Size = partitionSizes
                                .Where(row =>
                                    row.TableName == table.Name
                                    && row.PartitionName == partition.Name
                                )
                                .Sum(row => row.Size)
                        })
                        .OrderBy(p => p.Name)
                        .ToList()
                })
                .OrderBy(p => p.Name)
                .ToList();

            _cache.Set(tablesCacheKey, tableDescriptions, DateTime.Now.AddMinutes(10));
        }

        return tableDescriptions;
    }
}