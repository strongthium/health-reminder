<#
.SYNOPSIS
    Builds health-reminder and puts the finished program in public\.

.DESCRIPTION
    Runs the tests, then publishes health-reminder-win-x64-needs-dotnet.exe: about 3 MB, for x64 PCs, running on the
    .NET 10 Desktop Runtime installed on the PC (https://dotnet.microsoft.com/download/dotnet/10.0), which Microsoft
    Update keeps patched.

    public\ is emptied and gets the new file, with VERSION and SHA256SUMS, all directly inside it. The version is read
    from VERSION; -Bump or -Version builds a new one, and VERSION is changed once the build succeeds.

.PARAMETER Bump
    Raises the version before building: major (2.0.0), minor (1.1.0) or patch (1.0.1).

.PARAMETER Version
    Sets the version before building, as major.minor.patch.

.PARAMETER SkipTests
    Builds without running the tests first.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Bump patch
#>
[CmdletBinding()]
param(
    [ValidateSet('major', 'minor', 'patch')] [string] $Bump,
    [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$project = Join-Path $root 'src\HealthReminder\HealthReminder.csproj'
$versionFile = Join-Path $root 'VERSION'
$public = Join-Path $root 'public'
# dotnet publishes outside the repository first, so that public\ only changes once the build has worked.
$out = Join-Path ([IO.Path]::GetTempPath()) 'health-reminder-publish'
$name = 'health-reminder-win-x64-needs-dotnet.exe'

function Step([string] $text) { Write-Host "`n== $text" -ForegroundColor Cyan }

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed (exit code $LASTEXITCODE)." }
}

# .NET 10 SDK
$sdks = @(& dotnet --list-sdks 2>$null)
if (-not ($sdks | Where-Object { $_ -match '^10\.' })) {
    throw 'The .NET 10 SDK is needed to build: https://dotnet.microsoft.com/download/dotnet/10.0'
}

# The version: VERSION, changed first by -Version or -Bump.
$current = (Get-Content $versionFile -Raw).Trim()
if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "VERSION holds '$current', not major.minor.patch." }
$major, $minor, $patch = [int]$Matches[1], [int]$Matches[2], [int]$Matches[3]
$next = $current
if ($Version) { $next = $Version }
elseif ($Bump -eq 'major') { $next = "$($major + 1).0.0" }
elseif ($Bump -eq 'minor') { $next = "$major.$($minor + 1).0" }
elseif ($Bump -eq 'patch') { $next = "$major.$minor.$($patch + 1)" }
if ($next -ne $current) { Write-Host "Version $current -> $next (VERSION is changed once the build succeeds)" }
Write-Host "Building health-reminder $next"

if (-not $SkipTests) {
    Step 'Tests'
    Invoke-Dotnet test (Join-Path $root 'health-reminder.slnx') --nologo
}

Step 'win-x64, needs-dotnet'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
Invoke-Dotnet publish $project -c Release -r win-x64 "-p:Version=$next" -o $out --nologo

# public\ holds only the latest build. A program in it that is running can't be replaced: that is checked first, so
# that public\ isn't left half emptied.
Step 'public\'
if (Test-Path $public) {
    foreach ($file in Get-ChildItem $public -File -Force) {
        try { [IO.File]::Open($file.FullName, 'Open', 'ReadWrite', 'None').Dispose() }
        catch { throw "$($file.Name) is in use, probably running: quit it (Quit in its tray icon's menu) and build again." }
    }
    Get-ChildItem $public -Force | Remove-Item -Recurse -Force
}
else { New-Item -ItemType Directory $public | Out-Null }
$exe = Join-Path $public $name
Copy-Item (Join-Path $out 'health-reminder.exe') $exe
[IO.File]::WriteAllText((Join-Path $public 'VERSION'), "$next`n")
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $public 'SHA256SUMS'), "$hash  $name`n")
Remove-Item $out -Recurse -Force
if ($next -ne $current) { [IO.File]::WriteAllText($versionFile, "$next`n") }

'{0}  {1:N1} MB' -f $name, ((Get-Item $exe).Length / 1MB)
Write-Host "`nhealth-reminder $next is in $public" -ForegroundColor Green
