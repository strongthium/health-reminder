<p align="center">
  <a href="https://strongthium.com" title="Strongthium Labs"><img src="https://raw.githubusercontent.com/strongthium/.github/main/profile/banner-2200x440.png" alt="Strongthium Labs: Take back control of your internet."></a>
</p>

[health-reminder](../README.md) › [Manual](README.md) › Messages

# Messages

What `health-reminder` says when something can't be done, where it says it, and what to do.

## Looking a medication up

In [Add a reminder](reminder-editor.md), under the search box and the picture.

| | |
|---|---|
| **No Canadian product has that DIN. Check the 8 digits, or search by name.** | |
| **No label has that NDC. Check the number, or search by name.** | |
| **Nothing found. Check the spelling, or try the generic name (the ingredient).** | |
| **Couldn't reach Health Canada:** … / **Couldn't reach DailyMed:** … | no internet, or the service is down. Try again later; what the other one found is still listed |
| **Health Canada lists this product as "…": it may no longer be sold.** | it's listed faded. A pharmacy may still have it |
| **Couldn't read the label:** … | DailyMed's answer didn't come or couldn't be read. Try again |
| **This label lists no products.** | pick another label |
| **DailyMed has no photo of this exact pill. …** | the drawing is used; you can pick one of the label's pictures, or another maker's pill |
| **Couldn't look for a photo:** … / **Couldn't download its photo:** … | the drawing is used |
| **No description of this strength's pill was found in it. …** | the monograph doesn't describe it where it usually does. Pick the U.S. pill that looks like yours, or describe it under **Change how it looks** |
| **Health Canada has no product monograph for this product online.** | the same |
| **Couldn't find what it looks like:** … | the same |
| **None of their labels describes the pill.** / **Couldn't read the other labels:** … | under **The same drug and strength from other makers** |

The reasons it can't save a reminder are on [Add a reminder](reminder-editor.md#saving).

## The video

In [Record taking it](record.md), after **No video:**. **Done, without a video** marks the dose done anyway.

| | |
|---|---|
| **No camera was found. Plug in a webcam, or check that Windows lets desktop apps use it.** | |
| **Windows didn't allow the camera. Turn on Settings > Privacy & security > Camera > Let desktop apps access your camera.** | Windows' privacy settings block desktop apps from the camera |
| **The camera is being used by another program.** | close the other program (a video call, say) |
| **The camera stopped.** / **the video couldn't be saved.** | try again |

In [History](history.md), **Watch** says whether the video is unchanged, has been changed since it was recorded, or is
no longer there.

## Help from Claude

On [Groceries](groceries.md) (**From photos…**), the [Menu](menu.md) (**Ask Claude**) and a meal's
[reminder](alarm.md) (**Other ideas from my kitchen**).

| | |
|---|---|
| **Couldn't reach Anthropic: check the internet connection.** | |
| **Anthropic took too long to answer; try again.** | |
| **The Anthropic API key wasn't accepted: check it in Settings.** | the key is wrong, or was revoked. Save it again in [Settings](settings.md) |
| **Anthropic is busy, or the key's limit is reached: try again in a minute.** | |
| **Anthropic had a problem answering; try again in a minute.** | |
| **Anthropic couldn't answer:** … | with what Anthropic said |
| **Anthropic answered with something unreadable (HTTP …).** | |
| **Claude declined to answer this one.** / **Claude didn't answer; try again.** | |
| **No ideas came back; try again.** / **No food found in them.** | |
| **A photo couldn't be read:** … | the file isn't a photo Windows can open |
| **There's nothing in your kitchen yet: add groceries on the Groceries page (from photos, or as you buy them).** | ideas come from what's in [your kitchen](kitchen.md) |
| **That doesn't look like an Anthropic API key: they start with sk-ant-.** | in Settings |

## Updating

On [About](about.md).

| | |
|---|---|
| **Couldn't ask GitHub for the latest version** | with why: **GitHub took too long to answer.**, **GitHub's answer couldn't be read.**, **GitHub has had too many questions from this address for now. Try again in an hour.** |
| **The update wasn't installed** | with why, and **You can download it from the release's page instead: What's new.** |
| **This program can't be replaced from here: … needs an administrator to change it.** | the program is in a folder like `C:\Program Files`. Download it from the release's page and replace it as an administrator, or keep it in a folder of your own |
| **The download of … doesn't match the release's SHA256SUMS, so nothing was changed.** | a damaged download. Try again |
| **The release has no …** / **SHA256SUMS doesn't list …** | the release on GitHub is missing a file; nothing was changed |

## Groceries and the kitchen

| | |
|---|---|
| **That isn't a price: type what it cost, like 3.49.** | under a line to buy |
| **Kept as typed: it isn't an amount the list can compare.** | under **How much**: it's kept, but prices can't be compared per litre or kilogram |
| **To add groceries from photos, turn on Help from Claude in Settings.** | |
| **Nothing on the list, bought before or in the kitchen matches "…". …** | the search found nothing |
| A line's own message, in red | what was typed in an opened line of [your kitchen](kitchen.md) isn't an amount it can use |

## Elsewhere

| | |
|---|---|
| **Choose female or male.**, **Fill in your height.**, **Fill in your weight.**, **Fill in your age.**, **Fill in when you get up and go to bed.** | [Eat & drink](eat-drink.md), under **Work out my plan** |
| **The price isn't a number.** | [Smoking](smoking.md), saving the brand |
| **Pick at least one day.** | [Exercise](exercise.md), saving the plan |
| **That isn't a WAV file Health Reminder can play.** | [Settings](settings.md), choosing a sound file |
| **Cameras can't be listed:** … | Settings, under **Camera** |
| **The pages couldn't be made:** … / **It couldn't be printed:** … | [Print](print.md) |

← [Your data](your-data.md) · [Contents](README.md)

<!-- strongthium-links -->

---

<p align="center"><sub>
<b>Strongthium open-source tools:</b> <a href="https://github.com/strongthium/emudhra-security-patch">eMudhra Security Patch</a> · <a href="https://github.com/strongthium/folder-size">Folder Size</a> · <b>Health Reminder</b> · <a href="https://github.com/strongthium/host-dossier">Host Dossier</a> · <a href="https://github.com/strongthium/outbound-watch">Outbound Watch</a> · <a href="https://github.com/strongthium/raw-convert">RAW Convert</a> · <a href="https://github.com/strongthium/video-dispatch">Video Dispatch</a> · <a href="https://github.com/strongthium/webdav-server">WebDAV Server</a><br>
<b>More from Strongthium:</b> <a href="https://strongthium.com/linux-server-firewall">Guard</a> · <a href="https://strongthium.com/self-hosted-mail-server">Mail</a> · <a href="https://strongthium.com/self-hosted-live-chat">Chat</a> · <a href="https://strongthium.com/windows-ssh-client">Remote</a> · <a href="https://strongthium.com/twilio-sms-desktop-app">SMS</a> · <a href="https://strongthium.com/time-tracking-invoicing-software">Work</a> · <a href="https://strongthium.com/windows-registry-editor">Registry</a> · <a href="https://strongthium.com/cfml-server">DotCF</a> · <a href="https://strongthium.com/video-conferencing-dial-up-modem">Convey</a><br>
<a href="https://strongthium.com">Strongthium Labs</a> · Take back control of your internet.
</sub></p>
