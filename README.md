# Heat Retention Continued

A continuation of [Svarozhich's Heat Retention](https://mods.vintagestory.at/heatretention) for Vintage Story 1.22 / .NET 10.

Modpage: [mods.vintagestory.at/heatretentioncontinued](https://mods.vintagestory.at/heatretentioncontinued)

## What this mod does

Apply oakum to a chiseled block to seal its seams. Insulated chiseled blocks count as proper wall for Vintage Story's interior-detection check, so rooms enclosed by chiseled windows or decorative walls finally register as inside and hold their warmth through cold biomes and harsh winters.

Oakum is crafted from flax fibers. The more fibers fed into a single stack, the higher the durability of the resulting oakum, with a full stack of 64 producing a full-durability oakum. Partial oakums can be repaired by adding more fibers, or combined two-at-a-time to consolidate inventory.

## Install

Drop the zip into `Vintagestory/Mods/` (or your installation's equivalent path). Requires Vintage Story 1.22.0 or newer. Both client and server need the mod installed for multiplayer.

The release zip is attached to each tagged GitHub release: [latest release](https://github.com/Tirello-Nardell/HeatRetention/releases/latest).

## Migrating from the original Heat Retention

Uninstall the original `heatretention` mod before installing this continuation so saved inventories do not end up with duplicate oakum items. A bundled remap file renames legacy oakum items on first load, and the block-entity behavior recognizes the legacy attribute key so previously insulated chiseled blocks stay insulated after the swap.

## Building from source

```
git clone https://github.com/Tirello-Nardell/HeatRetention.git
cd HeatRetention
$env:VINTAGE_STORY = "C:/path/to/your/Vintagestory/install"
dotnet build -c Release
```

The build output lands in `bin/Release/Mods/mod/`. Zip the contents of that folder (preserving the `assets/` directory structure) and drop the zip into your mods folder.

## License

MIT, inherited from the upstream project. See [LICENSE](LICENSE).

## Credit

All textures, item shapes, recipes, lang strings, and the original block-behavior design are Svarozhich's work. This fork updates the build, packaging, and 1.22 API compatibility only. The upstream repository is at [DemiGodOfFire/HeatRetention](https://github.com/DemiGodOfFire/HeatRetention).
