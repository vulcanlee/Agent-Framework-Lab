using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WikiCli.Services;

namespace WikiCli.Tests;

public sealed class WikiCliTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WikiCliTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitAsync_CreatesExpectedStructure()
    {
        var app = CreateApplication(new FakeWikiAgent());

        var result = await app.InitAsync(CancellationToken.None);

        Assert.Contains("Initialized wiki structure", result);
        Assert.True(Directory.Exists(Path.Combine(_root, "raw")));
        Assert.True(Directory.Exists(Path.Combine(_root, "wiki", "sources")));
        Assert.True(File.Exists(Path.Combine(_root, "wiki", "index.md")));
        Assert.True(File.Exists(Path.Combine(_root, "wiki", "log.md")));
    }

    [Fact]
    public async Task IngestAsync_WritesSourcePagesAndUpdatesIndex()
    {
        Directory.CreateDirectory(_root);
        var sourcePath = Path.Combine(_root, "meeting.txt");
        await File.WriteAllTextAsync(
            sourcePath,
            "Decision: ship the CLI first. Action: document the wiki flow.",
            CancellationToken.None);

        var app = CreateApplication(new FakeWikiAgent());

        var result = await app.IngestAsync(sourcePath, CancellationToken.None);

        Assert.Contains("meeting.txt", result);
        Assert.True(File.Exists(Path.Combine(_root, "wiki", "sources", "meeting.md")));
        Assert.True(File.Exists(Path.Combine(_root, "wiki", "topics", "delivery-plan.md")));

        var index = await File.ReadAllTextAsync(Path.Combine(_root, "wiki", "index.md"));
        Assert.Contains("delivery-plan", index);

        var log = await File.ReadAllTextAsync(Path.Combine(_root, "wiki", "log.md"));
        Assert.Contains("ingest | meeting.txt", log);
    }

    [Fact]
    public async Task LintAsync_WritesReportAndLogEntry()
    {
        var fileService = new WikiFileService(CreateConfiguration());
        await fileService.EnsureBaseStructureAsync(CancellationToken.None);
        await fileService.WritePageAsync(
            "topics",
            "orphan-page",
            """
            # Orphan Page

            No inbound references yet.

            ## Key Facts
            - This page is isolated.

            ## Related Pages
            - None

            ## Sources
            - [sources/demo.md](sources/demo.md)

            Last updated: 2026-04-15
            """,
            CancellationToken.None);
        await fileService.RefreshIndexAsync(CancellationToken.None);

        var app = CreateApplication(new FakeWikiAgent());

        var result = await app.LintAsync(CancellationToken.None);

        Assert.Contains("Created lint report", result);
        Assert.Single(Directory.GetFiles(Path.Combine(_root, "wiki", "analyses"), "lint-*.md"));

        var log = await File.ReadAllTextAsync(Path.Combine(_root, "wiki", "log.md"));
        Assert.Contains("lint | lint-", log);
    }

    [Fact]
    public async Task SourceTextExtractor_ReadsDocx()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "notes.docx");

        using (var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("Line one"))),
                    new Paragraph(new Run(new Text("Line two")))));
        }

        var extractor = new SourceTextExtractor();
        var text = await extractor.ExtractTextAsync(path, CancellationToken.None);

        Assert.Contains("Line one", text);
        Assert.Contains("Line two", text);
    }

    [Fact]
    public async Task WikiSearchService_RanksMostRelevantPageFirst()
    {
        var fileService = new WikiFileService(CreateConfiguration());
        await fileService.EnsureBaseStructureAsync(CancellationToken.None);
        await fileService.WritePageAsync(
            "topics",
            "agent-framework",
            """
            # Agent Framework

            This page explains the CLI project design.

            ## Key Facts
            - Uses Microsoft Agent Framework.

            ## Related Pages
            - [GitHub Models](entities/github-models.md)

            ## Sources
            - [sources/overview.md](sources/overview.md)

            Last updated: 2026-04-15
            """,
            CancellationToken.None);
        await fileService.WritePageAsync(
            "entities",
            "random-note",
            """
            # Random Note

            Unrelated content.

            ## Key Facts
            - Nothing about the framework.

            ## Related Pages
            - None

            ## Sources
            - None

            Last updated: 2026-04-15
            """,
            CancellationToken.None);

        var search = new WikiSearchService(fileService);
        var results = await search.SearchAsync("agent framework cli", 5, CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.Equal("topics/agent-framework.md", results[0].RelativePath);
    }

    [Fact]
    public async Task WriteTextAsync_UsesUtf8WithoutBom()
    {
        var fileService = new WikiFileService(CreateConfiguration());
        var path = Path.Combine(_root, "docs", "utf8-check.md");
        await fileService.WriteTextAsync(path, "hello", CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
    }

    [Fact]
    public async Task RunAsync_WithoutArguments_EntersInteractiveMode()
    {
        var app = CreateApplication(new FakeWikiAgent());
        using var input = new StringReader("help\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliHost.RunAsync(
            [],
            app,
            input,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("interactive mode", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Type 'help'", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usage:", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Theory]
    [InlineData("exit\n")]
    [InlineData("quit\n")]
    public async Task RunAsync_InteractiveExitCommands_EndSession(string commandText)
    {
        var app = CreateApplication(new FakeWikiAgent());
        using var input = new StringReader(commandText);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliHost.RunAsync(
            [],
            app,
            input,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("wiki>", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_InteractiveAsk_UsesEntireRemainderAsQuestion()
    {
        var agent = new CapturingWikiAgent();
        var app = CreateApplication(agent);
        using var input = new StringReader("ask hello world\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        await CliHost.RunAsync([], app, input, output, error, CancellationToken.None);

        Assert.Equal("hello world", agent.LastQuestion);
        Assert.Contains("asked: hello world", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_InteractiveIngest_UnquotesPathWithSpaces()
    {
        Directory.CreateDirectory(_root);
        var sourceDirectory = Path.Combine(_root, "nested folder");
        Directory.CreateDirectory(sourceDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "notes file.txt");
        await File.WriteAllTextAsync(sourcePath, "Decision: keep parsing simple.", CancellationToken.None);

        var app = CreateApplication(new FakeWikiAgent());
        using var input = new StringReader($"ingest \"{sourcePath}\"\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliHost.RunAsync(
            [],
            app,
            input,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("wiki>", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(_root, "wiki", "sources", "notes-file.md")));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_InteractiveUnknownCommand_ShowsErrorAndContinues()
    {
        var app = CreateApplication(new FakeWikiAgent());
        using var input = new StringReader("wat\nhelp\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliHost.RunAsync(
            [],
            app,
            input,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Unknown command 'wat'.", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usage:", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_InteractiveGeneralException_ShowsErrorAndContinues()
    {
        var app = CreateApplication(new FakeWikiAgent());
        using var input = new StringReader("ingest missing.txt\nhelp\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliHost.RunAsync(
            [],
            app,
            input,
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Path not found", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usage:", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WikiApplication CreateApplication(IWikiAgent agent)
    {
        var configuration = CreateConfiguration();
        var fileService = new WikiFileService(configuration);
        var extractor = new SourceTextExtractor();
        var search = new WikiSearchService(fileService);
        return new WikiApplication(configuration, fileService, extractor, search, () => agent);
    }

    private AppConfiguration CreateConfiguration() =>
        new(
            _root,
            Path.Combine(_root, "raw"),
            Path.Combine(_root, "wiki"),
            Path.Combine(_root, "docs"),
            "openai/gpt-4.1");

    private sealed class FakeWikiAgent : IWikiAgent
    {
        public Task<IngestAgentResult> GenerateIngestPlanAsync(IngestRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new IngestAgentResult
            {
                SourceSummary = "Captured the key decision and follow-up action.",
                SourcePageMarkdown =
                    """
                    # Meeting

                    Captured the key decision and follow-up action.

                    ## Key Facts
                    - Ship the CLI first.
                    - Document the wiki flow.

                    ## Related Pages
                    - [Delivery Plan](topics/delivery-plan.md)

                    ## Sources
                    - [raw/imported/meeting.txt](../raw/imported/meeting.txt)

                    Last updated: 2026-04-15
                    """,
                AdditionalPages =
                [
                    new WikiPageDraft
                    {
                        Category = "topics",
                        Title = "Delivery Plan",
                        Summary = "Tracks the CLI-first rollout.",
                        Markdown =
                            """
                            # Delivery Plan

                            Tracks the CLI-first rollout.

                            ## Key Facts
                            - Start with a CLI.
                            - Turn the workflow into a technical article.

                            ## Related Pages
                            - [Meeting](sources/meeting.md)

                            ## Sources
                            - [sources/meeting.md](sources/meeting.md)

                            Last updated: 2026-04-15
                            """,
                    },
                ],
                LogSummary = "Created a source summary and a delivery plan topic page.",
            });
        }

        public Task<string> AnswerQuestionAsync(AskRequest request, CancellationToken cancellationToken) =>
            Task.FromResult("The wiki says to ship the CLI first [topics/delivery-plan.md].");

        public Task<string> CreateLintReportAsync(LintRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(
                """
                # Wiki Lint Report

                The wiki is small and has a few pages that still need cross-links.

                ## Findings
                - One page appears to be orphaned.

                ## Suggested Next Actions
                - Add inbound links from source summaries to topic pages.

                Last updated: 2026-04-15
                """);
    }

    private sealed class CapturingWikiAgent : IWikiAgent
    {
        public string? LastQuestion { get; private set; }

        public Task<IngestAgentResult> GenerateIngestPlanAsync(IngestRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new IngestAgentResult
            {
                SourceSummary = "Captured request.",
                SourcePageMarkdown =
                    """
                    # Notes File

                    Captured request.

                    ## Key Facts
                    - Parsed the path.

                    ## Related Pages
                    - None

                    ## Sources
                    - None

                    Last updated: 2026-04-16
                    """,
                LogSummary = "Captured request.",
            });

        public Task<string> AnswerQuestionAsync(AskRequest request, CancellationToken cancellationToken)
        {
            LastQuestion = request.Question;
            return Task.FromResult($"asked: {request.Question}");
        }

        public Task<string> CreateLintReportAsync(LintRequest request, CancellationToken cancellationToken) =>
            Task.FromResult("lint ok");
    }
}
