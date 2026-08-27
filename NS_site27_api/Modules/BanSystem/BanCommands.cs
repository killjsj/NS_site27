using CommandSystem;
using Exiled.API.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Utils;

namespace NS_site27_api.Modules.BanSystem
{

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Site27BanCommand : ICommand, IUsageProvider
    {
        public string Command => "sban";
        public string[] Aliases => new[] { "site27ban" };
        public string Description => "Ban a player";
        public string[] Usage => new[] { "%player%", "duration", "reason" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var runner = Player.Get(sender);
            if (runner == null || runner.KickPower < 4)
            {
                response = "You don't have permission.";
                return false;
            }

            if (arguments.Count < 2)
            {
                response = "Usage: sban <pass_player> <duration(m)> [reason]";
                return false;
            }

            var targets = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out string[] newargs);
            if (targets == null || targets.Count == 0)
            {
                response = "Player not found.";
                return false;
            }

            if (!long.TryParse(newargs[0], out long duration))
            {
                response = "Invalid duration.";
                return false;
            }

            string reason = newargs.Length > 1 ? newargs[1] : "No reason provided";
            var sql = GetSQL();

            foreach (var target in targets)
            {
                var player = Player.Get(target);
                if (player == null)
                {
                    continue;
                }

                DateTime endTime = DateTime.Now.AddMinutes(duration);
                _ = sql?.InsertBanRecordAsync(player.UserId, player.Nickname, runner.UserId, runner.Nickname, reason, DateTime.Now, endTime, Exiled.API.Features.Server.Port.ToString());
                player.Kick(reason, runner);
            }

            response = $"Banned {targets.Count} pass_player(s).";
            return true;
        }

        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        public class CBanCommand : ICommand
        {
            public string Command => "cban";
            public string[] Aliases => Array.Empty<string>();
            public string Description => "Query ban records for a pass_player";

            public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                if (arguments.Count < 1)
                {
                    response = "Usage: cban <playerid>";
                    return false;
                }

                var sql = GetSQL();
                if (sql == null)
                {
                    response = "Database not connected.";
                    return false;
                }

                var targets = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out _);
                string userId = targets == null || targets.Count == 0 ? arguments.At(0) : targets[0].authManager.UserId;
                GetBans(sender, sql, userId);
                response = "please wait...";
                return true;
            }

            private static async void GetBans(ICommandSender sender, MySQLConnect sql, string userId)
            {
                var bans = await SqlQueryAllBan(sql, userId);

                string response;
                if (bans.Count == 0)
                {
                    response = $"No ban records for {userId}.";
                    sender.Respond(response);
                    return;
                }

                response = $"Ban records for {userId}:\n";
                foreach (var (issuer_name, issuer_userid, name, userid, reason, start_time, end_time, port) in bans)
                {
                    response += $"- {name} banned by {issuer_name} ({start_time:yyyy-MM-dd} to {end_time:yyyy-MM-dd}): {reason}\n";
                }
                sender.Respond(response);
                return;
            }
        }

        private static MySQLConnect GetSQL()
        {
            return CorePlugin.Instance?.connect;
        }

        private static async Task<List<(string issuer_name, string issuer_userid, string name, string userid, string reason, DateTime start_time, DateTime end_time, string port)>> SqlQueryAllBan(MySQLConnect sql, string userId)
        {
            return await sql.QueryAllBanAsync(userId);
        }
    }
}
