namespace NvidiaTravelAgent.Services;

public sealed class ModelOutputException : InvalidOperationException
{
    public ModelOutputException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
