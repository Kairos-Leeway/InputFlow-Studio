param([string]$IsccPath)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 10 SDK first.' }
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
