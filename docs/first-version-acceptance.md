# ReMouse.InputProbe 第一版驗收清單

## XButton

目前 Terra Pro 實測硬體方向：下方側鍵 = `XButton1`，上方側鍵 = `XButton2`。以下清單中的名稱以 Windows 輸出為準。

- [ ] XButton1 click 的 Raw 欄位輸出一組 `Raw XButton1 Down`、`Raw XButton1 Up`
- [ ] XButton2 click 的 Raw 欄位輸出一組 `Raw XButton2 Down`、`Raw XButton2 Up`
- [ ] 長按只有一次 Down，放開只有一次 Up
- [ ] XButton1 與 XButton2 狀態互不影響
- [ ] 連續點擊 20 次得到 20 次 Down、20 次 Up
- [ ] 沒有 `Warning`（除非硬體真的送出異常重複事件）

## 原本滑鼠功能

- [ ] 左鍵、右鍵、中鍵正常
- [ ] 垂直滾輪正常
- [ ] 游標移動和拖曳正常
- [ ] XButton 原本的 Forward / Backward 正常

## 驅動對照

| Terra 驅動設定 | 觀察結果 |
|---|---|
| XButton1 / XButton2 | 應看到相應 XButton Down/Up |
| Forward / Backward | 先執行 `--keyboard --include-injected`；必要時改用 `--keyboard-all --include-injected`，記錄 BrowserBack/BrowserForward、XButton 或無事件 |

如果瀏覽器有前進／後退，但 InputProbe 沒有 XButton 或 BrowserBack/BrowserForward，請記錄為「驅動或 Windows AppCommand 路徑，未由本 probe 的低階滑鼠／鍵盤 hook 觀察到」。

## 關閉與重啟

- [ ] 正常關閉後滑鼠行為恢復
- [ ] Ctrl+C 後滑鼠行為恢復
- [ ] 連續啟動、關閉 20 次不會出現雙份事件
- [ ] 以 `--keyboard` 啟動和關閉後仍無殘留鍵盤 hook

## 執行測試

```powershell
dotnet restore reMOUSE.slnx
dotnet build reMOUSE.slnx --no-restore
dotnet test reMOUSE.slnx --no-restore
```
