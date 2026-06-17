using Exiled.API.Features;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NS_site27_api.Modules.PlayerManagement
{
    public enum AddPointReason
    {
        Kill,
        KillScp,
        KillScp0492,
        KillScp106,
        KillScp939,
        PocketDimensionKill,
        Escape,
        UseScpItem,
        Scp127Upgrade,
        Scp049Revive,
        Scp079StopHid,
        Scp079BlockHuman,
        Scp079ProtectTeammate,
        Scp079KillAssist,
        GeneratorActivation,
    }

    public static class AddPointReasonExtensions
    {
        public static string GetDisplayText(this AddPointReason reason)
        {
            return reason switch
            {
                AddPointReason.Kill => "击杀",
                AddPointReason.KillScp => "击杀SCP",
                AddPointReason.KillScp0492 => "SCP-049-2击杀",
                AddPointReason.KillScp106 => "SCP-106击杀",
                AddPointReason.KillScp939 => "SCP-939击杀",
                AddPointReason.PocketDimensionKill => "口袋维度击杀",
                AddPointReason.Escape => "逃离",
                AddPointReason.UseScpItem => "使用物品",
                AddPointReason.Scp127Upgrade => "SCP-127升级",
                AddPointReason.Scp049Revive => "复活他人",
                AddPointReason.Scp079StopHid => "SCP-079阻止HID",
                AddPointReason.Scp079BlockHuman => "SCP-079阻止人类",
                AddPointReason.Scp079ProtectTeammate => "SCP-079保护队友",
                AddPointReason.Scp079KillAssist => "SCP-079击杀助攻",
                AddPointReason.GeneratorActivation => "发电机激活",
                _ => "积分变动"
            };
        }
    }

    public static class PlayerDataManager
    {
        public static MySQLConnect SQL => Plugin.Instance?.connect;
        public static double GlobalMultiplier = 1;
        public static Dictionary<Player, int> PointCache = new Dictionary<Player, int>();
        public static Dictionary<Player, Stopwatch> TodayTimers = new Dictionary<Player, Stopwatch>();
        public static Dictionary<Player, Stopwatch> ServerTimers = new Dictionary<Player, Stopwatch>();
        public static Dictionary<Player, TimeSpan> TodayTimeCache = new Dictionary<Player, TimeSpan>();
        public static TimeSpan GetTodayTime(Player player)
        {
            if (player == null) return TimeSpan.Zero;
            if (TodayTimers.TryGetValue(player, out var sw))
            {
                var cached = TodayTimeCache.TryGetValue(player, out var ts) ? ts : TimeSpan.Zero;
                return sw.Elapsed + cached;
            }
            var t = Stopwatch.StartNew();
            TodayTimers[player] = t;
            var existing = SQL?.QueryUser(player.UserId).today_duration;
            if (existing.HasValue) TodayTimeCache[player] = existing.Value;
            return t.Elapsed + (existing ?? TimeSpan.Zero);
        }
        public static TimeSpan GetAllTime(Player player)
        {
            if (player == null) return TimeSpan.Zero;
            var existing = SQL?.QueryUser(player.UserId).total_duration;
            return (existing ?? TimeSpan.Zero) + GetServerTime(player);
        }
        public static TimeSpan GetServerTime(Player player)
        {
            if (player == null) return TimeSpan.Zero;
            if (ServerTimers.TryGetValue(player, out var sw))
            {
                return sw.Elapsed;
            }
            var t = Stopwatch.StartNew();
            ServerTimers[player] = t;
            return t.Elapsed;
        }
        public static void StopServerTime(Player player)
        {
            if (player == null) return ;
            if (ServerTimers.TryGetValue(player, out var sw))
            {
                sw.Stop();
            }
        }
        public static int GetPoint(Player player)
        {
            if (player == null) return 0;
            if (PointCache.TryGetValue(player, out var p)) return p;
            var result = SQL?.QueryUser(player.UserId).point ?? 0;
            PointCache[player] = result;
            return result;
        }

        public static void AddPoint(Player player, int points, AddPointReason reason)
        {
            if (player == null) return;
            var atkStats = PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Points + points;
            if (cur < 0) cur = 0;
            PointCache[player] = cur;
            atkStats.Points = cur;
            SQL?.Update(player.UserId, point: cur);
            PlayerHUDManager.AddScoreChange(player, points, reason.GetDisplayText());
        }
        public static void AddDeath(Player player, int count = 1)
        {
            var cr = SQL?.QueryPlayerStats(player.UserId);
            if (cr.HasValue)
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalDeaths: cr.Value.TotalDeaths + count);
            }
            else
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalDeaths: count);
            }
        }
        public static void AddKills(Player player, int count = 1)
        {
            var cr = SQL?.QueryPlayerStats(player.UserId);
            if (cr.HasValue)
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalKills: cr.Value.TotalKills + count);
            }
            else
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalKills: count);
            }
        }
        public static void AddEscape(Player player, int count = 1)
        {
            var cr = SQL?.QueryPlayerStats(player.UserId);
            if (cr.HasValue)
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalEscapes: cr.Value.TotalEscapes + count);
            }
            else
            {
                SQL?.UpdatePlayerStat(player.UserId, TotalEscapes: count);
            }
        }
    }
}
