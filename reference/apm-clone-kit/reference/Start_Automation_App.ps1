param([string]$ProjectRoot = '', [switch]$NoBrowser, [switch]$EngineOnly, [int]$Port = 18761)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$AppDir = $PSScriptRoot
$Session = [Guid]::NewGuid().ToString('N')
$Parents = @{}
$Projects = @{}
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$Listener = $null
$LogDir = Join-Path $env:LOCALAPPDATA 'AutomationProjectMaster'
$LogFile = Join-Path $LogDir 'startup.log'

function Get-Hash([string]$Path) {
    if (-not [System.IO.File]::Exists($Path)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Read-Json([string]$Path) {
    if (-not [System.IO.File]::Exists($Path)) { return $null }
    return [System.IO.File]::ReadAllText($Path, $Utf8) | ConvertFrom-Json
}
function Resolve-SafePath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative.StartsWith('/') -or $Relative.Contains('\') -or [System.IO.Path]::IsPathRooted($Relative)) {
        throw 'Duong dan tuong doi khong hop le.'
    }
    foreach ($Part in $Relative.Split('/')) {
        if ([string]::IsNullOrWhiteSpace($Part) -or $Part -eq '.' -or $Part -eq '..' -or $Part -match '[<>:"|?*\x00-\x1F]' -or $Part -match '[ .]$' -or $Part -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\..*)?$') {
            throw 'Ten folder/file khong hop le.'
        }
    }
    $FullRoot = [System.IO.Path]::GetFullPath($Root)
    $Prefix = $FullRoot.TrimEnd('\', '/') + '\'
    $FullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($FullRoot, $Relative.Replace('/', '\')))
    if (-not $FullPath.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Duong dan nam ngoai du an.' }
    return $FullPath
}
function Choose-Folder([string]$Description) {
    $Dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $Dialog.Description = $Description
    $Dialog.ShowNewFolderButton = $true
    try {
        if ($Dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return '' }
        return $Dialog.SelectedPath
    } finally { $Dialog.Dispose() }
}
function Register-Project([string]$Root) {
    $Root = [System.IO.Path]::GetFullPath($Root)
    $InfoPath = Join-Path $Root '_PROJECT_INFO.json'
    $StatePath = Join-Path $Root '_AUTOMATION_APP.json'
    if (-not [System.IO.File]::Exists($InfoPath) -and -not [System.IO.File]::Exists($StatePath)) {
        throw 'Chon root du an co _PROJECT_INFO.json hoac _AUTOMATION_APP.json.'
    }
    $Key = [Guid]::NewGuid().ToString('N')
    $Projects[$Key] = $Root
    return @{ projectId = $Key; path = $Root; info = (Read-Json $InfoPath); project = (Read-Json $StatePath);
        config = (Read-Json (Join-Path $Root '_PROJECT_CONFIG.json')); roadmap = (Read-Json (Join-Path $Root '_PROJECT_ROADMAP.json'));
        hash = (Get-Hash $StatePath) }
}
function Get-ProjectRoot([string]$Id) {
    if (-not $Projects.ContainsKey($Id)) { throw 'Mo lai du an de ket noi voi thu muc tren may.' }
    return $Projects[$Id]
}
function Write-AtomicJson([string]$Path, [object]$Value) {
    $Temp = $Path + '.tmp-' + [Guid]::NewGuid().ToString('N')
    try {
        [System.IO.File]::WriteAllText($Temp, ($Value | ConvertTo-Json -Depth 80), $Utf8)
        if ([System.IO.File]::Exists($Path)) { [System.IO.File]::Replace($Temp, $Path, $null) }
        else { [System.IO.File]::Move($Temp, $Path) }
    } finally { if ([System.IO.File]::Exists($Temp)) { [System.IO.File]::Delete($Temp) } }
}
function Handle-Api([string]$Route, [object]$Body) {
    switch ($Route) {
        'heartbeat' { return @{ ok = $true } }
        'startup' {
            if ($ProjectRoot -and [System.IO.Directory]::Exists($ProjectRoot)) { return Register-Project $ProjectRoot }
            return @{ ok = $true }
        }
        'pick-parent' {
            $Path = Choose-Folder 'Chon thu muc CHA de tao du an moi'
            if (-not $Path) { return @{ cancelled = $true } }
            $Key = [Guid]::NewGuid().ToString('N'); $Parents[$Key] = $Path
            return @{ parentId = $Key; path = $Path }
        }
        'open-project' {
            $Path = Choose-Folder 'Chon root du an co _PROJECT_INFO.json'
            if (-not $Path) { return @{ cancelled = $true } }
            return Register-Project $Path
        }
        'create-project' {
            if (-not $Parents.ContainsKey([string]$Body.parentId)) { throw 'Chon lai noi luu du an.' }
            $Parent = $Parents[[string]$Body.parentId]
            $Name = [string]$Body.folderName
            if ($Name.Contains('/') -or $Name.Contains('\') -or $Name.Length -gt 145) { throw 'Ten thu muc du an khong hop le.' }
            $Target = Resolve-SafePath $Parent $Name
            if ([System.IO.Directory]::Exists($Target) -or [System.IO.File]::Exists($Target)) { throw 'Thu muc du an da ton tai. Doi ma/ten hoac chon noi luu khac.' }
            if (@($Body.directories).Count -gt 6000 -or @($Body.files).Count -gt 500) { throw 'Cau truc vuot gioi han.' }
            $Stage = Join-Path $Parent ('.apm-staging-' + [Guid]::NewGuid().ToString('N'))
            $Seen = @{}
            foreach ($Relative in @($Body.directories)) {
                $CheckPath = Resolve-SafePath $Target ([string]$Relative)
                if ($CheckPath.Length -gt 240) { throw ('Duong dan vuot 240 ky tu: ' + $Relative) }
                if ((Resolve-SafePath $Stage ([string]$Relative)).Length -gt 240) { throw 'Duong dan staging qua dai. Chon noi luu ngan hon.' }
            }
            foreach ($File in @($Body.files)) {
                $CheckPath = Resolve-SafePath $Target ([string]$File.path)
                if ($CheckPath.Length -gt 240) { throw ('Duong dan vuot 240 ky tu: ' + $File.path) }
                if ((Resolve-SafePath $Stage ([string]$File.path)).Length -gt 240) { throw 'Duong dan staging qua dai. Chon noi luu ngan hon.' }
                if ($Seen.ContainsKey($CheckPath)) { throw 'File trung duong dan.' }; $Seen[$CheckPath] = $true
                if ([string]$File.base64 -notmatch '^[A-Za-z0-9+/]*={0,2}$') { throw 'Du lieu file khong hop le.' }
            }
            try {
                [void][System.IO.Directory]::CreateDirectory($Stage)
                foreach ($Relative in @($Body.directories)) { [void][System.IO.Directory]::CreateDirectory((Resolve-SafePath $Stage ([string]$Relative))) }
                foreach ($File in @($Body.files)) {
                    $Dest = Resolve-SafePath $Stage ([string]$File.path)
                    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Dest))
                    [System.IO.File]::WriteAllBytes($Dest, [Convert]::FromBase64String([string]$File.base64))
                }
                if (-not [System.IO.File]::Exists((Join-Path $Stage '_PROJECT_INFO.json')) -or -not [System.IO.File]::Exists((Join-Path $Stage '_AUTOMATION_APP.json'))) { throw 'Goi du an thieu metadata.' }
                [System.IO.Directory]::Move($Stage, $Target)
            } finally {
                if ([System.IO.Directory]::Exists($Stage)) { Remove-Item -LiteralPath $Stage -Recurse -Force }
            }
            return Register-Project $Target
        }
        'save-project' {
            $Root = Get-ProjectRoot ([string]$Body.projectId)
            $Path = Join-Path $Root '_AUTOMATION_APP.json'
            if ((Get-Hash $Path) -ne [string]$Body.expectedHash) { throw 'Ho so tren o dia da thay doi tu cua so khac. Xuat JSON va mo lai du an de tranh ghi de.' }
            if ($Body.project.schemaVersion -ne 4 -or -not $Body.project.code -or -not $Body.project.name) { throw 'Ho so ung dung khong hop le.' }
            $Information = @($Body.informationFiles)
            $Allowed = @('00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html', '00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.txt')
            $Updates = @{}
            foreach ($File in $Information) {
                if (-not $File) { continue }
                $Relative = [string]$File.path
                if ($Relative -notin $Allowed -or $Updates.ContainsKey($Relative)) { throw 'Ho so thong tin khong hop le.' }
                $Bytes = [Convert]::FromBase64String([string]$File.base64)
                if ($Bytes.Length -gt 1048576) { throw 'Ho so thong tin vuot 1 MB.' }
                $Updates[$Relative] = $Bytes
            }
            foreach ($Relative in $Updates.Keys) {
                $Destination = Resolve-SafePath $Root $Relative
                [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Destination))
                $TempFile = $Destination + '.tmp-' + [Guid]::NewGuid().ToString('N')
                try {
                    [System.IO.File]::WriteAllBytes($TempFile, $Updates[$Relative])
                    if ([System.IO.File]::Exists($Destination)) { [System.IO.File]::Replace($TempFile, $Destination, $Destination + '.bak') }
                    else { [System.IO.File]::Move($TempFile, $Destination) }
                } finally { if ([System.IO.File]::Exists($TempFile)) { [System.IO.File]::Delete($TempFile) } }
            }
            Write-AtomicJson $Path $Body.project
            return @{ ok = $true; hash = (Get-Hash $Path) }
        }
        'open-path' {
            $Root = Get-ProjectRoot ([string]$Body.projectId)
            $Path = Resolve-SafePath $Root ([string]$Body.relativePath)
            if (-not [System.IO.Directory]::Exists($Path) -and -not [System.IO.File]::Exists($Path)) { throw 'Folder/file nay chua co trong du an. Ung dung khong tu tao khi bam Mo.' }
            if ([System.IO.File]::Exists($Path) -and [System.IO.Path]::GetExtension($Path) -notin @('.xlsx', '.docx', '.pptx', '.pdf', '.txt', '.md', '.csv', '.json')) { throw 'Mo file engineering bang phan mem tuong ung tu Explorer.' }
            $Start = New-Object System.Diagnostics.ProcessStartInfo
            $Start.FileName = $Path; $Start.UseShellExecute = $true
            [void][System.Diagnostics.Process]::Start($Start)
            return @{ ok = $true }
        }
        default { throw 'Thao tac khong duoc ho tro.' }
    }
}
function Send-Response([System.IO.Stream]$Stream, [int]$Status, [string]$Type, [byte[]]$Bytes) {
    $Reason = if ($Status -eq 200) { 'OK' } elseif ($Status -eq 403) { 'Forbidden' } elseif ($Status -eq 404) { 'Not Found' } else { 'Bad Request' }
    $Header = 'HTTP/1.1 ' + $Status + ' ' + $Reason + "`r`nContent-Type: " + $Type + "`r`nContent-Length: " + $Bytes.Length + "`r`nConnection: close`r`nX-Content-Type-Options: nosniff`r`nCache-Control: no-store`r`nReferrer-Policy: no-referrer`r`nContent-Security-Policy: default-src 'self' data: blob:; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'none'`r`n`r`n"
    $HeaderBytes = [System.Text.Encoding]::ASCII.GetBytes($Header)
    $Stream.Write($HeaderBytes, 0, $HeaderBytes.Length); $Stream.Write($Bytes, 0, $Bytes.Length); $Stream.Flush()
}
if ($EngineOnly) { return }
try {
    [void][System.IO.Directory]::CreateDirectory($LogDir)
    $HtmlPath = Join-Path $AppDir 'Automation_Project_Master.html'
    if (-not [System.IO.File]::Exists($HtmlPath)) { throw 'Thieu Automation_Project_Master.html. Giai nen TOAN BO goi ung dung vao cung thu muc.' }
    $Html = [System.IO.File]::ReadAllText($HtmlPath, $Utf8).Replace('content="__DESKTOP_SESSION__"', ('content="' + $Session + '"'))
    for ($TryPort = $Port; $TryPort -lt $Port + 20; $TryPort++) {
        try { $Listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, [int]$TryPort); $Listener.Start(); $Port = $TryPort; break }
        catch { $Listener = $null }
    }
    if ($null -eq $Listener) { throw 'Khong tim duoc cong local trong. Dong cac cua so ung dung cu va thu lai.' }
    $Url = 'http://127.0.0.1:' + $Port + '/'
    if (-not $NoBrowser) {
        $Edge = @((Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'), (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe')) | Where-Object { [System.IO.File]::Exists($_) } | Select-Object -First 1
        $Chrome = @((Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'), (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe')) | Where-Object { [System.IO.File]::Exists($_) } | Select-Object -First 1
        if ($Edge) { Start-Process -FilePath $Edge -ArgumentList @("--app=$Url", '--new-window') }
        elseif ($Chrome) { Start-Process -FilePath $Chrome -ArgumentList @("--app=$Url", '--new-window') }
        else { Start-Process $Url }
    }
    $LastRequest = [DateTime]::UtcNow
    while (([DateTime]::UtcNow - $LastRequest).TotalMinutes -lt 10) {
        if (-not $Listener.Pending()) { Start-Sleep -Milliseconds 100; continue }
        $Client = $Listener.AcceptTcpClient(); $Stream = $Client.GetStream(); $Stream.ReadTimeout = 15000; $Stream.WriteTimeout = 15000
        try {
            $HeaderBytes = New-Object 'System.Collections.Generic.List[byte]'
            $LastFour = ''
            while ($HeaderBytes.Count -lt 32768) {
                $Byte = $Stream.ReadByte(); if ($Byte -lt 0) { throw 'HTTP request chua day du.' }
                $HeaderBytes.Add([byte]$Byte); $LastFour += [char]$Byte; if ($LastFour.Length -gt 4) { $LastFour = $LastFour.Substring($LastFour.Length - 4) }
                if ($LastFour -eq "`r`n`r`n") { break }
            }
            if ($LastFour -ne "`r`n`r`n") { throw 'HTTP header vuot gioi han.' }
            $HeadersText = [System.Text.Encoding]::ASCII.GetString($HeaderBytes.ToArray())
            $Lines = $HeadersText -split "`r`n"; $First = $Lines[0] -split ' '
            $Method = $First[0]; $Path = $First[1]; $Headers = @{}
            foreach ($Line in $Lines | Select-Object -Skip 1) { $Colon = $Line.IndexOf(':'); if ($Colon -gt 0) { $Headers[$Line.Substring(0, $Colon).ToLowerInvariant()] = $Line.Substring($Colon + 1).Trim() } }
            if ($Headers['host'] -ne ('127.0.0.1:' + $Port)) { Send-Response $Stream 403 'application/json; charset=utf-8' ($Utf8.GetBytes('{"error":"Host rejected"}')); continue }
            $LastRequest = [DateTime]::UtcNow
            if ($Method -eq 'GET' -and ($Path -eq '/' -or $Path -eq '/Automation_Project_Master.html')) {
                Send-Response $Stream 200 'text/html; charset=utf-8' ($Utf8.GetBytes($Html)); continue
            }
            if ($Method -eq 'POST' -and $Path -match '^/api/([a-z-]+)$') {
                $Route = $Matches[1]
                if ($Headers['x-apm-session'] -ne $Session -or ($Headers.ContainsKey('origin') -and $Headers['origin'] -ne $Url.TrimEnd('/'))) {
                    Send-Response $Stream 403 'application/json; charset=utf-8' ($Utf8.GetBytes('{"error":"Session rejected"}')); continue
                }
                $Length = 0
                if (-not [int]::TryParse([string]$Headers['content-length'], [ref]$Length) -or $Length -lt 0 -or $Length -gt 33554432) { throw 'Du lieu request vuot gioi han 32 MB.' }
                $Bytes = New-Object byte[] $Length; $Read = 0
                while ($Read -lt $Length) { $N = $Stream.Read($Bytes, $Read, $Length - $Read); if ($N -eq 0) { throw 'Body request chua day du.' }; $Read += $N }
                $Body = $Utf8.GetString($Bytes) | ConvertFrom-Json
                $Result = Handle-Api $Route $Body
                Send-Response $Stream 200 'application/json; charset=utf-8' ($Utf8.GetBytes(($Result | ConvertTo-Json -Depth 80 -Compress)))
            } else { Send-Response $Stream 404 'application/json; charset=utf-8' ($Utf8.GetBytes('{"error":"Not found"}')) }
        } catch {
            try { Send-Response $Stream 400 'application/json; charset=utf-8' ($Utf8.GetBytes((@{ error = $_.Exception.Message } | ConvertTo-Json -Compress))) } catch {}
        } finally { $Stream.Dispose(); $Client.Close() }
    }
} catch {
    [System.IO.File]::WriteAllText($LogFile, ([DateTime]::Now.ToString('s') + "`r`n" + $_.ToString()), $Utf8)
    [void][System.Windows.Forms.MessageBox]::Show(('Khong mo duoc Automation Project Master.' + "`r`n" + $_.Exception.Message + "`r`nLog: " + $LogFile), 'Automation Project Master', 'OK', 'Error')
    exit 1
} finally { if ($Listener) { $Listener.Stop() } }
