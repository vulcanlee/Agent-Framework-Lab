using System.Text;
using Agent_Test.Models;

namespace Agent_Test.Services;

public static class ReviewReportWriter
{
    public static async Task WriteAsync(FinalReviewReport report, string outputPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, report.Markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }
}
