using Exiled.API.Features;
using NS_site27_api.Modules.MySQL;
using System;

namespace NS_site27_api.Modules.PlayerManagement
{
    public static class PhaseManager
    {
        public static MySQLConnect SQL => Plugin.Instance?.connect;

        public enum GamePhase
        {
            FreshStart, FirstGlimpse, MinorAchievement, SteadyProgress,
            BattleHardened, SeasonedRider, HundredBattles, RegionalForce,
            RenownedFar, SupremeRealm
        }

        public static GamePhase GetPhase(Player player)
        {
            if (player == null) return GamePhase.FreshStart;
            return HoursToPhase(GetHours(player));
        }

        public static double GetHours(Player player)
        {
            if (player == null) return 0;
            var user = SQL?.QueryUser(player.UserId);
            return (user?.total_duration ?? TimeSpan.Zero).TotalHours;
        }

        public static GamePhase HoursToPhase(double hours)
        {
            if (hours < 5) return GamePhase.FreshStart;
            if (hours < 10) return GamePhase.FirstGlimpse;
            if (hours < 15) return GamePhase.MinorAchievement;
            if (hours < 20) return GamePhase.SteadyProgress;
            if (hours < 25) return GamePhase.BattleHardened;
            if (hours < 30) return GamePhase.SeasonedRider;
            if (hours < 35) return GamePhase.HundredBattles;
            if (hours < 45) return GamePhase.RegionalForce;
            if (hours < 55) return GamePhase.RenownedFar;
            return GamePhase.SupremeRealm;
        }

        public static string PhaseToName(GamePhase phase) => phase switch
        {
            GamePhase.FreshStart => "初入茅庐",
            GamePhase.FirstGlimpse => "渐窥门径",
            GamePhase.MinorAchievement => "小有成就",
            GamePhase.SteadyProgress => "稳步前行",
            GamePhase.BattleHardened => "久经沙场",
            GamePhase.SeasonedRider => "驰骋多时",
            GamePhase.HundredBattles => "身经百战",
            GamePhase.RegionalForce => "纵横一方",
            GamePhase.RenownedFar => "威名远扬",
            GamePhase.SupremeRealm => "登峰造极",
            _ => "?"
        };

        public static string GetPhaseProgressString(Player player)
        {
            double hours = GetHours(player);
            var phase = GetPhase(player);
            return GetPhaseProgressString(player, phase, hours);
        }

        public static string GetPhaseProgressString(Player player, GamePhase phase)
        {
            double hours = GetHours(player);
            return GetPhaseProgressString(player, phase, hours);
        }
        public static string PhaseToColor(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.FreshStart => "#808080",
                GamePhase.FirstGlimpse => "#FFFFFF",
                GamePhase.MinorAchievement => "#00FF00",
                GamePhase.SteadyProgress => "#00FFFF",
                GamePhase.BattleHardened => "#0099FF",
                GamePhase.SeasonedRider => "#FFAA00",
                GamePhase.HundredBattles => "#FF6600",
                GamePhase.RegionalForce => "#FF00FF",
                GamePhase.RenownedFar => "#FFD700",
                GamePhase.SupremeRealm => "#FF004D",
                _ => "#FFFFFF"
            };
        }
        private static string GetPhaseProgressString(Player player, GamePhase phase, double hours)
        {
            if (phase == GamePhase.SupremeRealm)
                return $"[{PhaseToName(phase)}]";

            double stageStart = phase switch
            {
                GamePhase.FreshStart => 0, GamePhase.FirstGlimpse => 5,
                GamePhase.MinorAchievement => 10, GamePhase.SteadyProgress => 15,
                GamePhase.BattleHardened => 20, GamePhase.SeasonedRider => 25,
                GamePhase.HundredBattles => 30, GamePhase.RegionalForce => 35,
                GamePhase.RenownedFar => 45, _ => 0
            };
            double stageMax = phase switch
            {
                GamePhase.FreshStart => 5, GamePhase.FirstGlimpse => 10,
                GamePhase.MinorAchievement => 15, GamePhase.SteadyProgress => 20,
                GamePhase.BattleHardened => 25, GamePhase.SeasonedRider => 30,
                GamePhase.HundredBattles => 35, GamePhase.RegionalForce => 45,
                GamePhase.RenownedFar => 55, _ => 0
            };
            return $"[{PhaseToName(phase)} 还剩{stageMax - hours:F1}小时晋级]";
        }
    }
}
