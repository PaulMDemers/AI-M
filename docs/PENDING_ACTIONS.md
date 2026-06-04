# Pending Actions

Pending actions are requests from an AI personality to make a durable change or run an approval-required tool.

## Why They Exist

AI-M lets personalities manage memories, conversation summaries, statuses, and system prompt notes. Those changes affect future behavior, so they are reviewed by the user before they are applied.

## Current Approval Types

- `memory.remember`
- `memory.forget`
- `personality.update_status`
- `personality.append_system_note`
- `conversation.summary.update`

## Live Approvals

When the originating chat window is still alive, approval does two things:

1. Executes the approved tool.
2. Continues the provider turn with the tool result so the personality can respond naturally.

## Restored Approvals

Pending tool approvals are persisted to:

```text
%LocalAppData%\AI-M\pending-actions.json
```

After restart, AI-M restores the approval list. Durable tool approvals can still be approved because the saved payload includes:

- Personality ID
- Conversation ID
- Tool call ID
- Tool name
- Tool arguments

Restored approvals execute the durable tool and write an audit message to the original conversation. They do not automatically trigger a provider follow-up turn because the original streaming chat turn no longer exists.

## Review Surfaces

- WPF buddy list pending-action strip.
- WPF Pending AI Actions review window.
- WinForms buddy list pending-action indicator.
- WinForms Pending AI Actions review dialog.
- Inline approval panels in active chat windows.

## Future Improvements

- Move pending action persistence into a shared core/storage service instead of shell-specific implementations.
- Add provider follow-up reconstruction for restored approvals.
- Add per-personality pending badges in the buddy list.
- Add action categories and filters.
