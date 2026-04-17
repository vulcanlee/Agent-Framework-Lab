using AiTopicPulse.Cli.Analysis;

namespace AiTopicPulse.Cli.Output;

public static class ConsoleReportWriter
{
    public static void Write(PulseReport report)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(report.Markdown);

        if (!string.IsNullOrWhiteSpace(report.CodeInterpreterTranscript))
        {
            Console.WriteLine();
            Console.WriteLine("=== Code Interpreter Trace ===");
            Console.WriteLine(report.CodeInterpreterTranscript);
        }
    }
}
