using Exiled.API.Features;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NS_site27_api.Modules.PlayerManagement
{
    public static class ExperienceManager
    {
        public static MySQLConnect SQL => Plugin.Instance?.connect;
        public static double GlobalMultiplier = 1;
        public static Dictionary<Player, int> ExpCache = new Dictionary<Player, int>();
        public static Dictionary<Player, int> PointCache = new Dictionary<Player, int>();
        public static Dictionary<Player, Stopwatch> TodayTimers = new Dictionary<Player, Stopwatch>();
        public static Dictionary<Player, Stopwatch> ServerTimers = new Dictionary<Player, Stopwatch>();
        public static Dictionary<Player, TimeSpan> TodayTimeCache = new Dictionary<Player, TimeSpan>();

        public static int GetExp(Player player)
        {
            if (player == null || player.IsNPC) return 0;
            if (ExpCache.TryGetValue(player, out var exp)) return exp;
            var result = SQL?.QueryUser(player.UserId).experience ?? 0;
            ExpCache[player] = result;
            return result;
        }

        public static void SetExp(Player player, int exp)
        {
            if (player == null || player.IsNPC) return;
            if (exp < 0) exp = 0;
            ExpCache[player] = exp;
            SQL?.Update(player.UserId, experience: exp);
        }

        public static void AddExp(Player player, int exp, bool ignoreMul = false, string reason = "")
        {
            if (player == null || !player.IsConnected || player.IsNPC) return;
            if (GlobalMultiplier <= 0 && !ignoreMul) return;

            int current = GetExp(player);
            double mul = ignoreMul ? 1 : Math.Max(1, GlobalMultiplier);
            int total = (int)(current + exp * mul);
            SetExp(player, total);
            //player.SendConsoleMessage($"获得经验:{(exp * mul):F0} 原因:{reason}", "green");
        }

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
            atkStats.Points += points;
            SQL?.Update(player.UserId, point: cur);
            player.AddMessage("AddPoint", $"<color=green><size=23>获得积分:{(points):F0}</size></color>", 3f,0,100);
        }
    }
}
