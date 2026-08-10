using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Roles;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using HarmonyLib;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using UnityEngine;
using UnityEngine.Windows.Speech;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    [CustomRole(PlayerRoles.RoleTypeId.NtfCaptain)]
    public class MainRole : CustomRolePlus
    {
        public override uint Id { get; set; } = 37198;
        public override int MaxHealth { get; set; } = 100;
        public override string Name { get; set; } = "testing";
        public override string Description { get; set; } = "1";
        public override string CustomInfo { get; set; } = "";
        public override RoleTypeId Role { get; set; } = RoleTypeId.NtfCaptain;
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
                if (player.Role.Base is IFpcRole fpc) {
                    if(!m.TryGetValue(player,out var primitives))
                    {
                        primitives = new();
                        m[player] = primitives;
                    }
                    foreach (var item in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>())
                        {
                        if (item == HumanBodyBones.LastBone || item < 0)
                        {
                            continue;
                        }
                        if (fpc.FpcModule.CharacterModelInstance is not AnimatedCharacterModel animatedCharacterModel)
                        {
                            continue;
                        }
                        if (anim_get != null) {
                            Animator animator = (Animator)anim_get.Invoke(animatedCharacterModel,null);
                            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                            {
                                Log.Info("animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman");
                                continue;
                            }
                            Transform boneTransform = null;
                            boneTransform = animator.GetBoneTransform(item);
                            if (boneTransform != null)
                            {
                                var pr = Primitive.Create(PrimitiveType.Cube, spawn: false, scale: Vector3.one);
                                var f = pr.GameObject.AddComponent<follower>();
                                
                                f.TargetFollower = boneTransform;

                                pr.Base.syncInterval = 0;
                                //pr.MovementSmoothing = 1;
                                pr.Scale = Vector3.one;
                                pr.Flags = AdminToys.PrimitiveFlags.Visible;

                                primitives.Add(pr);
                                
                                pr.Spawn();
                                //Log.Info($"Bone: {item} | Position: {boneTransform.position} | Rotation: {boneTransform.rotation} | localScale {boneTransform.localScale},cub;{pr.Position} rot {pr.Rotation} sca:{pr.Scale}");
                            }
                            else
                            {
                                Log.Info("boneTransform null");
                            }
                        }
                    }
                }
                else
                {
                    Log.Debug($"Not");
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
            if(ThisFollower == null)
            {
                ThisFollower = this.transform;
            }
            if(TargetFollower != null)
            {
                ThisFollower.position = TargetFollower.position;
                ThisFollower.rotation = TargetFollower.rotation;
            }
            else
            {
                Destroy(this.gameObject);
            }
        }
    }
}
