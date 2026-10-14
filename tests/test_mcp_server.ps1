$workerPath = Resolve-Path "dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\TiaWorker\KhaStudio.TiaWorker.exe"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $workerPath
$psi.WorkingDirectory = Split-Path $workerPath
$psi.Arguments = "--mcp"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

$initReq = '{"jsonrpc":"2.0","method":"initialize","id":1,"params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"GeminiClient","version":"1.0"}}}'
$proc.StandardInput.WriteLine($initReq)
$proc.StandardInput.Flush()

$toolsReq = '{"jsonrpc":"2.0","method":"tools/list","id":2,"params":{}}'
$proc.StandardInput.WriteLine($toolsReq)
$proc.StandardInput.Flush()

$proc.StandardInput.Close()

$out = $proc.StandardOutput.ReadToEnd()
$err = $proc.StandardError.ReadToEnd()
$proc.WaitForExit()

Write-Host "OUT: $out"
Write-Host "ERR: $err"
