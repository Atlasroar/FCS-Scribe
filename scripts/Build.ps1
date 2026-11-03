param(
    [Parameter(Mandatory = $true)][string]$FcsDirectory
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$contract = Join-Path $FcsDirectory 'FCS_extended\FCS_extended_plugin.dll'
if (-not (Test-Path -LiteralPath $contract)) {
    throw "FCS Extended's plugin contract was not found at $contract. Install FCS Extended first."
}
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'Run this script in Windows PowerShell 5.1 with .NET Framework 4.8 installed.'
}
$out = Join-Path $root 'build\FCS-Scribe'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler /nologo /target:library /warnaserror+ /optimize+ "/out:$out\FCS-Scribe.dll" `
    "/reference:$contract" /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'FCS_extended.json') -Destination $out
Write-Output "Built $out (not installed; no Kenshi files were changed)."
