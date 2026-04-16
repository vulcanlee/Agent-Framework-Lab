using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;

namespace WikiCli.Services;

public sealed record AppConfiguration(
    string RootPath,
    string RawPath,
    string WikiPath,
    string DocsPath,
    string DefaultModel)
{
    public static AppConfiguration FromEnvironment(string currentDirectory)
    {
        var root = Environment.GetEnvironmentVariable("WIKI_ROOT");
        var projectRoot = string.IsNullOrWhiteSpace(root) ? currentDirectory : Path.GetFullPath(root);
        var defaultModel = Environment.GetEnvironmentVariable("GITHUB_MODEL");

        return new AppConfiguration(
            projectRoot,
            Path.Combine(projectRoot, "raw"),
            Path.Combine(projectRoot, "wiki"),
            Path.Combine(projectRoot, "docs"),
            string.IsNullOrWhiteSpace(defaultModel) ? "openai/gpt-4.1" : defaultModel);
    }
}

public sealed record SearchResult(
    string RelativePath,
    string Title,
    string Summary,
    string Content,
    double Score);

public sealed record WikiPageRecord(
    string Category,
    string RelativePath,
    string Title,
    string Summary,
    string Content,
    string Slug);

public interface IWikiAgent
{
    Task<IngestAgentResult> GenerateIngestPlanAsync(IngestRequest request, CancellationToken cancellationToken);

    Task<string> AnswerQuestionAsync(AskRequest request, CancellationToken cancellationToken);

    Task<string> CreateLintReportAsync(LintRequest request, CancellationToken cancellationToken);
}

public sealed record IngestRequest(
    string SourceFileName,
    string SourceRelativePath,
    string SourceText,
    string CurrentIndex,
    IReadOnlyList<SearchResult> RelatedPages);

public sealed record AskRequest(
    string Question,
    string IndexMarkdown,
    IReadOnlyList<SearchResult> CandidatePages);

public sealed record LintRequest(
    string IndexMarkdown,
    string HeuristicReport,
    IReadOnlyList<SearchResult> CandidatePages);

public sealed record IngestAgentResult
{
    public string SourceSummary { get; init; } = string.Empty;

    public string SourcePageMarkdown { get; init; } = string.Empty;

    public List<WikiPageDraft> AdditionalPages { get; init; } = [];

    public string LogSummary { get; init; } = string.Empty;
}

public sealed record WikiPageDraft
{
    public string Category { get; init; } = "topics";

    public string Title { get; init; } = string.Empty;

    public string Markdown { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class SourceTextExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".txt",
        ".json",
        ".docx",
    };

    public bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task<string> ExtractTextAsync(string path, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path);

        return extension.ToLowerInvariant() switch
        {
            ".md" or ".txt" or ".json" => await File.ReadAllTextAsync(path, cancellationToken),
            ".docx" => ExtractDocx(path),
            _ => throw new InvalidOperationException($"Unsupported source type '{extension}'."),
        };
    }

    public IReadOnlyList<string> EnumerateSupportedFiles(string path)
    {
        if (File.Exists(path))
        {
            return IsSupported(path)
                ? [Path.GetFullPath(path)]
                : throw new InvalidOperationException($"Unsupported file type: {path}");
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Path not found: {path}");
        }

        return Directory
            .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(IsSupported)
            .Select(Path.GetFullPath)
            .ToList();
    }

    private static string ExtractDocx(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return string.Empty;
        }

        var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine, paragraphs);
    }
}

public sealed class WikiFileService(AppConfiguration configuration)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly string[] Categories = ["sources", "topics", "entities", "analyses"];

    public AppConfiguration Configuration { get; } = configuration;

    public async Task EnsureBaseStructureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(Configuration.RootPath);
        Directory.CreateDirectory(Configuration.RawPath);
        Directory.CreateDirectory(Configuration.WikiPath);
        Directory.CreateDirectory(Configuration.DocsPath);

        foreach (var category in Categories)
        {
            Directory.CreateDirectory(Path.Combine(Configuration.WikiPath, category));
        }

        var indexPath = GetIndexPath();
        if (!File.Exists(indexPath))
        {
            await WriteTextAsync(indexPath, BuildEmptyIndex(), cancellationToken);
        }

        var logPath = GetLogPath();
        if (!File.Exists(logPath))
        {
            await WriteTextAsync(logPath, "# Wiki Log\n\n", cancellationToken);
        }
    }

    public string GetIndexPath() => Path.Combine(Configuration.WikiPath, "index.md");

    public string GetLogPath() => Path.Combine(Configuration.WikiPath, "log.md");

    public string GetLintReportPath()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(Configuration.WikiPath, "analyses", $"lint-{timestamp}.md");
    }

    public string GetPagePath(string category, string slug)
    {
        return Path.Combine(Configuration.WikiPath, category, $"{slug}.md");
    }

    public async Task<string> CopySourceIntoRawAsync(string sourcePath, CancellationToken cancellationToken)
    {
        await EnsureBaseStructureAsync(cancellationToken);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (fullSourcePath.StartsWith(Configuration.RawPath, StringComparison.OrdinalIgnoreCase))
        {
            return fullSourcePath;
        }

        var importedDirectory = Path.Combine(Configuration.RawPath, "imported");
        Directory.CreateDirectory(importedDirectory);

        var fileName = Path.GetFileName(fullSourcePath);
        var slug = Slugify(Path.GetFileNameWithoutExtension(fileName));
        var extension = Path.GetExtension(fileName);
        var destinationPath = Path.Combine(importedDirectory, $"{slug}{extension}");

        var suffix = 1;
        while (File.Exists(destinationPath))
        {
            destinationPath = Path.Combine(importedDirectory, $"{slug}-{suffix}{extension}");
            suffix++;
        }

        await using var sourceStream = File.OpenRead(fullSourcePath);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        return destinationPath;
    }

    public async Task WritePageAsync(string category, string slug, string markdown, CancellationToken cancellationToken)
    {
        var normalizedCategory = NormalizeCategory(category);
        var pagePath = GetPagePath(normalizedCategory, Slugify(slug));
        await WriteTextAsync(pagePath, EnsureTrailingNewLine(markdown), cancellationToken);
    }

    public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, Utf8NoBom, cancellationToken);
    }

    public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public async Task RefreshIndexAsync(CancellationToken cancellationToken)
    {
        await EnsureBaseStructureAsync(cancellationToken);
        var pages = await LoadWikiPagesAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("# Wiki Index");
        builder.AppendLine();
        builder.AppendLine($"Last rebuilt: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();

        foreach (var category in Categories)
        {
            builder.AppendLine($"## {char.ToUpperInvariant(category[0])}{category[1..]}");
            builder.AppendLine();

            var categoryPages = pages
                .Where(page => page.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(page => page.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (categoryPages.Count == 0)
            {
                builder.AppendLine("- _No pages yet._");
                builder.AppendLine();
                continue;
            }

            foreach (var page in categoryPages)
            {
                builder.AppendLine($"- [{page.Title}]({page.RelativePath}) - {page.Summary}");
            }

            builder.AppendLine();
        }

        await WriteTextAsync(GetIndexPath(), builder.ToString(), cancellationToken);
    }

    public async Task AppendLogAsync(string kind, string title, string details, CancellationToken cancellationToken)
    {
        await EnsureBaseStructureAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine($"## [{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {kind} | {title}");
        builder.AppendLine();
        builder.AppendLine(details.Trim());
        builder.AppendLine();

        await File.AppendAllTextAsync(GetLogPath(), builder.ToString(), Utf8NoBom, cancellationToken);
    }

    public async Task<IReadOnlyList<WikiPageRecord>> LoadWikiPagesAsync(CancellationToken cancellationToken)
    {
        await EnsureBaseStructureAsync(cancellationToken);

        var files = Directory
            .EnumerateFiles(Configuration.WikiPath, "*.md", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("index.md", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith("log.md", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var pages = new List<WikiPageRecord>(files.Count);

        foreach (var file in files)
        {
            var content = await ReadTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(Configuration.WikiPath, file).Replace('\\', '/');
            var category = relativePath.Split('/')[0];
            var title = ExtractTitle(content, Path.GetFileNameWithoutExtension(file));
            var summary = ExtractSummary(content);
            var slug = Path.GetFileNameWithoutExtension(file);

            pages.Add(new WikiPageRecord(category, relativePath, title, summary, content, slug));
        }

        return pages;
    }

    public static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "topics";
        }

        var normalized = category.Trim().ToLowerInvariant();
        return Categories.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : "topics";
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "untitled";
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^\p{L}\p{Nd}]+", "-");
        normalized = Regex.Replace(normalized, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "untitled" : normalized;
    }

    public static string ExtractTitle(string markdown, string fallback)
    {
        var line = markdown
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(candidate => candidate.StartsWith("# ", StringComparison.Ordinal));

        return string.IsNullOrWhiteSpace(line) ? fallback : line[2..].Trim();
    }

    public static string ExtractSummary(string markdown)
    {
        var lines = markdown
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith('#'))
            .Where(line => !line.StartsWith("- "))
            .Where(line => !line.StartsWith("Last updated", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return lines.FirstOrDefault(line => line.Length > 0) ?? "No summary available.";
    }

    private static string BuildEmptyIndex() =>
        """
        # Wiki Index

        Last rebuilt: not yet

        ## Sources

        - _No pages yet._

        ## Topics

        - _No pages yet._

        ## Entities

        - _No pages yet._

        ## Analyses

        - _No pages yet._
        """;

    private static string EnsureTrailingNewLine(string text) =>
        text.EndsWith('\n') ? text : $"{text}\n";
}

public sealed class WikiSearchService(WikiFileService fileService)
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var pages = await fileService.LoadWikiPagesAsync(cancellationToken);
        var queryTokens = Tokenize(query);

        if (queryTokens.Count == 0)
        {
            return [];
        }

        return pages
            .Select(page => new SearchResult(
                page.RelativePath,
                page.Title,
                page.Summary,
                page.Content,
                Score(page, queryTokens)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private static double Score(WikiPageRecord page, IReadOnlyCollection<string> queryTokens)
    {
        var score = 0d;
        var title = page.Title.ToLowerInvariant();
        var summary = page.Summary.ToLowerInvariant();
        var content = page.Content.ToLowerInvariant();
        var path = page.RelativePath.ToLowerInvariant();

        foreach (var token in queryTokens)
        {
            if (title.Contains(token, StringComparison.Ordinal))
            {
                score += 8;
            }

            if (path.Contains(token, StringComparison.Ordinal))
            {
                score += 5;
            }

            if (summary.Contains(token, StringComparison.Ordinal))
            {
                score += 3;
            }

            if (content.Contains(token, StringComparison.Ordinal))
            {
                score += 1;
            }
        }

        return score;
    }

    private static IReadOnlyCollection<string> Tokenize(string value) =>
        Regex.Matches(value.ToLowerInvariant(), @"[\p{L}\p{Nd}]{2,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

public sealed class WikiApplication(
    AppConfiguration configuration,
    WikiFileService fileService,
    SourceTextExtractor extractor,
    WikiSearchService searchService,
    Func<IWikiAgent> agentFactory)
{
    public async Task<string> InitAsync(CancellationToken cancellationToken)
    {
        await fileService.EnsureBaseStructureAsync(cancellationToken);
        await fileService.RefreshIndexAsync(cancellationToken);

        return $"Initialized wiki structure under {configuration.RootPath}";
    }

    public async Task<string> IngestAsync(string path, CancellationToken cancellationToken)
    {
        await fileService.EnsureBaseStructureAsync(cancellationToken);
        var files = extractor.EnumerateSupportedFiles(path);

        if (files.Count == 0)
        {
            return "No supported files were found.";
        }

        var agent = agentFactory();
        var summaries = new List<string>();

        foreach (var file in files)
        {
            var rawCopyPath = await fileService.CopySourceIntoRawAsync(file, cancellationToken);
            var text = await extractor.ExtractTextAsync(rawCopyPath, cancellationToken);
            var relativeSourcePath = Path.GetRelativePath(configuration.RootPath, rawCopyPath).Replace('\\', '/');
            var currentIndex = await fileService.ReadTextAsync(fileService.GetIndexPath(), cancellationToken);
            var relatedPages = await searchService.SearchAsync(Path.GetFileNameWithoutExtension(file), 6, cancellationToken);

            var result = await agent.GenerateIngestPlanAsync(
                new IngestRequest(
                    Path.GetFileName(rawCopyPath),
                    relativeSourcePath,
                    text,
                    currentIndex,
                    relatedPages),
                cancellationToken);

            var sourceSlug = WikiFileService.Slugify(Path.GetFileNameWithoutExtension(rawCopyPath));
            await fileService.WritePageAsync("sources", sourceSlug, result.SourcePageMarkdown, cancellationToken);

            foreach (var page in result.AdditionalPages)
            {
                await fileService.WritePageAsync(
                    WikiFileService.NormalizeCategory(page.Category),
                    WikiFileService.Slugify(page.Title),
                    page.Markdown,
                    cancellationToken);
            }

            await fileService.RefreshIndexAsync(cancellationToken);
            await fileService.AppendLogAsync(
                "ingest",
                Path.GetFileName(rawCopyPath),
                string.IsNullOrWhiteSpace(result.LogSummary) ? result.SourceSummary : result.LogSummary,
                cancellationToken);

            summaries.Add($"- {Path.GetFileName(rawCopyPath)}: {result.SourceSummary}");
        }

        return $"Ingested {files.Count} file(s):{Environment.NewLine}{string.Join(Environment.NewLine, summaries)}";
    }

    public async Task<string> AskAsync(string question, CancellationToken cancellationToken)
    {
        await fileService.EnsureBaseStructureAsync(cancellationToken);
        var agent = agentFactory();
        var index = await fileService.ReadTextAsync(fileService.GetIndexPath(), cancellationToken);
        var candidates = await searchService.SearchAsync(question, 6, cancellationToken);

        var answer = await agent.AnswerQuestionAsync(
            new AskRequest(question, index, candidates),
            cancellationToken);

        await fileService.AppendLogAsync("ask", question, answer, cancellationToken);
        return answer;
    }

    public async Task<string> LintAsync(CancellationToken cancellationToken)
    {
        await fileService.EnsureBaseStructureAsync(cancellationToken);
        var heuristicReport = await BuildHeuristicLintReportAsync(cancellationToken);
        var index = await fileService.ReadTextAsync(fileService.GetIndexPath(), cancellationToken);
        var candidates = await searchService.SearchAsync("wiki quality contradictions orphans stale", 8, cancellationToken);
        var agent = agentFactory();

        var report = await agent.CreateLintReportAsync(
            new LintRequest(index, heuristicReport, candidates),
            cancellationToken);

        var reportPath = fileService.GetLintReportPath();
        await fileService.WriteTextAsync(reportPath, report, cancellationToken);
        await fileService.RefreshIndexAsync(cancellationToken);
        await fileService.AppendLogAsync(
            "lint",
            Path.GetFileName(reportPath),
            $"Generated lint report at {Path.GetRelativePath(configuration.RootPath, reportPath).Replace('\\', '/')}",
            cancellationToken);

        return $"Created lint report: {Path.GetRelativePath(configuration.RootPath, reportPath).Replace('\\', '/')}";
    }

    private async Task<string> BuildHeuristicLintReportAsync(CancellationToken cancellationToken)
    {
        var pages = await fileService.LoadWikiPagesAsync(cancellationToken);
        var titleGroups = pages
            .GroupBy(page => WikiFileService.Slugify(page.Title))
            .Where(group => group.Count() > 1)
            .ToList();

        var inboundLinks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            inboundLinks.TryAdd(page.RelativePath, 0);
        }

        var linkPattern = new Regex(@"\(([^)]+\.md)\)|\[\[([^\]]+)\]\]", RegexOptions.Compiled);

        foreach (var page in pages)
        {
            foreach (Match match in linkPattern.Matches(page.Content))
            {
                var markdownTarget = match.Groups[1].Value;
                var wikiTarget = match.Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(markdownTarget))
                {
                    var normalizedTarget = markdownTarget.Replace('\\', '/').TrimStart('/');
                    if (inboundLinks.ContainsKey(normalizedTarget))
                    {
                        inboundLinks[normalizedTarget]++;
                    }
                }

                if (!string.IsNullOrWhiteSpace(wikiTarget))
                {
                    var slug = WikiFileService.Slugify(wikiTarget);
                    var target = pages.FirstOrDefault(candidate =>
                        candidate.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

                    if (target is not null)
                    {
                        inboundLinks[target.RelativePath]++;
                    }
                }
            }
        }

        var orphanPages = inboundLinks
            .Where(pair => pair.Value == 0)
            .Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pagesMissingLastUpdated = pages
            .Where(page => !page.Content.Contains("Last updated", StringComparison.OrdinalIgnoreCase))
            .Select(page => page.RelativePath)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("# Heuristic Lint Signals");
        builder.AppendLine();
        builder.AppendLine("## Duplicate titles");
        builder.AppendLine();

        if (titleGroups.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var group in titleGroups)
            {
                builder.AppendLine($"- {group.Key}: {string.Join(", ", group.Select(page => page.RelativePath))}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Orphan pages");
        builder.AppendLine();
        builder.AppendLine(orphanPages.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, orphanPages.Select(page => $"- {page}")));
        builder.AppendLine();
        builder.AppendLine("## Pages missing 'Last updated'");
        builder.AppendLine();
        builder.AppendLine(pagesMissingLastUpdated.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, pagesMissingLastUpdated.Select(page => $"- {page}")));

        return builder.ToString();
    }
}
