# FORM CLONE APP — Automation Project Master (APM) v4.2.1 → v5 (Rev 3)

> **Rev 3 — quyết định của chủ app:** (1) bỏ PowerShell/`.cmd`, dùng launcher Electron; (2) **gắn vào Kha Studio** như một tool; (3) **KHÔNG sync liên tục, KHÔNG đăng nhập, KHÔNG backend.** Thay bằng nút **"Cập nhật lên"** (xuất bản chụp dự án vào thư mục Google Drive) và **chế độ Admin** (chỉ xem, đọc các bản chụp đó). Xem mục 13.

> Tài liệu này dành cho **Antigravity IDE (agent)**. Đọc hết file này, rồi đọc thư mục `reference/` trước khi viết code.
> Mọi thông tin dưới đây được trích từ file `AutomationProjectMaster_v4_2_1.exe` gốc (xem mục 12 để biết độ tin cậy).

---

## 1. TỔNG QUAN

| Mục | Nội dung |
|---|---|
| Tên app | Automation Project Master (APM) — "Engineering Workspace" |
| Phiên bản gốc | 4.2.1 (data schema project = 4; master data v3.0.x–4.0.0) |
| Mục đích | Quản lý dự án tự động hóa (PLC/HMI/Robot/Vision...): tạo bộ thư mục chuẩn + biểu mẫu, theo dõi lộ trình 12 giai đoạn, thiết bị & IP, issue, đo Cycle Time |
| Người dùng | 1 kỹ sư/PM, dùng offline trên Windows |
| Ngôn ngữ UI | Tiếng Việt |
| Kiến trúc gốc | Vỏ .NET WinForms + WebView2, bên trong là **1 file HTML duy nhất** (vanilla JS, không framework). Có bản engine PowerShell + HTTP localhost (`reference/Start_Automation_App.ps1`) |
| Phần cứng/OS | Windows 10/11 |

## 2. MỤC TIÊU CLONE

- [ ] Giữ **đúng hành vi** và dữ liệu như bản gốc (project JSON mở được qua lại giữa bản gốc và bản clone).
- [ ] Chạy offline, không cần mạng.
- [ ] Dễ thêm tính năng mới sau này (kiến trúc module hóa).
- [ ] Đóng gói được file cài đặt `.exe`.

**Trong phạm vi bản clone (v5):** chạy như một tool bên trong Kha Studio; nút **Cập nhật lên** (publish snapshot); **chế độ Admin** xem tổng hợp nhiều người/nhiều dự án (chỉ đọc).
**Giữ nguyên:** app **hoàn toàn offline, không có code kết nối mạng**. Chia sẻ dữ liệu = Google Drive for Desktop đồng bộ thư mục (không phải việc của app).
**Ngoài phạm vi:** đồng bộ realtime, đăng nhập/tài khoản, sửa dữ liệu của người khác, backend/database, đồng bộ file Office. (Để ngỏ: thêm provider Drive API hoặc Supabase sau này qua interface ở 13.9.)

## 3. TECH STACK & CÁCH GẮN VÀO KHA STUDIO

APM là **một tool** trong Kha Studio (app Electron mày đã có: `kha-studio-app.zip`, launcher đăng ký tool bằng mảng `TOOLS` trong `src/renderer/index.html`).

| Lớp | Lựa chọn |
|---|---|
| Shell | Electron của Kha Studio (dùng chung cửa sổ, menu, `Ctrl+H` về trang chủ) |
| UI APM | Vanilla JS/HTML/CSS như bản gốc, tách file; **không** kéo framework |
| Engine file hệ thống | Node `fs` trong main process (thay hoàn toàn PowerShell) |
| ZIP / template | `jszip` (local) |
| Chia sẻ dữ liệu | Thư mục Google Drive (Drive for Desktop) — ghi/đọc file JSON thường, **không dùng API/OAuth** (mục 13) |
| Đóng gói | electron-builder (đã có trong Kha Studio) |

**Thay đổi bắt buộc ở Kha Studio hiện tại** (bản cũ đang `sandbox: true`, **không có preload**):
1. Thêm `src/preload/preload.js` dùng `contextBridge.exposeInMainWorld('apm', { api, onEvent })` và đưa vào `webPreferences.preload`. Giữ `contextIsolation: true`, `nodeIntegration: false`.
2. Thêm `src/main/ipc/apm.js` đăng ký các `ipcMain.handle('apm:<route>')` (mục 7) và gọi trong `main.js`.
3. Thêm 1 dòng vào `TOOLS`: `{ icon:'🏗️', name:'Automation Project Master', desc:'Quản lý dự án tự động hóa...', path:'tools/apm/index.html' }`.
4. Đăng ký protocol `khastudio://` (mở dự án từ `Mo_Du_An.url`) và `app.requestSingleInstanceLock()`.
5. Thêm CSP cho `tools/apm/index.html`: `default-src 'self'; img-src 'self' data:; connect-src 'none'` — app **không có kết nối mạng**.
6. Pallet tool cũ **không được ảnh hưởng** (không đổi file của nó).

Cấu trúc thư mục mục tiêu (thêm vào Kha Studio):
```
src/
  main.js                       (sửa: preload, protocol, gọi ipc/apm)
  preload/preload.js            (mới)
  main/ipc/apm.js               (mới: handler mục 7)
  main/apm/fs-safe.js           resolveSafePath
  main/apm/workspace.js         đọc/ghi workspace.json (userData)
  main/apm/publish/             publisher.js, scanner.js (mục 13)
  renderer/tools/apm/
    index.html
    css/app.css
    js/core.js                  APMCore (mục 6, có unit test)
    js/ui/publish.js, admin.js  (mục 13)
    js/ui/*.js                  mỗi view 1 file (mục 5)
    data/*.json                 reference/data_*.json
    assets/templates/           18 file mẫu
tests/                          core.test.js, fs-safe.test.js, publish.test.js
```

## 4. DỮ LIỆU MASTER (copy nguyên từ `reference/`)

| File | Nội dung | Số lượng |
|---|---|---|
| `data_roadmap.json` | `stages[]` (12), `edges[]` (14), `continuous[]` (5), `folders[]` (247 mô tả thư mục) | 12 giai đoạn |
| `data_catalog.json` | `modules[]` (26 bộ môn: id, name, folder, children), `softwareGroups[]` (8 nhóm hãng) | 26 / 8 |
| `data_presets.json` | 9 preset: full_line, single_machine, robot_cell, biw_cell, control_system, vision_system, retrofit, tender, custom | 9 |
| `data_scopes.json` | 9 phạm vi: full, tender, concept, design, simulation, programming, commissioning, handover, service (mỗi scope có `modules[]`) | 9 |
| `data_templates.json` | 18 biểu mẫu: id, module, destination, revision, required, sha256 | 18 |
| `templates/*.xlsx|docx|pptx` | File mẫu thật (đã giải mã từ base64) | 18 file |

**12 giai đoạn (order → id):** 1 discovery · 2 tender · 3 concept · 4 design · 5 procurement · 6 simulation · 7 programming · 8 integration · 9 fat · 10 sat · 11 handover · 12 service.
Mỗi stage có: `title, lane, position[x,y], modules[], scopes[], when, purpose, entry[], actions[], outputs[], templates[], dependencies[], checks[{id,text,modules[]}]`.

**Module "continuous" (chạy suốt dự án):** management, followup, safety, quality, archive.

## 5. MÀN HÌNH / VIEW (sidebar 9 mục)

Layout: sidebar trái 236px (nền `#111d31`, brand "APM"), topbar 82px (breadcrumb + trạng thái lưu + dropdown chọn dự án), content tối đa 1700px. Màu chính `#2463eb`, nền `#f3f6fa`, font Segoe UI, radius 12px. Responsive 3 breakpoint (1150 / 800 / 460px) và có CSS `@media print`.

| # | View id | Tên | Chức năng |
|---|---|---|---|
| 1 | `dashboard` | Tổng quan | 4 metric card, thanh tiến độ %, lưới ô giai đoạn (trạng thái màu), danh sách dự án (project-cards), tìm kiếm, panel "bắt đầu" + preset nhanh, dự án demo |
| 2 | `create` | Tạo dự án mới | Wizard 4 bước: **(1) Thông tin** → **(2) Phạm vi & bộ môn** → **(3) Hãng & thiết bị** → **(4) Kiểm tra & tạo**. Panel tóm tắt sticky bên phải |
| 3 | `roadmap` | Lộ trình dự án | Danh sách 12 stage (trái) + chi tiết stage (phải): trạng thái, owner, due, ghi chú, checklist, outputs (link mở thư mục), templates |
| 4 | `folders` | Cấu trúc & hướng dẫn | Lọc theo module, chỉ-dự-án-này; thẻ thư mục (path, when, purpose); nút mở thư mục thật |
| 5 | `templates` | Biểu mẫu kỹ thuật | Lưới 18 mẫu, lọc theo loại (xlsx/docx/pptx), nút mở/tải |
| 6 | `devices` | Thiết bị & mạng | Bảng thiết bị (ID, bộ môn, hãng, chức năng, model, IP, phiên bản), modal thêm/sửa, import/export CSV |
| 7 | `issues` | Theo dõi Issue | CRUD issue: title, description, severity (High/Medium/Low), status (Open/In Progress/Closed), owner, due; lọc theo status |
| 8 | `ct` | Đo Cycle Time | Bảng CT: station, model, target, quantity, unit, note, **5 lần đo**; tính live (trung bình, min, max, chu kỳ/phút, sản phẩm/phút, sản phẩm/giờ, độ lệch so với target) |
| 9 | `settings` | Sao lưu & hướng dẫn | Sao lưu/khôi phục JSON toàn bộ, xuất hồ sơ dự án, xuất gói ZIP V4, gỡ dự án khỏi danh sách, hướng dẫn 2 cột; **mới:** "Cập nhật lên & chia sẻ" (cấu hình + Trạng thái chia sẻ + mở lại Trình hướng dẫn), "Chế độ Admin" (mục 13.5, 13.10) |

Thành phần UI dùng chung: modal (+ wide), toast, confirm, badge (blue/green/red/amber/gray), notice (info/warning/error), steps, progress bar, table-scroll.

## 6. LOGIC NGHIỆP VỤ (APMCore) — port nguyên văn từ `reference/Automation_Project_Master.html` (script thứ 2)

Hàm cần có (pure functions, viết unit test):

- `makeProject(D, input)` — chuẩn hóa/sanitize project. Schema:
  ```
  schemaVersion: 4, appVersion, id, code(≤40), name(≤100), customer, manager, plcEngineer,
  robotEngineer, author, revision (mặc định 'R00'), startDate, dueDate (YYYY-MM-DD),
  preset, scopes[], modules[], software[], devices[≤500], stages{}, issues[≤2000], ct[≤2000],
  createdAt, updatedAt, demo, sourceVersion
  ```
- `suggestedModules(D, preset, scopes)` — module gợi ý = preset ∩ scopes, luôn thêm `management, templates, archive`; thêm `tender` nếu scope có tender, `service` nếu có service.
- `validateProject / validateDevice` — xem quy tắc ở 6.1.
- `applies(stage, p)`, `relevantChecks(stage, p)`, `progress(D, p)` → `{total, done, na, blocked, percent}`.
- `setStageStatus(D, p, id, status)` — xem 6.2.
- `buildPlan(D, p)` → `{folderName, directories[], templates[], devicePaths{}, software[]}` — sinh cây thư mục.
- `ctStats(c)` — thống kê Cycle Time (6.4).
- `informationFiles(D, p)` — sinh `00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html` (trang A4 in được) và `.txt`.
- `csv(rows)` — BOM `\ufeff`, CRLF, chống CSV-injection (ô bắt đầu bằng `= + - @ \t \r` thì thêm `'`).
- `zip(entries)` — tự viết ZIP store (không nén); có thể thay bằng jszip.

### 6.1 Quy tắc validate
- **Mã dự án:** regex `^[A-Za-z0-9][A-Za-z0-9_,.\-]{0,39}$`, không kết thúc bằng `.` hoặc khoảng trắng.
- **Tên dự án:** không trống, ≤100 ký tự, không chứa `< > : " / \ | ? *`, không phải tên Windows reserved (`CON, PRN, AUX, NUL, COM1-9, LPT1-9`).
- Phải chọn ≥1 bộ môn; `dueDate ≥ startDate`.
- Tên thư mục dự án = `code + '_' + name (khoảng trắng → '_')`, tối đa **145** ký tự.
- **Đường dẫn tương đối** tối đa: `folderName.length + path.length + 4 ≤ 220`; phía host ≤ 240 ký tự tuyệt đối.
- **Thiết bị:** `id` regex `^[A-Za-z][A-Za-z0-9_-]{0,31}$` (không reserved, không trùng, không phân biệt hoa/thường); `module` phải thuộc module đã chọn; `software` phải thuộc nhóm hãng `perDevice` của module đó; `ip` IPv4 hợp lệ (mỗi phần 0–255) hoặc để trống, **không trùng IP** thiết bị khác.

### 6.2 Quy tắc trạng thái giai đoạn
Trạng thái: `not_started` (Chưa bắt đầu) · `in_progress` (Đang thực hiện) · `blocked` (Đang vướng) · `completed` (Hoàn tất) · `not_applicable` (Không áp dụng).
- Stage chỉ áp dụng khi: `stage.modules` giao với `p.modules` **và** (`p.scopes` có `full` hoặc giao với `stage.scopes`).
- Chọn `completed` ⇒ **phải tick đủ** checklist liên quan (check không có `modules` hoặc có module nằm trong dự án). Nếu thiếu, ném lỗi "Đánh dấu đủ checklist trước khi chọn Hoàn tất." Khi load dữ liệu cũ mà checklist thiếu → hạ về `in_progress`.
- Chọn `not_applicable` ⇒ **bắt buộc có ghi chú** lý do. Khi load mà không có ghi chú → về `not_started`.
- Ghi chú ≤ 8000 ký tự.

### 6.3 Sinh cây thư mục (`buildPlan`)
1. Với mỗi module được chọn: thêm `module.folder` và `folder/child` cho mọi `children`.
2. Nếu module có `softwareGroup`: lấy các hãng được chọn (hoặc có thiết bị dùng); nếu rỗng → dùng hãng có folder `99_Other` (hoặc phần tử cuối).
   - Nhóm `perDevice` (plc, hmi, robot, vision, drive, process): mỗi thiết bị có thư mục `module/brand/<DeviceID>/` + children của hãng. Nếu hãng không có thiết bị → tạo children trực tiếp dưới hãng.
   - Nhóm không perDevice (simulation, electrical): children dưới hãng.
3. Luôn thêm `_MASTER_SNAPSHOT` và `00_THONG_TIN_DU_AN`.
4. Template: lọc `templates` có `module` ∈ dự án; đích = `module.folder + '/' + destination`; tạo thư mục cha của đích.
5. Sort thư mục theo thứ tự chữ cái. Kiểm tra độ dài đường dẫn.

### 6.4 Cycle Time
- Chỉ lấy các phép đo là số hữu hạn **> 0** (tối đa 5 mẫu).
- `average = mean`, `min`, `max`, `cyclesMin = 60/average`.
- `unitsMin = 60*quantity/average`, `unitsHour = 3600*quantity/average` (chỉ khi `quantity > 0`).
- `delta = average - target` (chỉ khi `target > 0`).

### 6.5 Tiến độ
`percent = round(100 * done / active)`, trong đó `active` = stage áp dụng cho dự án, `done` = status `completed`.

## 7. GIAO TIẾP VỚI HỆ THỐNG TỆP (thay PowerShell bằng Electron IPC)

Bản gốc: UI gọi `api(route, body)` → (WebView2 `postMessage` hoặc `fetch('/api/<route>')` + header `X-APM-Session`). Bản clone: `window.apm.api(route, body)` → `ipcRenderer.invoke('apm:'+route)` → `ipcMain.handle`. **Không còn HTTP localhost, không còn session header, không còn PowerShell.** Mỗi handler phải kiểm tra `event.senderFrame.url` thuộc `tools/apm/` trước khi chạy.

| Route | Input | Hành vi | Output |
|---|---|---|---|
| `heartbeat` | – | kiểm tra sống | `{ok:true}` |
| `startup` | – | Nạp workspace; nếu app được mở bằng `khastudio://open?root=<path>` hoặc "Mở bằng…" một thư mục dự án thì đăng ký project đó | `{ok:true}` hoặc project |
| `pick-parent` | – | Hộp thoại chọn **thư mục cha** để tạo dự án | `{parentId, path}` hoặc `{cancelled:true}` |
| `open-project` | – | Chọn thư mục gốc dự án (phải có `_PROJECT_INFO.json` hoặc `_AUTOMATION_APP.json`) | `{projectId, path, info, project, config, roadmap, hash}` |
| `create-project` | `{parentId, folderName, directories[], files[{path, base64}]}` | Tạo thư mục **atomic**: ghi vào `.apm-staging-<guid>` rồi `rename` sang đích; lỗi → xóa staging | như `open-project` |
| `save-project` | `{projectId, expectedHash, project, informationFiles[]}` | Ghi `_AUTOMATION_APP.json` atomic (tmp + replace); **kiểm tra hash SHA-256 đang có = `expectedHash`**, khác ⇒ lỗi xung đột | `{ok, hash}` |
| `open-path` | `{projectId, relativePath}` | Mở thư mục/file bằng `shell.openPath`; chỉ cho đuôi `.xlsx .docx .pptx .pdf .txt .md .csv .json`; **không tự tạo** nếu chưa tồn tại | `{ok:true}` |
| `save-file` | `{name, data...}` | Hộp thoại Save As (xuất ZIP/JSON/CSV) | `{cancelled?}` |
| `save-workspace` | `{state}` | Lưu toàn bộ danh sách dự án + `nativeRoots` vào userData | `{ok}` |
| `ui-ready` / `close-ready` | – | Handshake hiển thị cửa sổ / cho phép đóng khi đã flush lưu | – |
| `open-in-app` | `{projectId}` | (thay `desktop-executable`) Tạo/ghi shortcut `Mo_Du_An.url` trỏ `khastudio://open?root=...` trong thư mục dự án để double-click mở lại bằng Kha Studio | `{ok}` |

> Route 8–11 là của lớp vỏ C# (không có source) → **suy ra từ lời gọi trong UI**, cần Antigravity tự thiết kế hợp lý.

### 7.1 Bảo mật đường dẫn (`resolveSafePath(root, relative)`) — BẮT BUỘC
- Từ chối rỗng, bắt đầu bằng `/`, chứa `\`, là đường dẫn tuyệt đối.
- Mỗi đoạn (split `/`): không rỗng, không `.`/`..`, không chứa `< > : " | ? *` hay ký tự điều khiển, không kết thúc bằng khoảng trắng/`.`, không là tên reserved Windows.
- Đường dẫn sau khi `path.resolve` phải nằm **trong** root (so sánh prefix, không phân biệt hoa/thường).
- `create-project`: tối đa 6000 thư mục, 500 file; path ≤ 240; base64 phải khớp `^[A-Za-z0-9+/]*={0,2}$`; không trùng file.
- `save-project`: chỉ cho ghi 2 file thông tin: `00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html|txt` (≤1MB, giữ `.bak`); `schemaVersion === 4` và có `code`, `name`.

## 8. ĐỊNH DẠNG DỮ LIỆU TRÊN ĐĨA

```
<DuAn>/
  _PROJECT_INFO.json         metadata dự án
  _AUTOMATION_APP.json       trạng thái app (project schema v4)  ← có hash chống ghi đè
  _PROJECT_CONFIG.json
  _PROJECT_ROADMAP.json
  Mo_Du_An.url               shortcut khastudio://open?root=...  (THAY cho _OPEN_AUTOMATION_APP.cmd)
  _MASTER_SNAPSHOT/          snapshot master data (chỉ JSON), KHÔNG còn Start_Automation_App.ps1
  .apm-publish/publish.json  (mới) lastPublishedAt, lastPublishedHash (không đổi schema project)
  00_THONG_TIN_DU_AN/THONG_TIN_DU_AN.html + .txt
  000_PROJECT_MANAGEMENT/ ... 024_PROCESS_EQUIPMENT/   (theo module đã chọn)
```
Lưu trạng thái: workspace trong `app.getPath('userData')/workspace.json` (danh sách dự án + đường dẫn gốc **theo từng máy**). Đường dẫn máy **không** được đồng bộ lên cloud. Khi nhập file `_AUTOMATION_APP.json` của bản gốc → đọc như cũ, **không cần đổi schema**; thông tin publish lưu riêng ở `.apm-publish/publish.json`.

Xuất **gói ZIP V4**: `<folderName>/` + thư mục + file thông tin + các template đã copy (kèm sổ đăng ký sha256) + `_MASTER_SNAPSHOT`. Sao lưu JSON: toàn bộ `state.projects`; **khôi phục** phải chạy lại `makeProject` cho từng dự án.

## 9. TÍNH NĂNG ĐẶC BIỆT CẦN GIỮ

- Tự lưu (debounce) + chỉ báo trạng thái lưu ở topbar (✓ / cảnh báo khi lỗi hoặc xung đột hash).
- Chống mất dữ liệu: flush mọi thay đổi local trước khi đóng cửa sổ (`close-ready`); thông báo "Xuất JSON nếu còn dữ liệu chưa lưu" khi app không phản hồi.
- Dự án **demo** (`demo: true`) có nhãn riêng; hàm `demoProject()`.
- In trang thông tin dự án ra PDF (nút "In / Lưu PDF").
- Import/Export CSV thiết bị (`deviceCSV`, `readFileImport`).
- Truy cập: focus-visible outline, `aria-label`, `aria-live` cho toast.

## 10. TIÊU CHÍ NGHIỆM THU (checklist test)

1. Tạo dự án mã `ETEK.2026-015_01`, tên `Casepacker Line Glass`, preset `full_line` ⇒ có cây thư mục + 18 template đúng vị trí, mở được trong Explorer.
2. Mã `con` hoặc tên có `?` ⇒ báo lỗi, không tạo.
3. Tạo 2 thiết bị Siemens PLC với IP trùng ⇒ báo "IP đã được thiết bị khác sử dụng".
4. Stage chưa tick đủ checklist ⇒ không chọn được "Hoàn tất".
5. "Không áp dụng" khi ghi chú trống ⇒ bị chặn.
6. Chỉnh `_AUTOMATION_APP.json` bằng tay trong lúc app mở rồi lưu từ app ⇒ báo xung đột hash, không ghi đè.
7. CT: mẫu `[10, 12, 11]`, target `10`, quantity `4` ⇒ avg `11`, cyclesMin `5.45`, unitsHour `1309.09`, delta `+1`.
8. Xuất JSON → xóa dự án → khôi phục ⇒ dữ liệu giống hệt.
9. Mở dự án do **bản gốc** tạo bằng bản clone và ngược lại ⇒ đọc được.
10. Kéo cửa sổ nhỏ <800px ⇒ sidebar thành menu trượt; in trang thông tin ⇒ gọn trong A4.
11. Path traversal: `open-path` với `../x` hoặc `a\b` ⇒ bị từ chối.
12. `npm run dist` ⇒ ra file cài đặt chạy được trên máy sạch, không cần mạng (chế độ cá nhân).
13. Pallet tool của Kha Studio vẫn chạy bình thường sau khi thêm preload/ipc.
14. Mở dự án bằng `Mo_Du_An.url` ⇒ Kha Studio mở đúng dự án; nếu đã mở sẵn thì focus cửa sổ cũ (single instance).
15. Nút **Cập nhật lên** khi chưa chọn thư mục Drive ⇒ mở hộp chọn thư mục; hủy ⇒ không ghi gì.
16. Publish dự án `ETEK.2026-015_01` ⇒ có `<Publisher>/ETEK.2026-015_01/latest.json` + 1 file trong `history/`; JSON hợp lệ, có `sha256` khớp payload.
17. Sửa dữ liệu sau khi publish ⇒ chip đổi sang "Có thay đổi chưa cập nhật"; publish lại ⇒ "Đã cập nhật lúc …".
18. Dự án có lỗi validate (mục 6.1) ⇒ **không cho publish**, nêu rõ lỗi.
19. Thư mục publish bị ngắt/đọc-only ⇒ báo lỗi rõ, không mất dữ liệu local, không để lại file `.tmp`.
20. Ghi atomic: kill app giữa chừng ⇒ `latest.json` cũ vẫn nguyên vẹn.
21. Giữ tối đa N bản `history/` (mặc định 30); chỉ xóa file **do chính mình publish**, không đụng file lạ.
22. **Admin:** chọn thư mục gốc có 3 publisher × 2 dự án ⇒ bảng tổng hợp đủ 6 dự án (tiến độ %, stage đang vướng, Issue High còn mở, lần cập nhật cuối); dự án >7 ngày chưa cập nhật có cảnh báo "cũ".
23. **Admin chỉ đọc:** mọi nút sửa/xóa/publish bị ẩn; IPC từ chối ghi vào đường dẫn snapshot (kể cả gọi trực tiếp từ DevTools).
24. Admin so sánh `latest` với bản history trước ⇒ hiển thị diff: stage đổi trạng thái, issue mới/đóng, CT thay đổi.
25. File snapshot độc hại/hỏng: JSON sai, >5 MB, note chứa `<script>`/HTML, `code` chứa `../` ⇒ bỏ qua hoặc chạy qua `makeProject` + escape, không crash, không thực thi, không đọc ngoài thư mục gốc (kể cả symlink).
26. Admin xuất Excel/CSV tổng hợp tất cả dự án (chống CSV-injection như `csv()` mục 6).
27. Lần đầu bấm **Cập nhật lên** ⇒ Trình hướng dẫn tự mở; hủy ⇒ không ghi gì, lần sau vẫn nhắc.
28. Máy không có Drive for Desktop ⇒ bước 1 báo ❌ + nút mở trang tải; vẫn chọn thư mục thủ công đi tiếp được.
29. Chọn thư mục không nằm trong Google Drive ⇒ cảnh báo `PUB_NOT_IN_DRIVE`, có nút "Chọn lại" và "Vẫn dùng".
30. Bước chia sẻ: nút mở Explorer / mở Drive trên web hoạt động; chưa tick "đã chia sẻ" ⇒ vẫn publish được nhưng có banner nhắc; tick ⇒ banner mất.
31. Bảng **Trạng thái chia sẻ** hiện đúng 5 mục; đổi tên/rút thư mục publish ⇒ `PUB_FOLDER_MISSING` kèm nút "Thử lại".
32. Admin: thư mục gốc rỗng ⇒ `ADMIN_EMPTY` liệt kê nguyên nhân và cách xử lý; thêm shortcut đúng rồi quét lại ⇒ thấy nhân viên/dự án.
33. Mọi mã thông báo trong catalog đều render được (có tiêu đề, nội dung, ≥1 nút); không có chuỗi hiển thị nào hard-code ngoài `messages.vi.js`.
34. `publish:open-external` từ chối URL ngoài whitelist; `publish:open-folder` từ chối đường dẫn ngoài thư mục publish/admin root.
35. Coach-mark lần đầu chỉ hiện 1 lần; nút **?** cạnh "Cập nhật lên" luôn mở được hướng dẫn 2 tab (Nhân viên / Admin) và FAQ.
36. Thư mục publish có file kiểu `latest (1).json` / `Copy of ...` do Drive sinh ⇒ báo `PUB_CONFLICT_COPY`, app vẫn chỉ dùng `latest.json`.

## 11. KẾ HOẠCH THỰC HIỆN CHO AGENT (làm tuần tự, mỗi bước chạy test + commit)

**Giai đoạn P1 — Bản local trong Kha Studio (tương đương app gốc)**
1. Sửa Kha Studio: preload, `ipc/apm.js`, protocol, single-instance, CSP (mục 3).
2. `core.js`: port APMCore → unit test (mục 6 + test #2,3,4,5,7).
3. `fs-safe.js` + các route mục 7 → test path traversal (#11).
4. UI từng view: dashboard → create → roadmap → devices → issues → ct → folders → templates → settings.
5. Xuất/nhập JSON, ZIP V4, CSV, trang thông tin in được. Tương thích file bản gốc (#9).
6. Chạy checklist #1–#14. **Dừng, báo cáo, chờ duyệt trước khi sang P2.**

**Giai đoạn P2 — Publish + hướng dẫn nhân viên** (mục 13.2–13.4, 13.10 phần nhân viên) → test #15–#21, #27–#31, #33–#36. Dừng, báo cáo, chờ duyệt.
**Giai đoạn P3 — Admin viewer + hướng dẫn admin** (mục 13.5–13.8, 13.10 phần admin) → test #22–#26, #32.
**Giai đoạn P4 — Tùy chọn, chỉ làm khi được yêu cầu:** thêm provider Drive API hoặc Supabase qua interface 13.9.

## 12. ĐỘ TIN CẬY & CÂU HỎI MỞ

| Thành phần | Độ tin cậy |
|---|---|
| Master data, logic APMCore, UI JS/CSS, host PowerShell | **Cao** — trích nguyên văn từ exe |
| Hành vi route `save-file`, `save-workspace`, `ui-ready`, `close-ready`, `desktop-executable` | **Trung bình** — chỉ thấy phía gọi, phần C# là mã đã biên dịch |
| Icon, tên file cài đặt, menu native của vỏ WinForms | **Chưa biết** |

**Đã chốt (Rev 3):** (1) launcher Electron/`khastudio://`; (2) gắn vào Kha Studio; (3) publish thủ công + Admin chỉ xem, qua thư mục Google Drive.
**Còn mở (agent đề xuất, chủ app duyệt):** (a) nhân viên có được xem dự án của nhau không (mặc định **không**, mục 13.1); (b) có publish kèm file `THONG_TIN_DU_AN.html` không (mặc định **có**, tắt được); (c) ngưỡng cảnh báo "cũ" (mặc định 7 ngày).

## 13. PUBLISH LÊN GOOGLE DRIVE & CHẾ ĐỘ ADMIN (thay cho cloud sync)

### 13.1 Mô hình chia sẻ (dùng sharing của Drive, app không làm bảo mật)
- Mỗi nhân viên có **thư mục riêng** trong Drive, vd `APM_Published_<Tên>`, do **chính họ tạo** và **chia sẻ cho admin** (Editor hoặc Viewer). Nhân viên khác không được chia sẻ ⇒ không thấy nhau.
- Mỗi máy cài **Google Drive for Desktop**; app chỉ đọc/ghi thư mục trên đĩa (vd `G:\My Drive\APM_Published_Kha`). Admin thấy các thư mục được chia sẻ trong "Shared with me"/Drive for Desktop (nên thêm shortcut vào My Drive để có đường dẫn cố định).
- **Quyền thật sự do Drive quyết định.** "Chế độ Admin" trong app chỉ là chế độ giao diện chỉ-đọc, **không phải ranh giới bảo mật**. Ghi rõ điều này trong màn hình Settings.
- Không có OAuth, token, mạng; không lưu bí mật nào trong app.

### 13.2 Nút "Cập nhật lên" (publish)
- Vị trí: topbar (cạnh trạng thái lưu) + trang chi tiết dự án. Chip trạng thái: `Chưa từng cập nhật` · `Đã cập nhật lúc dd/mm hh:mm` · `Có thay đổi chưa cập nhật` (so `sha256(payload hiện tại)` với `lastPublishedHash` trong `.apm-publish/publish.json`).
- Luồng: (1) chạy `validateProject` — lỗi thì chặn; (2) nếu chưa có cấu hình → mở **Trình hướng dẫn thiết lập** (13.10), không bao giờ im lặng thất bại; (3) hộp xác nhận hiện: ghi vào đâu, gồm file nào, kích thước; (4) ghi file (13.3); (5) toast "Đã ghi vào thư mục. Google Drive sẽ tự đồng bộ — xem biểu tượng Drive để biết khi nào xong." **App không thể biết Drive đã upload xong chưa**; không được hiển thị "đã gửi cho admin".
- Cài đặt (Settings → Cập nhật lên): tên người cập nhật, thư mục publish, số bản history giữ lại (mặc định 30), có kèm HTML thông tin dự án không, ngưỡng cảnh báo cũ.

### 13.3 Định dạng thư mục & file snapshot
```
<PublishRoot>/                       (thư mục Drive của nhân viên)
  <ProjectCode>/
    latest.json                      bản mới nhất (ghi đè atomic)
    history/<YYYY-MM-DDTHH-mm-ssZ>.json
    info/THONG_TIN_DU_AN.html        (tuỳ chọn)
```
`<ProjectCode>` đã được ràng buộc bởi regex mục 6.1 (không có `/`, `..`).
```json
{
  "snapshotVersion": 1,
  "appVersion": "5.0.0",
  "snapshotId": "uuid",
  "publishedAt": "ISO-8601 UTC",
  "publisher": { "name": "Kha" },
  "project": { /* nguyên project schema v4 (makeProject) */ },
  "summary": { "progress": {total,done,na,blocked,percent}, "openIssuesHigh": 0, "issuesOpen": 0,
               "blockedStages": ["sat"], "devices": 12 },
  "sha256": "băm của canonical JSON của {project,summary}"
}
```
- Ghi **atomic**: ghi `*.tmp-<guid>` cùng thư mục rồi `rename` (Drive không bị file dở dang).
- Dọn history: chỉ xóa file `history/*.json` **do app này tạo** (có `snapshotId` hợp lệ) khi vượt N.
- Không ghi đường dẫn máy, không ghi `nativeRoots`, không ghi token.
- `sha256` chỉ để phát hiện hỏng file, **không chứng minh người gửi**; người gửi thật = chủ sở hữu file trong Drive (admin xem được ở Drive).

### 13.4 Route IPC mới
| Route | Hành vi |
|---|---|
| `publish:get-config` / `publish:set-config` | đọc/ghi cấu hình (userData) |
| `publish:pick-folder` | hộp thoại chọn thư mục publish (kiểm tra ghi được bằng file thử rồi xóa) |
| `publish:status` | `{lastPublishedAt, dirty}` cho dự án |
| `publish:push` | thực hiện 13.2 bước 4, trả `{files[], snapshotId}` |

Mọi đường dẫn đi qua `resolveSafePath`. Tên file do app sinh, không nhận từ renderer.

### 13.5 Chế độ Admin
- Bật ở Settings → "Chế độ Admin" (lưu cục bộ). Chọn **thư mục gốc admin** (chứa các thư mục nhân viên đã chia sẻ).
- `admin:scan(root)` quét `*/*/latest.json` (tối đa 3 cấp, tối đa 2000 file, mỗi file ≤ 5 MB, **bỏ qua symlink/shortcut và đường dẫn nằm ngoài root**). Mỗi file đi qua: parse JSON → kiểm `snapshotVersion` → `makeProject` (coi là **không tin cậy**) → kiểm `sha256` (sai ⇒ gắn cờ "file hỏng", vẫn liệt kê).
- Chỉ-đọc ở **cả UI và main**: không có route ghi nào chấp nhận đường dẫn thuộc root admin; snapshot **không** được đăng ký thành "dự án đang mở" nên `save-project` không áp dụng được.

### 13.6 Màn hình Admin
1. **Tổng hợp:** bảng mọi dự án × người cập nhật: mã, tên, khách hàng, người cập nhật, tiến độ %, số stage đang vướng, Issue High còn mở, lần cập nhật cuối, nhãn `Cũ` nếu quá ngưỡng, nhãn `File hỏng`. Lọc theo người/trạng thái, tìm kiếm, sắp xếp.
2. **Chi tiết (chỉ đọc):** tái dùng các view roadmap / devices / issues / ct với cờ `readOnly` (ẩn nút sửa, input `disabled`); banner "Đang xem bản chụp của <tên> lúc <thời điểm>".
3. **Lịch sử & diff:** chọn bản history để so với `latest`: stage đổi trạng thái, issue thêm/đóng/đổi mức, thiết bị thêm/xóa, CT đổi.
4. **Xuất tổng hợp** Excel/CSV (dùng `csv()` chống injection).

### 13.7 Bảo mật khi đọc dữ liệu của người khác
- Mọi chuỗi từ snapshot khi render đều qua `esc()`; không dùng `innerHTML` với dữ liệu snapshot chưa escape; không mở link/`open-path` theo đường dẫn trong snapshot.
- Không thực thi gì từ snapshot; không nạp `info/*.html` vào cùng origin của app — mở bằng `shell.openPath` hoặc `<iframe sandbox>` không script.
- `open-path` trong chế độ Admin chỉ mở thư mục/file **bên trong root admin**.

### 13.8 Giới hạn đã biết (phải ghi trong UI/README)
- Admin thấy dữ liệu **sau khi Drive đồng bộ**, có thể chậm; không có thông báo realtime.
- Hai máy cùng một nhân viên publish cùng dự án ⇒ bản sau ghi đè `latest.json` (history vẫn giữ cả hai). Cân nhắc cho `publisher.name` + `machineId` ngắn trong tên file history.
- Xóa file trong Drive có thể mất snapshot (Drive giữ thùng rác một thời gian); app không chịu trách nhiệm backup — `Settings → Sao lưu JSON` vẫn dùng như cũ.
- Dữ liệu nằm trong Drive của từng nhân viên/công ty: kiểm tra chính sách bảo mật khách hàng (NDA) trước khi đưa dự án lên.

### 13.9 Chuẩn bị cho tương lai (không làm bây giờ)
Tạo interface `PublishProvider { push(snapshot): Promise<Result>; list(): Promise<SnapshotRef[]>; read(ref): Promise<Snapshot> }` và hiện thực `FolderProvider` (13.2–13.5). Sau này thêm `DriveApiProvider` hoặc `SupabaseProvider` mà không đổi core/UI.

### 13.10 Thao tác & hướng dẫn NGAY TRONG APP (bắt buộc)
Mục tiêu: người dùng **không phải đọc tài liệu ngoài** để chia sẻ được. Phần việc app tự làm được thì app làm; phần phải làm thủ công trên Google Drive thì app **hướng dẫn từng bước kèm nút bấm**. App **không có quyền** tạo chia sẻ trên Drive và **không thể xác nhận** Drive đã đồng bộ xong — phải nói thật điều này trong UI.

**A. Trình hướng dẫn thiết lập (Setup Wizard)** — tự mở lần đầu bấm *Cập nhật lên* hoặc bật *Chế độ Admin*; mở lại bất cứ lúc nào ở Settings → "Cập nhật lên & chia sẻ". Có nút Quay lại / Bỏ qua, nhớ tiến độ.

*Nhân viên (4 bước):*
1. **Kiểm tra Google Drive for Desktop** (app tự dò, 13.10.F): ✅ thấy Drive (hiện ổ/đường dẫn tìm được) · ⚠️ không chắc → "Tôi đã cài, chọn thư mục thủ công" · ❌ không thấy → nút **Tải Google Drive for Desktop** (mở trang tải của Google bằng trình duyệt hệ thống).
2. **Tạo thư mục để cập nhật:** app đề xuất `<Drive>\My Drive\APM_Published_<Tên>`; nút **Tạo thư mục này** (app tự tạo) hoặc **Chọn thư mục có sẵn**. Nhập **Tên người cập nhật** (bắt buộc, không ký tự cấm Windows). App kiểm tra: tồn tại, ghi được (ghi/xóa file thử), có nằm trong Drive không (heuristic).
3. **Chia sẻ cho admin** (làm tay, app dẫn từng bước): nút **Mở thư mục trong Explorer**, nút **Mở Google Drive trên web**; hướng dẫn: *chuột phải thư mục → Chia sẻ (Share) → nhập email admin → quyền "Người xem (Viewer)" → Gửi*. (Ghi chú: tên nút có thể khác theo ngôn ngữ giao diện Drive.) Có ô **Email admin** (chỉ lưu để nhắc) và nút **Sao chép lời nhắn gửi admin** (đoạn văn mẫu: tên, đường dẫn thư mục, việc admin cần làm). Ô tick **"Tôi đã chia sẻ thư mục cho admin"** — xác nhận thủ công, app không kiểm chứng được.
4. **Cập nhật thử:** publish dự án đang chọn (hoặc ghi `README_APM.txt` nếu chưa có dự án). Kết quả: danh sách file đã ghi + lời nhắc "Mở biểu tượng Google Drive ở khay hệ thống, chờ dấu ✓ rồi báo admin".

*Admin (3 bước):*
1. Kiểm tra Drive như trên.
2. **Thêm thư mục được chia sẻ vào Drive của bạn:** hướng dẫn *Drive trên web → "Được chia sẻ với tôi" (Shared with me) → chuột phải thư mục của nhân viên → Sắp xếp/Organize → Thêm lối tắt (Add shortcut) → My Drive*. Nút mở Drive trên web / Explorer. Mẹo: nếu Drive đang ở chế độ stream, chuột phải thư mục → *Có sẵn ngoại tuyến (Available offline)* để đọc nhanh, ổn định (tên menu có thể khác).
3. **Chọn thư mục gốc admin → Quét thử:** hiện "Tìm thấy X nhân viên, Y dự án". Nếu 0 ⇒ `ADMIN_EMPTY` (13.10.C) chẩn đoán từng nguyên nhân.

**B. Bảng "Trạng thái chia sẻ" (Share Health)** — trong Settings và popup của chip trạng thái. Checklist có biểu tượng + giải thích + nút sửa:
`Phát hiện Google Drive` · `Thư mục cập nhật hợp lệ & ghi được` · `Thư mục nằm trong Drive` · `Đã xác nhận chia sẻ cho admin` · `Lần cập nhật cuối / có thay đổi chưa cập nhật`. Nút **Chạy lại kiểm tra** (`publish:diagnose`). Bản Admin có `admin:diagnose` tương tự (root tồn tại, đọc được, số file hợp lệ/hỏng).

**C. Catalog thông báo hướng dẫn** — mỗi thông báo có: `code`, mức (`info/warning/error`), tiêu đề, nội dung (nguyên nhân + cách xử lý), danh sách nút hành động. Toàn bộ chữ nằm trong **một file** `renderer/js/messages.vi.js` (dễ sửa, sẵn sàng đa ngôn ngữ). Tối thiểu:

| Code | Khi nào | Nội dung gợi ý | Nút |
|---|---|---|---|
| `PUB_NOT_CONFIGURED` | Bấm Cập nhật lên lần đầu | Chưa thiết lập nơi lưu bản cập nhật. Làm theo hướng dẫn (khoảng 2 phút). | Bắt đầu |
| `PUB_NOT_IN_DRIVE` | Thư mục không nằm trong Google Drive | Thư mục này có vẻ không nằm trong Google Drive nên admin sẽ không nhận được. Hãy chọn thư mục bên trong "My Drive" / "Drive của tôi". | Chọn lại · Vẫn dùng |
| `PUB_FOLDER_MISSING` | Thư mục publish biến mất | Không thấy thư mục cập nhật. Có thể Google Drive chưa chạy, bạn đã đăng xuất, hoặc thư mục bị đổi tên. | Thử lại · Chọn thư mục khác |
| `PUB_NOT_WRITABLE` | Không ghi được | Không ghi được vào thư mục (hết quyền hoặc Drive đang ngắt). Dữ liệu trên máy bạn **không** bị mất. | Thử lại · Mở thư mục |
| `PUB_NOT_SHARED_CONFIRMED` | Chưa tick chia sẻ | Bạn chưa xác nhận đã chia sẻ thư mục cho admin — admin sẽ không thấy bản cập nhật. | Hướng dẫn chia sẻ · Tôi đã chia sẻ |
| `PUB_VALIDATION_FAILED` | Dự án có lỗi 6.1 | Chưa thể cập nhật vì dự án còn lỗi: (danh sách). | Đi tới chỗ sửa |
| `PUB_OK` | Ghi xong | Đã ghi bản cập nhật vào thư mục lúc hh:mm. Google Drive sẽ tự đồng bộ — chờ biểu tượng Drive hiện dấu ✓ rồi báo admin. *(Không được ghi "đã gửi".)* | Mở thư mục |
| `PUB_DIRTY` | Có thay đổi sau lần cập nhật cuối | Có thay đổi chưa cập nhật lên. | Cập nhật lên |
| `PUB_STALE_REMINDER` | Quá ngưỡng ngày + có thay đổi | Đã N ngày chưa cập nhật dự án này. | Cập nhật lên · Nhắc sau |
| `PUB_CONFLICT_COPY` | Có file `... (1).json`/`Copy of ...` | Drive tạo bản sao do trùng tên. App chỉ dùng `latest.json`; bạn có thể xóa các bản sao. | Mở thư mục |
| `ADMIN_NO_ROOT` | Chưa chọn thư mục gốc | Chưa chọn thư mục chứa các bản cập nhật. | Bắt đầu hướng dẫn |
| `ADMIN_EMPTY` | Quét ra 0 | Chưa thấy bản cập nhật nào. Kiểm tra: (1) nhân viên đã chia sẻ cho bạn chưa; (2) bạn đã thêm lối tắt vào My Drive chưa; (3) Drive đã đồng bộ chưa; (4) có đang chọn đúng thư mục gốc không. | Chạy lại kiểm tra · Hướng dẫn |
| `ADMIN_ACCESS_DENIED` | Lỗi quyền đọc | Không đọc được một số thư mục. Nhờ nhân viên chia sẻ lại cho bạn. | Danh sách thư mục lỗi |
| `ADMIN_FILE_ONLINE_ONLY` | Đọc lỗi/chậm do file chưa tải về | File chưa được tải về máy. Chọn "Có sẵn ngoại tuyến" cho thư mục rồi quét lại. | Hướng dẫn · Quét lại |
| `ADMIN_STALE` / `ADMIN_CORRUPT` | Bản quá cũ / hash sai | Bản cập nhật của <tên> đã N ngày chưa đổi / File bị hỏng, nhờ nhân viên cập nhật lại. | Sao chép lời nhắn |

**D. Trợ giúp luôn có sẵn:** nút **?** cạnh *Cập nhật lên* và trong màn Admin mở panel hướng dẫn 2 tab (**Nhân viên** / **Admin**) + FAQ: "Admin không thấy dữ liệu?", "Drive báo xung đột/trùng file?", "Xóa bản đã cập nhật thế nào?", "Có an toàn không?", "Cập nhật nhiều lần có sao không?", "Đổi máy thì làm gì?". **Empty state** có hướng dẫn (vd dashboard: "Dự án này chưa từng được cập nhật lên Drive"). **Coach-mark** lần đầu chỉ vào nút Cập nhật lên (bấm "Đã hiểu" thì không hiện lại).

**E. Quy tắc UX bắt buộc**
- Dùng từ dễ hiểu: "bản cập nhật" thay cho "snapshot", "thư mục chia sẻ" thay cho "publish root".
- Mỗi lỗi phải có **nguyên nhân + cách xử lý + nút hành động**; lỗi không làm mất dữ liệu local thì nói rõ điều đó.
- Cảnh báo mềm không chặn thao tác (trừ lỗi validate dự án và không ghi được).
- Toast có hành động giữ ≥ 8 giây; mọi trạng thái xem lại được ở Share Health.
- Không bao giờ hiển thị "đã gửi cho admin" khi mới ghi file.
- Toàn bộ chữ hiển thị lấy từ `messages.vi.js`.

**F. Dò Google Drive (best-effort, không dùng shell/exec)** — chỉ dùng `fs`:
- Windows: tìm `GoogleDriveFS.exe` trong `%ProgramFiles%\Google\Drive File Stream\*\`; quét các ổ `A:`–`Z:` có thư mục gốc tên `My Drive` hoặc `Drive của tôi`; thử `%USERPROFILE%\Google Drive`.
- macOS: `~/Library/CloudStorage/GoogleDrive-*`.
- "Nằm trong Drive" = đường dẫn chứa một trong các tên trên hoặc thuộc gốc đã dò thấy. **Kết quả chỉ là gợi ý**: luôn cho người dùng chọn tay và đi tiếp.

**G. Route IPC bổ sung** (mọi đường dẫn qua `resolveSafePath`, tên file do app sinh):
| Route | Hành vi |
|---|---|
| `publish:detect-drive` | trả `{found, roots[]}` theo 13.10.F |
| `publish:create-folder` | tạo thư mục con (tên đã sanitize, không ký tự cấm/reserved) dưới thư mục đã chọn |
| `publish:open-folder` | `shell.openPath` — chỉ đường dẫn trong thư mục publish / admin root |
| `publish:open-external` | `shell.openExternal` — **chỉ whitelist** trang tải Drive for Desktop và `https://drive.google.com/`; từ chối mọi URL khác |
| `publish:diagnose` / `admin:diagnose` | trả `[{code, ok, detail}]` cho Share Health |
| `publish:copy-text` | đưa văn bản mẫu (lời nhắn gửi admin) vào clipboard |

---

## PROMPT DÁN VÀO ANTIGRAVITY (Agent Manager)

```
Bạn là kỹ sư phần mềm. Nhiệm vụ: clone app "Automation Project Master v4.2.1" thành tool "APM" bên trong app Electron có sẵn tên Kha Studio (thư mục kha-studio-app), rồi thêm nút "Cập nhật lên" (publish snapshot vào thư mục Google Drive) và chế độ Admin chỉ xem.

1. Đọc toàn bộ CLONE_FORM.md (Rev 3) — đây là nguồn sự thật.
2. Đọc thư mục reference/: Automation_Project_Master.html (UI + logic gốc), Start_Automation_App.ps1 (chỉ để hiểu hành vi; KHÔNG dùng PowerShell trong bản mới), data_*.json, templates/.
3. Làm theo mục 11: P1 → P2 → P3. Hết mỗi giai đoạn: chạy test, commit, báo cáo pass/fail và DỪNG chờ tôi duyệt.
4. Giữ nguyên quy tắc nghiệp vụ mục 6, bảo mật đường dẫn mục 7.1, định dạng đĩa mục 8 để tương thích file của bản gốc. Không phá tool Pallet đang có.
5. App KHÔNG có kết nối mạng: không CDN, không fetch ra ngoài, CSP connect-src 'none'. Thư viện cài qua npm, chạy local. Không thêm Supabase/Firebase/OAuth.
6. Dữ liệu snapshot đọc ở chế độ Admin là KHÔNG TIN CẬY (mục 13.5, 13.7).
7. Mọi thao tác chia sẻ phải có hướng dẫn ngay trong app (Trình hướng dẫn, Trạng thái chia sẻ, catalog thông báo ở mục 13.10); toàn bộ chữ hiển thị đặt trong renderer/js/messages.vi.js.
8. Chỗ nào form ghi "Trung bình/Chưa biết/Còn mở": tự quyết định hợp lý và ghi vào DECISIONS.md.
Trước khi code, đưa plan ngắn (kèm định dạng snapshot và danh sách route IPC) để tôi duyệt.
```
