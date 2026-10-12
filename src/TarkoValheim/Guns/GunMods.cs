using TarkoValheim.Content;
using TarkoValheim.Stash;
using TarkoValheim.Util;
using UnityEngine;

namespace TarkoValheim.Guns
{
    /// <summary>
    /// Installing and removing weapon parts. An installed part stops being
    /// an inventory item: its prefab name is stored on the gun
    /// (m_customData "tv_mod_Barrel" etc.), so it travels with the gun into
    /// chests and graves. Removing it gives the item back.
    /// </summary>
    internal static class GunMods
    {
        private const string Prefix = "tv_mod_";

        public static PartSpec Installed(ItemDrop.ItemData gun, PartSlot slot)
        {
            return gun?.m_customData != null && gun.m_customData.TryGetValue(Prefix + slot, out var id)
                ? GunParts.Find(id)
                : null;
        }

        /// <summary>Puts a part from <paramref name="from"/> onto the gun,
        /// swapping out whatever was in that slot.</summary>
        public static bool Install(Player player, ItemDrop.ItemData gunItem, Inventory gunInventory,
            ItemDrop.ItemData partItem, Inventory from, out string error)
        {
            error = null;
            var gun = Firearms.FindGun(Gear.PrefabName(gunItem));
            var part = GunParts.Find(Gear.PrefabName(partItem));
            if (gun == null || part == null)
            {
                return false;
            }
            if (!part.FitsGun(gun))
            {
                error = "$tv_err_part_doesnt_fit";
                return false;
            }

            var previous = Installed(gunItem, part.Slot);

            from.RemoveItem(partItem, 1);
            Loc.Own(gunItem);
            gunItem.m_customData[Prefix + part.Slot] = part.Id;

            if (previous != null)
            {
                GiveBack(player, Reconciler.CreateItem(previous.Id, 1));
            }

            GameAccess.Changed(from);
            if (gunInventory != from)
            {
                GameAccess.Changed(gunInventory);
            }
            return true;
        }

        public static void Remove(Player player, ItemDrop.ItemData gunItem, Inventory gunInventory, PartSlot slot)
        {
            if (slot == PartSlot.Magazine)
            {
                EjectMag(player, gunItem);
                GameAccess.Changed(gunInventory);
                return;
            }

            var part = Installed(gunItem, slot);
            if (part == null)
            {
                return;
            }

            Loc.Own(gunItem);
            gunItem.m_customData.Remove(Prefix + slot);
            GiveBack(player, Reconciler.CreateItem(part.Id, 1));
            GameAccess.Changed(gunInventory);
        }

        // ------------------------------------------------------------------
        // Magazines

        private const string InitKey = "tv_init";

        public static MagSpec InstalledMag(ItemDrop.ItemData gun)
        {
            return gun?.m_customData != null && gun.m_customData.TryGetValue(Prefix + PartSlot.Magazine, out var id)
                ? Magazines.Find(id)
                : null;
        }

        /// <summary>
        /// A gun seen for the first time gets its standard magazine, empty.
        /// Rounds an older version loaded straight into the gun stay in it.
        /// </summary>
        public static bool EnsureDefaultMag(ItemDrop.ItemData gunItem)
        {
            var gun = Firearms.FindGun(Gear.PrefabName(gunItem));
            if (gun == null || gunItem.m_customData == null || gunItem.m_customData.ContainsKey(InitKey))
            {
                return false;
            }

            Loc.Own(gunItem);
            gunItem.m_customData[InitKey] = "1";
            var mag = Magazines.DefaultFor(gun);
            if (mag != null && InstalledMag(gunItem) == null)
            {
                gunItem.m_customData[Prefix + PartSlot.Magazine] = mag;
            }
            return true;
        }

        /// <summary>Puts a magazine item into the gun. The magazine that was in
        /// it comes back out as an item, keeping whatever rounds it had.</summary>
        public static bool InsertMag(Player player, ItemDrop.ItemData gunItem, Inventory gunInventory,
            ItemDrop.ItemData magItem, Inventory from, out string error)
        {
            error = null;
            var gun = Firearms.FindGun(Gear.PrefabName(gunItem));
            var mag = Magazines.Find(Gear.PrefabName(magItem));
            if (gun == null || mag == null)
            {
                return false;
            }
            if (!mag.FitsGun(gun))
            {
                error = "$tv_err_mag_doesnt_fit";
                return false;
            }

            int rounds = GunState.Mag(magItem);
            string ammo = GunState.LoadedAmmo(magItem);

            // Take the new one out first: that frees its space for the old one.
            from.RemoveItem(magItem);
            EjectMag(player, gunItem);

            Loc.Own(gunItem);
            gunItem.m_customData[Prefix + PartSlot.Magazine] = mag.Id;
            GunState.SetMag(gunItem, rounds);
            if (ammo != null)
            {
                GunState.SetLoadedAmmo(gunItem, ammo);
            }

            GameAccess.Changed(from);
            if (gunInventory != from)
            {
                GameAccess.Changed(gunInventory);
            }
            return true;
        }

        /// <summary>Takes the magazine out of the gun and gives it back as an
        /// item with its rounds. Without a magazine, loose rounds go back.</summary>
        public static void EjectMag(Player player, ItemDrop.ItemData gunItem)
        {
            var current = InstalledMag(gunItem);
            int rounds = GunState.Mag(gunItem);
            string ammo = GunState.LoadedAmmo(gunItem);
            GunState.SetMag(gunItem, 0);

            if (current == null)
            {
                if (rounds > 0)
                {
                    GunState.ReturnRounds(player, ammo, rounds);
                }
                return;
            }

            Loc.Own(gunItem);
            gunItem.m_customData.Remove(Prefix + PartSlot.Magazine);

            var magItem = Reconciler.CreateItem(current.Id, 1);
            if (magItem != null)
            {
                GunState.SetMag(magItem, rounds);
                if (ammo != null && rounds > 0)
                {
                    GunState.SetLoadedAmmo(magItem, ammo);
                }
                GiveBack(player, magItem);
            }
        }

        private static void GiveBack(Player player, ItemDrop.ItemData item)
        {
            if (item == null || player == null)
            {
                return;
            }

            if (Reconciler.CanFit(player, item, 1))
            {
                player.GetInventory().AddItem(item);
            }
            else
            {
                var pos = player.transform.position + player.transform.forward * 0.6f + Vector3.up;
                ItemDrop.DropItem(item, 1, pos, Quaternion.identity);
            }
        }
    }
}
