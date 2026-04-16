namespace Agent_Framework_Structure_Output.Cli;

internal sealed record CliOptions(
    InputMode Mode,
    string? InputPath,
    bool JsonOutput,
    string Model,
    bool ShowHelp);

internal enum InputMode
{
    Sample,
    File,
    Stdin
}
