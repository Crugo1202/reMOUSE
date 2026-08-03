# ReMouse 第二版：側鍵邏輯映射與 Swap

## 目前 Terra Pro 硬體映射

- 下方側鍵 → Windows `XButton1` → 預設 `PixelInspector`
- 上方側鍵 → Windows `XButton2` → 預設 `RadialMenu`

程式內部永遠保留 raw `XButton1/XButton2`，不把實體位置偽裝成另一個 Windows 按鍵。

## 自動驗收

```powershell
dotnet restore reMOUSE.slnx
dotnet build reMOUSE.slnx --no-restore
dotnet test reMOUSE.slnx --no-restore
```

必須同時通過：

- Core mapping/Swap/active-action/persistence tests
- 原有 InputProbe raw event tests
- InputProbe Raw/Binding formatter tests

## Mapping 驗收

- [ ] 預設 `XButton1 Down/Up` 都是 `PixelInspector`
- [ ] 預設 `XButton2 Down/Up` 都是 `RadialMenu`
- [ ] `S` + Enter 交換兩個 binding
- [ ] Swap 後 raw ID 仍顯示原本的 `XButton1/XButton2`
- [ ] Swap 兩次恢復預設
- [ ] `Down → Swap → Up` 的 Up 仍使用 Down 當時的 action
- [ ] 兩顆鍵同時按住後 Swap，兩組 Down/Up 不交叉
- [ ] duplicate Down 不再次派送；orphan Up 不派送

診斷格式：

```text
Raw XButton1 Down | Binding: PixelInspector
Raw XButton1 Up   | Binding: PixelInspector
```

## Persistence 驗收

- [ ] `%LOCALAPPDATA%\reMOUSE\settings.json` 可建立
- [ ] Swap 後關閉再啟動仍保留 binding
- [ ] 缺少整個檔案或欄位時回到安全預設
- [ ] 非法 enum、損壞 JSON、未知 schema 不會阻止 hook 啟動或退出
- [ ] 保存使用同目錄 temporary file，再替換目標檔
- [ ] 保存失敗時不改變目前有效 runtime binding

## 安全與非回歸

- [ ] `ReMouse.InputProbe` 預設 observer 路徑的 hook callback 只解析 raw event、`TryWrite`、`CallNextHookEx`
- [ ] `ReMouse.InputProbe` 預設 observer 路徑永遠呼叫 `CallNextHookEx`
- [ ] `ReMouse.InputProbe` 預設 observer 路徑不使用 `SendInput`、`mouse_event`、`keybd_event`；只有明確 `--middle-flick` opt-in 才會阻擋 chord，並由非 callback effect processor 使用 `SendInput`
- [ ] 不模擬另一顆 XButton
- [ ] 不改變瀏覽器原本 Back/Forward
- [ ] Ctrl+C、`Q`、`WM_QUIT` 後正常 unhook
- [ ] 左/右/中鍵、滾輪、游標、拖曳仍正常

## 尚未由自動測試證明的人工項目

需要在 Terra Pro 實機執行：

- [ ] Default、Swap、Swap twice
- [ ] 按住期間 Swap
- [ ] 兩鍵交錯/同時按住
- [ ] 重啟後讀回 settings
- [ ] 損壞 settings 啟動與退出
- [ ] 連續啟動/關閉 20 次

## 下一階段入口

`ReMouse.Core` 定義純資料效果：水平滾輪、中鍵 click、按鍵序列；`ReMouse.Windows` 已提供 `SendInput` adapter，並由 `ReMouse.App` 的 background effect pump 使用。第一版 `ReMouse.InputProbe` 仍維持 observer 預設，不會注入或阻擋輸入。完整 App 驗收請看 `docs/app-acceptance.md`。
