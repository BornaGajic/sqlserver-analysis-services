namespace Framework.Model
{
    public record DaxQuery
    {
        public string Query { get; init; }
        public object Param { get; init; }
        public DaxQuerySettings Settings { get; init; }
    }

    public record DaxQuerySettings
    {
        public string EffectiveUserName { get; init; }
        public string Database { get; init; }
    }
}