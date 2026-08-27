using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using HarmonyLib;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using NS_site27_heavy.heavy.Module.testing;
using PlayerRoles;
using PlayerRoles.FirstPersonControl.Thirdperson;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    [CustomRole(PlayerRoles.RoleTypeId.FacilityGuard)]
    public class MainRole : CustomRolePlus
    {
        public override uint Id { get; set; } = 37198;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "testing";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.FacilityGuard;
        public static MainRole r;
        public override void Init()
        {
            base.Init();
            abilities.Add(new TPAbility());
            abilities.Add(new DebuggersAbility2());
            abilities.Add(new DebuggersAbility3());
            anim_get = typeof(AnimatedCharacterModel).PropertyGetter("Animator");
            Exiled.Events.Handlers.Player.ChangingRole += cr;
            r = this;
        }
        public void cr(ChangingRoleEventArgs ev)
        {
            if (Check(ev.Player))
            {
                if (true)
                {
                    ModelAdd.Clear(ev.Player, "test-armor");
                    return;
                }
                if (m.TryGetValue(ev.Player, out var primitives))
                {
                    foreach (var primitive in primitives)
                    {
                        primitive.Destroy();
                    }
                    primitives.Clear();
                }

            }
        }

        public override void Destroy()
        {
            base.Destroy();
            r = null;
        }
        public MethodInfo anim_get;
        public Dictionary<Player, List<Primitive>> m = new();
        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);
            player.Position = new UnityEngine.Vector3(123, 289, 21);
            _ = Timing.CallDelayed(1f, () =>
            {
                if (true)
                {
                    ModelAdd.start(player, "test-armor");
                    return;
                }
            });

        }
    }
    public class follower : MonoBehaviour
    {
        public Transform TargetFollower;
        public Transform ThisFollower;
        public void Update()
        {
            if (ThisFollower == null)
            {
                ThisFollower = transform;
            }
            if (TargetFollower != null)
            {
                ThisFollower.position = TargetFollower.position;
                ThisFollower.rotation = TargetFollower.rotation;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
