param([string]$Version='2.4.4')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'La versión debe tener el formato mayor.menor.parche.'}
$taskExe=Join-Path $PSScriptRoot 'outputs\AutoNauta\AutoNauta.exe'
if(-not (Test-Path -LiteralPath $taskExe)){throw 'Primero compila AutoNauta.'}
$taskBinaryVersion=(Get-Item -LiteralPath $taskExe).VersionInfo.FileVersion
if($taskBinaryVersion -ne ($Version+'.0')){throw 'El ejecutable no corresponde a la versión de la Release.'}
$taskDestination=Join-Path $PSScriptRoot ('outputs\releases\v'+$Version)
New-Item -ItemType Directory -Path $taskDestination -Force|Out-Null
$taskZip=Join-Path $taskDestination ('AutoNauta-v'+$Version+'-windows-x64.zip')
if(Test-Path -LiteralPath $taskZip){throw 'El paquete ya existe. Consérvalo y revisa la versión antes de sustituirlo.'}
$taskStage=Join-Path $env:TEMP ('AutoNautaRelease-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage|Out-Null
Copy-Item -LiteralPath $taskExe -Destination (Join-Path $taskStage 'AutoNauta.exe')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\GUIA-AMIGOS.txt') -Destination (Join-Path $taskStage 'LEEME.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $taskStage 'LICENSE.txt')
$taskInside=@('AutoNauta.exe','LEEME.txt','LICENSE.txt')|ForEach-Object{(Get-FileHash -LiteralPath (Join-Path $taskStage $_) -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_}
$taskInside|Set-Content -LiteralPath (Join-Path $taskStage 'SHA256SUMS.txt') -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($taskStage,$taskZip,[IO.Compression.CompressionLevel]::Optimal,$false)
((Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($taskZip))|Set-Content -LiteralPath (Join-Path $taskDestination 'SHA256SUMS.txt') -Encoding utf8
[pscustomobject]@{Version=$Version;Paquete=$taskZip;Bytes=(Get-Item -LiteralPath $taskZip).Length;SHA256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant()}|ConvertTo-Json
