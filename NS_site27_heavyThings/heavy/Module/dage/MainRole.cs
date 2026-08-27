using CommandSystem;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;

namespace NS_site27_heavy.heavy.Module.dage
{
    [CustomRole(PlayerRoles.RoleTypeId.Tutorial)]
    public class MainRole : CustomRolePlus
    {
        public override uint Id { get; set; } = 9213;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "dage";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.Tutorial;
        public static MainRole r;
        public override void Init()
        {
            base.Init();
            abilities.Add(new DageAbi1());
            abilities.Add(new yx());
            abilities.Add(new rot());
            abilities.Add(new jum());
            r = this;
        }
        public override void Destroy()
        {
            base.Destroy();
            r = null;
        }
        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);
            player.Position = new UnityEngine.Vector3(123, 289, 21);
            _ = Timing.CallDelayed(1f, () =>
            {
                if (player.Role.Base is IFpcRole fpc)
                {

                }
                else
                {
                }
            });

        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ZhuxianCommand : ICommand
    {
        public string Command => "ZhuXian";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "ZhuXian";
        public string[] Usage => new[] { "zx" };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (MainRole.r == null)
            {
                response = "The dage role is not registered.";
                return false;
            }

            ZhuXian.guas.Clear();
            foreach (var i in MainRole.r.TrackedPlayers)
            {
                ZhuXian.guas.Add(i);
            }
            ZhuXian.start();
            response = "Success.";
            return true;
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SerialCommand : ICommand
    {
        public string Command => "serial";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count >= 1)
            {
                response = "args count error";
                return false;
            }
            var player = Player.Get(sender);
            response = "Success.Serial:";
            if (player.CurrentItem != null)
            {
                response += player.CurrentItem.Serial;
            }
            else
            {
                response += "NULL";
            }
            return true;
        }
    }

}
