<#
.SYNOPSIS
    Produces the OLED Space Sleeper release files.

.DESCRIPTION
    Output (all named OLED-Space-Sleeper-<version>-...):
      Setup.exe             per-user installer with x64 and x86 framework-dependent payloads
      Portable-x64.zip      self-contained build in its own folder
      Portable.exe          self-contained build packed into a single exe
      SHA256SUMS.txt        checksums of the files above

    The version comes from MinVer (nearest v* tag). The release workflow runs this same script, so a
    local build and a CI build produce the same thing. When GITHUB_OUTPUT is set, the version is also
    written there as 'version'.

.PARAMETER OutputDirectory
    Folder for the finished files. Default: <repo>\artifacts.

.PARAMETER IsccPath
    ISCC.exe to use when Inno Setup 6 is not on PATH or in a standard install folder.

.PARAMETER SkipPortable
    Only build the installer (quicker when working on the .iss).

.EXAMPLE
    .\build.ps1 -SkipPortable
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'),
    [string] $IsccPath,
    [switch] $SkipPortable
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$AppProject = Join-Path $PSScriptRoot 'OLED-Sleeper\OLED-Sleeper.csproj'
$SetupScript = Join-Path $PSScriptRoot 'installer\OLED-Sleeper.iss'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# Intermediate publish folders; the .iss reads its payload from <output>\publish\x64 and \x86.
$PublishRoot = Join-Path $OutputDirectory 'publish'

function Show-Stage([string] $Text) {
    Write-Host "`n--- $Text" -ForegroundColor Cyan
}

# Runs an external tool and turns a non-zero exit code into an error. Its output goes straight to the
# console so it never leaks into a function's return value.
function Invoke-Tool([string] $Tool, [string[]] $Arguments, [string] $FailureText) {
    & $Tool @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$FailureText (exit code $LASTEXITCODE)" }
}

function Find-InnoCompiler([string] $Requested) {
    if ($Requested) {
        if (-not (Test-Path -LiteralPath $Requested -PathType Leaf)) { throw "No ISCC.exe at '$Requested'." }
        return (Resolve-Path -LiteralPath $Requested).Path
    }

    $fromPath = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $standardFolders = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs')) |
        Where-Object { $_ }
    foreach ($folder in $standardFolders) {
        $candidate = Join-Path $folder 'Inno Setup 6\ISCC.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }

    throw ("Inno Setup 6 (ISCC.exe) is needed to build the installer but was not found.`n" +
        "Install it (for example: winget install JRSoftware.InnoSetup) or point to it with -IsccPath.")
}

function Get-MinVerVersion {
    # The MinVer target only exists after a restore, which a fresh clone has not had yet.
    Invoke-Tool 'dotnet' @('restore', $AppProject, '--nologo', '-v', 'quiet') 'dotnet restore failed'
    $reported = & dotnet msbuild $AppProject -nologo -v:quiet -t:MinVer -getProperty:Version
    if ($LASTEXITCODE -ne 0) { throw 'MinVer could not determine the version.' }
    $text = "$reported".Trim()
    if ([string]::IsNullOrWhiteSpace($text)) { throw 'MinVer returned an empty version.' }
    return $text
}

# Framework-dependent build for the installer. Never PORTABLE: an installed copy keeps its data in
# %APPDATA%, not next to the exe in a folder it may not write to.
function Publish-ForInstaller([string] $Rid) {
    $target = Join-Path $PublishRoot ($Rid -replace '^win-', '')
    Reset-Folder $target
    Invoke-Tool 'dotnet' @(
        'publish', $AppProject, '--nologo', '-c', 'Release', '-r', $Rid, '-o', $target,
        '--self-contained', 'false'
    ) "Publishing $Rid for the installer failed"
}

# Self-contained single-file build with PORTABLE defined (data lives in a Data folder beside the exe).
# Without -Bundled the WPF native libraries stay next to the exe; with it they are packed inside too.
function Publish-Portable([string] $Target, [switch] $Bundled) {
    Reset-Folder $Target
    $msbuildProps = [ordered]@{
        Portable                           = 'true'
        PublishSingleFile                  = 'true'
        EnableCompressionInSingleFile      = 'true'
        IncludeNativeLibrariesForSelfExtract = "$([bool]$Bundled)".ToLowerInvariant()
        DebugType                          = 'none'   # no .pdb next to the exe
    }
    $publishArgs = @('publish', $AppProject, '--nologo', '-c', 'Release', '-r', 'win-x64', '-o', $Target, '--self-contained', 'true')
    $publishArgs += $msbuildProps.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" }
    Invoke-Tool 'dotnet' $publishArgs 'Publishing the portable build failed'
}

function Reset-Folder([string] $Folder) {
    if (Test-Path -LiteralPath $Folder) { Remove-Item -LiteralPath $Folder -Recurse -Force }
}

function Write-Checksums([string] $Destination) {
    $entries = foreach ($file in Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name) {
        if ($file.Extension -notin '.exe', '.zip') { continue }
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        # Two spaces: the format sha256sum -c expects.
        '{0}  {1}' -f $hash, $file.Name
    }
    Set-Content -LiteralPath $Destination -Value $entries -Encoding utf8
}

# ---- main ----------------------------------------------------------------------------------------

$innoCompiler = Find-InnoCompiler $IsccPath

Show-Stage 'Determining the version'
$version = Get-MinVerVersion
Write-Host "Building $version"
if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "version=$version" -Encoding utf8 }
$prefix = "OLED-Space-Sleeper-$version"

Show-Stage 'Removing files from a previous build'
# Leftovers would end up in this build's checksum list.
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($stale in Get-ChildItem -LiteralPath $OutputDirectory -File) { Remove-Item -LiteralPath $stale.FullName -Force }

Show-Stage 'Publishing the installer payloads (x64, x86)'
Publish-ForInstaller 'win-x64'
Publish-ForInstaller 'win-x86'

Show-Stage 'Compiling the installer'
Invoke-Tool $innoCompiler @("/DAppVersion=$version", "/DArtifactsDir=$OutputDirectory", $SetupScript) 'ISCC failed'
$setupExe = Join-Path $OutputDirectory "$prefix-Setup.exe"
if (-not (Test-Path -LiteralPath $setupExe -PathType Leaf)) { throw "Expected installer missing: $setupExe" }

if ($SkipPortable) {
    Write-Host 'Portable builds skipped (-SkipPortable).'
}
else {
    Show-Stage 'Building the portable zip'
    # The zip holds a top-level OLED-Space-Sleeper folder so unpacking it stays tidy.
    $zipSource = Join-Path $PublishRoot 'portable\OLED-Space-Sleeper'
    Publish-Portable $zipSource
    $zipFile = Join-Path $OutputDirectory "$prefix-Portable-x64.zip"
    Compress-Archive -Path $zipSource -DestinationPath $zipFile -Force

    Show-Stage 'Building the single-file portable exe'
    $singleFolder = Join-Path $PublishRoot 'onefile'
    Publish-Portable $singleFolder -Bundled
    Copy-Item -LiteralPath (Join-Path $singleFolder 'OLED-Sleeper.exe') -Destination (Join-Path $OutputDirectory "$prefix-Portable.exe") -Force
}

Show-Stage 'Writing SHA-256 checksums'
Write-Checksums (Join-Path $OutputDirectory "$prefix-SHA256SUMS.txt")

Show-Stage "Finished: $OutputDirectory"
Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name | ForEach-Object {
    '  {0,-50} {1,9:N1} MB' -f $_.Name, ($_.Length / 1MB)
} | Write-Host
