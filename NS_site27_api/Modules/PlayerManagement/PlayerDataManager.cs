using Exiled.API.Features;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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
        public static Dictionary<Player, int> PointCache = new();
        public static Dictionary<Player, Stopwatch> TodayTimers = new();
        public static Dictionary<Player, Stopwatch> ServerTimers = new();
        public static Dictionary<Player, TimeSpan> TodayTimeCache = new();

        public static TimeSpan GetTodayTime(Player player)
        {
            if (player == null)
            {
                return TimeSpan.Zero;
            }

            if (TodayTimers.TryGetValue(player, out var sw))
            {
                var cached = TodayTimeCache.TryGetValue(player, out var ts) ? ts : TimeSpan.Zero;
                return sw.Elapsed + cached;
            }
            var t = Stopwatch.StartNew();
            TodayTimers[player] = t;
            var existing = TodayTimeCache.TryGetValue(player, out var cachedTime) ? cachedTime : TimeSpan.Zero;
            return t.Elapsed + existing;
        }

        public static TimeSpan GetAllTime(Player player)
        {
            return player == null ? TimeSpan.Zero : GetServerTime(player);
        }

        public static TimeSpan GetServerTime(Player player)
        {
            if (player == null)
            {
                return TimeSpan.Zero;
            }

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
            if (player == null)
            {
                return;
            }

            if (ServerTimers.TryGetValue(player, out var sw))
            {
                sw.Stop();
            }
        }

        public static async Task<int> GetPoint(Player player)
        {
            if (player == null)
            {
                return 0;
            }

            if (PointCache.TryGetValue(player, out var p))
            {
                return p;
            }

            var sql = SQL;
            if (sql == null)
            {
                return 0;
            }

            var (_, _, _, _, point, _, _, _, today_duration) = await sql.QueryUserAsync(player.UserId);
            if (today_duration.HasValue)
            {
                TodayTimeCache[player] = today_duration.Value;
            }

            PointCache[player] = point;
            return 0;
        }

        public static async Task AddPoint(Player player, int points, AddPointReason reason)
        {
            if (player == null)
            {
                return;
            }

            var atkStats = await PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Points + points;
            if (cur < 0)
            {
                cur = 0;
            }

            PointCache[player] = cur;
            atkStats.Points = cur;
            _ = SQL?.UpdateAsync(player.UserId, point: cur);
        }

        public static async Task AddDeath(Player player, int count = 1)
        {
            if (player == null)
            {
                return;
            }

            var atkStats = await PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Deaths + count;
            if (cur < 0)
            {
                cur = 0;
            }

            PointCache[player] = cur;
            atkStats.Deaths = cur;
            var sql = SQL;
            if (sql == null)
            {
                return;
            }

            var (_, TotalDeaths, _) = await SQL?.QueryPlayerStatsAsync(player.UserId);
            _ = (SQL?.UpdatePlayerStatAsync(player.UserId, TotalDeaths: TotalDeaths + count));

        }

        public static async Task AddKills(Player player, int count = 1)
        {
            if (player == null)
            {
                return;
            }

            var atkStats = await PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Kills + count;
            if (cur < 0)
            {
                cur = 0;
            }

            PointCache[player] = cur;
            atkStats.Kills = cur;
            var sql = SQL;
            if (sql == null)
            {
                return;
            }

            var (TotalKills, _, _) = await SQL?.QueryPlayerStatsAsync(player.UserId);
            _ = (SQL?.UpdatePlayerStatAsync(player.UserId, TotalKills: TotalKills + count));

        }

        public static async Task AddEscape(Player player, int count = 1)
        {
            if (player == null)
            {
                return;
            }

            var atkStats = await PlayerManagementModule.GetOrCreateStats(player);
            int cur = atkStats.Escapes + count;
            if (cur < 0)
            {
                cur = 0;
            }

            PointCache[player] = cur;
            atkStats.Escapes = cur;
            var sql = SQL;
            if (sql == null)
            {
                return;
            }

            var (_, _, TotalEscapes) = await SQL?.QueryPlayerStatsAsync(player.UserId);
            _ = (SQL?.UpdatePlayerStatAsync(player.UserId, TotalEscapes: TotalEscapes + count));

        }
    }
}
