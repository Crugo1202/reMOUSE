# ReMouse.App 目前版本驗收

## 啟動

```powershell
dotnet build reMOUSE.slnx --no-restore
dotnet test reMOUSE.slnx --no-restore
dotnet run --project src/ReMouse.App/ReMouse.App.csproj
```

設定檔位於 `%LOCALAPPDATA%\reMOUSE\settings.json`。第一次啟動會使用安全預設：

- Terra 下方側鍵 `XButton1` → Pixel Inspector
- Terra 上方側鍵 `XButton2` → Radial Menu
- flick delta → `120`

## Radial Menu

- 按住設定的側鍵開啟；移動到 slot，放開執行。
- 中央 dead-zone 放開不執行任何 action。
- UI 可選 `No action`、shortcut 或 `.exe` application。
- shortcut 欄位點擊後直接按 `Ctrl+C`、`Ctrl+Shift+V` 等組合鍵錄製。
- shortcut 欄位可按 `Clear`、Backspace 或 Delete 清除；改動要按 `Save settings` 才會套用。
- application 優先使用 `Running apps...` 或 `Start Menu...` 選取，不需要手動輸入完整 exe 路徑；只有找不到時才用 `Browse...`。
- Start Menu picker 只讀取使用者／共用 Start Menu 的 `.lnk`，會一併帶入捷徑 arguments，不掃描整顆磁碟。
- `Reset editor` 只把編輯器載入安全預設，按 `Save settings` 後才會改 runtime 與設定檔。

## Settings recovery

- 設定檔第一次不存在時會建立安全預設。
- JSON 損壞時會改名為 `settings.json.corrupt-<timestamp>.json`，並在 UI 顯示備份檔名。
- 不支援的 schema 或局部非法 flick/radial 值會回退安全預設，UI 會顯示「Save to repair」。
- 設定路徑不可讀或預設檔無法建立時仍會啟動 fail-open hook，UI 會提示檢查權限。

## Pixel Inspector

- 單擊設定的側鍵進入，畫面顯示即時原始像素 `X/Y`。
- 按住左鍵拖曳，放開後保留矩形；顯示 TL/TR/BL/BR 四點及 W/H。
- 再單擊同一側鍵退出；離開模式後左鍵、游標移動恢復原生行為。
- overlay 為 click-through，不會自己取得焦點。

## Middle flick

- 按住中鍵再按左鍵：送出負向水平 wheel delta。
- 按住中鍵再按右鍵：送出正向水平 wheel delta。
- 只有普通中鍵按下/放開時，才重播一個中鍵 click。
- effect 由背景 pump 呼叫 `SendInput`；低階 hook callback 不直接注入。
- radial/pixel modal 進出時會清掉中鍵 chord held state，避免跨模式殘留。

## 手動驗收

- [ ] 兩顆側鍵各自 Down/Up 一次，長按不重複。
- [ ] radial dead-zone、Copy/Paste shortcut、application launch。
- [ ] `Running apps...` 與 `Start Menu...` 能選出應用程式；Start Menu arguments 能保存。
- [ ] shortcut 可用 Clear/Backspace/Delete 清除；Reset editor 未 Save 前不改 runtime。
- [ ] 損壞 settings 會產生 `.corrupt-*.json` 備份並顯示恢復提示。
- [ ] pixel 即時 XY、反向拖曳仍正確顯示 normalized rectangle。
- [ ] 中鍵 click、左 flick、右 flick；設定 delta 後重啟確認生效。
- [ ] 按一般 Esc 不會暫停 reMOUSE；按 `Ctrl+Alt+F12` 可緊急暫停，Resume 可恢復。
- [ ] 關閉 App 後滑鼠恢復原生；重啟 20 次沒有雙重 hook。

若目前執行環境在 WPF `MS.Internal.FontCache.Util` 報 `UriFormatException`，那是環境的 WPF font-cache 問題；請在正常 Windows 桌面 session 重跑 App smoke，Core/App/Windows/InputProbe 自動測試仍可獨立驗證。
