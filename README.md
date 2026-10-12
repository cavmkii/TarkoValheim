# TarkoValheim

Tarkov's core loop on Valheim's open world. Build a hideout wherever you like.
Everything outside its radius is raid. If you die in raid you lose your kit,
except what's in your secure container. Guns have calibers and the ammo isn't
interchangeable.

A BepInEx plugin built on [Jötunn](https://github.com/Valheim-Modding/Jotunn).

## What's in 0.2.0

### Tarkov inventory

Valheim's inventory screen is replaced by a Tarkov-style one:

- **Multi-cell items.** A rifle is 5x1, an AK 4x2, a helmet 2x2, body armor
  3x3, materials 1x1. Press **R while dragging** to rotate.
- **Equipment slots:** Head, Body, Legs, Cape, Belt, Trinket (worn armor);
  Rig, Backpack, Secure container; Primary, Secondary, Holster, Melee
  (bound to keys **1-4**).
- **Pockets** (four 1x1), plus the **rig** and **backpack** grids. Their size
  depends on what you wear. With no rig and no backpack you have 4 pocket cells.
- **Secure container**, drawn in gold. Its contents survive death in raid.
  Every character starts with an Alpha (2x2); craft Beta, Gamma or Kappa at the
  Hideout and drag one onto the slot, while the old one is empty, to upgrade.
- **Quick slots 5-8.** Drag any carried item onto one to bind it to that key.
- **Stash.** A 10-wide chest built at the Hideout. Every chest, stash and
  tombstone opens in the same Tetris view.
- **Pickups need room.** If your grids have no space, you can't pick it up.

Every character also gets a one-time starter kit: Alpha secure container, Scav
vest and Sling bag.

Controls: **drag** to move, **R** while dragging to rotate, **Shift+drag** to
split a stack, **Ctrl+click** to send an item to or from the open chest,
**double-click** to use or equip, **right-click** for a menu (use, unload,
quick slot, drop), and drag off the panels to drop on the ground. Drag the
header to move the panel.

Existing characters: items that don't fit the new grids show up under
**UNSORTED** at the bottom. They are still carried and still count for
crafting. Move them into your stash or drop them.

### Guns

Firing no longer uses Valheim's crossbow attack:

- **Bullets** travel at real speed with gravity drop. Hits go through
  Valheim's normal damage path.
- **Magazines.** Rounds are tracked per gun and saved with it. In the
  inventory, **drag rounds onto a gun** to load it, as in Tarkov. **R** reloads,
  using only ammo in your pockets, rig or secure container (Tarkov rules,
  configurable). The MP-133 and Mosin load one round at a time, and firing
  interrupts the reload. Pressing R with a full magazine switches ammo type.
- **Fire modes:** **B** toggles them (the AK has auto and semi).
- **Recoil** kicks the view up and sideways. Higher Crossbows skill reduces it.
- **Aim down sights:** hold **right mouse**. The camera moves to first person
  and zooms, spread tightens, and a sight picture is drawn over the screen:
  iron sights (front post in a rear notch) for most guns, and a PSO-style scope
  with about 4x zoom for the SVD. The sights are flat overlays drawn in code.
  Real 3D sights need gun models from a Unity asset bundle.
- **Spread** grows while moving and during sustained fire.
- **Malfunctions** get more likely as durability falls. **R** clears them.
- **Ammo types per caliber:** 9x18 PST/PBM, 12ga buckshot/slug, 5.45 PS/BS,
  7.62x54R LPS/7N1. The ammo decides damage, the gun decides handling.
- **HUD:** an ammo readout, plus a crosshair sized to your real cone of fire.
  `AmmoDisplay = Tarkov` hides the count except for a rough estimate after
  checking the magazine (**T**).

| Gun | Caliber | Magazine | Modes | Tier |
|---|---|---|---|---|
| PM | 9x18 | 8 | semi | Bronze |
| MP-133 | 12ga | 6 (tube) | pump | Bronze |
| Mosin | 7.62x54R | 5 (stripper) | bolt | Iron |
| SVD | 7.62x54R | 10 | semi | Black metal + silver |
| AK-74 | 5.45x39 | 30 | auto/semi | Black metal |

### Raids (from 0.1.0)

Within 30 m of any Hideout you're out of raid. Leaving starts a raid;
returning extracts you. Death in raid follows `DeathRule`:

| Rule | Result |
|---|---|
| `LoseEverything` (default) | No tombstone. Everything is gone except the secure container. |
| `KeepSecure` | Normal tombstone, but the secure container stays on you. |
| `Vanilla` | Unchanged. |

`HardcoreSkillWipe = true` also resets all skills on a raid death.

## What it actually looks like

There are still no custom models or sounds. Guns are Arbalest clones (they
look like crossbows and use its firing sound), rounds look like iron bolts,
rigs and backpacks look like the armor and capes they were cloned from, and
secure containers look like the crypt key. Real models need a Unity
AssetBundle, which is the obvious next step.

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

**0.2.0 has not been run in the game.** The code compiles against Jotunn
2.30.2's real API and against hand-written stand-ins for the game.

Every game member it touches was checked against current open-source mods:
EquipmentAndQuickSlots and EpicLoot by RandyKnapp (both updated September
2026), aedenthorn's ValheimMods, and Jotunn's own source. These were not
found anywhere and are guarded:

- `Player.m_lookPitch` / `m_lookYaw` (recoil). If missing, recoil turns off
  with a log warning. The direction of the kick is my reading of how Valheim
  applies mouse input. If recoil pulls the view *down*, that reading is wrong.
- `HitData.m_hitCollider` (which piece of a rock a bullet hit). Set only if it exists.
- The `Sprites/Default` shader (tracers). Tracers turn off if it's missing.

Things that can only be judged in game: how the panel fits next to Valheim's
crafting panel at your resolution, whether the first-person camera position
looks right, and balance.

## Known conflicts

- Mods that replace `Player.OnDeath` wholesale (for example DeathTweaks with
  its prefix returning false) stop the raid death rules from running.
- Other inventory mods (EquipmentAndQuickSlots, AzuExtendedPlayerInventory,
  Auga) change the same inventory internals. Don't run them together.
- Gamepads aren't supported by the Tetris screen yet.

If the Tetris inventory misbehaves, set `TetrisInventory = false` in the
config to get Valheim's own screen back. Your items are kept either way.

## Ideas not built yet

- Gun models, sounds and muzzle flash via AssetBundle
- Health system: bleeding, fractures, bandages, splints, medkits
- Moving a full rig or backpack (contents packed into the item)
- Extraction points instead of "any hideout"
- Hideout modules: upgrades through station extensions (generator,
  medstation, stash expansion)
- Scav raiders: hostile human NPCs that carry gear
- Insurance: items returned after N in-game days if nobody looted your body

## Layout

```
src/TarkoValheim/
  Plugin.cs              entry point, config, keybinds
  Translations.cs        English strings
  Content/Firearms.cs    gun + ammo stats
  Content/Gear.cs        rigs, backpacks, secure containers
  Content/Hideout.cs     hideout and stash pieces, radius check
  Content/LootDrops.cs   enemy ammo drops
  Guns/                  firing, reload, recoil, ADS camera, bullets, HUD
  Stash/                 Tetris inventory: layout, moves, Valheim hooks
  Stash/UI/              inventory screen
  Raid/                  death rules, raid start / extract messages
stubs/                   compile-check stand-ins for the game, BepInEx, uGUI
```
