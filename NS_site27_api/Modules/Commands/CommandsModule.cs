using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using NS_site27_api.Core;
using NS_site27_api.Modules.MessageModule;
using PlayerRoles;
using PlayerRoles.Subroutines;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace NS_site27_api.Modules.Commands
{
    public class CommandsConfig : ModuleConfigBase
    {
        public bool EnableChangeScp { get; set; } = true;
    }

    public struct ScpChangeRequest
    {
        public Player From;
        public RoleTypeId To;
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class ScpChangeCommand : ICommand, IUsageProvider
    {
        public string Command => "ChangeSCP";
        public string[] Aliases => new[] { "CS" };
        public string Description => "Switch SCP role with another pass_player";
        public string[] Usage => new[] { "ChangeSCP", "target SCP number (e.g. 096)" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Failed to find sender.";
                return false;
            }

            if (!player.IsScp)
            {
                response = "You are not an SCP.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "Missing target SCP!";
                return false;
            }

            var target = GetRoleFromScpNumber(arguments.At(0));
            if (target == RoleTypeId.None)
            {
                response = "Invalid SCP!";
                return false;
            }

            var scpList = Player.Enumerable.Where(p => p.IsScp).ToList();
            CommandsModule.ScpsChangeRequests.Add(new ScpChangeRequest
            {
                From = player,
                To = target,
            });

            foreach (var scp in scpList)
            {
                if (scp.Role.Type == target)
                {
                    scp.AddHint("SwapScp", 10f, x => new MsgUpdateResult() { Content = $"<size=29><color=yellow>{player.DisplayNickname}想要与你交换SCP,控制台: .ScpArgee 同意</color></size>", Title = "SCP交换", NoticeCircleColor = Color.green }, PriorityLevel.Medium);

                }
            }

            response = "Success, waiting for agreement.";
            return true;
        }

        private RoleTypeId GetRoleFromScpNumber(string number)
        {
            return number switch
            {
                "049" => RoleTypeId.Scp049,
                "096" => RoleTypeId.Scp096,
                "106" => RoleTypeId.Scp106,
                "173" => RoleTypeId.Scp173,
                "3114" => RoleTypeId.Scp3114,
                "939" => RoleTypeId.Scp939,
                _ => RoleTypeId.None,
            };
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class ScpChangeAgreeCommand : ICommand, IUsageProvider
    {
        public string Command => "ScpArgee";
        public string[] Aliases => new[] { "SA" };
        public string Description => "Agree to SCP swap";
        public string[] Usage => new[] { "ScpArgee", "source SCP (optional, e.g. 096)" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Failed to find sender.";
                return false;
            }

            var waitForChange = CommandsModule.ScpsChangeRequests.Where(r => r.To == player.Role).ToList();
            if (waitForChange.Count == 0)
            {
                response = "No one wants to swap with you.";
                return false;
            }

            if (arguments.Count >= 1)
            {
                var target = GetRoleFromScpNumber(arguments.At(0));
                if (target == RoleTypeId.None)
                {
                    response = "Invalid SCP!";
                    return false;
                }

                var req = waitForChange.FirstOrDefault(r => r.From.Role == target);
                if (req.From != null)
                {
                    SwapRoles(req.From, player);
                    _ = CommandsModule.ScpsChangeRequests.RemoveAll(r => Equals(r, req));
                }
            }
            else
            {
                var req = waitForChange[0];
                SwapRoles(req.From, player);
                _ = CommandsModule.ScpsChangeRequests.RemoveAll(r => Equals(r, req));
            }

            response = "Success.";
            return true;
        }

        private void SwapRoles(Player a, Player b)
        {
            var prePos = a.Position;
            var preHp = a.Health;
            var preShield = a.HumeShield;
            var preRole = a.Role;

            a.RoleManager.ServerSetRole(b.Role, RoleChangeReason.Respawn, RoleSpawnFlags.AssignInventory);
            a.Position = b.Position;
            a.Health = b.Health;
            a.HumeShield = b.HumeShield;

            b.RoleManager.ServerSetRole(preRole, RoleChangeReason.Respawn, RoleSpawnFlags.AssignInventory);
            b.Position = prePos;
            b.Health = preHp;
            b.HumeShield = preShield;
        }

        private RoleTypeId GetRoleFromScpNumber(string number)
        {
            return number switch
            {
                "049" => RoleTypeId.Scp049,
                "096" => RoleTypeId.Scp096,
                "106" => RoleTypeId.Scp106,
                "173" => RoleTypeId.Scp173,
                "3114" => RoleTypeId.Scp3114,
                "939" => RoleTypeId.Scp939,
                _ => RoleTypeId.None,
            };
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class TPCommand : ICommand, IUsageProvider
    {
        public string Command => "tp";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "tp";
        public string[] Usage => new[] { "x","y","z" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Failed to find sender.";
                return false;
            }
            var x = float.Parse(arguments.Array[1]);
            var y = float.Parse(arguments.Array[2]);
            var z = float.Parse(arguments.Array[3]);
            player.Position = new Vector3(x, y, z);
            response = "Success.";
            return true;
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(ClientCommandHandler))]
    public class DanceCommand : ICommand
    {
        public string Command => "dance";

        public string[] Aliases => new[] { "" };

        public string Description => "";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "done!\n";
            var p = Player.Get(sender);
            if (p.Role is Scp3114Role s3114)
            {
                s3114.StartDancing(DanceType.RunningMan);
            }
            return true;
        }
    }
    [CommandHandler(typeof(ClientCommandHandler))]
    public class KillCommand : ICommand, IUsageProvider
    {
        public string Command => "kill";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "Suicide";
        public string[] Usage => new[] { "kill" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null)
            {
                response = "Failed to find sender.";
                return false;
            }

            player.Kill("Suicide");

            response = "Success.";
            return true;
        }
    }

    public class CommandsModule : ModuleBase<CommandsConfig>
    {
        public override string ModuleName => "Commands";
        public static List<ScpChangeRequest> ScpsChangeRequests = new();

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
            ScpsChangeRequests.Clear();
        }
    }
}
