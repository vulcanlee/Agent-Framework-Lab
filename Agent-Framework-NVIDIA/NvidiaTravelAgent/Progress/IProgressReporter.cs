namespace NvidiaTravelAgent.Progress;

public interface IProgressReporter
{
    void Report(ProgressUpdate update);
}
