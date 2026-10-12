using System.Collections.Generic;
using Jotunn.Managers;

namespace TarkoValheim
{
    internal static class Translations
    {
        public static void Register()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                { "tv_hideout", "Hideout" },
                { "tv_hideout_desc", "Your base of operations. Guns, ammunition and gear are made here. Inside its radius you are out of raid and die by normal rules." },
                { "tv_stash", "Stash" },
                { "tv_stash_desc", "A big lockbox for everything you don't take into raid." },

                { "tv_cal_9x18", "9x18mm PM" },
                { "tv_cal_12ga", "12 gauge" },
                { "tv_cal_762x54r", "7.62x54mmR" },
                { "tv_cal_545x39", "5.45x39mm" },

                { "tv_ammo_9x18", "9x18mm PST" },
                { "tv_ammo_9x18_desc", "Standard pistol round." },
                { "tv_ammo_9x18_pbm", "9x18mm PBM" },
                { "tv_ammo_9x18_pbm_desc", "Steel-core pistol round. Hits harder." },
                { "tv_ammo_12ga", "12ga buckshot" },
                { "tv_ammo_12ga_desc", "Eight pellets per shell. Devastating up close, useless at range." },
                { "tv_ammo_12ga_slug", "12ga slug" },
                { "tv_ammo_12ga_slug_desc", "One heavy lead slug. Accurate to medium range." },
                { "tv_ammo_545x39", "5.45x39mm PS" },
                { "tv_ammo_545x39_desc", "Standard intermediate rifle round." },
                { "tv_ammo_545x39_bs", "5.45x39mm BS" },
                { "tv_ammo_545x39_bs_desc", "Armor-piercing rifle round." },
                { "tv_ammo_762x54r", "7.62x54mmR LPS" },
                { "tv_ammo_762x54r_desc", "Full-power rifle round." },
                { "tv_ammo_762x54r_7n1", "7.62x54mmR 7N1" },
                { "tv_ammo_762x54r_7n1_desc", "Sniper round. The hardest hitting ammunition you can make." },

                { "tv_pm", "PM pistol" },
                { "tv_pm_desc", "8-round 9x18mm sidearm. Semi-automatic." },
                { "tv_mp133", "MP-133 shotgun" },
                { "tv_mp133_desc", "Pump-action 12 gauge. 6-shell tube, loaded one shell at a time." },
                { "tv_mosin", "Mosin rifle" },
                { "tv_mosin_desc", "Bolt-action 7.62x54mmR. 5 rounds, loaded one at a time. Slow and deadly." },
                { "tv_svd", "SVD" },
                { "tv_svd_desc", "Semi-automatic 7.62x54mmR marksman rifle. 10-round magazine." },
                { "tv_ak74", "AK-74" },
                { "tv_ak74_desc", "5.45x39mm assault rifle. 30-round magazine, full-auto and semi." },

                { "tv_rig_scav", "Scav vest" },
                { "tv_rig_scav_desc", "Tactical rig with 4x2 space. Ammo in your rig can be reloaded from." },
                { "tv_rig_carrier", "Plate carrier rig" },
                { "tv_rig_carrier_desc", "Tactical rig with 5x3 space." },
                { "tv_pack_sling", "Sling bag" },
                { "tv_pack_sling_desc", "Small backpack, 3x3." },
                { "tv_pack_scav", "Scav backpack" },
                { "tv_pack_scav_desc", "Backpack, 4x5." },
                { "tv_pack_pilgrim", "Pilgrim backpack" },
                { "tv_pack_pilgrim_desc", "Large tourist backpack, 5x7." },
                { "tv_secure_alpha", "Secure container Alpha" },
                { "tv_secure_alpha_desc", "2x2. Whatever is inside survives death in raid." },
                { "tv_secure_beta", "Secure container Beta" },
                { "tv_secure_beta_desc", "3x2. Whatever is inside survives death in raid." },
                { "tv_secure_gamma", "Secure container Gamma" },
                { "tv_secure_gamma_desc", "3x3. Whatever is inside survives death in raid." },
                { "tv_secure_kappa", "Secure container Kappa" },
                { "tv_secure_kappa_desc", "3x4. Whatever is inside survives death in raid." },

                { "tv_ui_inventory", "INVENTORY" },
                { "tv_ui_weight", "Weight" },
                { "tv_ui_gear", "EQUIPMENT" },
                { "tv_ui_quick", "QUICK SLOTS (keys 5-8)" },
                { "tv_ui_pockets", "POCKETS" },
                { "tv_ui_rig", "RIG" },
                { "tv_ui_backpack", "BACKPACK" },
                { "tv_ui_secure", "SECURE CONTAINER - kept when you die in raid" },
                { "tv_ui_secure_note", "SECURE CONTAINER\nIts contents survive death in raid." },
                { "tv_ui_no_rig_hint", "No rig or backpack: craft them at the Hideout to carry more." },
                { "tv_ui_unsorted", "UNSORTED - no room for these. Move them somewhere or drop them." },
                { "tv_ui_container_overflow", "Doesn't fit - take these out:" },
                { "tv_ui_take_all", "Take all" },
                { "tv_ui_size", "Size" },
                { "tv_ui_mag", "Magazine" },
                { "tv_ui_capacity", "Capacity" },

                { "tv_slot_head", "Head" },
                { "tv_slot_body", "Body" },
                { "tv_slot_legs", "Legs" },
                { "tv_slot_cape", "Cape" },
                { "tv_slot_belt", "Belt" },
                { "tv_slot_trinket", "Trinket" },
                { "tv_slot_rig", "Rig" },
                { "tv_slot_backpack", "Backpack" },
                { "tv_slot_secure", "Secure" },
                { "tv_slot_primary", "Primary (1)" },
                { "tv_slot_secondary", "Secondary (2)" },
                { "tv_slot_holster", "Holster (3)" },
                { "tv_slot_melee", "Melee (4)" },

                { "tv_menu_use", "Use / Equip" },
                { "tv_menu_take", "Take" },
                { "tv_menu_unload", "Unload ammo" },
                { "tv_menu_to_container", "Move to container" },
                { "tv_menu_bind", "Quick slot" },
                { "tv_menu_unbind", "Clear quick slot" },
                { "tv_menu_drop", "Drop" },

                { "tv_err_not_empty", "Empty it first." },
                { "tv_err_secure_fixed", "The secure container can't be removed. Drag a bigger one onto it while it's empty to upgrade." },
                { "tv_err_full", "No room." },
                { "tv_err_no_room", "No room in your rig, pockets or backpack." },

                { "tv_key_reload", "Reload" },
                { "tv_key_firemode", "Fire mode" },
                { "tv_key_magcheck", "Check magazine" },

                { "tv_msg_empty", "*click* Empty" },
                { "tv_msg_jammed", "Malfunction! Press R to clear" },
                { "tv_msg_clearing", "Clearing malfunction..." },
                { "tv_msg_broken", "Weapon is broken. Repair it at the Hideout." },
                { "tv_msg_no_ammo", "No ammo in pockets, rig or secure container" },
                { "tv_msg_reloading", "Reloading" },

                { "tv_mag_empty", "Empty" },
                { "tv_mag_full", "Full" },
                { "tv_mag_almost_full", "Almost full" },
                { "tv_mag_half", "About half" },
                { "tv_mag_less_half", "Less than half" },
                { "tv_mag_almost_empty", "Almost empty" },

                { "tv_msg_raid_start", "Raid started. Only your secure container is safe." },
                { "tv_msg_extracted", "Extracted." },
                { "tv_msg_kia", "Killed in action." },
                { "tv_msg_raid_time", "Raid time" },
            });
        }
    }
}
