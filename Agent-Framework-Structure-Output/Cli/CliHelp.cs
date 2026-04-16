namespace Agent_Framework_Structure_Output.Cli;

internal static class CliHelp
{
    public static string Text =>
        """
        會議行動清單產生器

        用法
          dotnet run
          dotnet run -- sample [--json] [--model <model-id>]
          dotnet run -- file <path> [--json] [--model <model-id>]
          dotnet run -- stdin [--json] [--model <model-id>]

        範例
          dotnet run
          dotnet run -- sample
          dotnet run -- sample --json
          dotnet run -- file samples/meeting-transcript.txt
          Get-Content samples/meeting-transcript.txt | dotnet run -- stdin --json

        必要設定
          GITHUB_TOKEN    GitHub Models API Token

        說明
          不帶參數時會進入互動式 REPL 模式
          sample  使用內建範例會議逐字稿
          file    讀取指定的 UTF-8 文字檔
          stdin   從標準輸入讀取內容
        """;
}
