using System.Diagnostics.CodeAnalysis;

namespace SqlServerAnalysisServices.Model
{
    public record DaxQuery
    {
        public string Query { get; init; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)]
        public object Param { get; init; }

        public DaxQuerySettings Settings { get; init; }
    }

    public record DaxQuerySettings
    {
        public string EffectiveUserName { get; init; }
        public string Database { get; init; }
        public int? Timeout { get; init; }
    }
}