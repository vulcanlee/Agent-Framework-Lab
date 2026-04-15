internal sealed record AppOptions
{
    public const string DefaultModel = "openai/gpt-4.1-mini";

    public string? FilePath { get; init; }
    public string? Prompt { get; init; }
    public string? ModelOverride { get; init; }

    public bool ShouldUseInteractiveMode =>
        string.IsNullOrWhiteSpace(FilePath) || string.IsNullOrWhiteSpace(Prompt);

    public static AppOptions Parse(string[] args)
    {
        string? filePath = null;
        string? prompt = null;
        string? model = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file":
                    filePath = ReadValue(args, ref i, "--file");
                    break;
                case "--prompt":
                    prompt = ReadValue(args, ref i, "--prompt");
                    break;
                case "--model":
                    model = ReadValue(args, ref i, "--model");
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new AppOptions
        {
            FilePath = filePath,
            Prompt = prompt,
            ModelOverride = model
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("A file path is required. Pass --file or use interactive mode.");
        }

        var fullPath = Path.GetFullPath(FilePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The specified file was not found.", fullPath);
        }

        if (string.IsNullOrWhiteSpace(Prompt))
        {
            throw new ArgumentException("A prompt is required. Pass --prompt or use interactive mode.");
        }

        _ = GetResolvedModel();
    }

    public string GetResolvedModel()
    {
        return FirstNonEmpty(ModelOverride, Environment.GetEnvironmentVariable("GITHUB_MODEL"), DefaultModel)
            ?? DefaultModel;
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index++;
        return args[index];
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- --file <path> --prompt <text> [--model <model-id>]");
        Console.WriteLine();
        Console.WriteLine("If --file or --prompt is omitted, the app switches to interactive mode.");
    }
}
