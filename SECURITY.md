# Security

AI-M can store provider credentials and long-running conversation context locally. Please treat security issues and accidental secret exposure carefully.

## Reporting

For now, report vulnerabilities privately to the repository owner instead of opening a public issue. Include:

- A short description of the issue.
- Reproduction steps.
- The affected project or feature.
- Whether credentials, local files, memories, conversations, or pending actions may be exposed.

## Secrets

Do not commit:

- API keys or provider credentials.
- Local SQLite databases.
- `%LocalAppData%\AI-M` contents.
- `.env` files or local app settings.
- Screenshots that show private conversations, keys, endpoints, or account IDs.

## Local Storage

By default AI-M stores data under `%LocalAppData%\AI-M`. Use `AIM_SQLITE_PATH` to test with an isolated database.
