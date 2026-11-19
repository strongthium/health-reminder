<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[health-reminder](../README.md) › [Manual](README.md) › Command line

# Command line

`health-reminder.exe` needs nothing on its command line: started on its own it opens its window, and keeps everything
in its usual folder. It takes these switches:

| | |
|---|---|
| `--data <folder>` | keeps everything in that folder instead of `%LocalAppData%\Strongthium\health-reminder`: on a USB stick, say, or to try the program out without touching your reminders. The folder is made if it isn't there |
| `--minimized` | starts in the tray, without opening the window; it comes up when something is due. **Start when I sign in to Windows** starts it this way |
| `--wait-for <process id>` | used by the update: the new program waits for the old one to quit, then deletes the old file. Not for typing |

```
health-reminder.exe --data "E:\health-reminder data"
health-reminder.exe --minimized
```

## One copy at a time

One copy runs for each Windows user and data folder, so that nothing rings twice. Starting it again while it runs
brings up the window of the copy that's running (unless it's started with `--minimized`), and the new one ends.

A copy started with its own `--data` folder runs beside the usual one, with its own tray icon and its own reminders.
There's one **Start when I sign in to Windows** entry: a copy with its own folder leaves it alone, but ticking the box
in that copy makes the entry start it, with its folder, in place of the usual one.

← [About](about.md) · [Contents](README.md) · [Your data](your-data.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <b>Health Reminder</b> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <a href="https://github.com/strongthium/outbound-watch">Outbound Watch</a> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
