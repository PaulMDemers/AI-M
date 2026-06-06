# Contributing

AI-M is early, so small, focused changes are easiest to review.

## Setup

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet restore AIM.slnx
dotnet build AIM.slnx
```

## Before Opening A Pull Request

Run:

```powershell
dotnet test tests\AIM.Tests\AIM.Tests.csproj
dotnet build AIM.slnx -c Release
```

For desktop UI work, also launch the affected shell:

```powershell
dotnet run --project src\AIM.Desktop.Wpf\AIM.Desktop.Wpf.csproj
dotnet run --project src\AIM.Desktop.WinForms\AIM.Desktop.WinForms.csproj
```

## Local Data

Do not commit local databases, provider credentials, app settings, `.env` files, or screenshots that show private conversations or API keys.

For isolated testing:

```powershell
$env:AIM_SQLITE_PATH="$pwd\artifacts\dev\aim.db"
```

For WPF demo-mode launches:

```powershell
$env:AIM_DEMO_MODE="true"
```

## Code Shape

- Keep provider-neutral behavior in `AIM.Core`.
- Keep provider API details in `AIM.Providers`.
- Keep persistence in `AIM.Storage`.
- Keep shell-specific UI behavior in the relevant desktop project.
- Durable agent actions must go through an approval path.
