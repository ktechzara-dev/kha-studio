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

# Token GitHub
$token = $env:GITHUB_TOKEN
$tokenFile = Join-Path $root "github-token.txt"
if (-not $token -and (Test-Path $tokenFile)) { $token = (Get-Content $tokenFile -Raw).Trim() }

# Lấy bản cũ từ GitHub để vpk tạo gói delta (cập nhật nhỏ gọn)
if ($Publish -and $isGh) {
  if ($token) {
    vpk download github --repoUrl $RepoUrl --token $token --outputDir $rel
  } else {
    vpk download github --repoUrl $RepoUrl --outputDir $rel
  }
}

if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 --self-contained -p:Version=$Version -p:UpdateUrl=$RepoUrl -o $pub
if ($LASTEXITCODE) { throw "dotnet publish lỗi" }

$iconFile = Join-Path $root "KhaStudio\app.ico"
if (Test-Path $iconFile) {
  vpk pack --packId KhaStudio --packVersion $Version --packDir $pub --mainExe KhaStudio.exe `
    --packTitle "Kha Studio" --packAuthors "Kha.nm" --runtime win-x64 --noPortable --icon $iconFile --outputDir $rel
} else {
  vpk pack --packId KhaStudio --packVersion $Version --packDir $pub --mainExe KhaStudio.exe `
    --packTitle "Kha Studio" --packAuthors "Kha.nm" --runtime win-x64 --noPortable --outputDir $rel
}
if ($LASTEXITCODE) { throw "vpk pack lỗi" }

# Loại bỏ file portable zip cũ nếu còn sót lại để không tốn thời gian upload
Remove-Item (Join-Path $rel "*-Portable.zip") -Force -ErrorAction SilentlyContinue

if ($Publish) {
  if (-not $isGh) { throw "-Publish hiện chỉ hỗ trợ GitHub. Với thư mục mạng: copy nội dung $rel lên đó." }
  if (-not $token) { throw "Chưa đặt `$env:GITHUB_TOKEN hoặc file dotnet\github-token.txt" }
  vpk upload github --repoUrl $RepoUrl --token $token --publish --merge --releaseName "Kha Studio $Version" --tag "v$Version" --timeout 60 --outputDir $rel
  if ($LASTEXITCODE) { throw "vpk upload lỗi" }
}
Write-Host "Xong: $rel" -ForegroundColor Green
