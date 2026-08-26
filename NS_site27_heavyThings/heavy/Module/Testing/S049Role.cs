using CustomPlayerEffects;
using CustomRendering;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.Events.EventArgs.Player;
using MEC;
using Next_generationSite_27.UnionP;
using NS_site27_api.Modules.Abilities;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.AirDrop
{
    [CustomRole(PlayerRoles.RoleTypeId.Scp049)]
    public class S049Role : CustomRolePlus
    {
        public override uint Id { get; set; } = 37193221;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "b049";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.Scp049;
        public static S049Role r;
        public override void Init()
        {
            base.Init();
            r = this;
            abilities.Add(new ReleaseZombie());
        }
        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);
        }
        public static Dictionary<Player, List<BetterZombie>> pbz = new();
        public void cr(ChangingRoleEventArgs ev)
        {
            if (Check(ev.Player))
            {
                if (pbz.TryGetValue(ev.Player,out var zombies))
                {
                    foreach (var item in zombies)
                    {
                        item.Zombie.Destroy();
                    }
                    pbz.Remove(ev.Player);
                }
            }
        }

        public override void Destroy()
        {
            base.Destroy();
            r = null;
        }
    }
    public class ReleaseZombie : KeyAbility
    {
        public override KeyCode KeyCode => KeyCode.Mouse3;

        public override string Name => "撕咬";

        public override string Des => "3比特 存在60s";

        public override int id => 1012316;
        public override double time => 100;
        public override float WaitForDoneTime => 0;
        public override bool OnTrigger()
        {
            if (!S049Role.pbz.TryGetValue(player, out var zombies))
            {
                zombies = new();
                S049Role.pbz[player] = zombies;
            }
            var bz = BetterZombie.Create(this.player);
            zombies.Add(bz);
            Timing.CallDelayed(60f, () =>
            {
                if (S049Role.pbz.TryGetValue(player, out var zombies))
                {
                    zombies.Remove(bz);
                    bz.Zombie.Destroy();
                }
            });
            return true;
        }
        public override int TotalCount { get; set; } = 3;
    }
}
