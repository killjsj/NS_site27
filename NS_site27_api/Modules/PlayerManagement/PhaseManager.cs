using Exiled.API.Features;
using NS_site27_api.Modules.MySQL;
using System;
using System.Threading.Tasks;

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

        public static async Task<GamePhase> GetPhase(Player player)
        {
            return player == null ? GamePhase.FreshStart : HoursToPhase(await GetHours(player));
        }

        public static async Task<double> GetHours(Player player)
        {
            if (player == null)
            {
                return 0;
            }

            var (_, _, _, _, _, _, _, total_duration, _) = await SQL?.QueryUserAsync(player.UserId);
            return (total_duration ?? TimeSpan.Zero).TotalHours;
        }

        public static GamePhase HoursToPhase(double hours)
        {
            if (hours < 5)
            {
                return GamePhase.FreshStart;
            }

            return hours < 10
                ? GamePhase.FirstGlimpse
                : hours < 15
                ? GamePhase.MinorAchievement
                : hours < 20
                ? GamePhase.SteadyProgress
                : hours < 25
                ? GamePhase.BattleHardened
                : hours < 30
                ? GamePhase.SeasonedRider
                : hours < 35
                ? GamePhase.HundredBattles
                : hours < 45 ? GamePhase.RegionalForce : hours < 55 ? GamePhase.RenownedFar : GamePhase.SupremeRealm;
        }

        public static string PhaseToName(GamePhase phase)
        {
            return phase switch
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
        }

        public static async Task<string> GetPhaseProgressString(Player player)
        {
            double hours = await GetHours(player);
            var phase = await GetPhase(player);
            return GetPhaseProgressString(player, phase, hours);
        }

        public static async Task<string> GetPhaseProgressString(Player player, GamePhase phase)
        {
            double hours = await GetHours(player);
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
            {
                return $"[{PhaseToName(phase)}]";
            }

            _ = phase switch
            {
                GamePhase.FreshStart => 0,
                GamePhase.FirstGlimpse => 5,
                GamePhase.MinorAchievement => 10,
                GamePhase.SteadyProgress => 15,
                GamePhase.BattleHardened => 20,
                GamePhase.SeasonedRider => 25,
                GamePhase.HundredBattles => 30,
                GamePhase.RegionalForce => 35,
                GamePhase.RenownedFar => 45,
                _ => 0
            };
            _ = phase switch
            {
                GamePhase.FreshStart => 5,
                GamePhase.FirstGlimpse => 10,
                GamePhase.MinorAchievement => 15,
                GamePhase.SteadyProgress => 20,
                GamePhase.BattleHardened => 25,
                GamePhase.SeasonedRider => 30,
                GamePhase.HundredBattles => 35,
                GamePhase.RegionalForce => 45,
                GamePhase.RenownedFar => 55,
                _ => 0
            };
            return $"[{PhaseToName(phase)}]";
        }
    }
}
