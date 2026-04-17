namespace AiTopicPulse.Cli.Analysis;

public static class CodeInterpreterPromptFactory
{
    public static string Create(AnalysisDataset dataset)
    {
        string sourceList = string.Join(", ", dataset.SuccessfulSources);

        return
            $"""
            You are an AI topic analyst. Use the code interpreter to analyze the attached JSON dataset.

            The JSON dataset contains recent {dataset.WindowHours}-hour discussions about "{dataset.Topic}" from: {sourceList}.

            Your responsibilities:
            1. Parse the JSON dataset.
            2. Deduplicate overlapping stories across sources.
            3. Rank the stories by engagement and recency.
            4. Produce a top 10 list with source, title, URL, and a short reason.
            5. Finish with a concise executive summary in Traditional Chinese.

            Return markdown with these sections:
            - Source status
            - Top 10 AI topics
            - Overall observations

            JSON dataset:
            {dataset.JsonPayload}
            """;
    }
}
