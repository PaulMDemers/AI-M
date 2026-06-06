# AI-M

AI-M is an instant messenger style desktop application for managing AI personalities. Think classic AIM or Pidgin, but each buddy is an AI persona with its own provider, memories, conversations, and approval flow.

## What Is Here

- `AIM.Core`: provider-neutral chat, personalities, memories, conversations, tool contracts, and self-management parsing.
- `AIM.Providers`: OpenAI, Ollama, AWS Bedrock, fake provider, diagnostics, and provider status services.
- `AIM.Storage`: SQLite persistence for personalities, memories, memory suggestions, provider accounts, conversation groups, conversations, and messages.
- `AIM.Desktop.Wpf`: primary WPF desktop shell with floating chats, buddy list, provider setup, memory review, personality editor, pending action review, and tray behavior.
- `AIM.Desktop.WinForms`: classic AIM-inspired WinForms shell using the same core/storage/provider stack.
- `AIM.Tests`: xUnit coverage for storage, providers, tools, context building, parsing, pending approvals, and migrations.

## Current Capabilities

- AIM/Pidgin-style buddy list for AI personalities.
- Floating chat windows and all-in-one WPF mode.
- Provider support for OpenAI, Ollama, AWS Bedrock, and a fake/demo provider.
- Provider readiness checks and first-run setup.
- Per-personality memory sets and conversation history.
- Conversation groups and summaries.
- Agent tools for memory, personality, conversation, and time operations.
- Approval-required durable changes, including memory writes, memory deletion, personality updates, system prompt notes, and conversation summary updates.
- Persistent pending action queue at `%LocalAppData%\AI-M\pending-actions.json`.
- Shared pending action queue used by both desktop shells.
- Restored pending tool approvals can be approved after restart and leave an audit trail in chat history.
- Prebuilt demo personalities and archetypes with avatar assets.

## Requirements

- Windows
- .NET 10 SDK
- Optional provider dependencies:
  - OpenAI API key
  - Ollama running locally or reachable over HTTP
  - AWS credentials/region for Bedrock

## Build

```powershell
dotnet restore AIM.slnx
dotnet build AIM.slnx
```

## Test

```powershell
dotnet test tests\AIM.Tests\AIM.Tests.csproj
```

## Run

WPF:

```powershell
dotnet run --project src\AIM.Desktop.Wpf\AIM.Desktop.Wpf.csproj
```

WinForms:

```powershell
dotnet run --project src\AIM.Desktop.WinForms\AIM.Desktop.WinForms.csproj
```

## Provider Configuration

The desktop apps include provider setup screens. OpenAI can also be configured with environment variables:

```powershell
$env:OPENAI_API_KEY="..."
$env:AIM_OPENAI_MODEL="gpt-4.1-mini"
```

Provider accounts saved through the UI are stored locally in SQLite. Credentials are protected with Windows data protection where available.

## Repository Notes

- Build outputs, Visual Studio state, local databases, local app settings, and environment files are ignored.
- Avatar images live in `assets/avatars`.
- EF Core tooling is pinned in `dotnet-tools.json`.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Development](docs/DEVELOPMENT.md)
- [Roadmap](docs/ROADMAP.md)
- [Pending Actions](docs/PENDING_ACTIONS.md)
