namespace Framework.Model
{
    public record XmlaSoapRequest
    {
        public string Request { get; init; }
        public XmlaSoapSettings Settings { get; init; }
    }

    public record XmlaSoapSettings
    {
        public string EffectiveUserName { get; init; }
    }
}