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
using Utils.NonAllocLINQ;
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
        public float BiteRange = 1f;
        public float LockOut = 40f;
        public BetterZombie(Npc zombie, Player owner)
        {
            Zombie = zombie;
            Owner = owner;
            serverSendRpcMethod = typeof(KeySubroutine<ZombieRole>).GetMethod(
"OnKeyDown",
BindingFlags.NonPublic | BindingFlags.Instance,
null,
new Type[] { },
null
);
            Timing.CallDelayed(0.05f, () =>
            {
                Timing.RunCoroutine(Update());
            }
            );
        }
        private Player _currentFollowTarget;
                                                                public void Follow(Player player)
        {
            if(Zombie.GameObject == null) return;
            var follower = Zombie.GameObject.GetComponent<PlayerFollower>();
            if (follower == null)
                follower = Zombie.GameObject.AddComponent<PlayerFollower>();

                        follower.OwnerHub = Owner.ReferenceHub;
            follower.OnStuck = HandleStuck;
            follower.TargetPos = (hub) => { return _moveTargetPosition ?? hub.GetPosition(); };
                        if (_currentFollowTarget != player || !follower.enabled)
            {
                follower.Init(player.ReferenceHub);
                _currentFollowTarget = player;
            }
        }
        private bool _movingToPosition = false;
        private Vector3? _moveTargetPosition = null;
        private float _moveStartTime = 0f;
        private const float MaxMoveDuration = 10f;
        private const float OwnerMaxDistance = 20f; 
        public void MoveToPosition(Vector3 position)
        {
            if (Zombie == null || Zombie.GameObject == null) return;

                        if (tracking)
            {
                tracking = false;
                if (CurrentTarget != null)
                {
                    Hatreds[CurrentTarget] = 0f;
                    CurrentTarget = null;
                }
            }

            _movingToPosition = true;
            _moveTargetPosition = position;
            _moveStartTime = Time.time;


            var follower = Zombie.GameObject.GetComponent<PlayerFollower>();
            if (follower == null)
                follower = Zombie.GameObject.AddComponent<PlayerFollower>();

            follower.OwnerHub = Owner.ReferenceHub;
            follower.OnStuck = HandleStuck;
            follower.TargetPos = (hub) => { return _moveTargetPosition ?? hub.GetPosition(); };

                        follower.Init(Owner.ReferenceHub, LockOut + 10f, BiteRange, GetZombieSpeed());
            _currentFollowTarget = null;         }

        private float GetZombieSpeed()
        {
            if (Zombie.Role is Scp0492Role role)
                return role.MovementSpeed;
            return 4f;
        }

                private void HandleStuck()
        {
            tracking = false;
            if (CurrentTarget != null)
            {
                Hatreds[CurrentTarget] = 0f;
                CurrentTarget = null;
            }
            _currentFollowTarget = Owner;
                        _movingToPosition = false;
            _moveTargetPosition = null;
            Follow(Owner);
        }
        public MethodInfo serverSendRpcMethod;
                                                                                                                                        public void Follow(Player player, float maxDistance, float minDistance, float speed = 4f)
        {
            if (player.GameObject == null|| Zombie == null || Zombie.GameObject == null) return;
            var follower = Zombie.GameObject.GetComponent<PlayerFollower>();
            if (follower == null)
                follower = Zombie.GameObject.AddComponent<PlayerFollower>();

                        follower.OwnerHub = Owner.ReferenceHub;
            follower.OnStuck = HandleStuck;
            follower.TargetPos = (hub) => { return _moveTargetPosition ?? hub.GetPosition(); };


                        if (_currentFollowTarget != player || !follower.enabled)
            {
                follower.Init(player.ReferenceHub, maxDistance, minDistance, speed);
                _currentFollowTarget = player;
            }
        }
        public AbilityCooldown AttackCooldown = new();
        public IEnumerator<float> Update()
        {
            while (Zombie.Role.Type == RoleTypeId.Scp0492)
            {
                            if(Zombie.GameObject == null) yield break;
                if (_movingToPosition)
                {
                                        try
                    {
                        if (Time.time - _moveStartTime > MaxMoveDuration ||
                            Vector3.Distance(Zombie.Position, Owner.Position) > OwnerMaxDistance)
                        {
                            _movingToPosition = false;
                            _moveTargetPosition = null;
                            Follow(Owner);
                            continue;
                        }

                                                foreach (var item in Player.Enumerable.Where(x => HitboxIdentity.IsEnemy(this.Zombie.ReferenceHub, x.ReferenceHub)))
                        {
                            if (Vector3.Distance(item.Position, Zombie.Position) <= 20f ||
                                VisionInformation.GetVisionInformation(Zombie.ReferenceHub, Zombie.CameraTransform, item.Position, 0.02f, 50f, true, true, 0, true).IsLooking ||
                                VisionInformation.GetVisionInformation(Owner.ReferenceHub, Owner.CameraTransform, item.Position, 0.02f, 50f, true, true, 0, true).IsLooking)
                            {
                                if (Hatreds.TryGetValue(item, out var h))
                                    Hatreds[item] += tick;
                                else
                                    Hatreds[item] = tick;
                            }

                            if (Hatreds.TryGetValue(item, out var n) && n > LockIn)
                            {
                                                                Hatreds[item] = LockIn;
                                _movingToPosition = false;
                                _moveTargetPosition = null;
                                tracking = true;
                                CurrentTarget = item;
                                var r = Zombie.Role as Scp0492Role;
                                Follow(item, LockOut + 10f, 1f, r.MovementSpeed);
                                break;
                            }
                        }

                                                if (!_movingToPosition)
                            continue;

                                                if (Zombie.CurrentRoom != null)
                        {
                            foreach (var door in Zombie.CurrentRoom.Doors)
                            {
                                if (!door.IsLocked && !door.IsMoving && !door.IsOpen &&
                                    Vector3.Distance(door.Position, Zombie.Position) <= DoorOpenRange)
                                {
                                    if (door.PermissionsPolicy.CheckPermissions(Zombie.ReferenceHub, door.Base, out _) || !door.IsKeycardDoor)
                                        door.IsOpen = true;
                                }
                            }
                        }
                    }catch(Exception e)
                    {
                        Zombie.Destroy();
                        Log.Info(e.ToString());
                        yield break;
                    }

                    yield return Timing.WaitForSeconds(tick);
                    continue;
                }
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
                                    LockTo(item);
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
                        if (ReferenceHub.AllHubs.TryGetFirst(x=> Vector3.Distance(x.GetPosition(), Zombie.Position) <= BiteRange && HitboxIdentity.IsEnemy(Zombie.ReferenceHub,x),out var t))
                        {
                            var Zr = Zombie.Role.Base as ZombieRole;
                            Zr.LookAtPoint(t.GetPosition());
                            var r = Zombie.Role as Scp0492Role;
                            if (serverSendRpcMethod != null)
                            {
                                if (r.AttackAbility.Cooldown.IsReady)
                                {
                                    Owner.ShowHitMarker();
                                }
                                serverSendRpcMethod.Invoke(r.AttackAbility, new object[] { });
                            }
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

        public void LockTo(Player item)
        {
            Hatreds[item] = LockIn;
            tracking = true;
            CurrentTarget = item;
            var r = Zombie.Role as Scp0492Role;
            Follow(item, LockOut + 5f, 1f, r.MovementSpeed);
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

