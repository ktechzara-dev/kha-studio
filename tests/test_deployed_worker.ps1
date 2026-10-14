$workerPath = Resolve-Path "dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\TiaWorker\KhaStudio.TiaWorker.exe"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $workerPath
$psi.WorkingDirectory = Split-Path $workerPath
$psi.Arguments = "--ipc"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)
$proc.StandardInput.WriteLine('{"id":"1","cmd":"tia.instances"}')
$proc.StandardInput.Flush()
$proc.StandardInput.WriteLine('{"id":"2","cmd":"tia.exit"}')
$proc.StandardInput.Flush()

$out = $proc.StandardOutput.ReadToEnd()
$err = $proc.StandardError.ReadToEnd()
$proc.WaitForExit()

Write-Host "OUT: $out"
Write-Host "ERR: $err"
