# Caravans Critically Enhanced

A RimWorld 1.6 mod that fixes the slow and fragile parts of forming a caravan, in a vanilla style.

![RimWorld Version](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)
![License](https://img.shields.io/badge/License-GPL%20v3-blue.svg)

## Status

In development and not yet released. The mod is built in slices, and this file describes only what
has landed. At the moment that is the settings window, the Load work type (colonists who are not
going load a forming caravan and lead its pen animals to it, and the caravan keeps loading while any
listed item can still be fetched), drafted caravan colonists keeping their leads, with a button
to drop a lead and a right-click option to take one, and the caravan's exit. When you send a caravan,
it leaves by a map edge inside the allowed area of every pawn restricted to one, preferring edges
away from hostiles. If no edge fits every area, the caravan packs and then waits at its spot, with
an alert naming the pawns, until an area change lets it go or you use Force depart. The features
behind the other switches arrive one slice at a time.

## What it will do

- A Load work type next to Haul, so colonists who are not going help load a forming caravan.
- Leads that survive drafting, plus a way to drop a lead or take one by hand.
- A formation window that lists cargo already on your animals instead of unloading it, and moves it
  between animals when you change which ones go.
- An exit chosen inside every restricted pawn's allowed area and away from threats, a caravan that
  waits with an alert when no such exit exists, and a button to leave from the edge your drafted
  colonists are standing at.
- Fixes for caravans that get stuck while forming, protection for cargo already loaded, and pack
  animals coming home that are unloaded at a stockpile before they are penned.

Every part will have its own switch, so you can turn off whichever one collides with another mod.

## Requirements

- RimWorld 1.6.
- The [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) mod. It is not
  bundled: the copy in this repository is a compile-time reference and is deliberately left out of
  the built mod folder.

## Settings

Options > Mod options > Caravans Critically Enhanced. Each switch has a tooltip saying what it does.
Some switches sit under another one and are greyed out while it is off.

## Building

```sh
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet build rimworld-caravans-critically-enhanced.sln -c Release
```

`./build.sh` builds Release, stages the mod and replaces the copy in the RimWorld install. It
deletes that folder first, so do not run it to check something.

Tests: `dotnet test Tests/CaravansCriticallyEnhanced.Tests.csproj`. See `Tests/README.md` for what
they reach and what only the game can check.

## Licence

GPL-3.0. See `LICENSE`.
