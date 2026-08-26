using CommandSystem;
using CommandSystem.Commands.RemoteAdmin.Dummies;
using DrawableLine;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Lockers;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Roles;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.EventArgs;
using Exiled.CustomItems.API.Features;
using Exiled.CustomRoles.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FacilityNavigation;
using GameObjectPools;
using InventorySystem.Items.Firearms.Extensions;
using MapGeneration;
using MEC;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Spawnpoints;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using PlayerRoles.PlayableScps.Scp079.Pinging;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Subroutines;
using PlayerRoles.Subroutines;
using PlayerStatsSystem;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Objects;
using RelativePositioning;
using Respawning;
using Respawning.Waves;
using Respawning.Waves.Generic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Utils;
using static NS_site27_heavy.Modules.Weapons.SpeedBuildItem.SpeedBuilditem;
using static UnityEngine.GraphicsBuffer;
namespace Next_generationSite_27.UnionP
{
    public class BetterZombie
    {
        public static BetterZombie Create(Player Owner)
        {
            var p = Npc.Spawn("Zombie", RoleTypeId.Scp0492,true, Owner.Position);
            p.RoleManager.ServerSetRole(RoleTypeId.Scp0492, RoleChangeReason.Died);
            p.Position = Owner.Position;
            return new BetterZombie(p, Owner);
        }
        public Player CurrentTarget { get; set; }
        public Npc Zombie { get; set; }
        public Player Owner { get; set; }
        public Dictionary<Player, float> Hatreds = new Dictionary<Player, float>();
        public float tick = 0.2f;
        public bool tracking = false;
        public float LockIn = 1f;
        public float DoorOpenRange = 2f;
        public float BiteRange = 2f;
        public float LockOut = 40f;
        public BetterZombie(Npc zombie, Player owner)
        {
            Zombie = zombie;
            Owner = owner;
            Timing.CallDelayed(0.05f, () =>
            {
                Timing.RunCoroutine(Update());
            }
            );
        }
        private Player _currentFollowTarget;
        //
        // 摘要:
        //     Follow a specific player.
        //
        // 参数:
        //   player:
        //     the Player to follow.
        public void Follow(Player player)
        {
            var follower = Zombie.GameObject.GetComponent<PlayerFollower>();
            if (follower == null)
                follower = Zombie.GameObject.AddComponent<PlayerFollower>();

            // 每次设置主人和卡死回调（确保最新）
            follower.OwnerHub = Owner.ReferenceHub;
            follower.OnStuck = HandleStuck;

            // 只有当目标改变或组件尚未初始化时才重新 Init
            if (_currentFollowTarget != player || !follower.enabled)
            {
                follower.Init(player.ReferenceHub);
                _currentFollowTarget = player;
            }
        }
        private void HandleStuck()
        {
            tracking = false;
            if (CurrentTarget != null)
            {
                Hatreds[CurrentTarget] = 0f; // 清空仇恨，避免立即重新锁定
                CurrentTarget = null;
            }
            _currentFollowTarget = Owner; // 同步当前跟随目标为主人
        }
        //
        // 摘要:
        //     Follow a specific player.
        //
        // 参数:
        //   player:
        //     the Player to follow.
        //
        //   maxDistance:
        //     the max distance the npc will go.
        //
        //   minDistance:
        //     the min distance the npc will go.
        //
        //   speed:
        //     the speed the npc will go.
        public void Follow(Player player, float maxDistance, float minDistance, float speed = 4f)
        {
            // 获取或添加 PlayerFollower 组件
            if (player.GameObject == null|| Zombie == null || Zombie.GameObject == null) return;
            var follower = Zombie.GameObject.GetComponent<PlayerFollower>();
            if (follower == null)
                follower = Zombie.GameObject.AddComponent<PlayerFollower>();

            // 每次设置主人和卡死回调（确保最新）
            follower.OwnerHub = Owner.ReferenceHub;
            follower.OnStuck = HandleStuck;

            // 只有当目标改变或组件尚未初始化时才重新 Init
            if (_currentFollowTarget != player || !follower.enabled)
            {
                follower.Init(player.ReferenceHub, maxDistance, minDistance, speed);
                _currentFollowTarget = player;
            }
        }
        public IEnumerator<float> Update()
        {
            while (Zombie.Role.Type == RoleTypeId.Scp0492)
            {
                // Lock instance
                try
                {
                    if (!tracking)
                    {
                        Follow(Owner);
                        foreach (var item in Player.Enumerable.Where(x => HitboxIdentity.IsEnemy(this.Zombie.ReferenceHub,x.ReferenceHub)))
                        {
                            if (Vector3.Distance(item.Position, Zombie.Position) <= 20f || VisionInformation.GetVisionInformation(Zombie.ReferenceHub, Zombie.CameraTransform, item.Position, 0.02f, 50f, true, true, 0, true).IsLooking || VisionInformation.GetVisionInformation(Owner.ReferenceHub, Owner.CameraTransform, item.Position, 0.02f, 50f, true, true, 0, true).IsLooking)
                            {
                                if (Hatreds.TryGetValue(item, out var h))
                                {
                                    Hatreds[item] += tick;

                                }
                                else
                                {
                                    Hatreds[item] = tick;
                                }
                            }
                            if (Hatreds.TryGetValue(item, out var n))
                            {
                                if (n > LockIn)
                                {
                                    Hatreds[item] = LockIn;
                                    tracking = true;
                                    CurrentTarget = item;
                                    var r = Zombie.Role as Scp0492Role;
                                    Follow(item, LockOut + 10f, 1f, r.MovementSpeed);
                                }
                            }
                        }

                    }
                    else
                    {
                        if (CurrentTarget == null)
                        {
                            tracking = false;
                            Follow(Owner);
                            continue;
                        }
                        if (Vector3.Distance(CurrentTarget.Position, Zombie.Position) > LockOut || CurrentTarget.IsDead)
                        {
                            tracking = false;
                            Hatreds.Remove(CurrentTarget);
                            CurrentTarget = null;
                            Follow(Owner);
                            continue;
                        }
                        if (Vector3.Distance(CurrentTarget.Position, Zombie.Position) <= BiteRange)
                        {
                            var Zr = Zombie.Role.Base as ZombieRole;
                            Zr.LookAtPoint(CurrentTarget.Position);
                            var r = Zombie.Role as Scp0492Role;
                            MethodInfo serverSendRpcMethod = typeof(KeySubroutine<ZombieRole>).GetMethod(
    "OnKeyDown",
    BindingFlags.NonPublic | BindingFlags.Instance,
    null,
    new Type[] { },
    null
);
                            if (serverSendRpcMethod != null)
                            {
                                serverSendRpcMethod.Invoke(r.AttackAbility, new object[] { });
                            }
                            //if (r != null && r.AttackAbility.Cooldown.IsReady)
                            //{
                            //    r.AttackAbility.Cooldown.Trigger(r.AttackCooldown);
                            //    CurrentTarget.Hurt(new Scp049DamageHandler(Zombie.ReferenceHub, r.AttackDamage, Scp049DamageHandler.AttackType.Scp0492));
                            //}
                        }
                        if (Zombie.CurrentRoom != null)
                        {
                            foreach (var item in Zombie.CurrentRoom.Doors)
                            {
                                if (!item.IsLocked && !item.IsMoving && !item.IsOpen)
                                {
                                    if (Vector3.Distance(item.Position, Zombie.Position) <= DoorOpenRange)
                                    {
                                        if (item.PermissionsPolicy.CheckPermissions(Zombie.ReferenceHub, item.Base, out _) || !item.IsKeycardDoor)
                                        {
                                            item.IsOpen = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Zombie.Destroy();
                    Log.Info(ex.ToString());
                    yield break;
                }
                yield return Timing.WaitForSeconds(tick);
            }
            Zombie.Destroy();
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    class BotZombieCommand : ICommand
    {
        string ICommand.Command { get; } = "BotZombie";

        string[] ICommand.Aliases { get; } = new[] { "BZZ" };

        string ICommand.Description { get; } = "!!! 使用后产生一个机器小僵尸 bzz [PlayerId(主人 可选)]";

        bool ICommand.Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var runner = Player.Get(sender);
            Player Owner = null;
            if (arguments.Count < 1)
            {
                Owner = runner;
            }
            else
            {

                string[] newargs;
                List<ReferenceHub> list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out newargs);
                if (list == null)
                {
                    response = "An unexpected problem has occurred during PlayerId/Name array processing.";
                    return false;
                }
                if (list[0] == null)
                {
                    response = "An unexpected problem has occurred during PlayerId/Name array processing.2";
                    return false;
                }
                Owner = Player.Get(list[0]);
            }
            if (runner.KickPower < 12)
            {
                response = "你没权 （player.KickPower < 12）";
                return false;
            }
            BetterZombie.Create(Owner);
            response = $"done!";
            return true;

        }
    }
}

// i dont want to do this