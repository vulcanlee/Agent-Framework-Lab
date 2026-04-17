namespace AiTopicPulse.Cli.Status;

public sealed class ConsoleStatusReporter : IStatusReporter
{
    public void Report(string message)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"[status] {message}");
    }
}
