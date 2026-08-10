using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Roles;
using Exiled.API.Features.Toys;
using HarmonyLib;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using System;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using UnityEngine;

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
                if (player.Role.Base is IFpcRole fpc) {
                    
                }
                else
                {
                }
            });

        }
    }
}
