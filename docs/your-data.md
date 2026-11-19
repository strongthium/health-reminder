<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[health-reminder](../README.md) › [Manual](README.md) › Your data

# Your data

Everything `health-reminder` keeps is on your computer, in one folder: `%LocalAppData%\Strongthium\health-reminder`,
or the folder given with `--data` (see [Command line](command-line.md)). **Your data** in [Settings](settings.md) says
where, and **Open the folder** opens it.

## What's in the folder

| | |
|---|---|
| `health-reminder.db` | one SQLite file: the reminders, everything answered and read, the food log, the smoking log, exercise done, the grocery list and prices, your kitchen, the menu and every setting |
| `images\` | the photos of pills saved with reminders |
| `videos\<date>\` | the videos recorded as evidence, a folder a day (see [Record taking it](record.md)). Deleting a reminder leaves its videos |
| `instance.lock` | held while the program runs, so that one copy runs at a time |

To back it up, quit the program (the tray icon's **Quit (no more reminders)**) and copy the folder. To move it to
another PC, copy the folder to the same place there, or start the program with `--data` and that folder.

Your Anthropic API key, if you saved one, is in the database encrypted for your Windows account (DPAPI): it can't be
read under another account or on another PC.

**Start when I sign in to Windows** adds the value `Strongthium health-reminder` to
`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. Apart from that, what you save yourself (a PDF, say)
and an update replacing the program file, nothing is written outside the folder.

## What's sent where

Nothing is sent anywhere but here:

| | |
|---|---|
| **DailyMed and RxNorm** (`dailymed.nlm.nih.gov`, `rxnav.nlm.nih.gov`) | when you search for a medication in [Add a reminder](reminder-editor.md): what you typed, then the labels, products, photos and drug concepts that follow from it |
| **Health Canada** (`health-products.canada.ca`) | the same, for a DIN or a name; then the product's monograph, from where the database links to it |
| **Anthropic** (`api.anthropic.com`) | only with **Help from Claude** on, and only when you use it: grocery photos (made smaller first), the list of food in your kitchen, and the meal's calories. Nothing about your medication or health. It's billed to your own Anthropic account |
| **GitHub** (`api.github.com`) | when [About](about.md) is opened, or **Check again** is pressed: which program and version is asking. An update is downloaded from GitHub only when **Update** is pressed |

DailyMed, RxNorm and Health Canada need no account or key. Each request says it's from `health-reminder` and its
version. What you search for is all they're told about you.

## Removing it all

Quit the program, untick **Start when I sign in to Windows** first if it's ticked, and delete the folder.

← [Command line](command-line.md) · [Contents](README.md) · [Messages](messages.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <b>Health Reminder</b> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <a href="https://github.com/strongthium/outbound-watch">Outbound Watch</a> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
