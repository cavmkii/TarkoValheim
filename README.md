# TarkoValheim

Tarkov's loop on top of vanilla Valheim. There are no guns. You fight with
Valheim's own weapons, but they're built from parts you craft, level and swap,
like Tarkov's attachments. Build a hideout; everything outside it is raid. If
you die in raid you lose your kit, except what's in your secure container.
The longer you stay out, the more the land turns on you.

A BepInEx plugin built on [Jotunn](https://github.com/Valheim-Modding/Jotunn).

## What's in 0.6.0

### Modular weapons

Every vanilla sword, knife, axe, club, spear, atgeir, bow, crossbow and shield
takes parts. Right-click it and choose **Modify weapon**, then drag parts onto
its slots, or drop a part straight onto the weapon in any grid.

| Family | Slots |
|---|---|
| Sword | blade, grip, guard, wrap |
| Knife | blade, grip, wrap |
| Axe / club | head, haft, wrap |
| Spear | head, shaft, wrap |
| Atgeir | head, shaft, langet, wrap |
| Bow / crossbow | limbs, grip / stock, string |
| Shield | handle, rim, straps |

| Part | Made of | What it gives |
|---|---|---|
| Heads (blade, axe head, mace head, spear head, limbs) | bronze, iron, silver, black metal, flametal | damage, durability; silver adds spirit damage, flametal adds fire |
| Grips / hafts / shafts / stocks | wood, fine wood, core wood, ancient bark, Yggdrasil | cheaper attacks (stamina), durability |
| Guards / rims | the five metals | block, parry force |
| Wraps / strings / straps | leather, deer hide, troll hide, wolf pelt, lox pelt | cheaper attacks, a little block |

- **Weapons are built from parts.** Every weapon and shield that uses metal
  is crafted from its parts, not raw materials. An Iron sword takes an iron
  blade, a grip, an iron guard and a leather wrap; the materials go into
  making those parts.
  - **Special ingredients stay.** Anything else the vanilla recipe needed,
    such as eitr, trophies or black cores, is still needed.
  - **Parts stay inside the weapon.** The best-levelled part of each kind
    you carry goes in.
  - **Weapons without metal keep their vanilla recipes.** Flint, wood, bone
    and antler weapons are made as before.
  - **It can be turned off.** Set `BuildFromParts = false` to get vanilla
    recipes back.
- **The standard build is the baseline.** A weapon built from its standard
  parts at level 1 is exactly the vanilla weapon. Levelled or different
  parts change it from there. Weapons you already had, or find as loot, get
  their standard parts fitted the first time the mod sees them.
- **Missing parts hurt.** Take a weapon's head off and it does a quarter of
  its damage. Without its handle it costs more stamina and wears faster.
- **Parts level up instead of weapons.** A part goes to level 4 in the
  CRAFTING tab (metal parts at the forge, wood and leather at the workbench).
  Weapons and shields no longer upgrade there.
- **Levels overlap the next material.** A maxed iron blade does slightly
  more damage than a level 1 silver one, but has no spirit damage.
- **Parts stay with the weapon.** They go into chests and graves with it, and
  come off at the level they went on.
- **Stats change in Valheim's own numbers.** Damage, durability, block, parry
  and attack stamina all change, so the vanilla tooltip shows the result.

### Raids

- **Raid area.** Anywhere outside a Hideout bench's radius is raid.
- **Hideout pins.** Every Hideout bench puts a pin on your map
  (`Raid/HideoutMapPins`).
- **Death in raid.** You lose everything except what's in your secure
  container (POUCH). In a hideout you die by normal rules.
- **Threat rises with time.** The first 5 minutes are calm, so a quick run
  for materials is cheap. After that the threat level rises every 5 minutes,
  up to 4:
  - Monsters spawning near you are more likely to have stars.
  - Hunting parties of the local biome's monsters come looking for you, more
    often and in bigger groups as threat rises.
- **Threat display.** Shown on the HUD (bottom left) and in the STATUS tab,
  including when it rises next. All of it is configurable.
- **Portals only work inside a hideout.** You can't build one outside, and
  an existing one outside does nothing. To portal out, build a forward
  hideout (FOB) with its own portal first.

### Hideout stash

Inside a hideout, every chest within its radius is one shared stash, shown
on the right of the GEAR tab as one scrolling list.

- **Moving items.** Drag between chests and your grids, or Ctrl+click to
  send an item to the first chest with room.
- **Crafting.** Crafting in the hideout uses materials from the stash as
  well as what you carry.
- **Building.** So does building with the hammer. What you carry is used
  first, and the stash makes up the rest. The build menu's counts aren't
  shown red when the stash covers them. Set `Crafting/BuildFromStash` to
  false to turn this off.
- **Dismantle.** Right-click > Dismantle, inside a hideout, breaks a crafted
  item into what it was made from and puts that in the stash.
  - Its upgrade costs come back too.
  - A weapon built from parts gives back its actual parts, at their levels.
  - Idols aren't returned.
  - Worn rigs, packs and pouches have to be taken off first, and locked
    items can't be dismantled.
- **What's left out.** Carts, ships, graves, private chests that aren't
  yours, and chests another player has open don't count.

### Injuries and meds

- **Bleeding.** Cutting and piercing hits can open a light or heavy bleed.
  Heavy hits are likelier to. Bleeds drain health until treated.
- **Fractures.** Heavy blunt hits and bad falls can fracture a bone: slow
  sprinting and a slower jog until splinted.
- **Meds.** Made at the Hideout:

  | Item | Treats |
  |---|---|
  | Bandage | light bleed |
  | Tourniquet | any bleed |
  | Splint | fracture |
  | Painkillers | hide fracture pain for 5 minutes |
  | Field kit | all bleeds, plus 30% health |
  | Surgical kit | everything, plus 50% health |

  Use them by double-click, a quick slot, or the menu. A med isn't used up
  if there's nothing for it to treat.
- **Where to see them.** Injuries show on the HUD and in the STATUS tab,
  with what treats them.

### The Tarkov screen

Tab (and E at any station) opens one full-screen screen with GEAR, CRAFTING,
SKILLS and STATUS tabs.

- **GEAR**: equipment slots, then rig / pockets / backpack / pouch grids,
  then the hideout stash or open chest (with a category filter), and a
  quick-use bar.
- **CRAFTING**: replaces Valheim's panel. It has CRAFT and UPGRADE sub-tabs,
  and each shows how many are ready. Crafting takes 2 seconds (upgrades 3),
  shown by a progress bar you can cancel. Walking away from the station
  cancels it too. `Crafting/CraftSeconds` sets the time; 0 is instant. What you can make now is listed
  first, and each ingredient shows have / need.
- **SKILLS**: every skill as a card with its level and progress.
- **STATUS**: vitals, injuries, food, status effects, raid threat and rules.

Item controls:

| Action | Control |
|---|---|
| Move | drag |
| Rotate while dragging | R |
| Split a stack | Shift+drag |
| Transfer | Ctrl+click |
| Use | double-click |
| Lock | middle-click |
| Menu | right-click |
| Swap two items of the same size | drop one on the other |
| Drop on the ground | the *drop here to discard* box |

### Tetris inventory (from 0.3.0)

- **Multi-cell items.** A two-handed weapon is 2x4, body armor 3x3,
  materials 1x1.
- **Equipment.** Rigs and backpacks set your grid sizes.
- **Secure containers.** Alpha 2x2 up to Kappa 3x4.
- **Starter kit.** Every character starts with an Alpha, a Scav vest and a
  Sling bag.
- **Pickups need room** in your grids.
- **Existing characters.** Items that don't fit show up under UNSORTED.

### Removed in 0.5.0

Guns, ammo, magazines and gun skills are gone. Any you were carrying
disappear when you load, because Valheim drops items whose prefab no longer
exists.

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

**One tester has run builds up to 0.4.0 in game. Versions 0.5.0 and 0.6.0
haven't been run yet.** The code compiles against Jotunn 2.30.2's real API and against
hand-written stand-ins for the game. The members it touches were checked
against EpicLoot, EquipmentAndQuickSlots and AdvancedPortals (RandyKnapp) and
aedenthorn's mods, which patch the same methods.

Every hook is optional. If a game update renames something, that one feature
logs a warning and turns off. These are the riskiest:

- **Weapon recipes:** rewritten when the world's item database loads
  (Jotunn's `OnItemsRegistered`). Recipes only appear once you've carried
  every ingredient, so craft the parts first.
- **Weapon stats:** `ItemData.GetDamage(int, float)`, `GetMaxDurability(int)`,
  `GetBaseBlockPower(int)`, `GetDeflectionForce(int)`, and
  `Attack.GetAttackStamina`.
- **Portal placement:** `Player.PlacePiece` / `TryPlacePiece`. If neither is
  found, portals can still be built anywhere, but still only work in
  hideouts.
- **Bleeds and fractures:**
  - `Character.RPC_Damage` causes them.
  - `Player.GetRunSpeedFactor` / `GetJogSpeedFactor` apply the fracture
    slowdown.
  - `Character.SetHealth` drains health from bleeding.
- **Meds:** `Player.ConsumeItem`.
- **Raid threat:**
  - `Character.SetLevel` adds the extra stars.
  - `BaseAI.SetHuntPlayer` sends hunting parties straight at you.
  - The biome comes from `WorldGenerator.GetBiome`.
- **Hideout stash:** `Container.Awake`, `CheckAccess` and `IsInUse`, plus
  taking network ownership of the stash chests. Multiplayer is the least
  tested part. Two players moving items in the same stash at once could
  conflict.

Part icons borrow vanilla weapon icons (an iron blade looks like the iron
sword) where one exists, and the material's icon otherwise. Med icons
borrow the icons of their ingredients.

Balance is first-pass and needs play. Part modifiers, bleed and fracture
chances, and threat timings are all guesses tuned on paper.

## Known conflicts

- Mods that replace `Player.OnDeath` wholesale (for example DeathTweaks with
  its prefix returning false) stop the raid death rules from running.
- Other inventory mods (EquipmentAndQuickSlots, AzuExtendedPlayerInventory,
  Auga) change the same inventory internals. Don't run them together.
- Gamepads aren't supported by the Tetris screen yet.

If the Tetris inventory misbehaves, set `TetrisInventory = false` in the
config to get Valheim's own screen back. Your items are kept either way.

## Ideas not built yet

- Extraction points instead of "any hideout"
- Traders and barter (Haldor, Hildir), then quests
- Tarkov loot tables in dungeon and ruin chests
- Hideout modules through station extensions
- Moving a full rig or backpack (contents packed into the item)
- Insurance: items returned after N in-game days if nobody looted your body

## Layout

```
src/TarkoValheim/
  Plugin.cs              entry point, config
  Translations.cs        English strings
  Content/Gear.cs        rigs, backpacks, secure containers
  Content/WeaponParts.cs the weapon part catalogue
  Content/Hideout.cs     hideout and stash pieces, radius check
  Modding/               fitting parts, and applying them to weapon stats
  Health/                bleeds, fractures, meds, status HUD
  Raid/                  death rules, raid timer, threat, portal rule
  Crafting/              crafting, upgrading, repairing (uses the stash)
  Stash/                 Tetris inventory, hideout stash
  Stash/UI/              the Tarkov screen: GEAR, CRAFTING, SKILLS, STATUS
stubs/                   compile-check stand-ins for the game, BepInEx, uGUI
```
