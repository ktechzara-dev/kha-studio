$baseDll = "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\Siemens.Engineering.Base.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($baseDll)
$type = $asm.GetType("Siemens.Engineering.TiaPortal")
$procs = $type.GetMethod("GetProcesses", [System.Reflection.BindingFlags]"Public, Static").Invoke($null, $null)

foreach ($p in $procs) {
    Write-Host "Item type: $($p.GetType().FullName)"
    $p.GetType().GetProperties() | ForEach-Object {
        Write-Host "  $($_.Name) ($($_.PropertyType.Name)) = $($_.GetValue($p))"
    }
}
