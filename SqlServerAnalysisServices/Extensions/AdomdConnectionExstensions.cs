using FastMember;
using SqlServerAnalysisServices.Attribute;
using SqlServerAnalysisServices.Model;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

namespace SqlServerAnalysisServices.Extensions;

internal static class AdomdConnectionExstensions
{
    private static readonly Dictionary<Type, TypeAccessor> TypeAccessorCache = [];

    /// <summary>
    /// Builds an Adomd Command using information found in the query. Applies <paramref name="query"/> params if any.
    /// </summary>
    public static AdomdCommand CreateCommand(this AdomdConnection connection, DaxQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Settings?.Database) && query.Settings.Database != connection.Database)
            connection.ChangeDatabase(query.Settings.Database);

        if (!string.IsNullOrWhiteSpace(query.Settings?.EffectiveUserName))
            connection.ChangeEffectiveUser(query.Settings.EffectiveUserName);

        var cmd = connection.CreateCommand();
        cmd.CommandText = query.Query;

        if (query.Param is not null)
        {
            var paramType = query.Param.GetType();
            var cacheHit = TypeAccessorCache.TryGetValue(paramType, out var typeAccessor);

            if (!cacheHit)
            {
                typeAccessor = TypeAccessor.Create(paramType);
                TypeAccessorCache.Add(paramType, typeAccessor);
            }

            foreach (var member in typeAccessor.GetMembers())
            {
                var excludeParam =
                    member.GetAttribute(typeof(SkipSsasQueryParameter), false) is SkipSsasQueryParameter skipQueryAttribute
                    && (
                        skipQueryAttribute.Condition.HasFlag(SkipSsasQueryParameter.SkipCondition.Skip)
                        || (
                            skipQueryAttribute.Condition.HasFlag(SkipSsasQueryParameter.SkipCondition.SkipIfNull)
                            && typeAccessor[query.Param, member.Name] is null
                        )
                    );

                if (!excludeParam)
                    cmd.Parameters.Add(member.Name, typeAccessor[query.Param, member.Name]);
            }
        }

        return cmd;
    }

    /// <summary>
    /// Deferres the execution of the query until enumerated.
    /// </summary>
    /// <exception cref="OperationCanceledException"></exception>
    internal static IEnumerable<TResult> ExecuteQuery<TResult>(this AdomdConnection connection, DaxQuery query, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        using var cmd = connection.CreateCommand(query);

        cancellationToken.ThrowIfCancellationRequested();
        using var cancellationRegistration = cancellationToken.Register(cmd.Cancel);

        var resultType = typeof(TResult);
        var cacheHit = TypeAccessorCache.TryGetValue(resultType, out var resultTypeAccessor);

        if (!cacheHit)
        {
            resultTypeAccessor = TypeAccessor.Create(resultType);
            TypeAccessorCache.Add(resultType, resultTypeAccessor);
        }

        using var adomdDataReader = cmd.ExecuteReader();

        foreach (var row in adomdDataReader)
        {
            var resultItem = resultTypeAccessor.CreateNew();

            foreach (var resulTypeMember in resultTypeAccessor.GetMembers())
            {
                resultTypeAccessor[resultItem, resulTypeMember.Name] = ChangeType(row[$"[{resulTypeMember.Name}]"], resulTypeMember.Type);
            }

            yield return (TResult)resultItem;
        }
    }

    private static object ChangeType(object value, Type conversion)
    {
        var t = conversion;

        if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
        {
            if (value == null)
            {
                return null;
            }

            t = Nullable.GetUnderlyingType(t);
        }

        return Convert.ChangeType(value, t);
    }
}