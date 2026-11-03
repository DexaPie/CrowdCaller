# Crowd Caller — GTA V Script Mod

Press **8** and up to **55 pedestrians** gather around you. Perfect for stunt videos, ragdoll / brick-throw mods, chaos clips and screenshots — no more chasing NPCs across the map.

- Pulls real ambient pedestrians from up to 300 m away and places them around you
- Creates extra random pedestrians if the area is empty
- Never touches mission characters, peds from other mods, people in cars or the player
- People spawn on safe ground (not inside buildings or on roofs) and keep wandering near you
- Everything is configurable in `CrowdCaller.ini` (key, count, radius, distance, hold time)
- Single-player only. Lightweight: one `.cs` file, no extra DLLs

## Requirements

- GTA V (PC, Story Mode)
- [Script Hook V](http://www.dev-c.com/gtav/scripthookv/)
- [ScriptHookVDotNet 3](https://github.com/scripthookvdotnet/scripthookvdotnet/releases)

## Install

1. Install Script Hook V and ScriptHookVDotNet 3.
2. Copy `scripts/CrowdCaller.3.cs` and `scripts/CrowdCaller.ini` into your `Grand Theft Auto V/scripts` folder (create it if it does not exist).
3. Start the game in Story Mode and press **8** (or NumPad 8).

To reload after editing the ini, press `Insert` in game (ScriptHookVDotNet reload).

## Settings (`CrowdCaller.ini`)

| Setting | Default | What it does |
|---|---|---|
| `Key` / `AltKey` | `D8` / `NumPad8` | Keys that call the crowd ([key names](https://learn.microsoft.com/dotnet/api/system.windows.forms.keys)) |
| `Count` | `55` | How many people to bring (1–120) |
| `GatherRadius` | `300` | How far to look for people (metres) |
| `MinDistance` / `MaxDistance` | `6` / `22` | Ring around you where they stand |
| `SpawnExtra` | `true` | Create random people if not enough are around |
| `HoldSeconds` | `90` | Seconds before the game may despawn them again |
| `ShowMessage` | `true` | Show "N people nearby" on screen |
| `CooldownSeconds` | `2` | Minimum time between two presses |

## Troubleshooting

- **Nothing happens:** check `scripts/CrowdCaller.log` and the ScriptHookVDotNet console (F4).
- **Key clashes with another mod:** change `Key` in the ini.
- **Too many people / FPS drop:** lower `Count` or set `SpawnExtra=false`.

## Contributing

Bug reports and pull requests are welcome. Please describe your game version, ScriptHookVDotNet version and what you expected to happen.

## License

[MIT](LICENSE) — free to use, modify and include in your own mod packs.

Made by TORNEX GAMING.
