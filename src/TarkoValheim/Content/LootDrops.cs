using Jotunn.Managers;

namespace TarkoValheim.Content
{
    /// <summary>
    /// Adds ammunition to the drop tables of armed enemies, so ammo can be
    /// looted in raid as well as crafted at home.
    /// </summary>
    internal static class LootDrops
    {
        private sealed class Entry
        {
            public string Enemy;
            public string Ammo;
            public int Min;
            public int Max;
            public float Chance;

            public Entry(string enemy, string ammo, int min, int max, float chance)
            {
                Enemy = enemy;
                Ammo = ammo;
                Min = min;
                Max = max;
                Chance = chance;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("Greydwarf_Elite", "TV_Ammo_9x18", 3, 8, 0.10f),
            new Entry("Skeleton", "TV_Ammo_9x18", 3, 8, 0.15f),
            new Entry("Draugr", "TV_Ammo_12ga", 2, 5, 0.12f),
            new Entry("Draugr_Elite", "TV_Ammo_762x54R", 2, 5, 0.15f),
            new Entry("Goblin", "TV_Ammo_545x39", 5, 15, 0.10f),
            new Entry("GoblinArcher", "TV_Ammo_545x39", 5, 15, 0.15f),
            new Entry("GoblinBrute", "TV_Ammo_762x54R", 3, 6, 0.20f),
        };

        public static void Apply()
        {
            if (!Plugin.EnemyAmmoDrops.Value)
            {
                return;
            }

            foreach (var entry in Entries)
            {
                var enemy = PrefabManager.Instance.GetPrefab(entry.Enemy);
                var ammo = PrefabManager.Instance.GetPrefab(entry.Ammo);
                if (enemy == null || ammo == null)
                {
                    Plugin.Log.LogWarning($"Skipping drop {entry.Ammo} on {entry.Enemy}: prefab missing.");
                    continue;
                }

                var drops = enemy.GetComponent<CharacterDrop>();
                if (drops == null)
                {
                    continue;
                }

                // Prefabs outlive a world, and this runs on every world load.
                if (drops.m_drops.Exists(d => d.m_prefab == ammo))
                {
                    continue;
                }

                drops.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab = ammo,
                    m_amountMin = entry.Min,
                    m_amountMax = entry.Max,
                    m_chance = entry.Chance,
                    m_onePerPlayer = false,
                    m_levelMultiplier = true
                });
            }
        }
    }
}
