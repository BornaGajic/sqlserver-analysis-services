namespace SqlServerAnalysisServices.Model;

public sealed class AzureResourceValidation
{
    public AzureResourceValidation(IReadOnlyList<string> errors) => Errors = errors ?? [];

    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Errors.Count == 0;

    public override string ToString() => IsValid ? "Azure resource is valid." : string.Join(Environment.NewLine, Errors);
}