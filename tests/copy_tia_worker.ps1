Remove-Item 'dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\KhaStudio.TiaWorker.exe*' -ErrorAction SilentlyContinue
Remove-Item 'dotnet\publish\KhaStudio.TiaWorker.exe*' -ErrorAction SilentlyContinue

$src = 'dotnet\KhaStudio\Modules\TiaOpenness\TiaWorker\bin\Release\net48\*'
$dst1 = 'dotnet\KhaStudio\bin\Debug\net8.0-windows\win-x64\TiaWorker'
$dst2 = 'dotnet\publish\TiaWorker'

New-Item -ItemType Directory -Force -Path $dst1 | Out-Null
New-Item -ItemType Directory -Force -Path $dst2 | Out-Null

Copy-Item $src -Destination $dst1 -Recurse -Force
Copy-Item $src -Destination $dst2 -Recurse -Force

Write-Host "Copy TiaWorker hoàn tất!"
