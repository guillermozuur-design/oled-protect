<#
.SYNOPSIS
    Packages OLED Protect for distribution.

.DESCRIPTION
    Every run starts from an empty output folder and ends with these files, all prefixed with
    OledProtect-<version>-:

        Setup.exe          installer (current user, no admin), x64 and x86 inside
        Portable-x64.zip   self-contained x64 build, unpacks into an OledProtect folder
        Portable.exe       the same self-contained build as one executable
        SHA256SUMS.txt     sha256sum-compatible list for the .exe and .zip files

    <version> is computed by MinVer from the latest v* tag. Inside GitHub Actions (GITHUB_OUTPUT set)
    the script also publishes it as the step output 'version'. The release workflow calls this script
    as well, so there is only one recipe.

.PARAMETER OutputDirectory
    Where the files land. Defaults to the 'artifacts' folder next to this script.

.PARAMETER IsccPath
    Explicit location of the Inno Setup 6 compiler (ISCC.exe), for when it cannot be found by itself.

.PARAMETER SkipPortable
    Leave out the zip and the single executable; handy while editing the installer script.

.EXAMPLE
    .\build.ps1
    Full build into .\artifacts.

.EXAMPLE
    .\build.ps1 -SkipPortable -IsccPath 'D:\Tools\Inno Setup 6\ISCC.exe'
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'),
    [string] $IsccPath,
    [switch] $SkipPortable
)

$ErrorActionPreference = [Management.Automation.ActionPreference]::Stop
Set-StrictMode -Version 3.0

$Csproj = Join-Path $PSScriptRoot 'OledProtect\OledProtect.csproj'
$IssFile = Join-Path $PSScriptRoot 'installer\OledProtect.iss'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# Scratch area for dotnet publish. OledProtect.iss expects its payloads in publish\x64 and publish\x86.
$Staging = Join-Path $OutputDirectory 'publish'

# ---- publish profiles ---------------------------------------------------------------------------
# Folder (below $Staging) -> how to publish into it. 'Portable' builds define PORTABLE, which keeps
# the settings in a Data folder beside the exe; installed copies use %APPDATA% instead.
$Profiles = @{
    'x64'                  = @{ Rid = 'win-x64'; Portable = $false }
    'x86'                  = @{ Rid = 'win-x86'; Portable = $false }
    'portable\OledProtect' = @{ Rid = 'win-x64'; Portable = $true; PackNative = $false }
    'onefile'              = @{ Rid = 'win-x64'; Portable = $true; PackNative = $true }
}

# ---- deliverables -------------------------------------------------------------------------------
# Suffix: part of the file name after 'OledProtect-<version>-'. Uses: publish profiles it needs.
# Make: produces the file; receives the full target path.
$Deliverables = @(
    @{
        Suffix   = 'Setup.exe'
        Optional = $false
        Uses     = @('x64', 'x86')
        Make     = {
            param($Target)
            # ISCC names the file itself (OutputBaseFilename); we only check it afterwards.
            Exec $InnoCompiler "/DAppVersion=$Version" "/DArtifactsDir=$OutputDirectory" $IssFile
        }
    }
    @{
        Suffix   = 'Portable-x64.zip'
        Optional = $true
        Uses     = @('portable\OledProtect')
        # Zipping the folder itself gives the archive a single top-level OledProtect directory.
        Make     = { param($Target) Compress-Archive -Path (Join-Path $Staging 'portable\OledProtect') -DestinationPath $Target -Force }
    }
    @{
        Suffix   = 'Portable.exe'
        Optional = $true
        Uses     = @('onefile')
        Make     = { param($Target) Copy-Item -LiteralPath (Join-Path $Staging 'onefile\OledProtect.exe') -Destination $Target -Force }
    }
)

# ---- helpers ------------------------------------------------------------------------------------

function Say([string] $What) { Write-Host "`n>> $What" -ForegroundColor Cyan }

# Runs a program with the console as its output and stops the build on a non-zero exit code.
function Exec {
    $program, $rest = $args
    & $program @rest | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "'$(Split-Path $program -Leaf) $($rest -join ' ')' exited with $LASTEXITCODE" }
}

function Get-InnoCompiler {
    if ($IsccPath) {
        if (Test-Path -LiteralPath $IsccPath -PathType Leaf) { return (Resolve-Path -LiteralPath $IsccPath).Path }
        throw "-IsccPath does not point to a file: $IsccPath"
    }
    $command = Get-Command -Name 'ISCC.exe' -CommandType Application -ErrorAction Ignore | Select-Object -First 1
    if ($null -ne $command) { return $command.Path }

    $found = @(${env:ProgramFiles(x86)}, $env:ProgramFiles, "$env:LOCALAPPDATA\Programs") |
        Where-Object { $_ } |
        ForEach-Object { Join-Path $_ 'Inno Setup 6\ISCC.exe' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
    if ($found) { return $found }

    throw "ISCC.exe (Inno Setup 6) not found on PATH or in Program Files / LocalAppData\Programs.`n" +
        'Install Inno Setup 6 (winget install JRSoftware.InnoSetup) or pass -IsccPath.'
}

function Get-Version {
    # MinVer's target is only available once the package is restored (a fresh clone has nothing yet).
    Exec 'dotnet' 'restore' $Csproj '--nologo' '-v' 'quiet'
    $raw = & dotnet msbuild $Csproj -t:MinVer -getProperty:Version -nologo -v:quiet
    if ($LASTEXITCODE -ne 0) { throw 'Reading the MinVer version failed.' }
    $value = ($raw | Out-String).Trim()
    if (-not $value) { throw 'MinVer gave no version.' }
    $value
}

function Publish-Profile([string] $Folder) {
    $spec = $Profiles[$Folder]
    $into = Join-Path $Staging $Folder
    if (Test-Path -LiteralPath $into) { Remove-Item -LiteralPath $into -Recurse -Force }

    $cli = @('publish', $Csproj, '-c', 'Release', '-r', $spec.Rid, '-o', $into, '--nologo')
    if ($spec.Portable) {
        $cli += '--self-contained', 'true', '-p:Portable=true', '-p:PublishSingleFile=true',
            '-p:EnableCompressionInSingleFile=true', '-p:DebugType=none'
        # Normally the WPF native DLLs sit beside the exe; packing them in makes a true one-file app.
        if ($spec.PackNative) { $cli += '-p:IncludeNativeLibrariesForSelfExtract=true' }
    }
    else {
        # The installer brings the .NET 8 Desktop Runtime, so its payloads stay framework-dependent.
        $cli += '--self-contained', 'false'
    }
    Exec 'dotnet' @cli
}

# ---- run ----------------------------------------------------------------------------------------

$InnoCompiler = Get-InnoCompiler

Say 'Version'
$Version = Get-Version
Write-Host $Version
if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "version=$Version" -Encoding utf8 }
$Prefix = "OledProtect-$Version"

# Old top-level files would otherwise slip into this run's checksum list.
Say "Emptying $OutputDirectory"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $OutputDirectory -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

foreach ($item in $Deliverables) {
    $file = "$Prefix-$($item.Suffix)"
    if ($item.Optional -and $SkipPortable) {
        Write-Host "(skipped $file because of -SkipPortable)"
        continue
    }
    Say $file
    foreach ($folder in $item.Uses) { Publish-Profile $folder }
    $target = Join-Path $OutputDirectory $file
    & $item.Make $target
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "$file was not created." }
}

Say "$Prefix-SHA256SUMS.txt"
$sums = Get-ChildItem -LiteralPath $OutputDirectory -File |
    Where-Object Extension -In '.exe', '.zip' |
    Sort-Object Name |
    ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
Set-Content -LiteralPath (Join-Path $OutputDirectory "$Prefix-SHA256SUMS.txt") -Value $sums -Encoding utf8

Say 'Result'
foreach ($f in Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name) {
    Write-Host ('  {0,-52}{1,10:N1} MB' -f $f.Name, ($f.Length / 1MB))
}
