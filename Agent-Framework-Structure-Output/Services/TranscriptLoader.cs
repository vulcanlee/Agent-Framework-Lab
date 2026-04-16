using System.Text;
using Agent_Framework_Structure_Output.Cli;

namespace Agent_Framework_Structure_Output.Services;

internal sealed class TranscriptLoader
{
    private const string SampleTranscript =
        """
        產品週會記錄：
        Amy：我們希望五月底前上線新的合作夥伴後台首頁，這次先聚焦在儀表板與通知中心。
        Leo：目前設計稿這週五會定稿，但資料整合還有一個風險，因為 CRM API 的欄位還沒完全對齊。
        Nina：後端這邊可以先把儀表板 API 做出第一版，下週三前提供測試環境。不過通知中心需要等設計稿確定後才能開工。
        Amy：那儀表板 API 就由 Nina 負責，下週三前完成。設計稿由 Leo 這週五前確認。通知中心的需求細節，我們下週一再開 30 分鐘釐清。
        Ben：我這邊會整理合作夥伴最常看的三個 KPI 指標，明天下班前提供給 Nina，避免 API 先做錯方向。
        Leo：另外提醒，CRM 團隊還沒有承諾正式欄位凍結日期，這可能影響整體串接時程。
        Amy：好，這件事先列為風險，並請我來跟 CRM 團隊追欄位凍結時間。
        """;

    public string GetSampleTranscript() => SampleTranscript;

    public async Task<string> LoadAsync(CliOptions options, CancellationToken cancellationToken)
    {
        return options.Mode switch
        {
            InputMode.Sample => SampleTranscript,
            InputMode.File => await LoadFromFileAsync(options.InputPath!, cancellationToken),
            InputMode.Stdin => await LoadFromStdInAsync(cancellationToken),
            _ => throw new InvalidOperationException($"不支援的輸入模式：{options.Mode}")
        };
    }

    public async Task<string> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new CliUsageException($"找不到輸入檔案：`{path}`。");
        }

        string content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new CliUsageException("指定檔案內容為空，請提供有效的 UTF-8 文字檔。");
        }

        return content;
    }

    public async Task<string> LoadFromStdInAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        string content = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new CliUsageException("從標準輸入讀到空內容，請透過管線提供會議內容。");
        }

        return content;
    }
}
