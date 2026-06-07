using Exiled.API.Features;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace NS_site27_api.Modules.MySQL
{
    public class MySQLConfig : Core.ModuleConfigBase
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public uint Port { get; set; } = 3306;
        public string Username { get; set; } = "root";
        public string Password { get; set; } = "";
        public string Database { get; set; } = "site27";
    }

    public class MySQLConnect
    {
        private MySqlConnection _connection;
        private string _connectionString;
        public bool Connected { get; private set; }

        public void Connect(string ip, uint port, string username, string password, string database)
        {
            _connectionString = $"Server={ip};Port={port};Database={database};Uid={username};Pwd={password};allowPublicKeyRetrieval=true;Connection Timeout=30;";

            try
            {
                _connection = new MySqlConnection(_connectionString);
                _connection.Open();
                _connection.Close();
                Connected = true;
                Log.Info("Database connected.");
            }
            catch (Exception ex)
            {
                Log.Error($"Database connection failed: {ex}");
            }
        }

        public (int uid, string name, int experience, double? expMultiplier, int point, string ip, DateTime? last_time, TimeSpan? total_duration, TimeSpan? today_duration) QueryUser(string userid)
        {
            if (!Connected) return (0, null, 0, 0, 1, null, null, null, null);

            string query = "SELECT uid, name, experience, experience_multiplier, point, ip, today_duration, total_duration, last_time FROM user WHERE userid = @userid";

            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int uid = reader.IsDBNull(reader.GetOrdinal("uid")) ? 0 : reader.GetInt32("uid");
                            string name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString("name");
                            int exp = reader.IsDBNull(reader.GetOrdinal("experience")) ? 0 : reader.GetInt32("experience");
                            double? expMul = reader.IsDBNull(reader.GetOrdinal("experience_multiplier")) ? (double?)null : reader.GetDouble("experience_multiplier");
                            int point = reader.IsDBNull(reader.GetOrdinal("point")) ? 0 : reader.GetInt32("point");
                            string ipStr = reader.IsDBNull(reader.GetOrdinal("ip")) ? "1.1.1.1" : reader.GetString("ip");
                            DateTime? lastTime = reader.IsDBNull(reader.GetOrdinal("last_time")) ? (DateTime?)null : reader.GetDateTime("last_time");
                            TimeSpan? totalDur = reader.IsDBNull(reader.GetOrdinal("total_duration")) ? (TimeSpan?)null : reader.GetTimeSpan("total_duration");
                            TimeSpan? todayDur = reader.IsDBNull(reader.GetOrdinal("today_duration")) ? (TimeSpan?)null : reader.GetTimeSpan("today_duration");
                            return (uid, name, exp, expMul, point, ipStr, lastTime, totalDur, todayDur);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Error($"QueryUser error: {ex.Message}"); }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
            return (0, null, 0, 0, 1, null, null, null, null);
        }

        public void Update(string userid, string name = null, int experience = -1, double? expMultiplier = null,
            string ip = null, int point = -1, DateTime? last_time = null, TimeSpan? today_duration = null, TimeSpan? total_duration = null)
        {
            if (!Connected || string.IsNullOrEmpty(userid)) return;

            try
            {
                var p = QueryUser(userid);
                name = name ?? p.name;
                point = point == -1 ? p.point : point;
                experience = experience == -1 ? p.experience : experience;
                expMultiplier = expMultiplier ?? p.expMultiplier;
                ip = ip ?? p.ip;
                last_time = last_time ?? p.last_time;
                today_duration = today_duration ?? p.today_duration;
                total_duration = total_duration ?? p.total_duration;

                string sql = @"INSERT INTO user (userid, name, experience, experience_multiplier, ip, point, today_duration, total_duration, last_time)
                    VALUES (@userid, @name, @experience, @experience_multiplier, @ip, @point, @today_duration, @total_duration, @last_time)
                    ON DUPLICATE KEY UPDATE name=VALUES(name), experience=VALUES(experience), experience_multiplier=VALUES(experience_multiplier),
                    ip=VALUES(ip), point=VALUES(point), today_duration=VALUES(today_duration), total_duration=VALUES(total_duration), last_time=VALUES(last_time);";

                _connection.Open();
                using (var cmd = new MySqlCommand(sql, _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.Parameters.AddWithValue("@name", name ?? string.Empty);
                    cmd.Parameters.AddWithValue("@experience", experience);
                    cmd.Parameters.AddWithValue("@experience_multiplier", expMultiplier ?? 1.0);
                    cmd.Parameters.AddWithValue("@ip", ip ?? string.Empty);
                    cmd.Parameters.AddWithValue("@point", point);
                    cmd.Parameters.AddWithValue("@today_duration", today_duration ?? TimeSpan.Zero);
                    cmd.Parameters.AddWithValue("@total_duration", total_duration ?? TimeSpan.Zero);
                    cmd.Parameters.AddWithValue("@last_time", last_time ?? DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Log.Error($"Update error: {ex.Message}"); }
            finally
            {
                if (_connection.State == ConnectionState.Open) _connection.Close();
            }
        }

        public void InsertChatLog(string userid, string name, string message, string channel, string port)
        {
            if (!Connected || string.IsNullOrEmpty(userid)) return;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(
                    "INSERT INTO chat_log (userid, name, message, channel, time, port) VALUES (@userid, @name, @message, @channel, @time, @port)",
                    _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.Parameters.AddWithValue("@name", name ?? "");
                    cmd.Parameters.AddWithValue("@message", message ?? "");
                    cmd.Parameters.AddWithValue("@channel", channel ?? "");
                    cmd.Parameters.AddWithValue("@time", DateTime.Now);
                    cmd.Parameters.AddWithValue("@port", port ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Log.Error($"InsertChatLog: {ex.Message}"); }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
        }

        public int CountUserViolations(string userid)
        {
            if (!Connected || string.IsNullOrEmpty(userid)) return 0;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM ban WHERE userid = @userid", _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex) { Log.Error($"CountUserViolations: {ex.Message}"); return 0; }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
        }
        public List<(string issuer_name, string issuer_userid, string name, string userid, string reason, DateTime start_time, DateTime end_time, string port)> QueryAllBan(string INuserid)
        {
            var bans = new List<(string, string, string, string, string, DateTime, DateTime, string)>();

            if (!Connected)
                return bans;

            string query = @"
SELECT 
            issuer_name,
            issuer_userid,
            name,
            userid,
            reason,
            start_time,
            end_time,
            port
FROM ban
WHERE userid = @userid";

            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(query, _connection))
                {
                    // ✅ 先添加参数，再执行
                    cmd.Parameters.AddWithValue("@userid", INuserid);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string issuer_name = reader["issuer_name"] as string ?? "Unknown";
                            string issuer_userid = reader["issuer_userid"] as string ?? "Unknown";
                            string name = reader["name"] as string ?? "Unknown";
                            string userid = reader["userid"] as string ?? "Unknown";
                            string reason = reader["reason"] as string ?? "未提供理由";
                            DateTime start_time = reader.GetDateTime("start_time");
                            DateTime end_time = reader.GetDateTime("end_time");
                            string port = reader["port"] as string ?? "Unknown";

                            bans.Add((issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"❌ 查询所有封禁记录失败: {ex.Message}");
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                    _connection.Close();
            }

            return bans;
        }
        public bool InsertBanRecord(string userid, string name, string issuer_userid, string issuer_name, string reason, DateTime start, DateTime end, string port)
        {
            if (!Connected || string.IsNullOrEmpty(userid)) return false;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(
                    "INSERT INTO ban (issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port) VALUES (@issuer_name, @issuer_userid, @name, @userid, @reason, @start_time, @end_time, @port)",
                    _connection))
                {
                    cmd.Parameters.AddWithValue("@issuer_name", issuer_name ?? "Unknown");
                    cmd.Parameters.AddWithValue("@issuer_userid", issuer_userid ?? "Unknown");
                    cmd.Parameters.AddWithValue("@name", name ?? "Unknown");
                    cmd.Parameters.AddWithValue("@userid", userid);
                    cmd.Parameters.AddWithValue("@reason", reason ?? "No reason");
                    cmd.Parameters.AddWithValue("@start_time", start);
                    cmd.Parameters.AddWithValue("@end_time", end);
                    cmd.Parameters.AddWithValue("@port", port ?? "Unknown");
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex) { Log.Error($"InsertBanRecord: {ex.Message}"); return false; }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
        }

        public (string issuer_name, string issuer_userid, string name, string userid, string reason, DateTime start, DateTime end, string port)? QueryBan(string userid)
        {
            if (!Connected || string.IsNullOrEmpty(userid)) return null;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port FROM ban WHERE userid = @userid AND end_time > NOW() ORDER BY end_time DESC LIMIT 1",
                    _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                            return (reader["issuer_name"] as string, reader["issuer_userid"] as string, reader["name"] as string,
                                reader["userid"] as string, reader["reason"] as string, reader.GetDateTime("start_time"),
                                reader.GetDateTime("end_time"), reader["port"] as string);
                    }
                }
            }
            catch (Exception ex) { Log.Error($"QueryBan: {ex.Message}"); }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
            return null;
        }

        public List<(string player_name, string port, string permissions, DateTime expiration, bool permanent, string notes)> QueryAdmin(string userid)
        {
            var result = new List<(string, string, string, DateTime, bool, string)>();
            if (!Connected || string.IsNullOrEmpty(userid)) return result;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT player_name, port, permissions, expiration_date, is_permanent, notes FROM admin WHERE userid = @userid AND (is_permanent = 1 OR expiration_date > NOW()) ORDER BY is_permanent DESC, expiration_date ASC",
                    _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            result.Add((reader["player_name"] as string ?? "Unknown", reader["port"] as string ?? "Unknown",
                                reader["permissions"] as string ?? "none", reader.GetDateTime("expiration_date"),
                                reader.GetBoolean("is_permanent"), reader["notes"] as string ?? ""));
                    }
                }
            }
            catch (Exception ex) { Log.Error($"QueryAdmin: {ex.Message}"); }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
            return result;
        }

        public void LogAdminPermission(string userid, string name, int port, string command, string result, string additionalInfo = "", string group = "")
        {
            if (!Connected) return;
            try
            {
                _connection.Open();
                using (var cmd = new MySqlCommand(
                    "INSERT INTO admin_log (userid, name, operation_time, port, command_name, command_result, additional_info, admingroup) VALUES (@userid, @name, @operation_time, @port, @command_name, @command_result, @additional_info, @admingroup)",
                    _connection))
                {
                    cmd.Parameters.AddWithValue("@userid", userid ?? "");
                    cmd.Parameters.AddWithValue("@name", name ?? "");
                    cmd.Parameters.AddWithValue("@operation_time", DateTime.Now);
                    cmd.Parameters.AddWithValue("@port", port);
                    cmd.Parameters.AddWithValue("@command_name", command ?? "");
                    cmd.Parameters.AddWithValue("@command_result", result ?? "");
                    cmd.Parameters.AddWithValue("@additional_info", additionalInfo ?? "");
                    cmd.Parameters.AddWithValue("@admingroup", group ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { Log.Error($"LogAdminPermission: {ex.Message}"); }
            finally { if (_connection.State == ConnectionState.Open) _connection.Close(); }
        }

        public void Close()
        {
            _connection?.Close();
        }

        ~MySQLConnect()
        {
            Close();
        }
    }
}
