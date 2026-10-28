# health-reminder

[![Build and release](https://github.com/strongthium/health-reminder/actions/workflows/release.yml/badge.svg?branch=deploy)](https://github.com/strongthium/health-reminder/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/strongthium/health-reminder?label=release)](https://github.com/strongthium/health-reminder/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

`health-reminder` is a Windows program that rings, and keeps on until you answer, when it's time to take a
medication, give an injection, test your blood sugar, eat or drink, and tells you how much. For a pill it shows what
the pill looks like (its colour, shape, size, score line and imprint, from the description its manufacturer filed with
the FDA or, for a Canadian product, its Health Canada product monograph) and a photo of that very pill when one
exists, so you can check you're taking the right one. It can record a time-stamped video of each dose being taken, as
evidence.

It runs on Windows 10 and 11, is one file with nothing to install, and keeps everything on your computer.

![The reminder](docs/alarm.png)

When something is due, it comes up in the middle of the screen, over everything, with a sound, until it's answered.
Here the dose of Adalat before this one was missed, so this one insists harder and says so, and there are 3 tablets
left.

## A tour

The pictures are of made-up sample data: someone on a blood pressure pill, metformin, vitamin D and insulin, testing
their blood sugar, losing weight slowly, cooking for one, exercising five days a week and cutting down on cigarettes.

### Today

![Today](docs/today.png)

The day by time, with what became of each (done, missed, still to come), and above it today's exercise and what's
happening in the kitchen: take the chicken out of the freezer, change its thawing water, start cooking. It follows
Windows' light or dark mode and accent colour.

<img src="docs/today-dark.png" alt="Today, dark" width="70%">

### Reminders and medication

![Reminders](docs/reminders.png)

Every reminder with its times and amounts. A medication shows what the pill looks like, its DIN and prescription
number, and how many are left; with a week or less to go it says to refill.

![Editing a medication](docs/editor.png)

Adding or editing one: the pill's look, the DIN and Rx numbers, the times and the amount at each. **On hand** is what
you counted, **Refill received** adds a refill, and only doses marked taken come off.

![A DIN looked up](docs/editor-din.png)

Type a DIN, an NDC or a name and **Search**: Health Canada finds Adalat XL 30 mg, and the pill is drawn from the
description in its product monograph, quoted beside it.

### History

![History](docs/history.png)

What was done, skipped and read, day by day, with the readings and amounts; the filter shows medication, checks or
meals and drinks alone.

### Eat & drink

![Eat & drink](docs/eat-drink.png)

Height, weight, age, how active you are and the goal (here losing weight, at the safe pace for a BMI of 30.1) give the
calories and the water for a day, and how long it would take to reach a healthy weight.

![The day's plan](docs/eat-drink-plan.png)

The day of meals and drinks, each meal with an everyday idea in plain amounts: 2 eggs and 2 slices of buttered toast.

### Menu and kitchen

![Menu](docs/menu.png)

**I feel like…** lists recipes for one for the next meal, closest to its calories first, and first of all those that
use up what won't keep (the bread here).

![The week's menu](docs/menu-week.png)

What to use soon, and the week's menu: a recipe, just an idea, or decide on the day.

![Shopping for the week](docs/menu-shopping.png)

The week's shopping in the packs things are sold in, with what's left over: what to freeze, and what won't keep.

![Ideas from Claude](docs/menu-ideas.png)

With **Help from Claude** on, three ideas from what's in your kitchen, with what you'd need to buy (a sample answer
here).

<img src="docs/kitchen-choose.png" alt="What do you feel like for dinner?" width="49%"> <img src="docs/kitchen-thaw.png" alt="Take the steak out of the freezer" width="49%">

Kitchen alerts: what you feel like, for a meal left to the day; and taking the steak out of the freezer the night
before, timed back from the meal.

<img src="docs/cooking.png" alt="Cooking steps" width="40%">

The steps, at the side of the screen and over other windows: what it needs, ticked off, and timers that chime.

### Groceries and your kitchen

![Groceries](docs/groceries.png)

The shopping list. Amounts go in any unit; **Got it** takes what was bought and its price, and next time shows the
last price per litre or kilogram.

![Groceries from photos](docs/groceries-photos.png)

**From photos…**: Claude lists what's in photos of the fridge, the cupboard or a receipt. Fix what's wrong, then
**That's what I have**, **I just bought these** or **Put them on the list** (a sample answer here).

![Your kitchen](docs/groceries-kitchen.png)

The cart, and your kitchen: a running stock that the meals you cook take off. What's in doubt is marked **check**.

<img src="docs/kitchen-check.png" alt="Time to check the kitchen" width="60%">

Once a week, when something's in doubt, a few minutes to count it.

### Exercise

![Exercise](docs/exercise.png)

Today's session, going out or staying in, with what you'll need; below it, the week against 150 minutes.

![The exercise plan](docs/exercise-plan.png)

The plan: the days, the time, how long, where you're starting from, and what equipment you have (none is needed).

<img src="docs/exercise-alert.png" alt="Time to exercise" width="56%"> <img src="docs/workout.png" alt="Workout steps" width="40%">

The alert at the time, and the steps with their timers.

### Smoking

![Smoking](docs/smoking.png)

Today against the goal, a day over the last week, the time since the last one and the cost; how long each takes and
when the air clears.

![Recalibrating the body](docs/smoking-steps.png)

Cutting down while the body recalibrates: one change a week, each with why it helps and how strong the evidence is.

<img src="docs/smoke-button.png" alt="The smoking button" width="80">

The button that stays on top of everything: a click logs one, and the number is today's count.

### Settings

![Settings](docs/settings.png)

A sound for each group, getting louder, and how hard a reminder insists.

![More settings](docs/settings-claude.png)

The camera for video evidence, the units for drinks and groceries, and Help from Claude.

<img src="docs/close.png" alt="Keep reminding you?" width="60%">

Closing the window asks whether to keep running in the tray.

## What it reminds you of

| | |
|---|---|
| **Medication** | pills, injections (insulin and other pens and syringes), liquids, inhalers, drops, patches and creams, each counted in its own unit: 2 tablets, ½ tablet, 14 units, 5 mL, 2 puffs |
| **Checks** | blood sugar tests, blood pressure, weight, or anything else; the reminder asks for the reading and keeps it |
| **Eating and drinking** | meals, snacks and water, planned for you from your height, weight and sex, to keep, lose or gain weight, with an everyday idea for each meal; or set by hand |

Beside the reminders: a **menu** for one person cooking for themself, with kitchen alerts timed back from each meal; a
**grocery list** that keeps what things cost and a running stock of your kitchen; **exercise** time set aside on the
days you choose; and a **smoking log**, with a button that stays on top of everything, for cutting down step by step
while the body recalibrates.

Each reminder has its own times and days, and its own amount at each time (2 tablets in the morning, 1 at night).
Everything due at the same time rings together: a blood sugar test, the insulin after it and the morning pills come up
in one window, and **Done with all** answers them at once.

For an injection it suggests the next site in your rotation (abdomen left, abdomen right, thigh...) and keeps the one
you used. For a check, the reading you type in is kept with the time, and the **History** tab lists them.

## Using it

Download a zip from [Releases](../../releases/latest), or take the `.exe` straight from [`public/`](public), which
always holds the latest build: `health-reminder-win-x64-needs-dotnet.exe`, about 3 MB, for Windows 10 and 11 on x64
PCs. It runs on the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) installed on the PC,
which Microsoft Update keeps patched. Without it, Windows offers to download it.

The program is small because it draws with what Windows and .NET already have: WPF, with Windows 11's own look (light
or dark, in your accent colour).

Run it; it needs no install.

1. Click **Add reminder** and choose what it is.
2. For a medication, type its name or, better, the number on the pharmacy label or the box: the **DIN** on a
   Canadian one (8 digits, like `02155907`) or the **NDC** on a U.S. one (like `0093-7663-56`). Either finds the exact
   product. Pick it and its strength; the name, unit and appearance fill themselves in, and for a pill it looks for a
   photo (below). The pharmacy's prescription (**Rx**) number can be kept with it, for refills.
3. Set the times, the amount at each, and the days, and **Save**.

When something is due, a window comes up in the middle of the screen, over everything else, with a sound. Answer each
item with **I took it**, **Snooze** or **Skip**, or all of them with **Done with all**.

Each kind of reminder can be told apart at a glance and by ear: it has its own colour (teal for medication, purple for
an injection, rose for a blood sugar test, blue for the other checks and for water, orange for a meal), across the top
of the reminder window, beside it in the lists and on its button, and each group (medication, checks, meals and drinks)
its own sound.

A dose that isn't answered keeps ringing until it's 12 hours old or the next dose of the same reminder is due,
whichever is first; it then counts as missed. A late reminder never asks for two doses of the same medication at once.

The **Today** tab lists the day by time, with what became of each. Closing the window asks
whether to keep `health-reminder` running in the tray or to quit, until you tell it not to ask again (**Settings**,
**Closing the window**, changes it). **Quit** in the tray icon's menu stops it too.

## Making sure it's noticed

Under **Settings**:

| | |
|---|---|
| **Sound** | one for each group: medication, checks, and meals and drinks (a chime, beeps and a bell to begin with). A chime, a bell, rising notes, beeps, an alarm clock, a siren, your own `.wav` file, or none, at the volume you set; ▷ tries it |
| **Repeating** | for each group, until answered or once (medication and checks repeat to begin with, meals and drinks sound once) |
| **Getting louder** | each time the reminder comes back, 15% louder, up to full volume |
| **Nagging** | **Gentle**: it comes up once. **Persistent**: every 30 seconds it comes back to the middle of the screen, in front of everything, and flashes in the taskbar. **Insistent**: every 10 seconds, shaking, its header flashing red, with every screen behind it darkened, and it can't be minimized. Only answering, snoozing or skipping puts it away |

Closing the reminder with its ✕ snoozes what's on it, so it comes back.

**After a missed dose** it insists more: when the dose before this one of a medication was neither taken nor skipped,
the alarm is a step more insistent than set (gentle becomes persistent, persistent becomes insistent), starts louder,
and says which dose was missed, and to take this one as usual rather than two to make up for it unless your doctor or
pharmacist has said to.

### How many are left

In a medication's editor, **On hand** is how many you have, typed in when you count them; **Refill received** adds a
refill. Only doses marked taken come off the count (not skipped or missed ones). The reminder's card shows how many are
left and for about how many days, and with a week or less to go, the card and the alarm say it's time to refill, with
the prescription number.

## Video evidence

A reminder can be set to **record a video** when it's answered, as evidence that the medication was taken (for a
carer, a clinical trial, or your own peace of mind). Answering it opens the webcam and starts recording at once, with a
preview; **Stop and save** ends it, or it stops by itself after the length set in **Settings** (a minute by default).
When several things are due together, one video covers them all.

Every frame carries the date and time, with the UTC offset, and what is being taken ("Metformin 500 mg: Take 2
tablets"), drawn into the picture before it is encoded, so the stamp can't be taken off without re-encoding. The video
is an H.264 `.mp4` (about 3 Mbit/s at 1280 × 720), saved in `videos\<date>\` in the data folder. Its SHA-256 is kept
with the dose when it is made; **Watch** in the History tab checks it again before playing, and says if the file has
changed since. The time is the computer's clock.

The camera is Windows' own (Media Foundation), so nothing else is needed. If Windows doesn't let desktop apps use the
camera (Settings > Privacy & security > Camera), it says so, and the dose can still be marked done without a video.

> [!IMPORTANT]
> `health-reminder` is not a medical device. Pill descriptions and photos come from manufacturers' labels and can be
> wrong or out of date, and your pharmacy may give you a different manufacturer's pill. If a pill doesn't match, ask
> your pharmacist before taking it. Don't rely on `health-reminder` alone for medication that is dangerous to miss.

## The photo of your pill

Labels on [DailyMed](https://dailymed.nlm.nih.gov/), the U.S. National Library of Medicine's copy of every drug label
filed with the FDA, describe each pill: colour, shape, size in millimetres, score and imprint. `health-reminder` draws
the pill from that. Many labels also give each product a photo, but a pharmacy's or repackager's label often doesn't,
so `health-reminder` looks in turn:

1. at the photo the product's own label gives it;
2. for a repackaged pill, at the label of the manufacturer's product it repackages (the label names its NDC);
3. at the other labels of the same drug and strength, found through its **RxNorm** concept (RxCUI), for a product with
   a photo and the same imprint, shape and colour: the same pill from the same maker. Up to 60 labels are read.

The photo it found, and the label it came from, are shown before you save. If none is found, the drawing is used, and
you can choose one of the label's other pictures by hand.

For a strength or a maker its own label doesn't have, the same drug and strength from every other maker on DailyMed is
listed too, each pill drawn as its label describes it, to pick the one that looks like yours.

### Canadian medications

A name or a DIN is also looked up in Health Canada's [Drug Product Database](https://www.canada.ca/en/health-canada/services/drugs-health-products/drug-products/drug-product-database.html),
which lists every drug sold in Canada under its DIN, with what's in it and its form, but not what it looks like. For
that `health-reminder` reads the product's **monograph** (the PDF the database links to), finds the sentence that
describes the pill of that strength, and draws it from that. For Adalat XL 30 mg (DIN 02155907) the monograph says
"Round, convex, dusty rose in color and are imprinted with "ADALAT 30" on one side", and it is drawn as a pink round
tablet marked ADALAT 30. The sentence is shown, with a link to the monograph.

The database has no photos. To have one, pick, from the same drug and strength made for the U.S. (found through
RxNorm), the pill that looks the same as yours, if there is one; its label's photo is then used. Products that are no
longer sold are listed too, faded, since a pharmacy may still have them.

DailyMed, RxNorm and Health Canada need no account or key; all they are asked for is what you search for and the
labels, products, monographs and drug concepts that follow from it. A medicine on neither can be described by hand
under **Change how it looks**.

## Eating and drinking

The **Eat & drink** tab works out a day of meals and drinks from your sex, height, weight, age and how active you are,
and the times you get up and go to bed. It asks for sex, not gender, because energy and water needs follow physiology.

| | How |
|---|---|
| Energy | the Mifflin-St Jeor equation for resting energy (10 × kg + 6.25 × cm − 5 × age, + 5 for men, − 161 for women), times 1.2 (mostly sitting) to 1.9 (physical job or training twice a day); + 350 kcal pregnant, + 400 breastfeeding |
| Drinks | 80% of the day's total water, the rest coming from food: the U.S. National Academies' adequate intake (3.7 L for men, 2.7 L for women, 3.0 L pregnant, 3.8 L breastfeeding), or 30 mL a kilogram if that's more; + 250 to 750 mL for exercise, at most 4.5 L |
| Times | breakfast half an hour after getting up, dinner 3 hours before bed, lunch between them, a snack in any gap of 4½ hours or more; energy split 25 / 30 / 30 and 7.5 a snack. Drinks of about a glass, evenly from getting up to an hour before bed |
| Losing or gaining | a kilogram of body weight taken as 7,700 kcal, so 0.1 kg a week is 110 kcal a day less or more; the pace follows the BMI (below); a plan to lose never goes under 1,200 kcal a day for a woman or 1,500 for a man |

**Your weight** can be kept, lost or gained, at a safe, medium or aggressive pace. The further the BMI is from the
healthy range (18.5 to 24.9), the faster it can safely change, so what each pace means depends on it:

| BMI | Lose: safe / medium / aggressive | Gain: safe / medium / aggressive |
|---|---|---|
| under 18.5 | not planned | 0.25 / 0.5 / 0.75 kg a week |
| 18.5 to 20 | not planned | 0.15 / 0.25 / 0.35 kg a week |
| 20 to 25 | 0.25 / 0.35 / 0.5 kg a week | 0.15 / 0.25 / 0.35 kg a week |
| 25 to 30 | 0.25 / 0.5 / 0.75 kg a week | not planned |
| 30 and over | 0.5 / 0.75 / 1 kg a week | not planned |

The plan says how fast that is and, from outside the healthy range, about how many weeks to its edge. Losing or
gaining isn't planned in pregnancy or while breastfeeding. Weight changes can change the doses some medication needs
(for diabetes or blood pressure, say), so the plan says to tell your doctor.

Each meal comes with an idea of plain, everyday food in household amounts near its calories: *2 eggs, 2 slices of
buttered toast and a glass of orange juice*, *a turkey sandwich, an apple and a handful of baby carrots*, *a chicken
breast, a baked potato and a cup of broccoli*. No brands and no special shopping: the calories are typical ones for
the usual size, from the USDA's FoodData Central, rounded. Ideas change from day to day, **Other meal ideas** shows
more, the meal's alarm shows the day's, and **Add them to my grocery list** puts what they need on the list.

You see the plan before anything is set up; **Use this plan** turns it into reminders, and using it again replaces
them. The formulas are for adults. They are a starting point, not dietary advice: if a doctor has told you to limit
fluids (for heart failure or kidney disease, for example) or gave you a meal plan for diabetes, follow that instead.

### Measurements

Drinks are measured in millilitres, fluid ounces or cups, as **Settings** says, everywhere: on reminders, alarms and
the plan; they're kept in millilitres, so changing it changes nothing else. Liquid medication stays in what it's
dosed in.

## Menu and cooking

The **Menu** tab is for one person cooking for themself. It has 22 plain recipes for one, from everyday groceries,
each with its ingredients in the amounts they're used in (85 g of dry pasta, not cups of spaghetti), how long it takes
to prepare and cook, its steps, and for meat and fish the safe temperature to cook to (poultry 74 °C / 165 °F, ground
meat 71 °C / 160 °F, whole cuts and fish 63 °C / 145 °F).

- **I feel like…** steak or beef, chicken, fish, pork, pasta, eggs, something light, comfort food, something spicy,
  no meat, or quick: the recipes for the next meal that fit, closest to its calories first, and first of all those
  that use up something bought that won't keep. **Have it** picks it for that meal.
- **The week's menu**: for each weekday's breakfast, lunch and dinner, a recipe, *just an idea* (the meal's
  suggestion, no cooking), or *decide on the day*, which asks what you feel like that morning (the evening before, for
  breakfast).
- **Kitchen alerts**, timed back from the meal: *take the chicken breast out of the freezer* (into the fridge the
  night before, moved to before bed if it would fall in the night; or into cold water an hour ahead, with a reminder to
  change the water at 30 minutes), *it should be thawed*, *time to start*, and *time to start cooking*. A meal picked too
  late for the fridge thaws in cold water. They're on the **Today** tab too, and ring once, with the meals sound.
- **The steps** open at the side of the screen when it's time to start (or from **Steps**): what it needs, ticked off,
  and each step, with a timer for those that take time that chimes and brings the window forward. It can stay in front
  of other windows.
- **Shopping for the next 7 days**: what the menu needs, in the packs it's sold in (a dozen eggs, a 454 g pack of
  ground beef, 4 chicken breasts), with what's left over: fresh meat to freeze in portions the day it's bought, and a
  warning when part of a pack would spoil before it's used. **Use soon** lists what was bought that's near the end of
  how long it keeps.

## Groceries

The **Groceries** tab is a shopping list. Amounts can be typed the way the package says (`2 L`, `1 gallon`,
`500g`, `1½ cups`, `2 x 355 mL`, `2 lb`, `12`, `a dozen`), and every liquid is shown in one unit and every weight in
another: metric (mL and L, g and kg) or U.S. (fl oz and gal, oz and lb), from **Settings**. As each thing goes in the
cart, **Got it** takes the amount bought and, if you like, its price; the list then shows the price per litre or
kilogram (or gallon or pound, or each), and next time the same thing is on the list, what it cost last time.
**Done shopping** clears what's been bought; its prices are kept.

### Your kitchen

What's bought (**Got it**), what's in grocery photos, and what's added with **I have it** goes into **your kitchen**:
a running stock, kept in each thing's own unit (eggs by the egg, bread by the slice, a loaf being 20; ground beef by
the gram; milk by the millilitre), with when it's best used by. The meals cooked take off what their recipes use:
**Cooked it** in the steps at the side of the screen, or marking the meal eaten when the menu had a recipe for it,
once a meal either way (Claude's ideas come off by name). The menu's shopping, **Use soon** and Claude's ideas go by
it.

Counts drift, so what's in doubt is marked **check**: an amount read from a photo, more used than was counted, or not
counted for two weeks. Each thing can be counted on the spot (**Set** what there is, **Right**, or **None left**), and
a time a week is set aside for it (Sunday at 10:00 to begin with): when something's in doubt, an alert says what and
about how long it takes, and opens the list.

### Help from Claude (optional)

With your own [Anthropic API key](https://console.anthropic.com) saved in **Settings** (kept on the computer,
encrypted for your Windows account with DPAPI), Claude (Claude Opus 5.5, its Messages API called directly over HTTPS) can:

- **read grocery photos**: on **Groceries**, **From photos…** takes photos of the fridge, freezer, cupboard,
  groceries on the counter or a receipt, and lists what's in them, with amounts, where each is kept and how many days
  it's good for, named as the catalog names them. You check the list, fix what's wrong, and then: **That's what I
  have** (a photo of where they're kept: these are the amounts now), **I just bought these** (a receipt or a haul:
  added), or **Put them on the list**.
- **suggest meals from what you have**: on **Menu**, **Ask Claude** gives three ideas for one for the chosen meal and
  what you feel like, from what's in your kitchen, using first what won't keep, each with what it uses, what you'd
  need to buy (one click puts that on the list), and timed steps that open at the side of the screen.

Only the photos (made smaller first), the list of food in your kitchen and the meal's calories are sent, to Anthropic;
nothing about medication or health. What's used is billed to your Anthropic account. It's off until a key is saved.

## Exercise

The **Exercise** tab sets time aside on the days you choose, at a time and for 15 to 60 minutes, and plans each
session from what you have: nothing at all, or any of a bike, jump rope, resistance bands, dumbbells, kettlebell,
pull-up bar, mat, stairs, treadmill, exercise bike or rowing machine.

- **Cardio, strength and stretching** in turn (cardio and strength, two days a week or fewer).
- **Going out or staying in**, asked each time or set: going out is a walk, brisk walk, walk-jog or jog with its
  distance (*walk about 2.3 km (1.4 mi)*), or every other time a ride if there's a bike; staying in is moves in a room
  in timed rounds (marching, step-ups on a stair, jumping jacks, high knees), or the treadmill, exercise bike or rower.
- **From where you are, then forward**: it starts at the level you say (just starting, somewhat active, fit) and steps up
  every three weeks: walk, brisk walk, bursts, walk-jog, jog; sit-to-stands to squats; wall push-ups, then knee
  push-ups, then full ones; more rounds. Equipment swaps in (goblet squats and rows with dumbbells, band rows,
  kettlebell swings, hangs then chin-ups on a bar).
- **Alerts**: 15 minutes before, what it is and what you'll need (shoes, water, a mat, the dumbbells); at the time,
  going out or staying in, and **Start** opens the steps at the side of the screen, each with its timer, and **Done**
  logs it. **Not today** skips it. It's on the **Today** tab too.
- **The week** against the WHO's 150 minutes, with the calories each session used (METs from the Compendium of
  Physical Activities × your weight from **Eat & drink**).

It says to start easy and stop at chest pain or faintness, and to ask a doctor first with a heart, lung or joint
condition, in pregnancy, or after a long time inactive. When there are insulin or blood sugar reminders, it adds that
exercise can lower blood sugar: test before and after, and carry fast sugar.

## Smoking

The **Smoking** tab logs cigarettes, to see the pattern and cut down.

- **A button on top of everything** (switched on there) logs one with a click, shows how many today, and can be
  dragged anywhere; right-click it to undo or hide it. The tray icon's menu logs one too.
- **Timing** (optional): click when you light one and again when it's out. The page then shows the average time a
  cigarette takes, against the two weeks before, and when the smoke will have cleared from a room after the last one
  (roughly 2 hours with a window open, 5 or more closed; on clothes and hair, until they're washed). How long a
  cigarette takes shows how hard it's smoked (quicker usually means bigger or deeper puffs, which take in more), not
  how well the lungs work; it says so, and that a spirometry test measures the lungs.
- **The first of the day**, against when you get up (from **Eat & drink**): within half an hour is a sign of strong
  dependence.
- **Your brand**, cigarettes a pack and its price (for the cost a week), and the tar, nicotine and carbon monoxide if
  the pack prints them. No public database lists every chemical by brand, so the page shows typical amounts in one
  cigarette's smoke across brands (Health Canada's intense machine-smoking method) and what each does.
- **Cutting down, step by step**, rather than all at once: a goal of so many fewer a day each week (shown on the
  button), and alongside it, the body recalibrated one change a week.

### Recalibrating the body

A craving isn't only habit or nicotine. The page explains what else the smoke does in the body: harman and norharman
in it block the enzymes (MAO-A and MAO-B) that break down dopamine and serotonin, about a third lower in smokers and
back over the weeks after stopping; nicotine hides hunger and moves blood sugar; smoke uses up vitamin C (smokers are
advised 35 mg a day more) and lowers folate, B6, B12 and E; it makes caffeine clear about twice as fast; and some food
and drink make a cigarette taste better. Each step works against one of these, in this order, a week apart, each kept
going as the next is added:

| Step | Against | Evidence |
|---|---|---|
| Eat before you get hungry: set meal times, protein in each | the hunger and blood-sugar dips nicotine hid, which feel like cravings | some |
| Halve the coffee as you cut down | caffeine building up as the liver slows back down: jitters and poor sleep that feel like withdrawal | strong |
| Swap what goes with a cigarette: milk, water, fruit instead of coffee or alcohol | the taste: smokers say these make a cigarette taste worse (Duke University survey, 2007) | some |
| Vitamin C every day, from food first | what the smoke used up | strong that it's low; not tested on cravings |
| Move when a craving comes | the craving itself: several trials found a short bout of exercise cuts it | strong |
| Leafy greens, beans and whole grains | the B vitamins smokers are low in | strong that they're low; not tested on cravings |
| Fish twice a week | one small trial (2014): 2.7 g of omega-3 a day cut cravings and cigarettes a day | early |
| Something sweet for a sudden craving | a blood-sugar dip; small trials of glucose tablets were mixed | early |

The page says plainly which steps rest on what, and warns against beta-carotene and vitamin A supplements, which
raised lung cancer in smokers in two large trials. Formaldehyde, often named, is harmful but isn't one of the addictive
compounds. Cutting down raises the levels of some medicines (clozapine, olanzapine, theophylline), so the page says to
tell your doctor or pharmacist, and gives the free quit lines for Canada and the U.S.

Nicotine replacement is there too, folded away under **Other help**, matched to how much you smoke as the packs dose
it, for anyone who wants a bridge on hard days.

## Your data

Everything is in one SQLite file, with the photos and videos beside it, in `%LocalAppData%\Strongthium\health-reminder`. Nothing is
sent anywhere except the DailyMed, RxNorm and Health Canada lookups above, and, only if you turn on **Help from Claude**, the
grocery photos and kitchen list described above. **Start when I sign in** adds `Strongthium health-reminder`
to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; it starts in the tray.

`--data <folder>` keeps everything in another folder instead: on a USB stick, say, or to try the program out without
touching your reminders. A copy started with its own folder runs beside the usual one.

## Building

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
.\build.ps1                 # tests, then the program into public\  (or double-click build.cmd)
.\build.ps1 -Bump patch     # the same as a new version: 1.0.0 -> 1.0.1
```

`build.ps1` runs the tests (`-SkipTests` skips them), publishes the program, and replaces what's in
[`public/`](public) with it, its `SHA256SUMS` and the `VERSION` it was built as.

The version is in [`VERSION`](VERSION), `major.minor.patch`, and every build reads it. `-Bump major|minor|patch` or
`-Version 2.0.0` builds a new one, and `VERSION` is changed once that build has succeeded, so the version in the
repository is always the one in `public/`.

For working on it:

```
dotnet build
dotnet test
dotnet run --project src/HealthReminder
```

| Folder | What's in it |
|---|---|
| [`src/HealthReminder.Core`](src/HealthReminder.Core) | the reminders and their schedule (SQLite), DailyMed, RxNorm and Health Canada, the photo search, reading a monograph (a small PDF text reader), the eating and drinking planner and meal ideas, the recipes, kitchen alerts and grocery catalog, Claude for grocery photos and ideas (its HTTP API, called directly), the kitchen's stock and medication on hand, exercise plans, amounts in any unit, the grocery list, the smoking log and nicotine replacement, the sounds |
| [`src/HealthReminder`](src/HealthReminder) | the windows (WPF, with its Fluent theme), the pill drawings and icons, the tray, the on-top smoking button, the sound, the nagging, the webcam recorder (Media Foundation) |
| [`tests/HealthReminder.Tests`](tests/HealthReminder.Tests) | tests of the core; they need no network |
| [`public`](public) | the latest build, made by `build.ps1` |
| [`build.ps1`](build.ps1), [`build.cmd`](build.cmd), [`VERSION`](VERSION) | the builder, and the version it builds |

`master` holds the source. A push to `deploy` (a merge into it counts) runs
[`.github/workflows/release.yml`](.github/workflows/release.yml), which:

1. runs the tests on Windows;
2. builds the x64 program, one `.exe`;
3. publishes release `v<VERSION>` with its zip and `SHA256SUMS`. A version is released once: to release
   again, raise it first (`build.ps1 -Bump patch`).

## License

MIT, see [LICENSE](LICENSE).
