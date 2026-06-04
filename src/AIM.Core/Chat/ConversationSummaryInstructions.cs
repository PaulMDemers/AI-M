namespace AIM.Core.Chat;

public static class ConversationSummaryInstructions
{
    public static string Build(Conversation? conversation, int visibleMessagesSinceSummary)
    {
        if (conversation is null)
        {
            return string.Empty;
        }

        var summaryState = string.IsNullOrWhiteSpace(conversation.Summary)
            ? "No durable summary exists yet."
            : $"The durable summary was last updated at {conversation.SummaryUpdatedAt:g}. There are {visibleMessagesSinceSummary} visible user/assistant messages since then.";

        return
            "Maintain the durable conversation summary when it would improve continuity. " +
            $"{summaryState} " +
            "If the conversation has accumulated important goals, decisions, preferences, constraints, or open threads not captured in the summary, privately request conversation.summary.update. " +
            "Keep summaries concise and durable; do not include transient wording, tool traces, or implementation noise.";
    }
}
