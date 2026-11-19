<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[health-reminder](../README.md) › [Manual](README.md) › Installing and updating

# Installing and updating

`health-reminder` is one program file, `health-reminder.exe`, for Windows 10 and 11 on x64 PCs. There's nothing to
install: put it where you like and run it.

## What it needs

| | |
|---|---|
| **Windows** | Windows 10 or 11, x64 |
| **.NET** | the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), which Microsoft Update keeps patched. Without it, Windows offers to download it the first time the program is started |
| **The internet** | only to look medication up (DailyMed, RxNorm, Health Canada), for Help from Claude if you turn it on, and for About's update check. Reminders ring without it |
| **A webcam** | only for reminders set to record a video |

## Getting it

From [Releases](../../../releases/latest), download `health-reminder-<version>-win-x64-needs-dotnet.zip`, unpack it, and
run `health-reminder.exe` (about 3 MB). The release's `SHA256SUMS` has the file's SHA-256, to check the download.
The same `.exe` is also in the source's [`public/`](../public) folder, which always holds the latest build.

## The first start

The main window opens on [Today](today.md). With no reminders yet it says **Nothing to remind you of yet**, with
**Add a medication or check** and **Plan meals and drinks** to start with. Pages are picked down the side: **Today**,
**Reminders**, **Eat & drink**, **Menu**, **Groceries**, **Exercise**, **Smoking**, **Stats**, **History**,
**Settings** and **About**.

The window follows Windows' light or dark mode and accent colour.

## In the tray

Reminders ring only while `health-reminder` runs. It puts an icon in the tray (the notification area), and keeps
running there when its window is closed, unless you tell it to quit (see [Close Health Reminder](close.md)). Under the
icon it says what's next: "Health Reminder: next, Metformin 500 mg at 18:00".

| | |
|---|---|
| **Click** the icon | opens the main window |
| **Open Health Reminder** | the same, from the icon's right-click menu |
| **Log a cigarette** | logs one, as the [button on top](smoke-button.md) does |
| **Log food…** | opens [Log food](log-food.md) |
| **Quit (no more reminders)** | stops the program: nothing rings until it's started again |

Starting `health-reminder.exe` again while it runs brings up the window of the copy that's running: one copy runs at a
time for each Windows user and data folder, so nothing rings twice.

## Starting with Windows

**Start when I sign in to Windows**, under **Start-up and data** in [Settings](settings.md), starts it in the tray
each time you sign in; it comes up when something is due. It adds the value `Strongthium health-reminder` to
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, which needs no administrator, and unticking it takes
the value away. If you move the program file, the next start puts the entry right.

## Updating

[About](about.md) asks GitHub whether there's a newer version when it's opened. If there is, **Update to
&lt;version&gt; and restart** downloads the release, checks it against the release's `SHA256SUMS`, puts the new program
in the place of the old one and starts it. Your reminders, history and kitchen are in their own folder (see
[Your data](your-data.md)) and aren't touched.

If the program's folder can't be changed without an administrator (`C:\Program Files`, say), About says so; **What's
new** opens the release's page, to download it by hand.

## Removing it

1. Untick **Start when I sign in to Windows** in Settings, if it's ticked.
2. Quit it from the tray icon's menu.
3. Delete `health-reminder.exe`, and, to delete everything it kept, the folder named under **Your data** in Settings
   (`%LocalAppData%\Strongthium\health-reminder` unless it was started with `--data`).

[Contents](README.md) · [Today](today.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <b>Health Reminder</b> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <a href="https://github.com/strongthium/outbound-watch">Outbound Watch</a> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
