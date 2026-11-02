param([string]$SourcePath = (Join-Path $PSScriptRoot '..\EULA.txt'), [string]$OutputPath = (Join-Path $PSScriptRoot '..\EULA.rtf'))
$ErrorActionPreference = 'Stop'
# EULA.txt is the single source; Unicode escapes keep the RTF portable.
$lines = [System.IO.File]::ReadAllLines($SourcePath, [System.Text.Encoding]::UTF8)
$rtf = [System.Text.StringBuilder]::new()
[void]$rtf.Append('{\rtf1\ansi\ansicpg936\deff0{\fonttbl{\f0\fnil Microsoft YaHei;}}\uc1\f0\fs20 ')
foreach ($line in $lines) {
    $important = $line -match '【|^[一二三四五六七八九十]+、|^InputFlow Studio'
    [void]$rtf.Append('\pard\sa140 ')
    if ($important) { [void]$rtf.Append('\b ') }
    foreach ($character in $line.ToCharArray()) {
        $value = [int]$character
        if ($value -gt 127) {
            if ($value -gt 32767) { $value -= 65536 }
            [void]$rtf.Append('\u' + $value + '?')
        } elseif ($character -in @('\', '{', '}')) {
            [void]$rtf.Append('\' + $character)
        } else { [void]$rtf.Append($character) }
    }
    if ($important) { [void]$rtf.Append('\b0 ') }
    [void]$rtf.Append('\par' + [Environment]::NewLine)
}
[void]$rtf.Append('}')
[System.IO.File]::WriteAllText($OutputPath, $rtf.ToString(), [System.Text.Encoding]::ASCII)
