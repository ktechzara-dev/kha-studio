# Build Kha Studio (.NET + Velopack) -> dotnet\Releases\KhaStudio-win-Setup.exe
# Build:            powershell -ExecutionPolicy Bypass -File dotnet\build.ps1 -Version 1.0.1
# Build + đăng lên GitHub Releases (cần token):
#   $env:GITHUB_TOKEN = "ghp_xxx"
#   powershell -ExecutionPolicy Bypass -File dotnet\build.ps1 -Version 1.0.1 -Publish
param(
  [Parameter(Mandatory)][string]$Version,
  [string]$RepoUrl = "",   # để trống = lấy <UpdateUrl> trong KhaStudio.csproj
  [switch]$Publish
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "KhaStudio\KhaStudio.csproj"
$pub = Join-Path $root "publish"
$rel = Join-Path $root "Releases"

if (-not (Test-Path (Join-Path $root "..\src\renderer\lib\jszip.min.js"))) {
  throw "Thiếu src\renderer\lib - chạy 'npm.cmd install' ở thư mục gốc trước."
}
if (-not $RepoUrl) { $RepoUrl = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup.UpdateUrl | Where-Object { $_ } | Select-Object -First 1 }
$isGh = $RepoUrl -match "github\.com"

# Lấy bản cũ từ GitHub để vpk tạo gói delta (cập nhật nhỏ gọn)
if ($Publish -and $isGh) {
  vpk download github --repoUrl $RepoUrl --token $env:GITHUB_TOKEN --outputDir $rel
}

if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 --self-contained -p:Version=$Version -p:UpdateUrl=$RepoUrl -o $pub
if ($LASTEXITCODE) { throw "dotnet publish lỗi" }

vpk pack --packId KhaStudio --packVersion $Version --packDir $pub --mainExe KhaStudio.exe `
  --packTitle "Kha Studio" --packAuthors "Kha.nm" --outputDir $rel
if ($LASTEXITCODE) { throw "vpk pack lỗi" }

if ($Publish) {
  if (-not $isGh) { throw "-Publish hiện chỉ hỗ trợ GitHub. Với thư mục mạng: copy nội dung $rel lên đó." }
  if (-not $env:GITHUB_TOKEN) { throw "Chưa đặt `$env:GITHUB_TOKEN" }
  vpk upload github --repoUrl $RepoUrl --token $env:GITHUB_TOKEN --publish --releaseName "Kha Studio $Version" --tag "v$Version" --outputDir $rel
  if ($LASTEXITCODE) { throw "vpk upload lỗi" }
}
Write-Host "Xong: $rel" -ForegroundColor Green
