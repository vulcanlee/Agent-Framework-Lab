using System.Text;

namespace AgentFunctionCall.Services;

public sealed class ConsoleInteractionLogger : IInteractionLogger
{
    public void LogSection(string title, string content)
    {
        Console.WriteLine(content);
    }

    public static void ConfigureConsoleEncoding()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
    }
}
