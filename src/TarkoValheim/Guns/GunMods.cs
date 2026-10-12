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
                GiveBack(player, previous.Id);
            }

            TrimMagazine(player, gunItem, gun);
            GameAccess.Changed(from);
            if (gunInventory != from)
            {
                GameAccess.Changed(gunInventory);
            }
            return true;
        }

        public static void Remove(Player player, ItemDrop.ItemData gunItem, Inventory gunInventory, PartSlot slot)
        {
            var gun = Firearms.FindGun(Gear.PrefabName(gunItem));
            var part = Installed(gunItem, slot);
            if (gun == null || part == null)
            {
                return;
            }

            Loc.Own(gunItem);
            gunItem.m_customData.Remove(Prefix + slot);
            GiveBack(player, part.Id);
            TrimMagazine(player, gunItem, gun);
            GameAccess.Changed(gunInventory);
        }

        // A smaller magazine than before: the extra rounds go back to the player.
        private static void TrimMagazine(Player player, ItemDrop.ItemData gunItem, GunSpec gun)
        {
            int capacity = GunStats.Of(gun, gunItem).MagSize;
            int mag = GunState.Mag(gunItem);
            if (mag > capacity)
            {
                GunState.SetMag(gunItem, capacity);
                GunState.ReturnRounds(player, GunState.LoadedAmmo(gunItem), mag - capacity);
            }
        }

        private static void GiveBack(Player player, string partId)
        {
            var item = Reconciler.CreateItem(partId, 1);
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
