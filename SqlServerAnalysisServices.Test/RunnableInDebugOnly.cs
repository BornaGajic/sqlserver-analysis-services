using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

namespace SqlServerAnalysisServices.Test;

public class RunnableInDebugOnlyAttribute : FactAttribute
{
    public RunnableInDebugOnlyAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = "Only running in interactive mode.";
        SkipType = typeof(RunnableInDebugOnlyAttribute);
        SkipUnless = nameof(DebuggerAttached);
    }

    public static bool DebuggerAttached => Debugger.IsAttached;
}