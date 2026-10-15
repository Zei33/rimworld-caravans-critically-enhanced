#!/bin/bash
set -e

export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api

if [ -z "${RimWorldDir}" ]; then
  echo "RimWorldDir is not set. Export it to your RimWorld install before building." >&2
  exit 1
fi

rm -Rf release
dotnet build rimworld-caravans-critically-enhanced.sln -c Release
mkdir -p release

cp -r About release/About

# Stage 1.6 by naming what ships, rather than copying it wholesale and deleting
# afterwards. The other mods in this workspace shipped their C# source and the
# vendored Harmony reference to every subscriber that way for a year. An allow-list
# is also the shape that stays correct when a folder is added: a new source folder
# is simply not staged, where a deny-list ships it until somebody notices.
mkdir -p release/1.6/Assemblies/net472

# Only our own assembly. Anything else here is a build artefact or a game assembly
# RimWorld already has loaded, and ModAssemblyHandler.ReloadAll loads every .dll at
# any depth, filtered on extension alone, so shipping one is not inert. The vendored
# 0Harmony.dll under Libraries/ is reference-only and is never staged.
cp 1.6/Assemblies/net472/CaravansCriticallyEnhanced.dll release/1.6/Assemblies/net472/

cp -r 1.6/Defs release/1.6/Defs
cp -r 1.6/Languages release/1.6/Languages

# 1.6/Patches holds the XML PatchOperations the game reads beside the C# Harmony patches it does not.
# RimWorld globs *.xml from a folder named exactly Patches, so only the XML is staged.
mkdir -p release/1.6/Patches
cp 1.6/Patches/*.xml release/1.6/Patches/

# Languages carries a developer README. Keep only what the game loads.
find release/1.6/Languages -type f ! -name '*.xml' -delete

rm -Rf "${RimWorldDir}/Mods/CaravansCriticallyEnhanced"
cp -r release "${RimWorldDir}/Mods/CaravansCriticallyEnhanced"
rm -Rf release
