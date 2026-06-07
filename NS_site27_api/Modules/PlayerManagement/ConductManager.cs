using Exiled.API.Features;
using NS_site27_api.Modules.MySQL;
using System.Collections.Generic;

namespace NS_site27_api.Modules.PlayerManagement
{
    public static class ConductManager
    {
        public static MySQLConnect SQL => Plugin.Instance?.connect;
        public static Dictionary<string, int> ViolationCache = new Dictionary<string, int>();

        public enum ConductTier { Outstanding, Acceptable, Ordinary, Lax, Negative, Worst }

        public static ConductTier GetConduct(Player player)
        {
            if (player == null) return ConductTier.Outstanding;
            return ViolationsToTier(GetViolations(player));
        }

        public static ConductTier ViolationsToTier(int violations) => violations switch
        {
            <= 0 => ConductTier.Outstanding,
            1 => ConductTier.Acceptable,
            2 => ConductTier.Ordinary,
            3 => ConductTier.Lax,
            4 => ConductTier.Negative,
            _ => ConductTier.Worst
        };

        public static int GetViolations(Player player)
        {
            if (player == null) return 0;
            if (ViolationCache.TryGetValue(player.UserId, out var c)) return c;
            int count = SQL?.CountUserViolations(player.UserId) ?? 0;
            ViolationCache[player.UserId] = count;
            return count;
        }

        public static string ConductToName(ConductTier tier) => tier switch
        {
            ConductTier.Outstanding => "出众",
            ConductTier.Acceptable => "尚可",
            ConductTier.Ordinary => "寻常",
            ConductTier.Lax => "散漫",
            ConductTier.Negative => "消极",
            ConductTier.Worst => "恶劣",
            _ => "?"
        };

        public static string ConductToColor(ConductTier tier) => tier switch
        {
            ConductTier.Outstanding => "#00FF00",
            ConductTier.Acceptable => "#66FF66",
            ConductTier.Ordinary => "#FFFF00",
            ConductTier.Lax => "#FFAA00",
            ConductTier.Negative => "#FF6600",
            ConductTier.Worst => "#FF0000",
            _ => "#FFFFFF"
        };
    }
}
