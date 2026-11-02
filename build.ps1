param([string]$IsccPath)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
& (Join-Path $PSScriptRoot 'packaging\Write-LicenseRtf.ps1')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 10 SDK first.' }
[xml]$projectXml = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'InputFlowStudio.csproj') -Raw
$appVersion = [string]$projectXml.Project.PropertyGroup.Version
$installerText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'setup.iss') -Raw
$installerVersion = [regex]::Match($installerText, '#define\s+MyAppVersion\s+"([^"]+)"').Groups[1].Value
if ($appVersion -notmatch '^\d+\.\d+\.\d+$' -or $appVersion -ne $installerVersion) { throw 'Project and Inno Setup versions must match (major.minor.patch).' }
dotnet publish InputFlowStudio.csproj -c Release -r win-x64 --self-contained true -o publish-v2
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
$iscc = $IsccPath
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (-not $iscc) {
    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, "$env:LOCALAPPDATA\Programs")
    foreach ($version in @(7, 6)) {
        foreach ($root in $roots) {
            if (-not $root) { continue }
            $candidate = Join-Path $root "Inno Setup $version\ISCC.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $iscc = $candidate; break }
        }
        if ($iscc) { break }
    }
}
if (-not $iscc) {
    $uninstallKeys = @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*')
    $installed = Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match '^Inno Setup [67]' -and $_.InstallLocation }
    foreach ($entry in $installed) {
        $candidate = Join-Path $entry.InstallLocation 'ISCC.exe'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $iscc = $candidate; break }
    }
}
if (-not $iscc -or -not (Test-Path -LiteralPath $iscc -PathType Leaf)) {
    throw 'Cannot find ISCC.exe. Run build.ps1 -IsccPath "D:\Your Install Folder\ISCC.exe".'
}
& $iscc setup.iss
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

$assetName = "InputFlowStudio-$appVersion-Setup.exe"
$assetPath = Join-Path $PSScriptRoot "installer\$assetName"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'installer\Setup.exe') -Destination $assetPath -Force
$assetHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'installer\SHA256SUMS.txt'), "$assetHash  $assetName" + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
Write-Output "Release asset: $assetPath"
