# Architecture

AI-M is split into a reusable application core, provider integrations, durable storage, and two Windows desktop shells.

## Projects

### AIM.Core

Defines the domain contracts and provider-neutral types:

- Chat messages, requests, context, and streaming chunks.
- Personalities and personality templates.
- Memories and memory suggestions.
- Conversation groups, conversations, and summaries.
- Provider accounts, health, diagnostics, and provider interfaces.
- Agent tools and approval metadata.
- Shared pending action queue and restored approval execution.
- Parsers for hidden tool requests and self-management directives.

### AIM.Providers

Contains provider implementations and provider-facing services:

- OpenAI chat provider.
- Ollama chat provider.
- AWS Bedrock chat provider.
- Fake/demo provider.
- Provider diagnostics and status caching.
- In-memory service implementations used by demos/tests.

### AIM.Storage

Provides SQLite persistence through EF Core:

- Personalities
- Memory sets and records
- Memory suggestions
- Provider accounts
- Conversation groups
- Conversations
- Messages

The storage layer owns migrations and database initialization.

### AIM.Desktop.Wpf

Primary desktop shell:

- Narrow AIM-style buddy list by default.
- Floating chat windows.
- Optional all-in-one mode.
- Provider setup and first-run flow.
- Personality editor and memory review.
- Pending action review and persistent approval queue.
- Tray behavior.

### AIM.Desktop.WinForms

Classic AIM-inspired shell using the same core, provider, and storage projects:

- Buddy list.
- Floating chat windows.
- Inline approvals in chat.
- Global pending action review.
- Provider setup for common providers.

## Runtime Data

Local data is stored under `%LocalAppData%\AI-M` by default. The pending approval queue is stored at:

```text
%LocalAppData%\AI-M\pending-actions.json
```

SQLite storage defaults are controlled by `AIM.Storage`.

## Provider Flow

1. A personality selects a provider key and model.
2. The desktop shell checks provider readiness from saved provider accounts and registered providers.
3. A chat turn builds context from recent messages, memories, personality prompt, and conversation summary.
4. The selected provider streams a response.
5. Hidden tool requests are parsed out of the model output.
6. Non-durable tools run immediately.
7. Durable tools become pending approvals.
8. Approved tools update storage and optionally trigger a provider follow-up turn.

## Agent Self-Management

Models can request memory and personality changes through structured directives or tools. Durable changes are not applied silently. They are surfaced as pending actions for the user to approve or deny.

## Pending Actions

Pending actions are global and persistent. The shared queue lives in `AIM.Core` so both desktop shells use the same file format and restored approval execution path. Live approvals can continue the provider turn. Restored durable tool approvals can still apply the saved tool change after restart and leave a system audit message in the original conversation.
