# TarkoValheim

Tarkov's core loop on Valheim's open world. Build a hideout wherever you like.
Everything outside its radius is raid. If you die in raid you lose your kit,
except what's in your secure container. Guns have calibers and the ammo isn't
interchangeable.

A BepInEx plugin built on [Jötunn](https://github.com/Valheim-Modding/Jotunn).

## What's in 0.1.0

**Hideout.** A buildable crafting station (Hammer → Crafting; 20 wood, 10 stone,
5 flint). Every gun and round is crafted here. Within 30 m of any Hideout you
are out of raid.

**Raids.** Leaving every hideout's radius starts a raid (on-screen message).
Coming back extracts you and shows your raid time.

**Death in raid**, controlled by `DeathRule`:

| Rule | Result |
|---|---|
| `LoseEverything` (default) | No tombstone. Everything is gone except the secure container. |
| `KeepSecure` | Normal tombstone (corpse run allowed), but secure-container items stay on you. |
| `Vanilla` | Unchanged. |

If you die inside a hideout radius, normal Valheim rules apply.

**Secure container.** By default it is the 4 rightmost slots of the bottom
inventory row. Items keep their slot through death, so they stay secured for
the next raid.

**Hardcore.** `HardcoreSkillWipe = true` resets every skill to zero on a raid
death. For real character deletion, use Valheim's own Hardcore death-penalty
world modifier. This mod doesn't duplicate it.

**Guns and ammo**

| Gun | Caliber | Notes | Unlock tier |
|---|---|---|---|
| PM pistol | 9x18mm PM | fast reload, weak | Bronze |
| MP-133 | 12ga buckshot | 8 pellets, 9° spread | Bronze |
| Mosin | 7.62x54mmR | 110 pierce, near-zero spread, 3 s reload | Iron |
| AK-74 | 5.45x39mm | 3-round burst per round loaded | Black metal |

Ammo is crafted at the Hideout from metal plus coal (coal stands in for powder).
Some armed enemies also drop it: skeletons and greydwarf brutes drop 9x18,
draugr drop 12ga and 7.62, fulings drop 5.45 and 7.62.

## What it actually looks like

There are no custom models or sounds. Every gun is a re-statted clone of the
Arbalest, and every round is a re-statted clone of iron bolts. They look and
sound like crossbows. Each one is single-shot with a reload after every shot,
because that is how Valheim's crossbow attack works. Real gun models need a
Unity AssetBundle, and that is the obvious next step.

## Install with Vortex

1. In Vortex, manage Valheim. Vortex offers to install BepInEx; accept. If it
   doesn't, install "BepInExPack for Valheim" from Nexus.
2. Install "Jotunn, the Valheim Library" from Nexus.
3. Drag `TarkoValheim-<version>.zip` onto Vortex's Mods page (or use
   *Install From File*), enable it, and deploy.
4. Everyone on a server needs the mod (enforced by Jotunn's version check).
   Server admins' config values are synced to clients.

Config file: `BepInEx/config/cavmkii.tarkovalheim.cfg`.

## Build (developers)

Requires the .NET 8 SDK, plus Valheim with BepInEx installed (through Vortex
is fine: it deploys BepInEx into the game folder).

```sh
cp Environment.props.example Environment.props   # set VALHEIM_INSTALL
dotnet build src/TarkoValheim/TarkoValheim.csproj
```

Each build writes `dist/TarkoValheim-<version>.zip`, ready for Vortex. Jotunn's
build task generates publicized game assemblies on the first build. Set
`DeployToGame` to true to also copy the DLL straight into `BepInEx/plugins`,
bypassing Vortex.

Without Valheim installed you can still type-check the code:

```sh
dotnet build src/TarkoValheim/TarkoValheim.csproj -p:UseStubs=true
```

This compiles against hand-written stand-ins for the game and BepInEx (see
`stubs/README.md`) plus the real Jotunn, HarmonyX and Unity assemblies from
NuGet. CI runs this on every push.

## How much of this is verified

This has **not been run in the game yet.** Here is what was checked and how:

- Compiles cleanly against Jotunn 2.30.2's real public API.
- Every Valheim member the code touches was checked against an outside
  source: either Jotunn 2.30.2's compiled references to `assembly_valheim`, or
  the source of ValheimPlus and aedenthorn's ValheimMods. The exceptions are
  `ItemData.SharedData.m_ammoType`, `HitData.DamageTypes.m_pierce`, `CraftingStation.m_name`,
  `Player.CreateTombStone` and the `Attack` reload, pellet-count and burst
  fields. Those are long-standing game members, but I didn't find them in a
  source I could read.
- Every prefab and material name exists in Jotunn's generated prefab lists.
- The less certain members go through reflection or a guarded Harmony patch.
  If one is missing, that one feature logs an error and switches off; the rest
  of the mod keeps working. In particular, if the burst fields don't exist, the
  AK fires single shots.

## Known conflicts

Mods that replace `Player.OnDeath` wholesale (for example DeathTweaks with its
prefix returning false) stop the raid death rules from running.

## Ideas not built yet

- Gun models and sounds via AssetBundle
- Magazines and full-auto (needs a custom attack, not the crossbow one)
- Extraction points instead of "any hideout"
- Hideout modules: upgrades through station extensions (generator,
  medstation, stash expansion)
- Scav raiders: hostile human NPCs that carry gear
- Insurance: items returned after N in-game days if nobody looted your body
- Medical items (bleeds, fractures, splints)

## Layout

```
src/TarkoValheim/
  Plugin.cs            entry point, config
  Translations.cs      English strings
  Content/Firearms.cs  gun + ammo definitions (stats table at the top)
  Content/Hideout.cs   hideout piece, radius check
  Content/LootDrops.cs enemy ammo drops
  Raid/DeathRules.cs   death patches, secure container
  Raid/RaidTracker.cs  raid start / extract messages
stubs/                 compile-check stand-ins for the game and BepInEx
```
