$files = Get-ChildItem "C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48\*.dll"
foreach ($f in $files) {
    try {
        $asm = [System.Reflection.Assembly]::LoadFrom($f.FullName)
        $t = $asm.GetType("Siemens.Engineering.TiaPortal")
        if ($t) {
            Write-Host "FOUND in: $($f.Name)"
        }
    } catch {
        # ignore
    }
}
