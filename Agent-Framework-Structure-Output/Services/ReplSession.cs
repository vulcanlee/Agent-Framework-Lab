using System.Text;

namespace Agent_Framework_Structure_Output.Services;

internal sealed class ReplSession(TranscriptLoader transcriptLoader, MeetingActionPlanAgent agent)
{
    private string _model = "openai/gpt-4.1-mini";
    private bool _jsonOutput;
    private string? _transcript;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        PrintIntro();

        while (true)
        {
            Console.Write("repl> ");
            string? input = Console.ReadLine();

            if (input is null)
            {
                Console.WriteLine();
                return 0;
            }

            string trimmed = input.Trim().TrimStart('\uFEFF');
            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                if (trimmed.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                if (trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    continue;
                }

                if (trimmed.Equals("/sample", StringComparison.OrdinalIgnoreCase))
                {
                    _transcript = transcriptLoader.GetSampleTranscript();
                    Console.WriteLine("已載入內建會議範例。");
                    continue;
                }

                if (trimmed.StartsWith("/file ", StringComparison.OrdinalIgnoreCase))
                {
                    string path = trimmed["/file ".Length..].Trim();
                    _transcript = await transcriptLoader.LoadFromFileAsync(path, cancellationToken);
                    Console.WriteLine($"已載入檔案：{path}");
                    continue;
                }

                if (trimmed.Equals("/paste", StringComparison.OrdinalIgnoreCase))
                {
                    _transcript = ReadMultilineTranscript();
                    Console.WriteLine("已更新目前的會議內容。");
                    continue;
                }

                if (trimmed.Equals("/show", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(string.IsNullOrWhiteSpace(_transcript) ? "目前尚未載入會議內容。" : _transcript);
                    continue;
                }

                if (trimmed.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    _transcript = null;
                    Console.WriteLine("已清除目前的會議內容。");
                    continue;
                }

                if (trimmed.StartsWith("/model", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateModel(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("/json", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateJsonMode(trimmed);
                    continue;
                }

                if (trimmed.Equals("/run", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(_transcript))
                    {
                        Console.WriteLine("目前沒有可執行的會議內容，請先使用 `/sample`、`/file <path>` 或 `/paste`。");
                        continue;
                    }

                    var plan = await agent.GenerateAsync(_transcript, _model, cancellationToken);
                    Console.WriteLine();
                    Console.WriteLine(_jsonOutput
                        ? OutputRenderer.RenderJson(plan)
                        : OutputRenderer.RenderSummary(plan));
                    Console.WriteLine();
                    continue;
                }

                Console.WriteLine("無法辨識的指令，請輸入 `/help` 查看可用指令。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"執行失敗：{ex.Message}");
            }
        }
    }

    private static string ReadMultilineTranscript()
    {
        Console.WriteLine("請貼上多行會議內容，輸入單獨一行 `.end` 結束：");

        var builder = new StringBuilder();
        while (true)
        {
            string? line = Console.ReadLine();
            if (line is null || line.Equals(".end", StringComparison.Ordinal))
            {
                break;
            }

            builder.AppendLine(line);
        }

        string transcript = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new InvalidOperationException("沒有讀到任何內容。");
        }

        return transcript;
    }

    private void UpdateModel(string input)
    {
        string value = input["/model".Length..].Trim();
        if (value.Length == 0)
        {
            Console.WriteLine($"目前模型：{_model}");
            return;
        }

        _model = value;
        Console.WriteLine($"已切換模型為：{_model}");
    }

    private void UpdateJsonMode(string input)
    {
        string value = input["/json".Length..].Trim().ToLowerInvariant();

        _jsonOutput = value switch
        {
            "" => !_jsonOutput,
            "on" => true,
            "off" => false,
            _ => throw new InvalidOperationException("`/json` 只接受 `on`、`off`，或不帶參數直接切換。")
        };

        Console.WriteLine($"JSON 輸出：{(_jsonOutput ? "開啟" : "關閉")}");
    }

    private static void PrintIntro()
    {
        Console.WriteLine("會議行動清單產生器 REPL");
        Console.WriteLine("不帶參數啟動時會進入互動模式。輸入 `/help` 查看指令。");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            可用指令
              /help           顯示這份說明
              /sample         載入內建會議範例
              /file <path>    載入 UTF-8 文字檔
              /paste          進入多行貼上模式，輸入 .end 結束
              /show           顯示目前會議內容
              /clear          清除目前會議內容
              /model          顯示目前模型
              /model <id>     切換模型
              /json           切換 JSON 輸出開關
              /json on        開啟 JSON 輸出
              /json off       關閉 JSON 輸出
              /run            執行目前會議內容
              /exit           離開 REPL
            """);
        Console.WriteLine();
    }
}
