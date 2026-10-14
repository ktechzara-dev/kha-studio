$workerPath = Resolve-Path "dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\TiaWorker\KhaStudio.TiaWorker.exe"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $workerPath
$psi.Arguments = "--ipc"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$proc = [System.Diagnostics.Process]::Start($psi)

function Send-Cmd($cmdObj) {
    $json = ConvertTo-Json $cmdObj -Compress
    $proc.StandardInput.WriteLine($json)
    $proc.StandardInput.Flush()
}

Send-Cmd @{ id = "1"; cmd = "tia.connect"; pid = 10072 }
Send-Cmd @{ id = "2"; cmd = "tia.devices" }
Send-Cmd @{ id = "3"; cmd = "tia.tagTables" }
Send-Cmd @{ id = "4"; cmd = "tia.blocks" }
Send-Cmd @{ id = "5"; cmd = "tia.exit" }

while (-not $proc.StandardOutput.EndOfStream) {
    $line = $proc.StandardOutput.ReadLine()
    if ($line) {
        Write-Host "RESP: $line"
    }
}

$proc.WaitForExit()
