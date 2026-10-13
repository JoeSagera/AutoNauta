$ErrorActionPreference = 'Stop'
$taskExecutable = Join-Path $PSScriptRoot 'outputs\AutoNauta\AutoNauta.exe'
$taskResult = Join-Path $env:LOCALAPPDATA 'AutoNauta\diagnostico.json'
$taskProcess = Start-Process -FilePath $taskExecutable -ArgumentList ('--diagnostic "' + $taskResult + '"') -PassThru
$taskProcess.WaitForExit()
if (Test-Path -LiteralPath $taskResult) { Get-Content -LiteralPath $taskResult; Write-Host "Resultado: $taskResult" }

