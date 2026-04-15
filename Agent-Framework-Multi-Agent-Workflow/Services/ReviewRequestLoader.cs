using System.Text.Json;
using Agent_Test.Models;

namespace Agent_Test.Services;

public static class ReviewRequestLoader
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ReviewRequest> LoadAsync(string inputPath, CancellationToken cancellationToken)
    {
        if (Directory.Exists(inputPath))
        {
            return await LoadFromDirectoryAsync(inputPath, cancellationToken);
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        var request = await LoadJsonFileAsync(inputPath, cancellationToken);
        return request with { ImageAttachments = [] };
    }

    private static async Task<ReviewRequest> LoadFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        var images = await LoadImagesAsync(directoryPath, cancellationToken);

        if (jsonFiles.Length == 0)
        {
            if (images.Count == 0)
            {
                throw new InvalidOperationException($"No JSON or supported image files were found in input directory: {directoryPath}");
            }

            return CreateImageOnlyRequest(directoryPath, images);
        }

        if (jsonFiles.Length > 1)
        {
            var fileList = string.Join(", ", jsonFiles.Select(Path.GetFileName));
            throw new InvalidOperationException($"Expected exactly one JSON file in input directory, but found multiple: {fileList}");
        }

        var request = await LoadJsonFileAsync(jsonFiles[0], cancellationToken);
        return request with { ImageAttachments = images };
    }

    private static async Task<ReviewRequest> LoadJsonFileAsync(string jsonPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(jsonPath);
        var request = await JsonSerializer.DeserializeAsync<ReviewRequest>(stream, JsonOptions, cancellationToken);
        return request ?? throw new InvalidOperationException($"Input JSON could not be parsed into ReviewRequest: {jsonPath}");
    }

    private static async Task<IReadOnlyList<ImageAttachment>> LoadImagesAsync(string directoryPath, CancellationToken cancellationToken)
    {
        var imagePaths = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var images = new List<ImageAttachment>(imagePaths.Length);
        foreach (var imagePath in imagePaths)
        {
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => throw new InvalidOperationException($"Unsupported image file type: {Path.GetFileName(imagePath)}")
            };

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read image file: {Path.GetFileName(imagePath)}", ex);
            }

            var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            images.Add(new ImageAttachment(Path.GetFileName(imagePath), mimeType, dataUrl));
        }

        return images;
    }

    private static ReviewRequest CreateImageOnlyRequest(string directoryPath, IReadOnlyList<ImageAttachment> images)
    {
        var partName = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(partName))
        {
            partName = "Image Only Review";
        }

        return new ReviewRequest(
            PartName: partName,
            FeatureJson: "No structured feature JSON was provided.",
            DimensionData: "No structured dimension data was provided.",
            DrawingNotes: "Review this case from the attached drawing images only.",
            ImageAttachments: images);
    }
}
