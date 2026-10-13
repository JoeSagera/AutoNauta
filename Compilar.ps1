$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $taskCompiler)) { throw 'No se encontró el compilador .NET Framework x64.' }
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$taskBuildDirectory = Join-Path $env:TEMP ('NautaBuild-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBuildDirectory | Out-Null
$taskTests = Join-Path $taskBuildDirectory 'Pruebas.exe'
& $taskCompiler /nologo /platform:x64 /target:exe "/out:$taskTests" /main:NautaReconnect.TestEntry /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de las pruebas.' }
& $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas. No se reemplazó el programa.' }
$taskOutput = Join-Path $PSScriptRoot 'outputs\AutoNauta\AutoNauta.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $taskOutput) -Force | Out-Null
& $taskCompiler /nologo /platform:x64 /target:winexe "/out:$taskOutput" /main:NautaReconnect.Program /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación. Cierra AutoNauta antes de recompilar.' }
Write-Host 'Programa compilado; pruebas correctas.'

