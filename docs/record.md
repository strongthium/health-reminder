<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[health-reminder](../README.md) › [Manual](README.md) › Record taking it

# Record taking it

A reminder can be set to record a video when it's answered (**Record a video with the webcam when I answer it**, in
[Add a reminder](reminder-editor.md)), as evidence that the medication was taken: for a carer, a clinical trial, or
your own peace of mind. Answering it, on [the reminder](alarm.md) or on [Today](today.md), opens this window first.

## The window

| | |
|---|---|
| **Record yourself taking it** | and under it what's being taken, a line each: **Metformin 500 mg: Take 1 tablet** |
| **The picture** | what the camera sees. **Starting the camera…** until it's up; it starts recording at once |
| Under it | **Recording with** the camera's name and how long so far: **Show the pill, then take it. It stops by itself in 42 s.** |
| **Stop and save** | ends the video and saves it; the dose is then marked done |
| **Cancel** | stops, deletes what was recorded, and leaves the dose unanswered |

It stops by itself after the time set under **Stop recording after** in [Settings](settings.md) (a minute to begin
with; 30 seconds to 5 minutes), and saves the video then. When several doses are answered together, one video covers
them all. While it records, the reminder is quiet and doesn't come back. Closing the window with its **✕** while
recording deletes what was recorded.

## What's in the video

Every frame has the date and time, with the UTC offset, and what's being taken, drawn into the picture before it's
encoded, so the stamp can't be taken off without re-encoding the video. The time is the computer's clock.

The video is an H.264 `.mp4`, saved in `videos\<date>\` in the [data folder](your-data.md). Its SHA-256 is kept with
the dose when it's made, and **Watch** in the [History](history.md) checks it again before playing it.

The camera is the one chosen under **Camera** in Settings (the first one, to begin with), through Windows' own Media
Foundation: nothing else is needed.

## When there's no camera

If the camera can't be used, the picture says **No video:** and why, and **Done, without a video** marks the dose done
anyway:

| | |
|---|---|
| **No camera was found. Plug in a webcam, or check that Windows lets desktop apps use it.** | |
| **Windows didn't allow the camera. Turn on Settings > Privacy & security > Camera > Let desktop apps access your camera.** | |
| **The camera is being used by another program.** | |
| **The camera stopped.** | |
| **The camera doesn't give a picture Windows can convert.** | |

← [The reminder](alarm.md) · [Contents](README.md) · [History](history.md) →

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <b>Health Reminder</b> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <a href="https://github.com/strongthium/outbound-watch">Outbound Watch</a> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
