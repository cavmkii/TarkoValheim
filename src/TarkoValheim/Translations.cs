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
                { "tv_hideout_desc", "Your base of operations. Guns and ammunition are made here. Inside its radius you are out of raid and die by normal rules." },

                { "tv_cal_9x18", "9x18mm PM" },
                { "tv_cal_12ga", "12 gauge" },
                { "tv_cal_762x54r", "7.62x54mmR" },
                { "tv_cal_545x39", "5.45x39mm" },

                { "tv_ammo_9x18", "9x18mm PM rounds" },
                { "tv_ammo_9x18_desc", "Pistol ammunition. Cheap, weak, everywhere." },
                { "tv_ammo_12ga", "12ga buckshot" },
                { "tv_ammo_12ga_desc", "Eight pellets per shell. Devastating up close, useless at range." },
                { "tv_ammo_762x54r", "7.62x54mmR rounds" },
                { "tv_ammo_762x54r_desc", "Full-power rifle cartridge." },
                { "tv_ammo_545x39", "5.45x39mm rounds" },
                { "tv_ammo_545x39_desc", "Intermediate rifle cartridge. One round fires a three-shot burst." },

                { "tv_pm", "PM pistol" },
                { "tv_pm_desc", "Takes 9x18mm PM. Quick to reload, low damage." },
                { "tv_mp133", "MP-133 shotgun" },
                { "tv_mp133_desc", "Takes 12ga. Pump-action; slow between shots." },
                { "tv_mosin", "Mosin rifle" },
                { "tv_mosin_desc", "Takes 7.62x54mmR. Bolt-action. Hits very hard, very accurately, very slowly." },
                { "tv_ak74", "AK-74" },
                { "tv_ak74_desc", "Takes 5.45x39mm. Fires three-round bursts." },

                { "tv_msg_raid_start", "Raid started. Only your secure container is safe." },
                { "tv_msg_extracted", "Extracted." },
                { "tv_msg_kia", "Killed in action." },
                { "tv_msg_raid_time", "Raid time" },
            });
        }
    }
}
