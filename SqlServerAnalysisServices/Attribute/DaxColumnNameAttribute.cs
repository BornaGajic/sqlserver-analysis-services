namespace SqlServerAnalysisServices.Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class DaxColumnNameAttribute : System.Attribute
{
    public DaxColumnNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}