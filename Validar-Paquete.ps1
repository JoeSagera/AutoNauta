param([string]$Version='2.4.5',[string]$PackagePath='')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Versión inválida'}
if(!$PackagePath){$PackagePath=Join-Path $PSScriptRoot ('outputs\releases\v'+$Version+'\AutoNauta-v'+$Version+'-windows-x64.zip')}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[IO.Compression.ZipFile]::OpenRead($PackagePath)
try{
    $taskNames=@($taskZip.Entries.FullName | Sort-Object)
    $taskExpected=@('AutoNauta.exe','LEEME.txt','LICENSE.txt','SHA256SUMS.txt')|Sort-Object
    if(@(Compare-Object $taskNames $taskExpected).Count -ne 0){throw 'El ZIP contiene archivos inesperados o faltantes'}
    $taskHashes=@{}
    foreach($taskName in @('AutoNauta.exe','LEEME.txt','LICENSE.txt')){
        $taskStream=$taskZip.GetEntry($taskName).Open();$taskSha=[Security.Cryptography.SHA256]::Create()
        try{$taskHashes[$taskName]=[BitConverter]::ToString($taskSha.ComputeHash($taskStream)).Replace('-','').ToLowerInvariant()}finally{$taskStream.Dispose();$taskSha.Dispose()}
    }
    $taskReader=[IO.StreamReader]::new($taskZip.GetEntry('SHA256SUMS.txt').Open())
    try{$taskLines=$taskReader.ReadToEnd().Trim() -split '\r?\n'}finally{$taskReader.Dispose()}
    if($taskLines.Count -ne 3){throw 'Sumas internas incompletas'}
    $taskSeen=@{}
    foreach($taskLine in $taskLines){
        if($taskLine -notmatch '^([a-f0-9]{64})  (AutoNauta\.exe|LEEME\.txt|LICENSE\.txt)$'){throw 'Formato de suma inválido'}
        if($taskSeen.ContainsKey($Matches[2]) -or $taskHashes[$Matches[2]] -ne $Matches[1]){throw 'Suma interna no coincide'}
        $taskSeen[$Matches[2]]=$true
    }
    foreach($taskPair in @(@('AutoNauta.exe','outputs\AutoNauta\AutoNauta.exe'),@('LEEME.txt','docs\GUIA-AMIGOS.txt'),@('LICENSE.txt','LICENSE'))){
        if($taskHashes[$taskPair[0]] -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $taskPair[1]) -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Un archivo empaquetado no coincide con la entrega revisada'}
    }
    if((Get-Item -LiteralPath (Join-Path $PSScriptRoot 'outputs\AutoNauta\AutoNauta.exe')).VersionInfo.FileVersion -ne ($Version+'.0')){throw 'Versión del ejecutable incorrecta'}
    $taskOuter=(Get-Content -LiteralPath (Join-Path (Split-Path $PackagePath -Parent) 'SHA256SUMS.txt') -Raw).Trim()
    $taskZipHash=(Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if($taskOuter -ne ($taskZipHash+'  '+[IO.Path]::GetFileName($PackagePath))){throw 'Suma externa no coincide'}
    Write-Output ('Paquete verificado: '+$Version+', cuatro archivos esperados, licencia, versiones y SHA256 correctos.')
}finally{$taskZip.Dispose()}
