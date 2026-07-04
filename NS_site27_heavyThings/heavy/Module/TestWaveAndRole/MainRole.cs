using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Spawn;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    [CustomRole(PlayerRoles.RoleTypeId.Tutorial)]
    public class MainRole : CustomRolePlus
    {
        public override uint Id { get; set; } = 37198;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "testing";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.Tutorial;
        public static MainRole r;
        public override void Init()
        {
            base.Init();
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
        }
    }
}
