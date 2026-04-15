using System.Text;
using Microsoft.Extensions.AI;

internal static class FileInputBuilder
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private static readonly HashSet<string> SupportedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".json",
        ".csv",
        ".xml",
        ".cs",
        ".js",
        ".ts",
        ".tsx",
        ".jsx",
        ".html",
        ".css",
        ".yml",
        ".yaml"
    };

    public static async Task<FileInput> BuildAsync(AppOptions options, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(options.FilePath!);
        var extension = Path.GetExtension(fullPath);

        if (SupportedImageExtensions.Contains(extension))
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            var mediaType = GetImageMediaType(extension);
            var contents = new List<AIContent>
            {
                new TextContent(options.Prompt!),
                new DataContent(bytes, mediaType)
            };

            return new FileInput(fullPath, Path.GetFileName(fullPath), mediaType, true, contents);
        }

        if (SupportedTextExtensions.Contains(extension))
        {
            var text = await ReadUtf8TextAsync(fullPath, cancellationToken);
            var composedPrompt = $"""
                {options.Prompt}

                Attached file: {Path.GetFileName(fullPath)}
                File path: {fullPath}
                File type: text

                File content:
                ```text
                {text}
                ```
                """;

            var contents = new List<AIContent>
            {
                new TextContent(composedPrompt)
            };

            return new FileInput(fullPath, Path.GetFileName(fullPath), "text/plain; charset=utf-8", false, contents);
        }

        throw new NotSupportedException(
            $"Unsupported file type: {extension}. Supported text types: {string.Join(", ", SupportedTextExtensions.Order())}. Supported image types: {string.Join(", ", SupportedImageExtensions.Order())}.");
    }

    private static async Task<string> ReadUtf8TextAsync(string filePath, CancellationToken cancellationToken)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream, utf8, detectEncodingFromByteOrderMarks: true);
        using var registration = cancellationToken.Register(() => stream.Dispose());

        try
        {
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidOperationException(
                $"The file '{filePath}' is not valid UTF-8 text. This demo only supports UTF-8 text files.",
                ex);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private static string GetImageMediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => throw new NotSupportedException($"Unsupported image extension: {extension}")
    };
}
