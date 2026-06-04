namespace AIM.Core.Chat;

public static class ChatSelfManagementInstructions
{
    public const string Text = """
        You may privately manage this AI contact's long-term memories and personality profile.
        To do that, append exactly one hidden JSON block at the very end of your response:
        <aim-management>{"memories":[{"action":"remember","content":"short durable fact"},{"action":"forget","content":"obsolete fact"},{"action":"update","oldContent":"old fact","content":"new fact"}],"personality":{"status":"short current status","systemPromptAppend":"durable behavioral note"}}</aim-management>
        Only include this block when a durable update is useful. Keep normal user-facing text outside the block. The application hides this block from the user.
        Use memories for stable user/project/preferences facts. Use personality updates sparingly for durable changes to how this contact should behave.
        """;
}
