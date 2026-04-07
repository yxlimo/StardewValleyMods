# Smart Filtered Hopper

A smart hopper mod that filters items between input and output chests with Automate integration.

## Features

- **Item Filtering** — Transfer items from input chests to output chests based on filter items stored in the hopper
- **Automate Integration** — Works with Automate groups to grab items from multiple connected chests
- **Quality Comparison** — Optionally match items by quality in addition to item ID
- **Configurable Transfer Speed** — Adjust how often items are transferred (in frames)
- **Generic Mod Config Menu** — All options configurable through GMCM

## How It Works

1. Place a chest above a hopper — this is the **input chest**
2. Place a chest below or beside the hopper — this is the **output chest**
3. Put filter items inside the **hopper** itself (just like a normal chest)
4. Items from the input chest matching your filters will automatically transfer to the output chest

### Automate Integration

When **Connect Automate Chest Group** is enabled and you have [Automate](https://www.nexusmods.com/stardewvalley/mods/1063) installed:
- If the input chest is part of an Automate storage network, the hopper will pull items from **all chests** in that network
- Perfect for large farms with complex storage systems

## Configuration

| Setting | Default | Description |
|---|---|---|
| Enable Debug | Off | Enable detailed debug logging |
| Compare Quality | Off | Match items by quality in addition to item ID |
| Transfer Interval | 360 | How often items transfer (1 = every frame, 60 = every second) |
| Connect Automate Chest Group | Off | Pull from all chests in Automate groups |

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `SmartFilteredHopper` folder into your `Mods` directory
3. Run the game

## Compatibility

- Stardew Valley 1.6+
- SMAPI 4.0+
- Support [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)
- Works with [Automate](https://www.nexusmods.com/stardewvalley/mods/1063)

