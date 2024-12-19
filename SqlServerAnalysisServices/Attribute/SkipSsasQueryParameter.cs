namespace SqlServerAnalysisServices.Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class SkipSsasQueryParameter : System.Attribute
{
    [Flags]
    public enum SkipCondition
    {
        SkipIfNull,
        Skip
    };

    public SkipCondition Condition { get; set; } = SkipCondition.Skip;
}