namespace Agent_Framework_Structure_Output.Cli;

internal sealed class CliParser
{
    private const string DefaultModel = "openai/gpt-4.1-mini";

    public CliOptions Parse(string[] args)
    {
        bool jsonOutput = false;
        bool showHelp = false;
        string model = DefaultModel;
        string? command = null;
        string? inputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string current = args[i];

            switch (current)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--json":
                    jsonOutput = true;
                    break;

                case "--model":
                    if (i + 1 >= args.Length)
                    {
                        throw new CliUsageException("`--model` 後面需要接模型名稱。");
                    }

                    model = args[++i];
                    break;

                default:
                    if (command is null)
                    {
                        command = current;
                    }
                    else if (command.Equals("file", StringComparison.OrdinalIgnoreCase) && inputPath is null)
                    {
                        inputPath = current;
                    }
                    else
                    {
                        throw new CliUsageException($"無法辨識的參數：`{current}`。");
                    }

                    break;
            }
        }

        if (showHelp)
        {
            return new CliOptions(InputMode.Sample, null, jsonOutput, model, true);
        }

        return command?.ToLowerInvariant() switch
        {
            "sample" => new CliOptions(InputMode.Sample, null, jsonOutput, model, false),
            "stdin" => new CliOptions(InputMode.Stdin, null, jsonOutput, model, false),
            "file" when !string.IsNullOrWhiteSpace(inputPath) => new CliOptions(InputMode.File, inputPath, jsonOutput, model, false),
            "file" => throw new CliUsageException("`file` 模式需要指定輸入檔案路徑。"),
            _ => throw new CliUsageException($"無法辨識的命令：`{command}`。")
        };
    }
}
