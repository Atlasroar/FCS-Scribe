param(
    [string]$FcsDirectory,
    [switch]$TodoViewOnly,
    [switch]$HistoryViewOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
$out = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $out | Out-Null
if ($TodoViewOnly -or $HistoryViewOnly) {
    if ($TodoViewOnly -and $HistoryViewOnly) { throw 'Choose one non-showing view selector at a time.' }
    if (-not $FcsDirectory) { throw 'Non-showing view checks require FcsDirectory for the installed plugin contract.' }
    $contract = Join-Path $FcsDirectory 'FCS_extended\FCS_extended_plugin.dll'
    if (-not (Test-Path -LiteralPath $contract)) { throw "FCS Extended's plugin contract was not found at $contract." }
    $viewName = 'ModTodoViewTests'
    $viewFolder = 'todo-view-tests'
    if ($HistoryViewOnly) { $viewName = 'HistoryArchiveViewTests'; $viewFolder = 'history-view-tests' }
    $viewOut = Join-Path $out $viewFolder
    New-Item -ItemType Directory -Force -Path $viewOut | Out-Null
    Copy-Item -LiteralPath $contract -Destination $viewOut
    $viewSources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
    & $compiler /nologo /target:exe /platform:x86 /debug:pdbonly /warnaserror+ "/main:$viewName" "/out:$viewOut\$viewName.exe" `
        /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        /reference:System.Runtime.Serialization.dll "/reference:$contract" `
        $viewSources (Join-Path $root "tests\$viewName.cs")
    if ($LASTEXITCODE -ne 0) { throw "Non-showing $viewName compilation failed." }
    & (Join-Path $viewOut "$viewName.exe")
    if ($LASTEXITCODE -ne 0) { throw "Non-showing $viewName checks failed." }
    return
}
$sources = @((Join-Path $root 'src\Core.cs'), (Join-Path $root 'src\Tracking.cs'), (Join-Path $root 'src\Journal.cs'),
    (Join-Path $root 'src\RangeExport.cs'), (Join-Path $root 'src\DraftWorkspace.cs'), (Join-Path $root 'src\SettingsStorage.cs'),
    (Join-Path $root 'src\ExportStorage.cs'), (Join-Path $root 'src\HistoryArchives.cs'),
    (Join-Path $root 'src\MarkdownExport.cs'),
    (Join-Path $root 'src\ModWork.cs'),
    (Join-Path $root 'src\ModTodoQuery.cs'),
    (Join-Path $root 'tests\CoreTests.cs'), (Join-Path $root 'tests\TrackingTests.cs'), (Join-Path $root 'tests\JournalTests.cs'),
    (Join-Path $root 'tests\ModWorkTests.cs'), (Join-Path $root 'tests\MarkdownTests.cs'),
    (Join-Path $root 'tests\HistoryArchiveTests.cs'))
& $compiler /nologo /target:exe /warnaserror+ "/out:$out\CoreTests.exe" `
    /reference:System.Core.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $out 'CoreTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
if ($FcsDirectory) {
    & (Join-Path $PSScriptRoot 'Build.ps1') -FcsDirectory $FcsDirectory
}
