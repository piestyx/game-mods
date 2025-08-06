![Qud State Extractor Banner](assets/QudStateExtractorBanner.png)

[![.NET](https://img.shields.io/badge/.NET-6.0%2C%207.0%2C%208.0%2C%209.0-512BD4)](https://learn.microsoft.com/en-us/dotnet/)
[![language](https://img.shields.io/badge/language-C%23-239120)](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/overview)
[![OS](https://img.shields.io/badge/OS-Linux-0078D4)](https://kernel.org)
[![CPU](https://img.shields.io/badge/CPU-x86__64-FF8C00)](#dependencies)
[![GitHub last commit](https://img.shields.io/github/last-commit/piestyx/game-mods)](https://github.com/piestyx/game-mods/commits/main/)
[![Getting Started](https://img.shields.io/badge/getting_started-guide-1D76DB)](#setup)
[![Free for Non-Commercial Use](https://img.shields.io/badge/free_for_non_commercial_use-brightgreen)](#license)

# Qud State Extractor

**Game Website**: [Caves of Qud](https://www.cavesofqud.com/)  
**Official Twitter/X**: [@cavesofqud](https://x.com/cavesofqud)

## Quickstart

```bash
git clone https://github.com/piestyx/game-mods.git
cd game-mods/caves-of-qud/QudStateExtractor
cp .env.template .env
# Edit .env to choose your output directory
bash build.sh
```

* Then drop the `.cs` files and `mod.json` into your Qud `Mods/` folder.

---

## Overview

**Qud State Extractor** is a lightweight Harmony-based mod for *Caves of Qud* that logs all in-game player messages and player state to disk in plaintext and JSON formats. Ideal for AI-driven narration, streaming enhancements, or gameplay data analysis. It builds on the previous `QudLogExporter` to target specific action events to trigger a richer log context.

---

## Note

Caves of Qud compiles the mod to a separate `ModAssemblies/` folder, so any runtime use of `Assembly.GetExecutingAssembly().Location` points to the wrong directory. To fix this:

* `EnvHelper.cs` assumes the `.env` file lives in the mod’s root install directory (e.g. `~/.config/unity3d/.../Mods/QudStateExtractor`)
* It loads environment variables from `.env` during game launch
* Paths like `${HOME}` are resolved to the local environment

This design avoids hardcoded user paths and hopefully keeps the mod portable across machines and distros.

---

## Features

* Logs all player-facing messages to `message_log.txt`
* Dumps state snapshots:

  * `message_log.txt`: all messages sent to the in game message log
  * `agent_state.json`: current HP expressed as %, inventory, abilities, mutations
  * `world_state.json`: zone name, visible entities (hostile/non-hostile), weather
  * `dialogue.json`: currently active dialogue and node selection options
  * `quests.json`: all active quests and their status with objective and step
  * `journal.json`: any journal entries populated during gameplay

* Fully configurable paths in `.env`
* Resets log file if it exceeds a given size
* Toggle debug output with `ENABLE_VERBOSE_LOGS`

---

## How It Works

The output for `message_log.txt` hooks into:

```csharp
XRL.Messages.MessageQueue\:AddPlayerMessage(string message, string color, bool capitalize)
```

`agent_state`, `world_state` and `journal` all trigger an update based on in-game event fires. These are controlled within `QudStateTriggers.cs`:

"ObjectAddedToPlayerInventory"
"PerformDrop"
"SyncMutationLevels"
"BeforeCooldownActivatedAbility"
"AccomplishmentAdded"
"GetPointsOfInterest"
"QuestStarted"
"LookedAt"
"AIWakeupBroadcast"

These triggers, although not always directly linked to a specific export state, provide a wide enough coverage to ensure that the AI agent will always receive the latest contextually informed prompt.  

Upon event fire each writes to:

```bash
${BASE_FILE_PATH}/message_log.txt
${BASE_FILE_PATH}/agent_state.json
${BASE_FILE_PATH}/world_state.json
${BASE_FILE_PATH}/dialogue.json
${BASE_FILE_PATH}/quests.json
${BASE_FILE_PATH}/journal.json
```

---

## Dependencies

| Dependency      | Purpose                                   | Notes                 |
| --------------- | ----------------------------------------- | --------------------- |
| `mono-complete` | Required to compile with `mono-csc`       | Linux only            |
| `Harmony`       | Patch engine for runtime method overrides | Qud already ships it  |
| `UnityEngine.*` | Game interface layer                      | Needed for build only |

---

## Setup

1. Place the mod in your Qud Mods folder:

   ```bash
   ~/.config/unity3d/Freehold\ Games/CavesOfQud/Mods/QudStateExtractor
   ```

2. Copy and edit the environment template:

   ```bash
   cp .env.template .env
   ```

3. Example `.env`:

```dotenv
# Required path for output files
BASE_FILE_PATH=${HOME}/.config/unity3d/Freehold Games/CavesOfQud/StateLogs/

# Optional maximum file size (in bytes) before logs reset
LOG_FILE_MAX_SIZE=1048576

# Enable or disable Unity debug logs
ENABLE_VERBOSE_LOGS=true
```

> Use `${HOME}` for cross-system compatibility.

---

## Compiling the Mod

You only need to compile if you want a `.dll` instead of using `.cs` source files.

Use the included build script:

```bash
bash build.sh
```

Or manually:

```bash
mono-csc -target:library -out:ModAssemblies/QudStateExtractor.dll \
  -reference:"/path/to/CavesOfQud/CoQ_Data/Managed/Assembly-CSharp.dll" \
  -reference:"/path/to/CavesOfQud/CoQ_Data/Managed/0Harmony.dll" \
  -reference:"/path/to/CavesOfQud/CoQ_Data/Managed/UnityEngine.CoreModule.dll" \
  -reference:"/usr/lib/mono/4.8-api/Facades/netstandard.dll" \
  src/*.cs
```

---

## Usage

1. Enable the mod in the Qud **Mods** menu
2. Start a game
3. Logs will appear at the path set in your `.env`

---

## Sample Output

### `message_log.txt`

```text
[21:54:40] You see a {{B|{{W|wet}} {{B|snapjaw warrior}}}} to the northwest and stop moving.
[21:54:42] {{&R|You begin bleeding!}}
[21:54:44] &yYou died.
```

### `agent_state.json`

```json
{
  "hp":
  {
    "current":31,
    "max":31,
    "penalty":0
  },
  "inventory":[
    {
      "name":"waterskin {{y|[{{K|empty}}]}}",
      ...
    }
  ]
}
```

### `world_state.json`

```json
{
  "zone":
  {
    "name":"rusty salt marsh, kitchen of Shwyshrashur, legendary chef, surface",
    "zone_id":"JoppaWorld.11.21.0.0.10",
    "position":{"x":0,"y":0,"z":10}
  },
  "entities":[
    {
      "name":"{{B|{{B|wet}} glowfish}} {{y|[{{B|swimming}}]}}",
      "hp":5,
      "max_hp":5,
      "hostile":false,
      ...
    }
  ]
}
```

### `dialogue.json`

```json
{
  "speaker":"Mehmet",
  "listener":"{{B|{{B|wet}} Orielle}}",
  "current_node":"Aye",
  "text":"{{y|Aye.}}",
  "last_choice":"AyeChoice",
  "choices":[
    {
      "id":"VillageChoice",
      "text":"{{g|Can you tell me about your village, Joppa?}}",
      "selected":true
    },
    {
      "id":"EndChoice",
      "text":"{{G|Live and drink.}} {{K|[End]}}",
      "selected":false
    }
  ]
}
```

### `quests.json`

```json
{
  "active":[
    {
      "id":"What's Eating the Watervine?",
      "name":"{{W|What's Eating the Watervine?}}",
      "steps":[
        {"name":"Travel to Red Rock",
        "text":"Journey two parasangs north of Joppa to Red Rock.",
        "optional":false,
        "finished":false,
        "failed":false
        },
        {
          "name":"Find the Vermin",
          "text":"Find the creatures that are eating Joppa's watervine.",
          ...
        }
      ]
    }
  ]
}
```

### `journal.json`

```json
{
  "sultan_notes":[
    {
      "text":"On an expedition around the Electricians' Monarchy of Duggatara, Artayudukht was captured by bandits. She languished in captivity for eight years, eventually escaping to Aazobal Steeple."
    }
  ]
}
```

---

## Limitations

* Event triggers aren't always directly related to the export. For instance, journal entries are added through an API that has a trigger event per frame. This then has an impact on game performance with every frame json logging
* `.env` must exist and be correctly formatted
* The mod doesn't auto-create folders so all output directories must exist
* Currently Linux-only (Windows support untested)

---

## License

MIT-style. Fork it, adapt it, use it.

> Created by: **piestyx**
> First released: **2025-06-24**