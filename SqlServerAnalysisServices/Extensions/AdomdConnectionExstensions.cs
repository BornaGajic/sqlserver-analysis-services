using FastMember;
using SqlServerAnalysisServices.Attribute;
using SqlServerAnalysisServices.Model;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using System.Reflection;

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

        if (query.Settings?.Timeout is not null)
        {
            cmd.CommandTimeout = query.Settings.Timeout.Value;
        }

        if (query.Param is not null)
        {
            var paramType = query.Param.GetType();
            var cacheHit = TypeAccessorCache.TryGetValue(paramType, out var typeAccessor);

            if (!cacheHit)
            {
                typeAccessor = TypeAccessor.Create(paramType);
                TypeAccessorCache.Add(paramType, typeAccessor);
            }

            var skipDaxQueryParamOnClass = paramType.GetCustomAttribute<SkipDaxQueryParameterAttribute>();

            foreach (var member in typeAccessor.GetMembers())
            {
                var excludeParam =
                    (skipDaxQueryParamOnClass ?? member.GetAttribute(typeof(SkipDaxQueryParameterAttribute), false)) is SkipDaxQueryParameterAttribute skipQueryAttribute
                    && (
                        skipQueryAttribute.Condition.HasFlag(SkipDaxQueryParameterAttribute.SkipCondition.Skip)
                        || (
                            skipQueryAttribute.Condition.HasFlag(SkipDaxQueryParameterAttribute.SkipCondition.SkipIfNull)
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
        cmd.Prepare();

        cancellationToken.ThrowIfCancellationRequested();
        using var cancellationRegistration = cancellationToken.Register(cmd.Cancel);

        var resultType = typeof(TResult);

        if (!TypeAccessorCache.TryGetValue(resultType, out var resultTypeAccessor))
        {
            resultTypeAccessor = TypeAccessor.Create(resultType);
            TypeAccessorCache.Add(resultType, resultTypeAccessor);
        }

        using var adomdDataReader = cmd.ExecuteReader();

        var fieldDict = Enumerable.Range(0, adomdDataReader.FieldCount).ToDictionary(
            idx => adomdDataReader.GetName(idx).Trim('[', ']'),
            StringComparer.OrdinalIgnoreCase
        );

        var resultTypeMembers = (
            from member in resultTypeAccessor.GetMembers()
            where member.GetAttribute(typeof(DaxNotMappedAttribute), false) is null
            let nameAttribute = member.GetAttribute(typeof(DaxColumnNameAttribute), false) as DaxColumnNameAttribute
            let ordinal = fieldDict.GetValueOrDefault(nameAttribute is not null ? nameAttribute.Name : member.Name, -1)
            where ordinal != -1
            select (member, ordinal)
        ).ToList();

        foreach (var row in adomdDataReader)
        {
            var resultItem = resultTypeAccessor.CreateNew();

            foreach (var (member, ordinal) in resultTypeMembers)
            {
                resultTypeAccessor[resultItem, member.Name] = ChangeType(row[ordinal], member.Type);
            }

            yield return (TResult)resultItem;
        }
    }

    internal static TResult ExecuteScalar<TResult>(this AdomdConnection connection, DaxQuery query, CancellationToken cancellationToken = default)
        => connection.ExecuteQuery<TResult>(query, cancellationToken).SingleOrDefault();

    private static object ChangeType(object value, Type targetType)
    {
        if (value == null || value == DBNull.Value)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        var actualType = underlyingType ?? targetType;

        if (actualType.IsEnum)
        {
            if (value is string strValue)
            {
                return Enum.Parse(actualType, strValue, ignoreCase: true);
            }

            return Enum.ToObject(actualType, value);
        }

        if (actualType == typeof(Guid) && value is string guidString)
        {
            return Guid.Parse(guidString);
        }

        if (actualType == typeof(DateOnly))
        {
            return value switch
            {
                DateTime dt => DateOnly.FromDateTime(dt),
                string str => DateOnly.Parse(str),
                _ => throw new NotSupportedException($"Failed to convert value of type {value.GetType()} to DateOnly.")
            };
        }

        return Convert.ChangeType(value, actualType);
    }
}