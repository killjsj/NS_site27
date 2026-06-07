using CommandSystem;
using Exiled.API.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils;

namespace NS_site27_api.Modules.BanSystem
{

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class Site27BanCommand : ICommand, IUsageProvider
    {
        public string Command => "sban";
        public string[] Aliases => new[] { "site27ban" };
        public string Description => "Ban a player with MySQL integration";
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
                response = "Usage: sban <player> <duration(m)> [reason]";
                return false;
            }

            string[] newargs;
            var targets = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out newargs);
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
                if (player == null) continue;

                DateTime endTime = DateTime.Now.AddMinutes(duration);
                sql?.InsertBanRecord(player.UserId, player.Nickname, runner.UserId, runner.Nickname, reason, DateTime.Now, endTime, Exiled.API.Features.Server.Port.ToString());
                //player.Ban((uint)(duration * 60), reason);
                player.Kick(reason,runner);
            }

            response = $"Banned {targets.Count} player(s).";
            return true;
        }

        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        public class CBanCommand : ICommand
        {
            public string Command => "cban";
            public string[] Aliases => Array.Empty<string>();
            public string Description => "Query ban records for a player";

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

                string userId = arguments.At(0);
                var bans = SqlQueryAllBan(sql, userId);

                if (bans.Count == 0)
                {
                    response = $"No ban records for {userId}.";
                    return true;
                }

                response = $"Ban records for {userId}:\n";
                foreach (var i in bans)
                {
                    response += $"- {i.name} banned by {i.issuer_name} ({i.start_time:yyyy-MM-dd} to {i.end_time:yyyy-MM-dd}): {i.reason}\n";
                }
                return true;
            }
        }

        private static MySQLConnect GetSQL()
        {
            return CorePlugin.Instance?.connect;
        }

        private static List<(string issuer_name, string issuer_userid, string name, string userid, string reason, DateTime start_time, DateTime end_time, string port)> SqlQueryAllBan(MySQLConnect sql, string userId)
        {
            return GetSQL()?.QueryAllBan(userId);
        }
    }
}
