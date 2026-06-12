param(
    [ValidateSet("All", "Wpf", "WinForms")]
    [string]$App = "All",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SelfContained,

    [string]$OutputRoot = "artifacts/publish"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedOutputRoot = Join-Path $repoRoot $OutputRoot

$projects = @()

if ($App -in @("All", "Wpf")) {
    $projects += [pscustomobject]@{
        Name = "AIM.Desktop.Wpf"
        Path = "src/AIM.Desktop.Wpf/AIM.Desktop.Wpf.csproj"
    }
}

if ($App -in @("All", "WinForms")) {
    $projects += [pscustomobject]@{
        Name = "AIM.Desktop.WinForms"
        Path = "src/AIM.Desktop.WinForms/AIM.Desktop.WinForms.csproj"
    }
}

foreach ($project in $projects) {
    $output = Join-Path $resolvedOutputRoot $project.Name
    $projectPath = Join-Path $repoRoot $project.Path
    $selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()

    Write-Host "Publishing $($project.Name) to $output"
    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained $selfContainedValue `
        --output $output
}
