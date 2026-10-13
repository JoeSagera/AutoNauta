param([Parameter(Mandatory=$true)][int]$TargetProcess,[int]$Seconds=60,[Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class AutoNautaResourceReader {
 [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process,uint flag);
}
'@
$taskWatch=[Diagnostics.Stopwatch]::StartNew()
$taskRows=[Collections.Generic.List[object]]::new()
$taskFirstCpu=$null
$taskCores=[Environment]::ProcessorCount
while($taskWatch.Elapsed.TotalSeconds -lt $Seconds){
 $taskProcess=Get-Process -Id $TargetProcess
 $taskProcess.Refresh()
 if($null -eq $taskFirstCpu){$taskFirstCpu=$taskProcess.TotalProcessorTime.TotalSeconds}
 $taskRows.Add([pscustomobject]@{second=$taskWatch.Elapsed.TotalSeconds;cpuSeconds=$taskProcess.TotalProcessorTime.TotalSeconds;workingMiB=$taskProcess.WorkingSet64/1MB;privateMiB=$taskProcess.PrivateMemorySize64/1MB;handles=$taskProcess.HandleCount;threads=$taskProcess.Threads.Count;gdi=[AutoNautaResourceReader]::GetGuiResources($taskProcess.Handle,0);userObjects=[AutoNautaResourceReader]::GetGuiResources($taskProcess.Handle,1)})
 Start-Sleep -Seconds 2
}
$taskProcess=Get-Process -Id $TargetProcess
$taskCpuDelta=$taskProcess.TotalProcessorTime.TotalSeconds-$taskFirstCpu
$taskSummary=[pscustomobject]@{durationSeconds=$taskWatch.Elapsed.TotalSeconds;logicalProcessors=$taskCores;cpuSeconds=$taskCpuDelta;cpuPercentOneCore=100*$taskCpuDelta/$taskWatch.Elapsed.TotalSeconds;cpuPercentWholeMachine=100*$taskCpuDelta/$taskWatch.Elapsed.TotalSeconds/$taskCores;first=$taskRows[0];last=$taskRows[$taskRows.Count-1];peakWorkingMiB=($taskRows|Measure-Object workingMiB -Maximum).Maximum;peakPrivateMiB=($taskRows|Measure-Object privateMiB -Maximum).Maximum;samples=$taskRows}
$taskSummary|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $ResultPath -Encoding utf8
$taskSummary|Select-Object durationSeconds,cpuSeconds,cpuPercentOneCore,cpuPercentWholeMachine,peakWorkingMiB,peakPrivateMiB|ConvertTo-Json
