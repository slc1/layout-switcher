# LayoutSwitcher v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app that converts typed or selected text between the UA and EN layouts on a hotkey.

**Architecture:** `LayoutSwitcher.Core` holds every decision as pure, unit-tested code over layout tables passed in as data.
`LayoutSwitcher.App` is a thin WinForms + Win32 shell: a low-level keyboard hook feeds a worker thread that calls into Core
and performs `SendInput`, layout switches and clipboard work.

**Tech Stack:** .NET 10 (SDK 10.0.404), C# 14, WinForms, CsWin32, System.Text.Json, xUnit.

**Spec:** `docs/design.md`

## Global Constraints

- Target frameworks: Core and tests `net10.0`; App `net10.0-windows` with `<UseWindowsForms>true</UseWindowsForms>`.
- `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`.
- Core references no Windows API and no NuGet package beyond the BCL.
- Only two layouts exist for the app: `LayoutId.En` and `LayoutId.Ua`.
- Default hotkeys: `Pause` (last word), `Shift+Pause` (selection), `Ctrl+Pause` (all typed), `Win+F8` (toggle).
- Buffer capacity 300 keystrokes; idle reset after 10 minutes; selection limit 2 000 chars.
- Typed characters, keystrokes and clipboard content never reach a log, at any level.
- The project folder is not a git repository yet; commits happen only when the user asks.
- Verification command for Core: `dotnet test tests/LayoutSwitcher.Core.Tests` — expected `Passed!` with 0 failed.

## Review Focus

1. **CapsLock on while typing** — `GHBDSN` typed with CapsLock must become `ПРИВІТ`, not `привіт`. Test in Task 2.
2. **Capital first letter via Shift** — `Ghbdsn` → `Привіт`. Test in Task 2.
3. **Backspace corrections inside the word** — typing `ghbdq`, Backspace, `sn` must convert as `ghbdsn`. Test in Task 4.
4. **Hotkey on an empty buffer or a buffer of only digits/spaces** — no erase, no typing (plan is `null`). Test in Task 5.
5. **Selection with text the layouts cannot map** (newlines, emoji surrogate pairs, digits, other scripts) — passed through unchanged. Test in Task 3.

---

## Phase 1 — Core

### Task 1: Solution scaffold and layout tables

**Files:**
- Create: `LayoutSwitcher.slnx`, `Directory.Build.props`
- Create: `src/LayoutSwitcher.Core/LayoutSwitcher.Core.csproj`
- Create: `src/LayoutSwitcher.Core/KeyStroke.cs`, `LayoutId.cs`, `LayoutTable.cs`, `LayoutPair.cs`
- Create: `tests/LayoutSwitcher.Core.Tests/LayoutSwitcher.Core.Tests.csproj`
- Create: `tests/LayoutSwitcher.Core.Tests/TestLayouts.cs` (fixture), `LayoutTableTests.cs`

**Interfaces:**
- Produces:
  - `readonly record struct KeyStroke(ushort ScanCode, bool Shift, bool CapsLock)`
  - `enum LayoutId { En, Ua }` + extension `LayoutId Other(this LayoutId id)`
  - `sealed class LayoutTable(IReadOnlyDictionary<KeyStroke, char> chars)` with
    `bool TryGetChar(KeyStroke stroke, out char ch)` and
    `bool TryGetStroke(char ch, out KeyStroke stroke)` — reverse lookup prefers `CapsLock=false`, then `Shift=false`.
  - `sealed record LayoutPair(LayoutTable En, LayoutTable Ua)` with indexer `LayoutTable this[LayoutId id]`.
  - Test fixture `TestLayouts.En`, `TestLayouts.Ua`, `TestLayouts.Pair` and helper
    `IReadOnlyList<KeyStroke> TestLayouts.Type(string text, LayoutId layout)` (the keystrokes that type `text` in `layout`).

Fixture: build both tables from per-row strings over scan codes `0x29` (`` ` ``), `0x02–0x0D`, `0x10–0x1B`, `0x1E–0x28`,
`0x2B` (`\`), `0x2C–0x35`, `0x39` (space). EN rows: US QWERTY. UA rows (Ukrainian Enhanced approximation):
`'1234567890-=`, `йцукенгшщзхї`, `фівапролджє`, `ячсмитьбю.`, `\` → `ґ`; shifted `₴!"№;%:?*()_+`, `ЙЦУКЕНГШЩЗХЇ`,
`ФІВАПРОЛДЖЄ`, `ЯЧСМИТЬБЮ,`, `Ґ`. CapsLock entries: for letters, CapsLock inverts the Shift case; for non-letters
CapsLock has no effect. Real tables come from `ToUnicodeEx` in Task 9; this fixture only has to be self-consistent.

- [ ] **Step 1: Create the solution** — `dotnet new classlib`, `dotnet new xunit`, `dotnet new sln --format slnx`, add both projects, test project references Core. Delete template `Class1.cs` / `UnitTest1.cs`.
- [ ] **Step 2: Write failing tests** in `LayoutTableTests`:
  - `TryGetChar` for `KeyStroke(0x10, false, false)` → `'q'` in En, `'й'` in Ua; `(0x10, true, false)` → `'Q'` / `'Й'`; `(0x10, false, true)` → `'Q'` / `'Й'`; `(0x10, true, true)` → `'q'` / `'й'`.
  - `TryGetChar(KeyStroke(0x33, false, false))` → `','` in En, `'б'` in Ua.
  - `TryGetStroke('Q')` in En → `KeyStroke(0x10, true, false)` (Shift preferred over CapsLock).
  - `TryGetStroke('€')` → `false`.
  - `Pair[LayoutId.Ua]` returns the Ua table; `LayoutId.Ua.Other()` is `En`.
- [ ] **Step 3: Run** `dotnet test tests/LayoutSwitcher.Core.Tests` — expected: build fails (types missing).
- [ ] **Step 4: Implement** the types listed under *Produces*. `LayoutTable` builds the reverse dictionary once in the constructor.
- [ ] **Step 5: Run** the tests — expected: all pass.

### Task 2: Stroke classification and text rendering

**Files:**
- Create: `src/LayoutSwitcher.Core/StrokeClassifier.cs`, `TextConverter.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/StrokeClassifierTests.cs`, `TextConverterTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  - `enum StrokeKind { Letter, Joiner, Separator }`
  - `static StrokeKind StrokeClassifier.Classify(KeyStroke stroke, LayoutPair pair, string extraWordChars)` —
    `Letter` if the stroke yields a letter (`char.IsLetter`) in either layout; else `Joiner` if either char is
    `'`, `ʼ` (U+02BC), `’` (U+2019) or is in `extraWordChars`; else `Separator`.
  - `static string TextConverter.Render(IEnumerable<KeyStroke> strokes, LayoutTable layout, LayoutTable fallback)` —
    char from `layout`, else from `fallback`, else the stroke is skipped.

- [ ] **Step 1: Write failing tests:**
  - Classify: `q` key → `Letter`; `,` key (0x33) → `Letter` (UA `б`); `/` key (0x35) → `Separator` (EN `/`, UA `.`);
    `2` key → `Separator`; space → `Separator`; `` ` `` key (0x29) → `Joiner` (UA `'`); `-` key with extra `"-_"` → `Joiner`,
    with extra `""` → `Separator`.
  - Render: `Type("ghbdsn", En)` rendered in Ua → `"привіт"`; `Type("Ghbdsn", En)` → `"Привіт"`;
    the strokes of `GHBDSN` typed with CapsLock on (`KeyStroke(sc, false, true)`) → `"ПРИВІТ"`;
    `Type("ghbdsn/", En)` → `"привіт."`; `Type("руддщ", Ua)` rendered in En → `"hello"`.
- [ ] **Step 2: Run** tests — expected: build fails.
- [ ] **Step 3: Implement** `StrokeClassifier` and `TextConverter.Render`.
- [ ] **Step 4: Run** tests — expected: pass.

### Task 3: Selection conversion

**Files:**
- Modify: `src/LayoutSwitcher.Core/TextConverter.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/TextConverterTests.cs`

**Interfaces:**
- Produces:
  - `const int TextConverter.MaxSelectionLength = 2000`
  - `static string TextConverter.Convert(string text, LayoutTable from, LayoutTable to)` — per char: `from.TryGetStroke` then `to.TryGetChar`; unmapped chars pass through.
  - `static LayoutId TextConverter.DetectSource(string text)` — `Ua` when Cyrillic letters (U+0400–U+04FF) outnumber Latin letters (A–Z, a–z), else `En`.
  - `static bool TextConverter.TryConvertSelection(string text, LayoutPair pair, out string result, out LayoutId target)` —
    `false` for empty text or text longer than `MaxSelectionLength`; otherwise `target = DetectSource(text).Other()`.

- [ ] **Step 1: Write failing tests:**
  - `TryConvertSelection("ghbdsn cdsn", …)` → `"привіт світ"`, target `Ua`.
  - `"руддщ цщкдв"` → `"hello world"`, target `En`.
  - `"Ghbdsn, 2024!\r\n😀"` → `"Привітб 2024!\r\n😀"` (digits and `!` map via shared keys; newline and emoji pass through).
  - `"привіт hello"` (6 Cyrillic vs 5 Latin) → target `En`, result `"ghbdsn hello"` (Latin letters have no stroke in Ua and pass through).
  - Empty string → `false`; `new string('a', 2001)` → `false`; `new string('a', 2000)` → `true`.
- [ ] **Step 2: Run** — expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** — expected: pass.

### Task 4: Typed buffer

**Files:**
- Create: `src/LayoutSwitcher.Core/TypedKey.cs`, `TypedBuffer.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/TypedBufferTests.cs`, `tests/LayoutSwitcher.Core.Tests/ManualTimeProvider.cs`

**Interfaces:**
- Produces:
  - `readonly record struct TypedKey(KeyStroke Stroke, LayoutId Layout)` — `Layout` is the layout the char is **currently displayed in**.
  - `sealed class TypedBuffer(TimeProvider time, int capacity = 300, TimeSpan? idleTimeout = null /* 10 min */)` with
    `void Add(TypedKey key)`, `void Backspace()`, `void Clear()`,
    `IReadOnlyList<TypedKey> Snapshot()` (a copy), `void MarkDisplayed(int start, LayoutId layout)` (sets `Layout` for `[start..Count)`).
  - Idle rule: if `now - lastAdd >= idleTimeout`, the buffer is cleared before `Add` and before `Snapshot`.
  - Test helper `ManualTimeProvider : TimeProvider` with `Advance(TimeSpan)`.

- [ ] **Step 1: Write failing tests:** add 3 → snapshot has 3 in order; `Backspace` removes the last; `Backspace` on empty is a no-op;
  301 adds → 300 kept, first one dropped; advance 10 min → snapshot empty; advance 9:59 → kept;
  `MarkDisplayed(1, Ua)` changes only keys 1..end; typing `ghbdq`, Backspace, `sn` → snapshot strokes equal `Type("ghbdsn", En)`.
- [ ] **Step 2: Run** — expected: fail.
- [ ] **Step 3: Implement** with a `List<TypedKey>` (capacity is small; `RemoveAt(0)` is fine).
- [ ] **Step 4: Run** — expected: pass.

### Task 5: Word segmentation and revert planning

**Files:**
- Create: `src/LayoutSwitcher.Core/WordSegmenter.cs`, `RevertPlanner.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/WordSegmenterTests.cs`, `RevertPlannerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces:
  - `static int WordSegmenter.LastWordStart(IReadOnlyList<KeyStroke> strokes, LayoutPair pair, string extraWordChars)` —
    index of the first stroke of the last word; the range runs to the end and so includes separators typed after the word.
    A `Joiner` counts as part of the word only with a `Letter` on both sides. Returns `strokes.Count` when there is no `Letter`.
  - `enum RevertScope { LastWord, AllTyped }`
  - `sealed record RevertPlan(int Start, int EraseCount, string Text, LayoutId Target)`
  - `static RevertPlan? RevertPlanner.Plan(RevertScope scope, IReadOnlyList<TypedKey> keys, LayoutPair pair, string extraWordChars)` —
    range = `[LastWordStart..Count)` for `LastWord`, `[0..Count)` for `AllTyped`; `null` if the range has no `Letter`.
    `Target = (first Letter in the range).Layout.Other()` — leading digits/spaces say nothing about the source. `Text` = each stroke rendered in `Target`, fallback its own `Layout`.
    `EraseCount = Count - Start`.

Undo needs no state: after the engine applies a plan it calls `MarkDisplayed(plan.Start, plan.Target)`, so the next plan over the
same range targets the original layout.

- [ ] **Step 1: Write failing tests** (strokes via `TestLayouts.Type`, all displayed in `En` unless stated):
  - `LastWordStart`: `"ghbdsn"` → 0; `"ghbdsn "` → 0; `"hello ghbdsn"` → 6; `"ghbdsn, "` → 0 (`,` is a letter key);
    `"vjz v'znf"` → 4 (apostrophe joins); `"ab 123 "` → 0 (digits are separators, word is `ab`); `"  "` → 2; `""` → 0.
  - `Plan(LastWord, "hello ghbdsn ")` → `Start 6, EraseCount 7, Text "привіт ", Target Ua`.
  - `Plan(AllTyped, "ghbdsn cdsn")` → `Start 0, EraseCount 11, Text "привіт світ"`.
  - Undo: apply `MarkDisplayed` with the first plan, plan again → `Text "ghbdsn "`, `Target En`.
  - Mixed: `"ghbdsn "` displayed `En` + `"світ"` displayed `Ua` (typed as `Type("cdsn", En)` keys with `Layout = Ua`),
    `Plan(AllTyped)` → `Target Ua`, `Text "привіт світ"`.
  - `Plan(LastWord, "123 ")` → `null`; `Plan(AllTyped, [])` → `null`.
- [ ] **Step 2: Run** — expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** — expected: pass.

### Task 6: Hotkeys and key classification

**Files:**
- Create: `src/LayoutSwitcher.Core/Input/Modifiers.cs`, `Hotkey.cs`, `KeyClassifier.cs`, `VirtualKeys.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/HotkeyTests.cs`, `KeyClassifierTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] enum Modifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }`
  - `readonly record struct Hotkey(Modifiers Modifiers, ushort Vk)` with `static bool TryParse(string text, out Hotkey hotkey)`
    (case-insensitive, `+`-separated, whitespace tolerant; key names `Pause`, `F1`–`F24`, `A`–`Z`, `0`–`9`, `Space`,
    `CapsLock`, `ScrollLock`, `Insert`) and `ToString()` in canonical order `Ctrl+Alt+Shift+Win+Key`.
  - `static ushort Hotkey.NormalizeVk(ushort vk, ushort scanCode)` — `VK_CANCEL (0x03)` with scan `0x46` → `VK_PAUSE (0x13)`;
    everything else unchanged. Reason: Windows reports `Ctrl+Pause` as `VK_CANCEL`.
  - `enum KeyAction { Record, Backspace, Reset, Ignore }`
  - `static KeyAction KeyClassifier.Classify(ushort vk, Modifiers held)` — modifier keys, CapsLock, NumLock, ScrollLock,
    PrintScreen, Pause → `Ignore`; any `Ctrl`/`Alt`/`Win` held → `Reset`; Backspace → `Backspace`;
    Insert (Shift+Insert pastes), Enter, Esc, arrows, Home, End, PgUp, PgDn, Delete, `F1`–`F24` → `Reset`; everything else → `Record`.
  - `VirtualKeys` — named `const ushort` values for the codes above.

- [ ] **Step 1: Write failing tests:** parse `"Pause"`, `"shift + pause"`, `"Ctrl+Pause"`, `"Win+F8"`; reject `""`, `"Ctrl+"`,
  `"Foo"`, `"Ctrl+Shift"`; round-trip `ToString()` of `"shift+ctrl+pause"` → `"Ctrl+Shift+Pause"`;
  `NormalizeVk(0x03, 0x46)` → `0x13`, `NormalizeVk(0x03, 0x00)` → `0x03`;
  classify `'Q'` → `Record`, `'Q'` with Ctrl → `Reset`, `'Q'` with Shift → `Record`, Backspace → `Backspace`, Enter → `Reset`,
  LShift → `Ignore`, Space → `Record`, Tab → `Record`.
- [ ] **Step 2: Run** — expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** — expected: pass.

### Task 7: Settings and exclusions

**Files:**
- Create: `src/LayoutSwitcher.Core/Config/Settings.cs`, `SettingsStore.cs`, `ExclusionList.cs`
- Test: `tests/LayoutSwitcher.Core.Tests/SettingsStoreTests.cs`, `ExclusionListTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Settings` — `Enabled = true`, `Autostart = true`, `Hotkeys` (`ConvertLastWord = "Pause"`,
    `ConvertSelection = "Shift+Pause"`, `ConvertAllTyped = "Ctrl+Pause"`, `ToggleEnabled = "Win+F8"`),
    `ExtraWordChars = "-_"`, `ExcludedPrograms = ["mstsc.exe"]`, `LogLevel = "None"`,
    `[JsonExtensionData] Dictionary<string, JsonElement>? Extra` (unknown keys survive a save).
  - `sealed class SettingsStore(string path)` — `Settings Load()`: missing file → defaults (nothing written);
    malformed JSON → file renamed to `settings.json.bad` (overwriting an older `.bad`), defaults returned.
    `void Save(Settings s)`: camelCase, indented, writes `path + ".tmp"` then `File.Move(tmp, path, overwrite: true)`; creates the directory.
  - `sealed class ExclusionList(IEnumerable<string> entries)` — `bool IsExcluded(string exePath)`: case-insensitive; an entry
    without a directory separator matches the file name, otherwise the full path; `/` and `\` are equivalent.

- [ ] **Step 1: Write failing tests** (temp dir per test): load missing → defaults; save/load round-trip; unknown key `"future": 1`
  survives load+save; malformed file → defaults and `.bad` exists; exclusions: `"mstsc.exe"` matches `C:\Windows\System32\MSTSC.EXE`,
  `"c:/games/x.exe"` matches `C:\Games\x.exe` but not `D:\Games\x.exe`.
- [ ] **Step 2: Run** — expected: fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole suite — expected: all pass.

---

## Phase 2 — App

App code is Win32 glue; each task ends with a manual check instead of unit tests. Before Task 8 starts, the Phase 1 suite is green.

### Task 8: App scaffold and tray

**Files:** `src/LayoutSwitcher.App/LayoutSwitcher.App.csproj` (WinExe, `net10.0-windows`, CsWin32 package, `NativeMethods.txt`),
`Program.cs`, `Ui/TrayIcon.cs`, `AppPaths.cs`.

- Single instance via a named mutex `Local\LayoutSwitcher`; a second launch exits.
- Settings path `%AppData%\LayoutSwitcher\settings.json` via `SettingsStore`.
- Tray: text icon `EN`/`UA` drawn into a 16×16/32×32 bitmap (DPI-aware); menu Enable/Disable (checked), Settings…, Exit.
- **Check:** run, icon appears, menu works, Exit removes the icon.

### Task 9: Layout service

**Files:** `Layouts/LayoutService.cs`, `Layouts/LayoutTableBuilder.cs`.

- `LayoutService.FindLayouts()` → HKLs for the first installed UA (`LANGID 0x0422`) and EN (primary language `0x09`) from `GetKeyboardLayoutList`; missing one → tray balloon and the app stays disabled.
- `LayoutTableBuilder.Build(HKL)` → `LayoutTable`: for each scan code `0x02–0x35`, `0x39`, each Shift/CapsLock combo:
  `MapVirtualKeyEx(sc, MAPVK_VSC_TO_VK_EX, hkl)` then `ToUnicodeEx(vk, sc, state, buf, 4, 0x4, hkl)`; keep results equal to 1 char.
- `LayoutId? GetFocusedLayout()` — `GetGUIThreadInfo(foreground thread).hwndFocus` (fallback foreground window) → `GetKeyboardLayout(thread)`; unknown HKL → `null`.
- `void SetLayout(LayoutId)` — `PostMessage(hwndFocus, WM_INPUTLANGCHANGEREQUEST, 0, hkl)`.
- **Check:** a debug menu item "Dump layouts" writes the table sizes (not contents) to the log; switching via `SetLayout` changes the
  layout in Notepad and in Windows Terminal (record the result for the console case in `docs/design.md` §11).

### Task 10: Keyboard hook and input sender

**Files:** `Input/KeyboardHook.cs`, `Input/MouseHook.cs`, `Input/InputSender.cs`, `Input/InputEvent.cs`.

- Dedicated thread with a message loop installs `WH_KEYBOARD_LL` and `WH_MOUSE_LL`.
- Callback: skip `dwExtraInfo == InputSender.Tag`; compute held `Modifiers` from its own tracked key state (not `GetKeyState`);
  `vk = Hotkey.NormalizeVk(vk, scan)`; if a configured hotkey matches on key-down → post `HotkeyEvent`, return `1`;
  else post `KeyEvent(vk, scan, shift, capsLockToggled, modifiers)` and call `CallNextHookEx`. Mouse button down → `ResetEvent`.
  Events go into a `Channel<InputEvent>` created with `SingleReader = true`.
- `InputSender`: `ReleaseModifiers(Modifiers)`, `SendBackspaces(int)`, `SendText(string)` (`KEYEVENTF_UNICODE`, surrogate pairs as two events),
  `SendChord(Modifiers, ushort vk)` (for `Ctrl+C` / `Ctrl+V`); every event carries `dwExtraInfo = Tag`.
- Extended scan codes (`LLKHF_EXTENDED`: numpad `/`, numpad Enter, …) → `ResetEvent`; Core keys strokes by scan code only,
  so numpad `/` would otherwise be retyped as main `/` (`.` in UA).
- **Check:** with a temporary log of event *kinds and counts only*, typing produces `KeyEvent`s; pressing `Pause`, `Shift+Pause`,
  `Ctrl+Pause` each produces exactly one `HotkeyEvent` and Notepad receives nothing.

### Task 11: Switch engine — typed text (F1, F3)

**Files:** `Engine/SwitchEngine.cs`.

- Worker loop reads the channel. `KeyEvent` → `KeyClassifier.Classify`; for `Record`: `GetFocusedLayout()`; `null` or the stroke has no char in that
  layout → `Clear()` (**required**: `RevertPlan.EraseCount` counts every stroke, so an unmapped stroke left in the buffer would be erased and not retyped), else `Add(new TypedKey(stroke, layout))`. `Backspace` → `Backspace()`. `Reset` → `Clear()`.
- `EVENT_SYSTEM_FOREGROUND` (via `SetWinEventHook` on the hook thread) → `ResetEvent`.
- Excluded foreground process or app disabled → events dropped, buffer cleared.
- Hotkey `ConvertLastWord` / `ConvertAllTyped` → `RevertPlanner.Plan`; on a plan: `ReleaseModifiers`, `SendBackspaces(EraseCount)`,
  `SendText(Text)`, `SetLayout(Target)`, `MarkDisplayed(Start, Target)`, update the tray icon.
- **Check:** in Notepad, `ghbdsn` + `Pause` → `привіт`, `Pause` again → `ghbdsn`; `ghbdsn cdsn` + `Ctrl+Pause` → `привіт світ`;
  `Pause` on an empty line does nothing.

### Task 12: Selection (F2) and clipboard guard

**Files:** `Clipboard/ClipboardGuard.cs`, `Engine/SwitchEngine.cs`.

- `ClipboardGuard` runs on the UI thread (marshalled via `Control.Invoke`): `Backup()` snapshots `CF_UNICODETEXT`, `CF_HDROP`, `CF_DIB`,
  `Preferred DropEffect`, `Shell IDList Array`, total ≤ 1 MB; `Restore()` writes them back and wipes the snapshot; snapshot is wiped after 10 s regardless.
  Every write the app makes also sets `ExcludeClipboardContentFromMonitorProcessing`.
- F2 sequence exactly as `docs/design.md` §7.2: backup → release modifiers → `Ctrl+C` → wait for `GetClipboardSequenceNumber` change (400 ms) →
  `TextConverter.TryConvertSelection` → set text → `Ctrl+V` → after 300 ms restore → `SetLayout(target)` → `Clear()`.
- **Check:** copy an image in Paint, select `ghbdsn` in Notepad, `Shift+Pause` → `привіт`; paste in Paint → the original image.
  `Shift+Pause` with nothing selected → nothing changes and the clipboard is intact.

### Task 13: Settings window, toggle, autostart, logging

**Files:** `Ui/SettingsForm.cs`, `Startup/Autostart.cs`, `Logging/AppLog.cs`.

- Settings form: four hotkey capture boxes (press a combo to set it; conflicting duplicates rejected; a bare letter/digit or
  Shift+letter/digit is rejected because the hook would swallow normal typing), extra word chars, excluded programs
  (list + add/remove + "pick running process"), autostart checkbox. Save applies live without restart.
- `Win+F8` and the tray item toggle `Settings.Enabled`; the icon greys out when disabled.
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\LayoutSwitcher` = quoted exe path.
- Logging: file sink in `%LocalAppData%\LayoutSwitcher\logs`, level from settings, `None` by default. Log statements take only event
  kinds, counts, layout ids and timings — reviewed by grep for any `Text`, `Stroke`, `clipboard` value passed to a logger.
- Elevated foreground window (process token elevated, ours not) → one tray balloon per session explaining the limitation.
- **Check:** the manual checklist in `docs/design.md` §12, all apps, both directions, undo.
