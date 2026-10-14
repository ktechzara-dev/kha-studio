$workerPath = Resolve-Path "dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\KhaStudio.TiaWorker.exe"
Write-Host "Testing Worker: $workerPath"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $workerPath
$psi.Arguments = "--ipc"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

$cmd1 = '{"cmd":"list_instances"}'
Write-Host "Sending: $cmd1"
$proc.StandardInput.WriteLine($cmd1)
$proc.StandardInput.Flush()

Start-Sleep -Seconds 2

$proc.StandardInput.WriteLine('{"cmd":"exit"}')
$proc.StandardInput.Flush()

$out = $proc.StandardOutput.ReadToEnd()
$err = $proc.StandardError.ReadToEnd()
$proc.WaitForExit(3000)

Write-Host "OUTPUT:"
Write-Host $out
if ($err) {
    Write-Host "ERROR:"
    Write-Host $err
}
