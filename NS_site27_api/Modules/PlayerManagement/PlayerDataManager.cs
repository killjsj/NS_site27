using Exiled.API.Features;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NS_site27_api.Modules.PlayerManagement
{
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

        public static void AddPoint(Player player, int points)
        {
            if (player == null) return;
            var atkStats = PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Points + points;
            if (cur < 0) cur = 0;
            PointCache[player] = cur;
            atkStats.Points = cur;
            SQL?.Update(player.UserId, point: cur);
            player.AddMessage("AddPoint", $"<color=green><size=23>获得积分:{(points):F0}</size></color>", 3f,0,100);
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
