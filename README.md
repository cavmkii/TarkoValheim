# TarkoValheim

Tarkov's core loop on Valheim's open world. Build a hideout wherever you like.
Everything outside its radius is raid. If you die in raid you lose your kit,
except what's in your secure container. Guns have calibers and the ammo isn't
interchangeable.

A BepInEx plugin built on [Jötunn](https://github.com/Valheim-Modding/Jotunn).

## What's in 0.4.0

### One Tarkov screen for everything

Tab (and E on any crafting station) opens a full-screen Tarkov-style screen
that replaces all of Valheim's inventory screen. It has **BACK** at the top
left, then **GEAR | CRAFTING | SKILLS | HEALTH** tabs. Anything you can
press has a light-orange outline. The screen is laid out on a 1920x1080
frame and scaled to fit.

- **GEAR**: Tarkov's layout.
  - Left: equipment slots (HEADWEAR, BODY ARMOR, LEGS, CAPE, BELT, ARMBAND,
    ON SLING, ON BACK, HOLSTER, SCABBARD), with health, stamina, weight and
    armor underneath.
  - Middle: TACTICAL RIG, POCKETS, BACKPACK and POUCH. Each one shows its
    item box beside its grid and a used/total cell count.
  - Right: the open chest or stash, with a category filter (ALL, WEAPONS,
    AMMO, MODS, GEAR, ARMOR, FOOD, OTHER). The filter dims everything else.
    When you modify a weapon, its panel opens here instead.
  - Bottom: QUICK USE bar 1-8, and a *drop here to discard* box.
- **CRAFTING**: replaces Valheim's crafting panel at every station, and does
  hand crafting with no station. The left list puts what you can make now
  first, then what needs a higher station level, then what you're missing
  items for. Upgrades of items you carry are in the same list. The right
  side shows the recipe: the item as it will come out, the station and level
  it needs, and every ingredient as have / need. **REPAIR ALL** repairs
  everything this station can fix.
- **SKILLS**: every skill as a card with its level and progress bar. Gun
  skills also say what the level buys (less recoil, faster reload, tighter
  aimed spread).
- **HEALTH**: health, stamina, eitr, weight, armor, the active foods with
  time left, status effects, and whether you're in raid and which death rule
  applies.

QoL from Tarkov players' wish lists:

- Caliber on ammo, magazine and gun icons.
- Rounds/capacity on magazines.
- Used/total cells on worn rigs and packs.
- Armor value on armor.
- **Middle-click to lock** an item. A locked item can't be dragged,
  transferred or discarded.
- **Drop an item on another of the same size** to swap them.
- **Rotate** in the right-click menu.
- Shortcuts listed in every tooltip and along the bottom bar.

Crafting through this screen is instant: there's no progress bar. Recipes,
station levels and costs are Valheim's own.

### Tarkov inventory (from 0.3.0)

Valheim's inventory grid is replaced by Tetris grids:

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
**double-click** to use or equip, **middle-click** to lock, **right-click**
for a menu (use, modify, unload, rotate, quick slot, lock, discard). Drop on
the *drop here to discard* box to drop an item on the ground.

Existing characters: items that don't fit the new grids show up under
**UNSORTED** at the bottom. They are still carried and still count for
crafting. Move them into your stash or drop them.

### Guns

Firing no longer uses Valheim's crossbow attack:

- **Bullets** travel at real speed with gravity drop. Hits go through
  Valheim's normal damage path.
- **Magazines.** Detachable magazines are items: drag rounds onto a magazine
  to load it, keep spares in your rig or pockets, and **R** swaps the fullest
  reachable one into your gun (the old one comes back with what was left in
  it). New guns come with an empty standard magazine. Bigger magazines (AK 45-round,
  95-round drum, SVD 20-round) are slower to swap. You can also drag rounds
  straight onto a gun to top up the magazine in it. **R** also
  clears malfunctions. Only pockets, rig and secure container count (Tarkov
  rules, configurable). The MP-133 and Mosin have no detachable magazine:
  R loads them one round at a time, and firing interrupts the reload. With
  their tube full, R switches to the other ammo type.
- **Fire modes:** **B** toggles them (the AK has auto and semi).
- **Recoil** kicks the view up and sideways. The gun's class skill reduces it.
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

| Gun | Class | Caliber | Magazine | Modes | Tier |
|---|---|---|---|---|---|
| PM | Handgun | 9x18 | 8 | semi | Bronze |
| PP-91 Kedr | SMG | 9x18 | 30 | auto/semi | Iron |
| MP-133 | Shotgun | 12ga | 6 (tube) | pump | Bronze |
| Mosin | Marksman | 7.62x54R | 5 (stripper) | bolt | Iron |
| SVD | Marksman | 7.62x54R | 10 | semi | Black metal + silver |
| AK-74 | Assault rifle | 5.45x39 | 30 | auto/semi | Black metal |

### Gun skills

Each gun class trains its own skill: Handguns, SMGs, Shotguns, Assault rifles
or Marksman rifles. Hits train it. At level 100 you get 40% less recoil, 30%
faster reloads and 30% tighter aimed spread.

### Weapon modding

Right-click a gun and choose **Modify weapon**. The modding panel shows one
slot per part the gun takes (Barrel, Handguard, Stock, Muzzle, Sight,
Magazine) and its stats against the bare gun. Drag a part onto a slot, or
straight onto the gun in any grid, to install it. Right-click a slot to take
the part off. Installed parts are stored on the gun, so they travel with it.

- **Barrels, handguards, stocks** come in five materials: bronze, iron,
  silver, black metal and flametal. Each tier up improves the same stats:
  a barrel adds damage and muzzle velocity, a handguard and stock cut recoil.
- **Muzzles:** flash hider, muzzle brake, suppressor.
- **Sights:** reflex (red dot), 4x scope, 8x scope. These change the sight
  picture and zoom.
- **Magazine:** the slot shows the inserted magazine. Drop a magazine on it
  to swap, right-click to take it out.

Pistols take a barrel, muzzle and magazine only. All parts are crafted at the
Hideout.

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

**One tester has run builds up to 0.3.1 in game. This version (0.4.0) hasn't
been run yet.** The code compiles against Jotunn 2.30.2's real API and
against hand-written stand-ins for the game.

Every game member it touches was checked against current open-source mods:
EquipmentAndQuickSlots and EpicLoot by RandyKnapp (both updated September
2026), aedenthorn's ValheimMods, and Jotunn's own source. These were not
found anywhere and are guarded:

- `Player.m_lookPitch` / `m_lookYaw` (recoil). If missing, recoil turns off
  with a log warning. The direction of the kick is my reading of how Valheim
  applies mouse input. If recoil pulls the view *down*, that reading is wrong.
- `HitData.m_hitCollider` (which piece of a rock a bullet hit). Set only if it exists.
- The `Sprites/Default` shader (tracers). Tracers turn off if it's missing.

New in 0.4.0 and only read through reflection, with fallbacks if missing:
`CraftingStation.GetLevel` (falls back to level 1), `InventoryGui.Hide`
(BACK button), `InventoryGui.CanRepair` (falls back to its own station-and-level
check), `Player.GetFoods`, `Player.GetBodyArmor`, `Skill.GetLevelPercentage`,
the status effect list, and `Recipe.m_requireOnlyOneIngredient`.

Things that can only be judged in game: whether the first-person camera
position looks right, and balance.

Not reachable from the new screen yet: Valheim's trophies, compendium and
the vanilla crafting progress animation. Set `TetrisInventory = false` to
get them back.

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
  Stash/UI/              the Tarkov screen: GEAR, CRAFTING, SKILLS, HEALTH tabs
  Crafting/              crafting, upgrading and repairing without Valheim's panel
  Raid/                  death rules, raid start / extract messages
stubs/                   compile-check stand-ins for the game, BepInEx, uGUI
```
