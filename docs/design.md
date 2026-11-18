# LayoutSwitcher — Design

A Windows tray utility that fixes text typed in the wrong keyboard layout (UA ↔ EN) on a hotkey.
Inspired by [SimpleSwitcher](https://github.com/Aegel5/SimpleSwitcher); a from-scratch C# implementation
with a deliberately smaller scope.

## 1. Scope

### v1

| # | Feature | Default hotkey |
|---|---|---|
| F1 | Convert the **last typed word** and switch the layout. Pressing again right away undoes it. | `Pause` |
| F2 | Convert the **selected text** and switch the layout. | `Shift+Pause` |
| F3 | Convert **all recently typed text** (since the buffer was last reset) and switch the layout. Pressing again undoes it. | `Ctrl+Pause` |
| F4 | Toggle the app on/off. | `Win+F8` |
| F5 | Tray icon showing the current layout as text (`EN` / `UA`); menu: Enable/Disable, Settings, Exit. | — |
| F6 | Settings window: hotkeys, excluded programs, autostart. | — |
| F7 | Excluded programs (by exe name or full path): no interception at all. | — |
| F8 | Autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. | — |

Supported layouts: exactly two — Ukrainian and English (any US/UK variant). If more or fewer
layouts are installed, the app picks the first installed UA and EN layouts and ignores the rest.

### v2 backlog

- Hotkeys for cyclic layout change and for "switch to EN" / "switch to UA".
- Selected text: UPPER/lower toggle, iNVERT cASE.
- Paste without formatting.
- Hotkey triggers on double tap (`Shift ×2`) and on key-up.
- Working inside elevated (admin) windows — needs the app started elevated via Task Scheduler,
  or `uiAccess` with a signed exe in `Program Files`.
- Automatic wrong-layout detection (dictionary / n-gram based).

### Out of scope

Reminders, command runner and snippets, skins and themes, scancode remapping, the Ctrl+LAlt→RAlt fix,
the RDP passthrough mode, and disabling accessibility shortcuts.

## 2. Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (LTS), `net10.0-windows` |
| UI (tray, settings) | WinForms (`NotifyIcon`, one settings `Form`) |
| Win32 interop | [CsWin32](https://github.com/microsoft/CsWin32) source generator (`NativeMethods.txt`) |
| Config | `System.Text.Json`, `%AppData%\LayoutSwitcher\settings.json` |
| Logging | `Microsoft.Extensions.Logging` → rolling file in `%LocalAppData%\LayoutSwitcher\logs`, off by default |
| Tests | xUnit |
| Distribution | single-file publish, framework-dependent by default (self-contained as an option) |

## 3. Solution layout

```
LayoutSwitcher.slnx
src/
  LayoutSwitcher.Core/        net10.0 — pure logic, no Win32 calls
    KeyStroke.cs, TypedKey.cs scan code + Shift + CapsLock; plus the layout it is displayed in
    LayoutTable.cs, LayoutPair.cs  char ↔ keystroke tables (injected data)
    TypedBuffer.cs            capped list of typed keys + idle reset
    StrokeClassifier.cs, WordSegmenter.cs  letter / joiner / separator, word boundaries
    RevertPlanner.cs          what to erase / what to type / target layout
    TextConverter.cs          rendering keystrokes, converting a selected string
    Input/                    Hotkey parsing, key classification (record / reset / ignore)
    Config/                   Settings, SettingsStore, ExclusionList
  LayoutSwitcher.App/         net10.0-windows — WinForms + Win32
    Program.cs
    Input/KeyboardHook.cs     WH_KEYBOARD_LL on a dedicated thread
    Input/InputSender.cs      SendInput wrappers, own-input tagging
    Layouts/LayoutService.cs  get/set layout of the focused window, build LayoutTable via ToUnicodeEx
    Clipboard/ClipboardGuard.cs  backup / restore of the user's clipboard
    Engine/SwitchEngine.cs    worker thread; orchestrates F1–F3
    Ui/TrayIcon.cs, Ui/SettingsForm.cs
    Config/Settings.cs, Config/SettingsStore.cs
tests/
  LayoutSwitcher.Core.Tests/
```

`Core` takes layout tables as data, so all of the conversion and segmentation logic is unit-testable
without Windows. Tests use a hand-written US / Ukrainian (Enhanced) fixture; the app builds the real tables at
runtime from the installed layouts.

## 4. Architecture

```
 ┌──────────────── Hook thread ────────────────┐
 │ WH_KEYBOARD_LL callback                      │
 │  • ignore own injected input (dwExtraInfo)   │
 │  • match hotkey → swallow (return 1)         │
 │  • enqueue raw event, return immediately     │
 └───────────────┬──────────────────────────────┘
                 │ Channel<InputEvent>
 ┌───────────────▼──── Worker thread ──────────┐
 │ SwitchEngine                                 │
 │  • keystroke → TypedBuffer                   │
 │  • hotkey → F1/F2/F3                         │
 │  • SendInput, layout change, clipboard       │
 └───────────────┬──────────────────────────────┘
                 │ events (layout changed, enabled)
 ┌───────────────▼──── UI thread (STA) ────────┐
 │ NotifyIcon, SettingsForm, clipboard access   │
 └──────────────────────────────────────────────┘
```

Rules:

- **The hook callback does no work beyond matching and enqueueing.** Windows silently removes a
  low-level hook whose callback exceeds `LowLevelHooksTimeout`. No allocation, no logging, no locks
  in the callback; the event struct is pre-allocated.
- The hook thread runs its own message loop (`GetMessage`), required for `WH_KEYBOARD_LL`.
- Clipboard calls go through the UI (STA) thread; the worker awaits them.
- Own input is tagged with a constant in `KEYBDINPUT.dwExtraInfo` and skipped by the hook, so the app
  never records or reacts to what it types itself.
- Mouse clicks are watched with `WH_MOUSE_LL` on the same hook thread, only to reset the buffer.

## 5. Typed-text buffer

Each keystroke is recorded as `KeyStroke { ScanCode, Vk, Shift, CapsLock }`, never as a character.
Characters are derived on demand for a given layout. This is what makes conversion exact.

| Input | Effect |
|---|---|
| Printable key, alone or with Shift | append |
| `Backspace` | pop last |
| `Space`, `Tab` | append (word separator) |
| `Enter`, arrows, `Home`, `End`, `PgUp`, `PgDn`, `Delete`, `Esc`, `F1`–`F24` | reset |
| Any combo with `Ctrl`, `Alt` or `Win` (except our hotkeys) | reset (includes `Ctrl+Backspace`, `Ctrl+V`) |
| Mouse button down | reset |
| Foreground window changed (`EVENT_SYSTEM_FOREGROUND`) | reset |
| No typing for 10 minutes | reset |
| Excluded program focused / app disabled | nothing recorded |

Capacity: 300 keystrokes; the oldest are dropped.

## 6. Word segmentation

A keystroke is a **letter** if it produces a letter (or an apostrophe, see below) in **either** layout.
Otherwise, it is a **separator**. This matters because on the Ukrainian layout `[ ] ; ' , .` produce
`х ї ж є б ю`. So `ghbdsn,` and `руддщб` both map to whole words and are not split on `,` or `б`.
The tables come from `ToUnicodeEx` on the installed layouts, so variant differences (standard vs.
enhanced Ukrainian: where `ґ` and the apostrophe live) need no hard-coding.

- Apostrophe (`'` U+0027, `ʼ` U+02BC, `’` U+2019) is treated as a letter when it falls between two letters,
  so `м'ята` is one word.
- Digits are separators. Converting `2 ghbdsns` must not alter `2`.
- Configurable `extraWordChars` (default `-_`) are treated as letters when between letters.

**Last word (F1)** = trailing separators + the last letter run before them. Typing `ghbdsn ` and
pressing `Pause` converts `ghbdsn` and keeps the space after it.

## 7. Conversion flows

### 7.1 Typed text (F1, F3)

1. Read the focused window's layout: `GetGUIThreadInfo` → `hwndFocus` (fallback: foreground window),
   then `GetKeyboardLayout(threadId)`. Target = the other layout of the UA/EN pair.
2. Build the target string by mapping each recorded keystroke through `ToUnicodeEx` for the target layout
   (flag `0x4`: do not alter the kernel dead-key state).
3. Release any modifiers the user is still holding for the hotkey (synthetic key-up), so
   `Ctrl` from `Ctrl+Pause` does not turn our input into shortcuts.
4. `SendInput`: N × `Backspace` (N = number of keystrokes being replaced), then the target string as
   `KEYEVENTF_UNICODE` events.
5. Switch the focused window to the target layout: `PostMessage(hwndFocus, WM_INPUTLANGCHANGEREQUEST, 0, hkl)`.
6. Mark the converted keystrokes as now displayed in the target layout.

The buffer stores each keystroke together with the layout it is **currently displayed in**
(`TypedKey(KeyStroke, LayoutId)`). The target of a conversion is the opposite of the layout of the first
keystroke in the range. Keystrokes themselves never change — they are layout-independent.

**Why Unicode injection instead of replaying the keystrokes in the new layout**: replaying depends on the
target window having *already* applied the layout switch, which is asynchronous and differs per app. Typing
precomputed Unicode removes that race. The trade-off: some games and remote-desktop clients ignore
`KEYEVENTF_UNICODE`. Those belong on the excluded list.

**Undo** needs no extra state: pressing the hotkey again converts the same range back, because it is now
marked as displayed in the target layout. The same rule makes `Pause` then `Ctrl+Pause` produce one consistent
layout for the whole text.

### 7.2 Selected text (F2)

1. `ClipboardGuard.Backup()`: snapshot `CF_UNICODETEXT`, `CF_HDROP`, `CF_DIB` and the shell formats,
   total cap 1 MB.
2. Release held modifiers (`Shift` from `Shift+Pause`), then send `Ctrl+C`.
3. Wait for `GetClipboardSequenceNumber` to change (timeout 400 ms). On timeout: nothing was selected →
   restore the clipboard and stop.
4. Read the text. If it is longer than 2 000 characters, restore and stop.
5. Detect direction **by content, not by the current layout**: if Cyrillic letters outnumber Latin, convert
   UA→EN, otherwise EN→UA. `TextConverter` maps each char back to a keystroke in the source layout
   (`VkKeyScanEx`, precomputed into `LayoutMap`) and forward through the target layout. Chars with no
   mapping (digits, emoji, other scripts) pass through unchanged.
6. Put the result on the clipboard, send `Ctrl+V`.
7. After 300 ms, restore the original clipboard (`ClipboardGuard.Restore()`); clear the snapshot from memory.
8. Switch the window to the target layout.
9. Reset the typed buffer.

Writes made by the app are excluded from clipboard history and cloud sync by setting the
`ExcludeClipboardContentFromMonitorProcessing` format.

## 8. Hotkeys

- Hotkeys are matched in the hook against the **current modifier state + VK**, and the triggering key
  event is swallowed (`return 1`) so the target app never sees it.
- **Trap — `Ctrl+Pause`**: with `Ctrl` down, Windows reports `Pause` as `VK_CANCEL` (0x03), not `VK_PAUSE`.
  The matcher normalizes `VK_CANCEL` + scan code `0x46` (extended) to `VK_PAUSE` before matching.
- `RegisterHotKey` is not used: all hotkeys go through the hook, which lets a key be swallowed and keeps
  modifier handling in one place.
- Config format: `"Ctrl+Pause"`, `"Shift+Pause"`, `"Win+F8"`; left/right modifiers are not distinguished in v1.

## 9. Configuration

```json
{
  "enabled": true,
  "autostart": true,
  "hotkeys": {
    "convertLastWord": "Pause",
    "convertSelection": "Shift+Pause",
    "convertAllTyped": "Ctrl+Pause",
    "toggleEnabled": "Win+F8"
  },
  "extraWordChars": "-_",
  "excludedPrograms": ["mstsc.exe"],
  "logLevel": "None"
}
```

Unknown keys are preserved on save. A malformed file falls back to defaults and is renamed to
`settings.json.bad` rather than overwritten.

## 10. Privacy and security

- The keystroke buffer lives only in memory, is capped, and resets on window change and timeout.
- **Logs never contain typed characters, keystrokes, or clipboard content** — only event kinds, lengths,
  layout ids and timings. This holds at every log level.
- The clipboard snapshot is wiped right after restore, and in any case after 10 s.
- No network access.

## 11. Known limitations (v1)

- **Elevated windows**: UIPI keeps a non-elevated app out of an elevated window both ways — its low-level
  hook never sees keys bound for that window (so the hotkey cannot even be detected), and it could not inject
  input there anyway. Verified on Windows 11. The foreground-change event still arrives, so while an elevated
  window is focused the tray icon shows a grey `!` with a tooltip, and a notification appears once per session.
  Working there needs the app itself to run elevated (v2 backlog).
- **Console windows** (`conhost`, Windows Terminal): the layout reported for the focused thread can be
  stale. Verify during implementation; fallback is emulating the system layout-switch shortcut.
- Apps that ignore `KEYEVENTF_UNICODE` (some games, RDP/VNC clients) → excluded programs.
- Autocomplete/IntelliSense popups may consume `Backspace` or insert text between our events.

## 12. Testing

**Unit (Core)**: segmentation (`ghbdsn,` → one word; `м'ята`; digits; trailing spaces), conversion tables
for both directions incl. Shift and CapsLock, undo state, buffer reset rules.

**Manual checklist** (each of F1–F3, both directions, then undo):
Notepad, Word, Chrome (address bar + text field), VS Code, Telegram Desktop, Windows Terminal,
an elevated Notepad (expect the notification), an excluded program (expect no action).
Also: user clipboard contains an image/file before F2 → identical after F2.
