using BepInEx.Configuration;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using TarkoValheim.Content;
using TarkoValheim.Health;
using TarkoValheim.Modding;
using TarkoValheim.Raid;
using TarkoValheim.Stash.UI;
using TarkoValheim.Stash;
using UnityEngine;

namespace TarkoValheim
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cavmkii.tarkovalheim";
        public const string PluginName = "TarkoValheim";
        public const string PluginVersion = "0.6.0";

        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        internal static ConfigEntry<DeathRule> RaidDeathRule;
        internal static ConfigEntry<int> SecureSlots;
        internal static ConfigEntry<bool> WipeSkillsOnRaidDeath;
        internal static ConfigEntry<float> HideoutRadius;
        internal static ConfigEntry<bool> RaidHostility;
        internal static ConfigEntry<bool> HuntingParties;
        internal static ConfigEntry<float> CalmMinutes;
        internal static ConfigEntry<float> ThreatStepMinutes;
        internal static ConfigEntry<bool> PortalsOnlyInHideouts;
        internal static ConfigEntry<bool> HealthConditions;
        internal static ConfigEntry<bool> PartRecipes;

        internal static ConfigEntry<bool> TetrisInventory;
        internal static ConfigEntry<int> CellSize;
        internal static ConfigEntry<int> ContainerHeightFactor;
        internal static ConfigEntry<string> SizeOverrides;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();

            Translations.Register();
            PrefabManager.OnVanillaPrefabsAvailable += RegisterContent;
            ItemManager.OnItemsRegistered += () => Guarded("WeaponRecipes", WeaponRecipes.Apply);

            _harmony = new Harmony(PluginGuid);
            Guarded("DeathRules", () => DeathRules.Patch(_harmony));
            Guarded("InventoryHooks", () => InventoryHooks.Patch(_harmony));
            Guarded("HideoutStash", () => HideoutStash.Patch(_harmony));
            Guarded("WeaponPatches", () => WeaponPatches.Patch(_harmony));
            Guarded("Hostility", () => Hostility.Patch(_harmony));
            Guarded("PortalRules", () => PortalRules.Patch(_harmony));
            Guarded("Conditions", () => Conditions.Patch(_harmony));
            Guarded("Meds", () => Meds.Patch(_harmony));

            gameObject.AddComponent<RaidTracker>();
            gameObject.AddComponent<Hostility>();
            gameObject.AddComponent<Conditions>();
            gameObject.AddComponent<StatusHud>();
            gameObject.AddComponent<StashUi>();
        }

        // One system failing to hook (a game update renamed something) must
        // not take the rest down with it.
        private static void Guarded(string what, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                Log.LogError($"{what} could not be set up: {e}");
            }
        }

        private void RegisterContent()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterContent;
            Hideout.Register();
            Gear.Register();
            WeaponParts.Register();
            Meds.Register();
        }

        private void BindConfig()
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            RaidDeathRule = Config.Bind("Raid", "DeathRule", DeathRule.LoseEverything,
                new ConfigDescription(
                    "What happens to your inventory when you die outside a hideout.\n" +
                    "Vanilla: normal tombstone.\n" +
                    "KeepSecure: normal tombstone, but the secure container stays with you.\n" +
                    "LoseEverything: no tombstone; everything outside the secure container is gone.",
                    null, admin));

            SecureSlots = Config.Bind("Raid", "SecureContainerSlots", 4,
                new ConfigDescription(
                    "Only used with the Tetris inventory turned off: number of slots, counted leftward " +
                    "from the bottom-right corner of the vanilla inventory, that survive death in raid.",
                    new AcceptableValueRange<int>(0, 8), admin));

            WipeSkillsOnRaidDeath = Config.Bind("Raid", "HardcoreSkillWipe", false,
                new ConfigDescription("If true, dying in raid resets every skill to zero.", null, admin));

            HideoutRadius = Config.Bind("Raid", "HideoutRadius", 30f,
                new ConfigDescription(
                    "Distance in metres from a Hideout workbench that counts as being out of raid.",
                    new AcceptableValueRange<float>(5f, 100f), admin));

            RaidHostility = Config.Bind("Raid", "Hostility", true,
                new ConfigDescription(
                    "If true, the longer you stay out of a hideout, the more dangerous it gets: monsters spawning near you " +
                    "are more likely to have stars, and (with HuntingParties) groups come looking for you.",
                    null, admin));

            HuntingParties = Config.Bind("Raid", "HuntingParties", true,
                new ConfigDescription("If true, raid threat sends groups of the local biome's monsters after you.", null, admin));

            CalmMinutes = Config.Bind("Raid", "CalmMinutes", 5f,
                new ConfigDescription("Minutes at the start of a raid with no extra threat.",
                    new AcceptableValueRange<float>(0f, 60f), admin));

            ThreatStepMinutes = Config.Bind("Raid", "ThreatStepMinutes", 5f,
                new ConfigDescription("Minutes between each rise in threat level after the calm period (4 levels).",
                    new AcceptableValueRange<float>(1f, 60f), admin));

            PortalsOnlyInHideouts = Config.Bind("Raid", "PortalsOnlyInHideouts", true,
                new ConfigDescription(
                    "If true, portals can only be built, and only work, inside a hideout's radius. " +
                    "Build a forward hideout (FOB) to portal out to it.",
                    null, admin));

            PartRecipes = Config.Bind("Weapons", "BuildFromParts", true,
                new ConfigDescription(
                    "If true, weapons and shields made with metal are crafted from parts (head, grip, guard, wrap) " +
                    "instead of raw materials, and keep those parts. Takes effect on the next world load.",
                    null, admin));

            HealthConditions = Config.Bind("Health", "BleedsAndFractures", true,
                new ConfigDescription("If true, hits can cause bleeds and fractures that need treating.", null, admin));

            TetrisInventory = Config.Bind("Inventory", "TetrisInventory", true,
                "Tarkov-style inventory (multi-cell items, rig, backpack, secure container). " +
                "Turn off to get Valheim's own inventory screen back; your items are kept either way.");

            CellSize = Config.Bind("Inventory", "CellSize", 48,
                new ConfigDescription("Size of one inventory cell in pixels at 1080p.",
                    new AcceptableValueRange<int>(32, 72)));

            ContainerHeightFactor = Config.Bind("Inventory", "ContainerHeightFactor", 2,
                new ConfigDescription(
                    "Chests show this many Tetris rows per Valheim row, since items now take several cells.",
                    new AcceptableValueRange<int>(1, 3), admin));

            SizeOverrides = Config.Bind("Inventory", "SizeOverrides", "",
                new ConfigDescription(
                    "Item sizes by prefab name, overriding the defaults. Example: SwordIron=1x4, Wood=1x1",
                    null, admin));
        }
    }

    internal enum DeathRule
    {
        Vanilla,
        KeepSecure,
        LoseEverything
    }
}
