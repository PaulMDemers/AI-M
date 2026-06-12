# Development

## Setup

Install the .NET 10 SDK and restore tools:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet restore AIM.slnx
```

## Build Everything

```powershell
dotnet build AIM.slnx
```

## Publish Desktop Builds

```powershell
.\scripts\publish-desktop.ps1
```

See [Releasing](RELEASING.md) for artifact details.

## Run Tests

```powershell
dotnet test tests\AIM.Tests\AIM.Tests.csproj
```

## Run Desktop Apps

```powershell
dotnet run --project src\AIM.Desktop.Wpf\AIM.Desktop.Wpf.csproj
dotnet run --project src\AIM.Desktop.WinForms\AIM.Desktop.WinForms.csproj
```

To run WPF in demo mode without showing the first-run provider setup dialog:

```powershell
$env:AIM_DEMO_MODE="true"
dotnet run --project src\AIM.Desktop.Wpf\AIM.Desktop.Wpf.csproj
```

To isolate local data while testing:

```powershell
$env:AIM_SQLITE_PATH="$pwd\artifacts\dev\aim.db"
```

## EF Core Migrations

The EF tool is pinned in `dotnet-tools.json`.

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef migrations add MigrationName --project src\AIM.Storage\AIM.Storage.csproj
```

## Coding Notes

- Keep provider-neutral behavior in `AIM.Core`.
- Keep provider SDK/API concerns in `AIM.Providers`.
- Keep durable persistence in `AIM.Storage`.
- Keep WPF-only behavior in `AIM.Desktop.Wpf`.
- Keep WinForms-only behavior in `AIM.Desktop.WinForms`.
- Approval-required tools should never mutate durable user data without an approval path.
- Avoid committing local databases, app settings, provider credentials, or Visual Studio user files.

## Screenshot Assets

Public screenshots live in `docs/screenshots`.

When refreshing screenshots:

- Use a temporary database with `AIM_SQLITE_PATH`.
- Use `AIM_DEMO_MODE=true` for WPF screenshots that should skip first-run setup.
- Do not capture real API keys, local provider credentials, personal conversations, or private pending actions.
- Rebuild the target desktop project before capturing so screenshots match current code.

## Verification Checklist

Before committing:

```powershell
dotnet test tests\AIM.Tests\AIM.Tests.csproj
dotnet build src\AIM.Desktop.Wpf\AIM.Desktop.Wpf.csproj
dotnet build src\AIM.Desktop.WinForms\AIM.Desktop.WinForms.csproj
```

Before public pushes, also run:

```powershell
dotnet build AIM.slnx -c Release
```
