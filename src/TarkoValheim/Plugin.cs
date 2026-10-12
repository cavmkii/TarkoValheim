using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using TarkoValheim.Content;
using TarkoValheim.Raid;

namespace TarkoValheim
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cavmkii.tarkovalheim";
        public const string PluginName = "TarkoValheim";
        public const string PluginVersion = "0.1.0";

        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        internal static ConfigEntry<DeathRule> RaidDeathRule;
        internal static ConfigEntry<int> SecureSlots;
        internal static ConfigEntry<bool> WipeSkillsOnRaidDeath;
        internal static ConfigEntry<float> HideoutRadius;
        internal static ConfigEntry<bool> EnemyAmmoDrops;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();

            Translations.Register();
            PrefabManager.OnVanillaPrefabsAvailable += RegisterContent;
            PrefabManager.OnPrefabsRegistered += LootDrops.Apply;

            _harmony = new Harmony(PluginGuid);
            DeathRules.Patch(_harmony);

            gameObject.AddComponent<RaidTracker>();
        }

        private void RegisterContent()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterContent;
            Hideout.Register();
            Firearms.Register();
        }

        private void BindConfig()
        {
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            RaidDeathRule = Config.Bind("Raid", "DeathRule", DeathRule.LoseEverything,
                new ConfigDescription(
                    "What happens to your inventory when you die outside a hideout.\n" +
                    "Vanilla: normal tombstone.\n" +
                    "KeepSecure: normal tombstone, but secure-container slots stay with you.\n" +
                    "LoseEverything: no tombstone; everything outside the secure container is gone.",
                    null, admin));

            SecureSlots = Config.Bind("Raid", "SecureContainerSlots", 4,
                new ConfigDescription(
                    "Number of slots, counted leftward from the bottom-right corner of the inventory, " +
                    "that survive death in raid. 0 disables the secure container.",
                    new AcceptableValueRange<int>(0, 8), admin));

            WipeSkillsOnRaidDeath = Config.Bind("Raid", "HardcoreSkillWipe", false,
                new ConfigDescription(
                    "If true, dying in raid resets every skill to zero.",
                    null, admin));

            HideoutRadius = Config.Bind("Raid", "HideoutRadius", 30f,
                new ConfigDescription(
                    "Distance in metres from a Hideout workbench that counts as being out of raid.",
                    new AcceptableValueRange<float>(5f, 100f), admin));

            EnemyAmmoDrops = Config.Bind("Loot", "EnemyAmmoDrops", true,
                new ConfigDescription(
                    "If true, some enemies drop ammunition. Applied when a world loads; turning it off needs a game restart to take effect.",
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
