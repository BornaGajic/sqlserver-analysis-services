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

        foreach (var row in adomdDataReader)
        {
            var resultItem = resultTypeAccessor.CreateNew();

            foreach (var resulTypeMember in resultTypeAccessor.GetMembers().Where(m => m.GetAttribute(typeof(DaxNotMappedAttribute), false) is null))
            {
                var memberName = resulTypeMember.GetAttribute(typeof(DaxColumnNameAttribute), false) is DaxColumnNameAttribute columnNameAttribute
                    ? columnNameAttribute.Name
                    : resulTypeMember.Name;

                resultTypeAccessor[resultItem, resulTypeMember.Name] = ChangeType(row[$"[{memberName}]"], resulTypeMember.Type);
            }

            yield return (TResult)resultItem;
        }
    }

    internal static TResult ExecuteScalar<TResult>(this AdomdConnection connection, DaxQuery query, CancellationToken cancellationToken = default)
        => connection.ExecuteQuery<TResult>(query, cancellationToken).SingleOrDefault();

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