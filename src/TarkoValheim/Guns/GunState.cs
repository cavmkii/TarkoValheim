using System.Collections.Generic;
using System.Globalization;
using TarkoValheim.Content;
using TarkoValheim.Stash;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Per-gun state (rounds in the magazine, which round, fire mode, jam),
    /// stored in the gun item's m_customData so it saves with the item and
    /// travels with it into chests and graves.
    /// </summary>
    internal static class GunState
    {
        private const string MagKey = "tv_mag";
        private const string AmmoKey = "tv_ammo";
        private const string ModeKey = "tv_mode";
        private const string JamKey = "tv_jam";

        public static int Mag(ItemDrop.ItemData gun)
        {
            return gun.m_customData.TryGetValue(MagKey, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }

        public static void SetMag(ItemDrop.ItemData gun, int rounds)
        {
            Loc.Own(gun);
            gun.m_customData[MagKey] = Mathf.Max(0, rounds).ToString(CultureInfo.InvariantCulture);
        }

        public static string LoadedAmmo(ItemDrop.ItemData gun)
        {
            return gun.m_customData.TryGetValue(AmmoKey, out var id) ? id : null;
        }

        public static void SetLoadedAmmo(ItemDrop.ItemData gun, string ammoId)
        {
            Loc.Own(gun);
            gun.m_customData[AmmoKey] = ammoId;
        }

        public static string LoadedAmmoName(ItemDrop.ItemData gun)
        {
            var id = LoadedAmmo(gun);
            return Mag(gun) > 0 && id != null ? "$" + id.ToLowerInvariant() : "";
        }

        public static bool Jammed(ItemDrop.ItemData gun)
        {
            return gun.m_customData.ContainsKey(JamKey);
        }

        public static void SetJammed(ItemDrop.ItemData gun, bool jammed)
        {
            Loc.Own(gun);
            if (jammed)
            {
                gun.m_customData[JamKey] = "1";
            }
            else
            {
                gun.m_customData.Remove(JamKey);
            }
        }

        public static FireMode Mode(ItemDrop.ItemData gun, GunSpec spec)
        {
            int i = gun.m_customData.TryGetValue(ModeKey, out var raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            return spec.Modes[Mathf.Clamp(i, 0, spec.Modes.Length - 1)];
        }

        public static FireMode CycleMode(ItemDrop.ItemData gun, GunSpec spec)
        {
            int i = 0;
            for (int k = 0; k < spec.Modes.Length; k++)
            {
                if (spec.Modes[k] == Mode(gun, spec))
                {
                    i = k;
                }
            }
            i = (i + 1) % spec.Modes.Length;
            Loc.Own(gun);
            gun.m_customData[ModeKey] = i.ToString(CultureInfo.InvariantCulture);
            return spec.Modes[i];
        }

        public static string ModeNames(GunSpec spec)
        {
            var names = new List<string>();
            foreach (var mode in spec.Modes)
            {
                names.Add(mode.ToString().ToUpperInvariant());
            }
            return string.Join("/", names.ToArray());
        }

        // ------------------------------------------------------------------
        // Ammo in the player's inventory

        /// <summary>
        /// Rounds of a caliber the player can reload from. With the Tetris
        /// inventory on, that's pockets, rig and secure container only (as in
        /// Tarkov), plus the backpack if the config allows it.
        /// </summary>
        public static List<ItemDrop.ItemData> Sources(Player player, string caliber)
        {
            var result = new List<ItemDrop.ItemData>();
            foreach (var item in player.GetInventory().GetAllItems())
            {
                var ammo = Firearms.FindAmmo(Gear.PrefabName(item));
                if (ammo == null || ammo.Caliber != caliber || !Reachable(item))
                {
                    continue;
                }
                result.Add(item);
            }
            return result;
        }

        private static bool Reachable(ItemDrop.ItemData item)
        {
            if (!Reconciler.Active)
            {
                return true;
            }
            if (!Loc.TryRead(item, out var loc))
            {
                return false;
            }
            return loc.Grid == Loadout.Pockets || loc.Grid == Loadout.RigGrid || loc.Grid == Loadout.SecureGrid
                || (loc.Grid == Loadout.PackGrid && Plugin.ReloadFromBackpack.Value);
        }

        public static int Count(Player player, string caliber, string ammoId)
        {
            int n = 0;
            foreach (var item in Sources(player, caliber))
            {
                if (Gear.PrefabName(item) == ammoId)
                {
                    n += item.m_stack;
                }
            }
            return n;
        }

        /// <summary>
        /// Which round to load: the loaded type if any are left, otherwise
        /// whichever type there is most of. With <paramref name="next"/>, the
        /// next type after the loaded one (to switch ammo).
        /// </summary>
        public static string PickAmmo(Player player, GunSpec gun, ItemDrop.ItemData item, bool next)
        {
            var types = new List<string>();
            foreach (var ammo in Firearms.Ammo)
            {
                if (ammo.Caliber == gun.Caliber && Count(player, gun.Caliber, ammo.Id) > 0)
                {
                    types.Add(ammo.Id);
                }
            }
            if (types.Count == 0)
            {
                return null;
            }

            var loaded = LoadedAmmo(item);
            int at = types.IndexOf(loaded);
            if (next)
            {
                return at < 0 ? types[0] : types.Count > 1 ? types[(at + 1) % types.Count] : null;
            }
            if (at >= 0)
            {
                return loaded;
            }

            string best = types[0];
            foreach (var id in types)
            {
                if (Count(player, gun.Caliber, id) > Count(player, gun.Caliber, best))
                {
                    best = id;
                }
            }
            return best;
        }

        public static int TakeRounds(Player player, string caliber, string ammoId, int wanted)
        {
            var inventory = player.GetInventory();
            int taken = 0;
            foreach (var stack in Sources(player, caliber))
            {
                if (taken >= wanted)
                {
                    break;
                }
                if (Gear.PrefabName(stack) != ammoId)
                {
                    continue;
                }

                int n = Mathf.Min(wanted - taken, stack.m_stack);
                stack.m_stack -= n;
                taken += n;
                if (stack.m_stack <= 0)
                {
                    inventory.RemoveItem(stack);
                }
            }

            if (taken > 0)
            {
                inventory.Changed();
            }
            return taken;
        }

        /// <summary>Puts rounds back: onto existing stacks first, then as new
        /// stacks wherever there's room, then on the ground.</summary>
        public static void ReturnRounds(Player player, string ammoId, int count)
        {
            if (count <= 0 || ammoId == null)
            {
                return;
            }

            var inventory = player.GetInventory();
            foreach (var item in inventory.GetAllItems())
            {
                if (count <= 0)
                {
                    break;
                }
                if (Gear.PrefabName(item) == ammoId && item.m_stack < item.m_shared.m_maxStackSize)
                {
                    int n = Mathf.Min(count, item.m_shared.m_maxStackSize - item.m_stack);
                    item.m_stack += n;
                    count -= n;
                }
            }

            while (count > 0)
            {
                var stack = Reconciler.CreateItem(ammoId, 1);
                if (stack == null)
                {
                    break;
                }

                int n = Mathf.Min(count, stack.m_shared.m_maxStackSize);
                stack.m_stack = n;
                count -= n;

                if (Reconciler.CanFit(player, stack, n))
                {
                    inventory.AddItem(stack);
                }
                else
                {
                    var pos = player.transform.position + player.transform.forward * 0.6f + Vector3.up;
                    ItemDrop.DropItem(stack, n, pos, Quaternion.identity);
                }
            }

            inventory.Changed();
        }

        public static void Unload(Player player, ItemDrop.ItemData gun)
        {
            if (player == null)
            {
                return;
            }

            int mag = Mag(gun);
            SetMag(gun, 0);
            ReturnRounds(player, LoadedAmmo(gun), mag);
        }
    }
}
