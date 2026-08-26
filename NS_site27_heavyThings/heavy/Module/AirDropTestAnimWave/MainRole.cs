using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.Events.EventArgs.Player;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;

namespace NS_site27_heavy.heavy.Module.AirDrop
{
    [CustomRole(PlayerRoles.RoleTypeId.NtfCaptain)]
    public class MainRole : CustomRolePlus
    {
        public override uint Id { get; set; } = 371932;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "testing-air";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.NtfCaptain;
        public static MainRole r;
        public override void Init()
        {
            base.Init();
            r = this;
        }
        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);
            player.Position = new UnityEngine.Vector3(123, 289, 21);
        }
        public void cr(ChangingRoleEventArgs ev)
        {
            if (Check(ev.Player))
            {
            }
        }

        public override void Destroy()
        {
            base.Destroy();
            r = null;
        }
    }
}
