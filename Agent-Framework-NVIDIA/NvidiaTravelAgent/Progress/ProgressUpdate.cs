namespace NvidiaTravelAgent.Progress;

public sealed record ProgressUpdate(
    ProgressStage Stage,
    string Message,
    ProgressDetailLevel DetailLevel = ProgressDetailLevel.Normal);
