# Releasing

AI-M does not have an installer yet. Current release artifacts are zipped Windows publish outputs for the WPF and WinForms desktop apps.

## Build A Local Release

From the repository root:

```powershell
.\scripts\publish-desktop.ps1
```

This writes framework-dependent Windows x64 builds to:

```text
artifacts/publish/AIM.Desktop.Wpf
artifacts/publish/AIM.Desktop.WinForms
```

To build only one shell:

```powershell
.\scripts\publish-desktop.ps1 -App Wpf
.\scripts\publish-desktop.ps1 -App WinForms
```

To produce self-contained builds:

```powershell
.\scripts\publish-desktop.ps1 -SelfContained
```

## GitHub Actions Artifacts

The CI workflow builds and tests the solution, then publishes both desktop apps as zipped workflow artifacts:

- `AI-M-WPF-win-x64`
- `AI-M-WinForms-win-x64`

These artifacts are intended for smoke testing and preview downloads. They are not signed installers.

## Before Tagging

Run:

```powershell
dotnet test tests\AIM.Tests\AIM.Tests.csproj
dotnet build AIM.slnx -c Release
.\scripts\publish-desktop.ps1
```

Then manually launch both published apps from `artifacts/publish` and verify:

- The buddy list opens.
- A seeded demo contact can open a chat window.
- Provider setup does not expose local credentials in screenshots or logs.
- Pending actions open from the buddy list if present.

## Release Notes Checklist

Include:

- User-visible UI changes.
- Provider or storage changes.
- Migration notes.
- Known limitations.
- Verification commands.
