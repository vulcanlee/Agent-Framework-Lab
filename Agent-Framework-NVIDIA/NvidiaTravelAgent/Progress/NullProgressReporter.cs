namespace NvidiaTravelAgent.Progress;

public sealed class NullProgressReporter : IProgressReporter
{
    public static NullProgressReporter Instance { get; } = new();

    private NullProgressReporter()
    {
    }

    public void Report(ProgressUpdate update)
    {
    }
}
