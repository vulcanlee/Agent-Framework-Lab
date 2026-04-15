namespace Agent_Test.Models;

public sealed record CliOptions(string InputPath, string OutputPath)
{
    public static CliOptions Parse(string[] args)
    {
        var rootPath = Directory.GetCurrentDirectory();
        var inputPath = Path.Combine(rootPath, "sample-input");
        var outputPath = Path.Combine(rootPath, "output", "review-report.md");

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if ((arg.Equals("--input", StringComparison.OrdinalIgnoreCase) || arg.Equals("-i", StringComparison.OrdinalIgnoreCase)) && index + 1 < args.Length)
            {
                inputPath = args[++index];
            }
            else if ((arg.Equals("--output", StringComparison.OrdinalIgnoreCase) || arg.Equals("-o", StringComparison.OrdinalIgnoreCase)) && index + 1 < args.Length)
            {
                outputPath = args[++index];
            }
        }

        return new CliOptions(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath));
    }
}
