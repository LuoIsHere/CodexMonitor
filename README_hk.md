> **Attention**
> CodexMonitor 是個人項目，與 OpenAI 沒有任何隸屬關係，亦沒有 OpenAI 官方人員參與。

# CodexMonitor

[English](README.md) · [简体中文](README_cn.md)

一款輕巧的 Windows 工具，透過主視窗、通知區域及浮動視窗顯示 Codex 額度、重設時間、訂閱類型與更新狀態。

版本：`0.3.2`

![CodexMonitor 主視窗](assets/screenshots/codex-monitor-dashboard.png)

## 功能

- 顯示約 5 小時（`5H`）及 7 天（`7D`）時段的剩餘額度與本地重設時間。
- 啟動時讀取一次，支援手動更新及 1～60 分鐘自動更新間隔，預設為 3 分鐘。
- 讀取失敗時保留最近成功取得的資料，顯示狀態並按設定發出通知。
- 通知區域提供還原視窗、設定、浮動視窗控制及結束程式的選項。
- 浮動視窗可獨立選擇顯示內容、拖動並儲存位置；鎖定後置頂並支援滑鼠點擊穿透，與主視窗共用監控狀態。
- 可選擇在目前使用者登入 Windows 後自動啟動，並最小化至通知區域。
- 只執行一個執行個體：手動重複啟動會還原現有視窗，自動重複啟動則靜默結束。
- 主視窗、設定及通知區域支援簡體中文、英語及繁體中文（香港），浮動視窗保留英語顯示。

Token/API Key 帳戶的額度與更新時間顯示為 `None`，重設時間顯示為 `-`。

## 系統要求與下載

需要 Windows 10 1809 或以上版本、x64 系統，以及已安裝並登入的 Windows 版 Codex。建議使用 Windows 11。

| 發佈套件 | 是否需要另行安裝執行階段 |
| --- | --- |
| 自包含單一 EXE 或 ZIP | 不需要 |
| 依賴執行階段的單一 EXE 或 ZIP | 需要 .NET 10 Desktop Runtime x64 |

EXE 檔案名稱格式如下，`<版本號>` 例如 `0.3.2`：

- 自包含版：`CodexMonitor-<版本號>-win-x64-self-contained.exe`
- 依賴執行階段版：`CodexMonitor-<版本號>-win-x64-framework-dependent.exe`

ZIP 套件沿用相同名稱，副檔名改為 `.zip`。

各套件功能相同。ZIP 解壓縮後執行 `CodexMonitor.exe`，單一 EXE 可直接執行。從原始碼建置需要 .NET 10 SDK。

已驗證的 Codex 版本：

| Codex 版本 | CodexMonitor |
| --- | --- |
| `codex-cli 0.147.0-alpha.6.6` | 0.3.0 |
| `codex-cli 0.155.0-alpha.2.6` | 0.3.1 |
| Codex 桌面版 26.917.51856（`codex-cli 0.155.0-alpha.16`） | 0.3.2 |

Codex 升級可能會改變本地 app-server 通訊協定。

## 使用

執行程式後會顯示主視窗，可手動更新額度。關閉主視窗後，程式仍會在通知區域的背景執行；以滑鼠左鍵按一下通知區域圖示可還原視窗，按右鍵可開啟設定或選擇「結束程式」。

在「設定 → 一般 → 介面語言」選擇語言並儲存，即時生效；預設使用簡體中文。

![CodexMonitor 浮動視窗](assets/screenshots/codex-monitor-floating-window.png)

在通知區域選單或設定中啟用浮動視窗。解鎖後可拖動，鎖定後置頂並讓滑鼠點擊穿透。可透過通知區域選單或設定解鎖；顯示內容與主視窗分開設定。

## 登入 Windows 後自動啟動

在「設定 → 一般」勾選「登入 Windows 後自動啟動」並儲存。此選項預設關閉，毋須管理員權限。

勾選「啟動後最小化至通知區域」時，自動啟動只會顯示通知區域圖示，已啟用的浮動視窗仍會顯示。取消勾選後，自動啟動會顯示主視窗。手動啟動一律開啟或還原應用程式視窗。

只有明確儲存自動啟動設定時才會修改啟動項目。**是否實際啟動仍由 Windows 啟動應用程式設定控制**，程式不會恢復在 Windows 設定或工作管理員中停用的自動啟動。

移動程式後，從新位置執行，在設定中勾選「重新登記目前路徑（儲存後執行）」並儲存。

## 設定與日誌

預設位置：

```text
%LOCALAPPDATA%\CodexMonitor\settings.json
%LOCALAPPDATA%\CodexMonitor\logs\codex-monitor.log
```

設定包括自動啟動、通知、更新間隔及視窗顯示內容。舊設定升級時會保留原有選項，自動啟動預設關閉。手動編輯 JSON 後須重新啟動；Windows 自動啟動請透過設定視窗啟用。

啟用系統通知後：

- 關閉主視窗轉入背景時，提示「程式仍在通知區域執行」，每次執行期間最多提示一次。
- 額度更新失敗時，提示簡短原因，詳細資料記錄於日誌。

`codexExecutable` 可指定 `codex.exe` 路徑，通常可留空；自動尋找時優先使用 `%LOCALAPPDATA%\OpenAI\Codex\bin` 中的程式。環境變數 `CODEX_MONITOR_HOME` 可指定設定與日誌的儲存目錄。

## 實作與權限

- `CodexMonitor.Core`：模型、顯示格式及更新狀態。
- `CodexMonitor.Infrastructure`：Codex 程序通訊、JSON 解析、設定、日誌及使用者自動啟動登記。
- `CodexMonitor.App`：WPF 介面、通知區域生命週期、單一執行個體啟用及更新排程。

額度透過本地 `codex.exe app-server` 的標準輸入輸出讀取。每次讀取後關閉子程序，最近成功取得的資料保留在記憶體中。
應用程式統一排程更新，主視窗和浮動視窗分別讀取同一份監控狀態。

程式以一般使用者權限執行，驗證由 Codex 處理，不直接讀取憑證檔案或要求取得權杖內容，亦會忽略帳戶的電郵地址欄位。日誌記錄讀取結果及錯誤資料；錯誤可能包含本機路徑，分享前請先檢查。

自動啟動只使用目前使用者 Windows Run 登錄項目中的 `CodexMonitor` 值。

## 建置與測試

在已安裝 .NET 10 SDK 的 Windows 上執行：

```powershell
dotnet build .\CodexMonitor.sln -c Release
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release
```

測試使用模擬啟動項目儲存及臨時設定，不會修改目前使用者的實際啟動項目。WPF 生命週期測試約需一分鐘，用以驗證實際的定時更新，不會顯示視窗。項目沒有第三方 NuGet 相依套件，基本建置使用已安裝的 SDK 及桌面目標套件。

可選用實際 Codex 讀取測試：

```powershell
dotnet run --project .\tests\CodexMonitor.Tests\CodexMonitor.Tests.csproj -c Release -- --live
```

產生四種發佈套件至 `artifacts/`：

```powershell
powershell.exe -NoProfile -File .\build\Publish.ps1
```

自包含發佈可能會從 NuGet.org 下載 Microsoft 執行階段套件。ZIP 套件包含三種語言的 README、目前更新記錄及授權條款。

## 項目資訊

額度顯示參考了 [CodexQuotaMonitor](https://github.com/DiMY-CN/CodexQuotaMonitor)，輕巧 Windows 監察工具的使用方式參考了 [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor)。

[目前更新記錄](CHANGELOG.md) · [完整歷史](src/CHANGELOG.md) · [GitHub Releases](https://github.com/LuoIsHere/CodexMonitor/releases)

[MIT 授權條款](LICENSE) · Copyright (c) 2026 luoishere

本項目為借助 OpenAI Codex 開發的個人工具，可能存在缺陷，按 MIT 授權條款不提供任何擔保。
