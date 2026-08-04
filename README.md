# reMOUSE

> 一個為 Windows 打造的滑鼠側鍵重映射與效率工具。
>
> 把側鍵變成真正符合自己工作流的工具：像素檢查器、放射狀選單、快捷鍵、程式啟動，還有中鍵水平 flick。

<p>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10">
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/license-non--commercial-F59E0B" alt="Non-commercial license"></a>
</p>

reMOUSE 透過 Windows low-level mouse hook 讀取 `XButton1` / `XButton2`，在不改變實體按鍵身份的前提下，將它們綁定到不同的工作模式。專案目前仍在早期開發階段；使用前請先在自己的硬體上完成驗收。

## ✨ 功能

| 功能 | 說明 |
|---|---|
| **側鍵重映射** | 將 `XButton1`、`XButton2` 分別綁定到 Pixel inspector、Radial menu 或 Pass through。 |
| **Pixel inspector** | 顯示游標座標；用左鍵拖曳量測矩形，按住 `Shift` 可限制 45°，`Ctrl+C` 複製座標。 |
| **Radial menu** | 按住指定側鍵、移到選項後放開；每個 slot 可執行快捷鍵、啟動程式或保持空白。 |
| **中鍵水平 flick** | 按住中鍵後按左／右鍵，送出水平滾輪效果；一般中鍵點擊與中鍵拖曳仍可保留。 |
| **設定編輯器** | 直接編輯側鍵、flick delta、選單標籤、快捷鍵、程式路徑與啟動參數。 |
| **安全暫停** | UI 的 Pause／Resume，或全域快捷鍵 `Ctrl+Alt+F12` 緊急暫停重映射。 |

### 預設側鍵配置

以目前 Terra Pro 實測結果為例：

| 實體位置 | Windows raw input | 預設功能 |
|---|---|---|
| 下方側鍵 | `XButton1` | Pixel inspector |
| 上方側鍵 | `XButton2` | Radial menu |

> 不同滑鼠或驅動程式可能回報不同的 XButton。reMOUSE 會保留 Windows 的 raw 名稱；如果方向不符合預期，可在設定視窗重新指定功能，或先使用 `ReMouse.InputProbe` 確認輸入。

## 🚀 快速開始

### 環境需求

- Windows 10 19041 或更新版本／Windows 11
- .NET 10 SDK（專案目前以 `10.0.302` 為基準）
- 可正常接收 `XButton1` / `XButton2` 的滑鼠或驅動程式

確認 SDK：

```powershell
dotnet --version
```

若尚未安裝 .NET 10 SDK：

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

### 建置與啟動完整 App

```powershell
dotnet restore reMOUSE.slnx
dotnet build reMOUSE.slnx --no-restore
dotnet run --project src/ReMouse.App/ReMouse.App.csproj
```

啟動後會開啟 `reMOUSE` 設定視窗並安裝全域滑鼠 hook。完成設定後按 **Save settings**，變更會立即套用並保存。

### 執行測試

```powershell
dotnet test reMOUSE.slnx --no-restore
```

## 🔍 輸入診斷工具

如果要先確認滑鼠實際送出的事件，可執行 `ReMouse.InputProbe`：

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj
```

預設模式只觀察低階輸入，不攔截、不重寫、不注入事件；按 `Ctrl+C` 可安全退出。執行期間也可以輸入：

| 指令 | 作用 |
|---|---|
| `S`／`swap` | 交換兩個 ReMouse 功能並保存設定 |
| `P`／`print` | 顯示目前 binding |
| `Q`／`quit` | 離開程式 |

常用參數：

```powershell
# 只記錄瀏覽器 Back/Forward 類型的鍵盤事件
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard

# 記錄所有低階鍵盤事件，包含其他程式標記為 injected 的事件
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard-all --include-injected

# 明確啟用中鍵 flick 診斷；此模式可能攔截 chord 並注入效果
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --middle-flick --flick-delta 120

# 指定測試用設定檔
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --settings C:\temp\remouse-settings.json
```

完整參數可查看：

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --help
```

## ⚙️ 設定與安全行為

- 預設設定檔：`%LOCALAPPDATA%\reMOUSE\settings.json`
- 設定檔不存在、損壞或欄位不合法時，程式會回到安全預設值，不讓 hook 因此無法啟動。
- 正式 App 的快捷鍵、程式啟動與水平滾輪效果會透過 Windows `SendInput` 執行。
- `ReMouse.InputProbe` 預設維持 observer 模式；只有明確使用 `--middle-flick` 才會啟用可能攔截／注入的中鍵 chord。
- 停止 App 前會先停止事件處理、關閉 overlay 並移除全域 hook。

如果輸入行為異常，請先按 `Ctrl+Alt+F12` 暫停，再關閉 App；也請保留終端機輸出與設定檔內容，方便後續排查。

## 🧱 專案結構

```text
src/
├─ ReMouse.App/          WPF 設定視窗、overlay 與 runtime event pump
├─ ReMouse.Core/         平台無關的輸入狀態機、效果模型與設定保存
├─ ReMouse.Windows/      Windows hook、DPI、程式啟動與 SendInput adapter
└─ ReMouse.InputProbe/   低階輸入觀察與硬體診斷工具

tests/                   Core、App、Windows 與 InputProbe 自動化測試
docs/
├─ first-version-acceptance.md   第一版輸入觀察驗收清單
├─ second-version-acceptance.md  側鍵 mapping／Swap 驗收清單
└─ app-acceptance.md             完整 App 驗收清單
```

## 🧪 驗收文件

目前實機驗收以 Terra Pro 為主要參考硬體，建議依序閱讀：

1. [`docs/first-version-acceptance.md`](docs/first-version-acceptance.md)
2. [`docs/second-version-acceptance.md`](docs/second-version-acceptance.md)
3. [`docs/app-acceptance.md`](docs/app-acceptance.md)

## 📄 授權

本專案採用自訂的 **reMOUSE 非商業使用授權**：允許個人使用、研究、複製與修改，但未經授權不得用於商業產品、付費服務、銷售、廣告或其他以商業利益為目的的活動。

詳見 [`LICENSE.md`](LICENSE.md)。第三方套件或元件仍受其各自授權條款約束。

## ⚠️ 免責聲明

本軟體以「現況」提供，不提供任何明示或默示保證。全域輸入 hook 與輸入注入可能影響其他應用程式；請先在可接受風險的環境測試，並在離開電腦前確認 reMOUSE 已暫停或關閉。
