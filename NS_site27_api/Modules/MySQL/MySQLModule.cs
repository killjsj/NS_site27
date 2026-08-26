using Exiled.API.Features;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NS_site27_api.Modules.MySQL
{
    public class MySQLConnect
    {
        private string _connectionString;
        public bool Connected { get; private set; }

        public async Task ConnectAsync(string ip, uint port, string username, string password, string database)
        {
            _connectionString = $"Server={ip};Port={port};Database={database};Uid={username};Pwd={password};" +
                                "allowPublicKeyRetrieval=true;Connection Timeout=30;Pooling=true;";

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                }
                Connected = true;
                Log.Info("Database connected.");
            }
            catch (Exception ex)
            {
                Connected = false;
                Log.Error($"Database Connection failed: {ex}");
            }
        }

        public async Task<(int uid, string name, int experience, double? expMultiplier, int point, string ip,
            DateTime? last_time, TimeSpan? total_duration, TimeSpan? today_duration)> QueryUserAsync(string userid)
        {
            if (!Connected)
            {
                return (0, null, 0, null, 0, null, null, null, null);
            }

            const string query = @"SELECT uid, name, experience, experience_multiplier, point, ip,
                                    last_time, total_duration, today_duration
                                   FROM user WHERE userid = @userid";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(query, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    // 获取所有列序号
                    int uidOrd = reader.GetOrdinal("uid");
                    int nameOrd = reader.GetOrdinal("name");
                    int expOrd = reader.GetOrdinal("experience");
                    int mulOrd = reader.GetOrdinal("experience_multiplier");
                    int ptOrd = reader.GetOrdinal("point");
                    int ipOrd = reader.GetOrdinal("ip");
                    int lastOrd = reader.GetOrdinal("last_time");
                    int totalDurOrd = reader.GetOrdinal("total_duration");
                    int todayDurOrd = reader.GetOrdinal("today_duration");

                    int uid = reader.IsDBNull(uidOrd) ? 0 : reader.GetInt32(uidOrd);
                    string name = reader.IsDBNull(nameOrd) ? null : reader.GetString(nameOrd);
                    int exp = reader.IsDBNull(expOrd) ? 0 : reader.GetInt32(expOrd);
                    double? expMul = reader.IsDBNull(mulOrd) ? null : reader.GetDouble(mulOrd);
                    int point = reader.IsDBNull(ptOrd) ? 0 : reader.GetInt32(ptOrd);
                    string ipStr = reader.IsDBNull(ipOrd) ? null : reader.GetString(ipOrd);
                    DateTime? lastTime = reader.IsDBNull(lastOrd) ? null : reader.GetDateTime(lastOrd);
                    TimeSpan? totalDur = reader.IsDBNull(totalDurOrd) ? null : ((MySqlDataReader)reader).GetTimeSpan(totalDurOrd);
                    TimeSpan? todayDur = reader.IsDBNull(todayDurOrd) ? null : ((MySqlDataReader)reader).GetTimeSpan(todayDurOrd);

                    return (uid, name, exp, expMul, point, ipStr, lastTime, totalDur, todayDur);
                }
            }
            catch (Exception ex) { Log.Error($"QueryUser error: {ex}"); }

            return (0, null, 0, null, 0, null, null, null, null);
        }

        public async Task<(int TotalKills, int TotalDeaths, int TotalEscapes)> QueryPlayerStatsAsync(string userid)
        {
            if (!Connected)
            {
                return (0, 0, 0);
            }

            const string query = "SELECT total_kills, total_deaths, total_escapes FROM player_stats WHERE userid = @userid";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(query, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int killsOrd = reader.GetOrdinal("total_kills");
                    int deathsOrd = reader.GetOrdinal("total_deaths");
                    int escOrd = reader.GetOrdinal("total_escapes");

                    int kills = reader.IsDBNull(killsOrd) ? 0 : reader.GetInt32(killsOrd);
                    int deaths = reader.IsDBNull(deathsOrd) ? 0 : reader.GetInt32(deathsOrd);
                    int escapes = reader.IsDBNull(escOrd) ? 0 : reader.GetInt32(escOrd);

                    return (kills, deaths, escapes);
                }
            }
            catch (Exception ex) { Log.Error($"QueryPlayerStats error: {ex}"); }

            return (0, 0, 0);
        }

        public async Task UpdatePlayerStatAsync(string userid, int TotalKills = -1, int TotalDeaths = -1, int TotalEscapes = -1)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return;
            }


            var current = await QueryPlayerStatsAsync(userid);
            int kills = TotalKills == -1 ? current.TotalKills : TotalKills;
            int deaths = TotalDeaths == -1 ? current.TotalDeaths : TotalDeaths;
            int escapes = TotalEscapes == -1 ? current.TotalEscapes : TotalEscapes;

            const string sql = @"INSERT INTO player_stats (userid, total_kills, total_deaths, total_escapes)
                                 VALUES (@userid, @kills, @deaths, @escs)
                                 ON DUPLICATE KEY UPDATE
                                     total_kills = VALUES(total_kills),
                                     total_deaths = VALUES(total_deaths),
                                     total_escapes = VALUES(total_escapes);";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                _ = cmd.Parameters.AddWithValue("@kills", kills);
                _ = cmd.Parameters.AddWithValue("@deaths", deaths);
                _ = cmd.Parameters.AddWithValue("@escs", escapes);
                await conn.OpenAsync();
                _ = await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Log.Error($"UpdatePlayerStat error: {ex}"); }
        }

        public async Awaitable UpdateAsync(string userid, string name = null, int experience = -1, double? expMultiplier = null,
            string ip = null, int point = -1, DateTime? last_time = null, TimeSpan? today_duration = null, TimeSpan? total_duration = null)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return;
            }

            var p = await QueryUserAsync(userid);
            name ??= p.name;
            point = point == -1 ? p.point : point;
            experience = experience == -1 ? p.experience : experience;
            expMultiplier ??= p.expMultiplier;
            ip ??= p.ip;
            last_time ??= p.last_time;
            today_duration ??= p.today_duration;
            total_duration ??= p.total_duration;

            const string sql = @"INSERT INTO user (userid, name, experience, experience_multiplier, ip, point,
                                                    today_duration, total_duration, last_time)
                                 VALUES (@userid, @name, @experience, @experience_multiplier, @ip, @point,
                                         @today_duration, @total_duration, @last_time)
                                 ON DUPLICATE KEY UPDATE
                                     name = VALUES(name),
                                     experience = VALUES(experience),
                                     experience_multiplier = VALUES(experience_multiplier),
                                     ip = VALUES(ip),
                                     point = VALUES(point),
                                     today_duration = VALUES(today_duration),
                                     total_duration = VALUES(total_duration),
                                     last_time = VALUES(last_time);";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                _ = cmd.Parameters.AddWithValue("@name", (object)name ?? DBNull.Value);
                _ = cmd.Parameters.AddWithValue("@experience", experience);
                _ = cmd.Parameters.AddWithValue("@experience_multiplier", (object)expMultiplier ?? DBNull.Value);
                _ = cmd.Parameters.AddWithValue("@ip", (object)ip ?? DBNull.Value);
                _ = cmd.Parameters.AddWithValue("@point", point);
                _ = cmd.Parameters.AddWithValue("@today_duration", today_duration ?? TimeSpan.Zero);
                _ = cmd.Parameters.AddWithValue("@total_duration", total_duration ?? TimeSpan.Zero);
                _ = cmd.Parameters.AddWithValue("@last_time", last_time ?? DateTime.Now);
                await conn.OpenAsync();
                _ = await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Log.Error($"Update error: {ex}"); }
        }

        public async Awaitable InsertChatLogAsync(string userid, string name, string message, string channel, string port)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return;
            }


            const string sql = "INSERT INTO chat_log (userid, name, message, channel, time, port) VALUES (@userid, @name, @message, @channel, @time, @port)";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                _ = cmd.Parameters.AddWithValue("@name", name ?? "");
                _ = cmd.Parameters.AddWithValue("@message", message ?? "");
                _ = cmd.Parameters.AddWithValue("@channel", channel ?? "");
                _ = cmd.Parameters.AddWithValue("@time", DateTime.Now);
                _ = cmd.Parameters.AddWithValue("@port", port ?? "");
                await conn.OpenAsync();
                _ = await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Log.Error($"InsertChatLog: {ex}"); }
        }

        public async Task<int> CountUserViolationsAsync(string userid)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return 0;
            }

            const string sql = "SELECT COUNT(*) FROM ban WHERE userid = @userid";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Log.Error($"CountUserViolations: {ex}");
                return 0;
            }
        }

        public async Task<List<(string issuer_name, string issuer_userid, string name, string userid, string reason,
            DateTime start_time, DateTime end_time, string port)>> QueryAllBanAsync(string userid)
        {
            var bans = new List<(string, string, string, string, string, DateTime, DateTime, string)>();
            if (!Connected)
            {
                return bans;
            }


            const string query = @"SELECT issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port
                                   FROM ban WHERE userid = @userid";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(query, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                int issuerNameOrd = reader.GetOrdinal("issuer_name");
                int issuerUserIdOrd = reader.GetOrdinal("issuer_userid");
                int nameOrd = reader.GetOrdinal("name");
                int userIdOrd = reader.GetOrdinal("userid");
                int reasonOrd = reader.GetOrdinal("reason");
                int startOrd = reader.GetOrdinal("start_time");
                int endOrd = reader.GetOrdinal("end_time");
                int portOrd = reader.GetOrdinal("port");

                while (await reader.ReadAsync())
                {
                    bans.Add((
                        reader.IsDBNull(issuerNameOrd) ? "Unknown" : reader.GetString(issuerNameOrd),
                        reader.IsDBNull(issuerUserIdOrd) ? "Unknown" : reader.GetString(issuerUserIdOrd),
                        reader.IsDBNull(nameOrd) ? "Unknown" : reader.GetString(nameOrd),
                        reader.IsDBNull(userIdOrd) ? "Unknown" : reader.GetString(userIdOrd),
                        reader.IsDBNull(reasonOrd) ? "未提供理由" : reader.GetString(reasonOrd),
                        reader.GetDateTime(startOrd),
                        reader.GetDateTime(endOrd),
                        reader.IsDBNull(portOrd) ? "Unknown" : reader.GetString(portOrd)
                    ));
                }
            }
            catch (Exception ex) { Log.Error($"查询所有封禁记录失败: {ex}"); }
            return bans;
        }

        public async Task<bool> InsertBanRecordAsync(string userid, string name, string issuer_userid, string issuer_name,
            string reason, DateTime start, DateTime end, string port)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return false;
            }


            const string sql = @"INSERT INTO ban (issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port)
                                 VALUES (@issuer_name, @issuer_userid, @name, @userid, @reason, @start_time, @end_time, @port)";

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    _ = cmd.Parameters.AddWithValue("@issuer_name", issuer_name ?? "Unknown");
                    _ = cmd.Parameters.AddWithValue("@issuer_userid", issuer_userid ?? "Unknown");
                    _ = cmd.Parameters.AddWithValue("@name", name ?? "Unknown");
                    _ = cmd.Parameters.AddWithValue("@userid", userid);
                    _ = cmd.Parameters.AddWithValue("@reason", reason ?? "No reason");
                    _ = cmd.Parameters.AddWithValue("@start_time", start);
                    _ = cmd.Parameters.AddWithValue("@end_time", end);
                    _ = cmd.Parameters.AddWithValue("@port", port ?? "Unknown");
                    await conn.OpenAsync();
                    _ = await cmd.ExecuteNonQueryAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"InsertBanRecord: {ex}");

                return false;
            }
        }

        public async Task<(string issuer_name, string issuer_userid, string name, string userid, string reason,
            DateTime start, DateTime end, string port)?> QueryBanAsync(string userid)
        {
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return null;
            }


            const string sql = @"SELECT issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port
                                 FROM ban WHERE userid = @userid AND end_time > NOW()
                                 ORDER BY end_time DESC LIMIT 1";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int issuerNameOrd = reader.GetOrdinal("issuer_name");
                    int issuerUserIdOrd = reader.GetOrdinal("issuer_userid");
                    int nameOrd = reader.GetOrdinal("name");
                    int userIdOrd = reader.GetOrdinal("userid");
                    int reasonOrd = reader.GetOrdinal("reason");
                    int startOrd = reader.GetOrdinal("start_time");
                    int endOrd = reader.GetOrdinal("end_time");
                    int portOrd = reader.GetOrdinal("port");
                    

                    return (
                        reader.IsDBNull(issuerNameOrd) ? null : reader.GetString(issuerNameOrd),
                        reader.IsDBNull(issuerUserIdOrd) ? null : reader.GetString(issuerUserIdOrd),
                        reader.IsDBNull(nameOrd) ? null : reader.GetString(nameOrd),
                        reader.IsDBNull(userIdOrd) ? null : reader.GetString(userIdOrd),
                        reader.IsDBNull(reasonOrd) ? null : reader.GetString(reasonOrd),
                        reader.GetDateTime(startOrd),
                        reader.GetDateTime(endOrd),
                        reader.IsDBNull(portOrd) ? null : reader.GetString(portOrd)
                    );
                }
            }
            catch (Exception ex) { Log.Error($"QueryBan: {ex}"); }
            

            return null;
        }

        public async Task<List<(string player_name, string port, string permissions, DateTime expiration, bool permanent, string notes)>> QueryAdminAsync(string userid)
        {
            var result = new List<(string, string, string, DateTime, bool, string)>();
            if (!Connected || string.IsNullOrEmpty(userid))
            {
                return result;
            }

            

            const string sql = @"SELECT player_name, port, permissions, expiration_date, is_permanent, notes
                                 FROM admin WHERE userid = @userid
                                 AND (is_permanent = 1 OR expiration_date > NOW())
                                 ORDER BY is_permanent DESC, expiration_date ASC";

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                int nameOrd = reader.GetOrdinal("player_name");
                int portOrd = reader.GetOrdinal("port");
                int permOrd = reader.GetOrdinal("permissions");
                int expOrd = reader.GetOrdinal("expiration_date");
                int permanentOrd = reader.GetOrdinal("is_permanent");
                int notesOrd = reader.GetOrdinal("notes");

                while (await reader.ReadAsync())
                {
                    result.Add((
                        reader.IsDBNull(nameOrd) ? "Unknown" : reader.GetString(nameOrd),
                        reader.IsDBNull(portOrd) ? "Unknown" : reader.GetString(portOrd),
                        reader.IsDBNull(permOrd) ? "none" : reader.GetString(permOrd),
                        reader.GetDateTime(expOrd),
                        reader.GetBoolean(permanentOrd),
                        reader.IsDBNull(notesOrd) ? "" : reader.GetString(notesOrd)
                    ));
                }
            }
            catch (Exception ex) { Log.Error($"QueryAdmin: {ex}"); }
            

            return result;
        }

        public async Task<List<(string player_name, string badge, string color, DateTime expiration_date, bool is_permanent, string notes)>> QueryBadgeAsync(string userid)
        {
            var badges = new List<(string, string, string, DateTime, bool, string)>();
            if (!Connected)
            {
                return badges;
            }

            const string query = @"SELECT player_name, badge, color, expiration_date, is_permanent, notes
                                   FROM badge WHERE userid = @userid
                                   AND (is_permanent = 1 OR expiration_date > NOW())
                                   ORDER BY is_permanent DESC, expiration_date ASC";
            

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(query, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid);
                await conn.OpenAsync();

                using var reader = await cmd.ExecuteReaderAsync();
                int nameOrd = reader.GetOrdinal("player_name");
                int badgeOrd = reader.GetOrdinal("badge");
                int colorOrd = reader.GetOrdinal("color");
                int expOrd = reader.GetOrdinal("expiration_date");
                int permanentOrd = reader.GetOrdinal("is_permanent");
                int notesOrd = reader.GetOrdinal("notes");

                while (await reader.ReadAsync())
                {
                    badges.Add((
                        reader.IsDBNull(nameOrd) ? string.Empty : reader.GetString(nameOrd),
                        reader.IsDBNull(badgeOrd) ? "" : reader.GetString(badgeOrd),
                        reader.IsDBNull(colorOrd) ? "white" : reader.GetString(colorOrd),
                        reader.GetDateTime(expOrd),
                        reader.GetBoolean(permanentOrd),
                        reader.IsDBNull(notesOrd) ? string.Empty : reader.GetString(notesOrd)
                    ));
                }
            }
            catch (Exception ex) { Log.Error($"查询用户 {userid} 的徽章失败: {ex}"); }
            

            return badges;
        }

        public async Task LogAdminPermissionAsync(string userid, string name, int port, string command, string result,
            string additionalInfo = "", string group = "")
        {
            if (!Connected)
            {
                return;
            }

            const string sql = @"INSERT INTO admin_log (userid, name, operation_time, port, command_name, command_result, additional_info, admingroup)
                                 VALUES (@userid, @name, @operation_time, @port, @command_name, @command_result, @additional_info, @admingroup)";
            

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                using var cmd = new MySqlCommand(sql, conn);
                _ = cmd.Parameters.AddWithValue("@userid", userid ?? "");
                _ = cmd.Parameters.AddWithValue("@name", name ?? "");
                _ = cmd.Parameters.AddWithValue("@operation_time", DateTime.Now);
                _ = cmd.Parameters.AddWithValue("@port", port);
                _ = cmd.Parameters.AddWithValue("@command_name", command ?? "");
                _ = cmd.Parameters.AddWithValue("@command_result", result ?? "");
                _ = cmd.Parameters.AddWithValue("@additional_info", additionalInfo ?? "");
                _ = cmd.Parameters.AddWithValue("@admingroup", group ?? "");
                await conn.OpenAsync();
                _ = await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { Log.Error($"LogAdminPermission: {ex}"); }
        }

        public void Close() { Connected = false; }
    }
}