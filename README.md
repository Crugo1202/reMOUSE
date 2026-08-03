# ReMouse.InputProbe

第一版只做輸入觀察：監聽 Windows low-level mouse hook 的 `XButton1` / `XButton2`，輸出按下與放開事件，永遠呼叫 `CallNextHookEx`，不攔截、不重寫、不注入輸入。

## 前置條件

Windows 10/11、.NET 10 SDK。確認：

```powershell
dotnet --version
```

若沒有 .NET 10 SDK，可用：

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact
```

## 執行

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj
```

預期輸出：

```text
Raw XButton1 Down | Binding: PixelInspector
Raw XButton1 Up | Binding: PixelInspector
Raw XButton2 Down | Binding: RadialMenu
Raw XButton2 Up | Binding: RadialMenu
```

目前 Terra Pro 實測映射：

| 實體位置 | Windows 輸出 |
|---|---|
| 下方側鍵 | `XButton1` |
| 上方側鍵 | `XButton2` |

InputProbe 保留 Windows 的標準名稱，不在第一版交換或重映射兩顆按鍵。

目前第二版 Core 已加入內部功能綁定，但仍不吞事件、不注入事件。程式執行期間：

- 輸入 `S` 再按 Enter：交換兩個 ReMouse 功能並保存
- 輸入 `P` 再按 Enter：顯示目前 binding
- 輸入 `Q` 再按 Enter：安全退出

設定預設保存於 `%LOCALAPPDATA%\reMOUSE\settings.json`。可用 `--settings <path>` 指定測試用檔案。

測試 Forward / Backward 是否實際送出瀏覽器鍵盤事件：

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard
```

如果 Terra 的 Forward / Backward 是透過另一層輸入注入，請加上 `--include-injected` 一起觀察：

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard --include-injected
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard-all --include-injected
```

如果需要查看所有低階鍵盤事件：

```powershell
dotnet run --project src/ReMouse.InputProbe/ReMouse.InputProbe.csproj -- --keyboard-all
```

第一版 `ReMouse.InputProbe` 預設 observer 路徑不使用 `SendInput`、`mouse_event`、`keybd_event`，也不建立 Overlay；只有明確 `--middle-flick` opt-in 才會阻擋 chord，正式 App 的 effects 才由背景 pump 使用 `SendInput`。

## 停止

按 `Ctrl+C`。程式會送出 `WM_QUIT`，等待 hook thread 離開並移除已安裝的 hook。

## 驗收

請參考 `docs/first-version-acceptance.md`。

## 專案結構

```text
src/ReMouse.InputProbe/
  Program.cs                 啟動、Ctrl+C 與安全關閉
  LowLevelHookHost.cs        WH_MOUSE_LL／可選 WH_KEYBOARD_LL 與 CallNextHookEx
  NativeMethods.cs           user32/kernel32 P/Invoke 宣告
  ProbeEvent*.cs             非同步事件佇列、狀態驗證與輸出
  ProbeOptions.cs            僅診斷用的鍵盤記錄選項
src/ReMouse.Core/
  Input/                    Raw XButton、功能 binding、Swap 與 active action
  Settings/                 預設值、JSON settings store
tests/ReMouse.InputProbe.Tests/
  ProbeEventProcessorTests.cs  不接觸真實輸入的事件順序與狀態測試
tests/ReMouse.Core.Tests/
  SideButtonMapperTests.cs     mapping、Swap、Down/Up 配對測試
  JsonSettingsStoreTests.cs    設定保存、損壞回復與原子替換測試
docs/first-version-acceptance.md  Terra 驅動驗收矩陣
docs/second-version-acceptance.md 第二版 mapping/Swap 驗收矩陣
```
