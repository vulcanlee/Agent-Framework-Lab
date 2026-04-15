using System.Text.RegularExpressions;

internal static partial class FilePromptResolver
{
    [GeneratedRegex(@"(?<path>([A-Za-z]:\\|\\\\)[^\r\n""<>|?*]+?\.(png|jpg|jpeg|webp|txt|md|json|csv|xml|cs|js|ts|tsx|jsx|html|css|yml|yaml))", RegexOptions.IgnoreCase)]
    private static partial Regex FilePathRegex();

    public static string ResolveFilePath(AppOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            return Path.GetFullPath(options.FilePath);
        }

        var prompt = options.Prompt ?? string.Empty;
        var match = FilePathRegex().Match(prompt);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                "No file path was provided. Add --file or include a full Windows file path in the prompt.");
        }

        var resolvedPath = Path.GetFullPath(match.Groups["path"].Value.Trim());
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("The file path found in the prompt does not exist.", resolvedPath);
        }

        return resolvedPath;
    }
}
