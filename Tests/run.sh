#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
mkdir -p Tests/bin
PET_MCS="$(command -v mcs || true)"
PET_MONO="$(command -v mono || true)"
if [ -z "$PET_MCS" ]; then PET_MCS=/Library/Frameworks/Mono.framework/Commands/mcs; fi
if [ -z "$PET_MONO" ]; then PET_MONO=/Library/Frameworks/Mono.framework/Commands/mono; fi
"$PET_MCS" -warnaserror -out:Tests/bin/PetTests.exe Assets/TabletopMayhem/Core/PetSimulation.cs Assets/TabletopMayhem/Core/PetCareProgress.cs Assets/TabletopMayhem/Core/PetRoster.cs Tests/PetTests.cs
"$PET_MONO" Tests/bin/PetTests.exe
