using Decals;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Spawn;
using Exiled.API.Features.Toys;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Player;
using Footprinting;
using InventorySystem.Items.Firearms.Modules;
using MapGeneration.StaticHelpers;
using Mirror;
using NS_site27_api.Modules.CustomRolePlus;
using NS_site27_api.Modules.SettingManagement;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerStatsSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
namespace NS_site27_heavy.Modules.Weapons.SpeedBuildItem
{
    public class SpeedBuilditem : Core.ModuleBase<SBIConfig>
    {

        public static uint SpeedBuildItemID = 5096;

        public override string ModuleName => "SpeedBuilditem";

        [CustomItem(ItemType.GrenadeFlash)]
        public class SpeedBuildItem : CustomItemPlus
        {

            public static CustomItemPlus instance { get; private set; }
            public override uint Id { get; set; } = SpeedBuildItemID;
            public override string Name { get; set; } = "速凝掩体";
            public override string Description { get; set; }
            public override Vector3 Scale { get => new(0.5f, 0.5f, 0.5f); set => base.Scale = value; }
            public override float Weight { get; set; } = 1f;
            public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties()
            {
            };
            public override void Destroy()
            {
                IUnsubscribeEvents();
                base.Destroy();
            }
            public override void Init()
            {
                ISubscribeEvents();
                instance = this;
                MenuInit();
                base.Init();
            }
            private static readonly int ytKey = 1238123;
            private static readonly int ytButton = 1238121;
            public void ChangingRole(ChangingRoleEventArgs ev)
            {
                var b = SettingManager.Instance.MenuCache.First(x => x.Id == ytButton);
                var k = SettingManager.Instance.MenuCache.First(x => x.Id == ytKey);
                if (ev.Player.Role.Type.IsScp())
                {
                    _ = SettingManager.Instance.Unregister(ev.Player, new SettingBase[] { b, k });
                }
                if (ev.NewRole.IsScp())
                {
                    _ = SettingManager.Instance.Register(ev.Player, new SettingBase[] { b, k });
                }
            }
            public static void MenuInit()
            {

                var m = new List<SettingBase>() {
                    new ButtonSetting(ytButton, "销毁掩体", "", 0.5f, "销毁周围5m的掩体", onChanged: (player, sb) =>
                    {
                        if(sb != null)
                        {
                            if(player != null)
                            {
                                if(!ScpDisarmer.p2b.TryGetValue(player,out var bs) )
                                {
                                    return;
                                }
                                if(bs != null)
                                {
                                    foreach (var item in bs)
                                    {
                                        if(Vector3.Distance(item.transform.position,player.Position) <= 5)
                                        {
                                            NetworkServer.Destroy(item.gameObject);
                                        }
                                    }
                                }
                            }
                        }
                    }),
                    new KeybindSetting(ytKey, "销毁掩体", KeyCode.Mouse1, onChanged: (player, sb) =>
                    {
                        if(sb != null)
                        {
                            if(player != null)
                            {
                                var ray = new Ray(player.CameraTransform.position,player.CameraTransform.forward);
                                if(Physics.Raycast(ray,out var hitInfo, 5f))
                                {
                                    var b = hitInfo.collider.gameObject.GetComponent<bunker>();
                                    if(b != null)
                                    {
                                        NetworkServer.Destroy(b.gameObject);
                                    }
                                }

                            }
                        }
                    })
                };
                SettingManager.Instance.MenuCache.AddRange(m);
            }
            protected void ISubscribeEvents()
            {
                Exiled.Events.Handlers.Player.ThrownProjectile += OnDroppedItem;
                Exiled.Events.Handlers.Player.ChangingRole += ChangingRole;
            }
            protected void IUnsubscribeEvents()
            {
                Exiled.Events.Handlers.Player.ThrownProjectile -= OnDroppedItem;
                Exiled.Events.Handlers.Player.ChangingRole -= ChangingRole;
            }

            public void OnDroppedItem(ThrownProjectileEventArgs ev)
            {
                if (Check(ev.Pickup))
                {
                    ev.Pickup.Base.gameObject.AddComponent<Builder>().init(ev.Pickup, ev.Player.Rotation, ev.Player);
                }
            }
        }

        private class Builder : MonoBehaviour
        {
            public Pickup pickup = null;
            public Quaternion playerRotation;
            public Player Owner = null;
            private void OnCollisionEnter(Collision collision)
            {
                if (Owner == null)
                {
                    Log.Error("Owner is null!");
                }

                if (pickup == null)
                {
                    Log.Error("pickup is null!");
                }

                if (collision == null)
                {
                    Log.Error("wat");
                }

                if (!collision.collider)
                {
                    Log.Error("water");
                }

                if (collision.collider.gameObject == null)
                {
                    Log.Error("pepehm");
                }

                if (!(collision.collider.gameObject == Owner.GameObject) && (Player.Get(collision.gameObject) != Owner) && !Spawned)
                {
                    Spawned = true;
                    Vector3 wallNormal = collision.contacts[0].normal;
                    Quaternion bunkerRotation = CalculateBunkerRotation(wallNormal, playerRotation);

                    CreateBunker(collision.contacts[0].point, bunkerRotation);
                    pickup.Destroy();
                    Destroy(this);
                }
            }
            public bool Spawned = false;
            public void init(Pickup Pickup, Quaternion rotation, Player player)
            {
                playerRotation = rotation;
                pickup = Pickup;
                Owner = player;
            }

            private Quaternion CalculateBunkerRotation(Vector3 wallNormal, Quaternion playerRot)
            {
                Vector3 playerForward = playerRot * Vector3.forward;
                Vector3 playerRight = playerRot * Vector3.right;
                Vector3 projectedForward = Vector3.ProjectOnPlane(playerForward, wallNormal).normalized;
                _ = Vector3.ProjectOnPlane(playerRight, wallNormal).normalized;
                if (projectedForward.magnitude < 0.1f)
                {
                    projectedForward = Vector3.ProjectOnPlane(Vector3.forward, wallNormal).normalized;
                    _ = Vector3.ProjectOnPlane(Vector3.right, wallNormal).normalized;
                }

                return Quaternion.LookRotation(projectedForward, wallNormal);
            }
        }

        private class ScpDisarmer : MonoBehaviour
        {
            public bunker Owner = null;
            public static Dictionary<Player, List<bunker>> p2b = new();
            private BoxCollider _collider;

            private void Start()
            {
                _collider = gameObject.AddComponent<BoxCollider>();
                _collider.isTrigger = true;
                gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
            }

            private void OnTriggerEnter(Collider collision)
            {
                if (Owner == null)
                {
                    Log.Error("Owner is null!");
                }

                if (collision == null)
                {
                    Log.Error("wat");
                }

                if (!collision)
                {
                    Log.Error("water");
                }

                if (collision.gameObject == null)
                {
                    Log.Error("pepehm");
                }

                if (!(collision.gameObject == Owner.gameObject) && Player.TryGet(collision.gameObject, out var p))
                {
                    if (!p2b.ContainsKey(p))
                    {
                        p2b[p] = new List<bunker>();
                    }
                    p2b[p].Add(Owner);
                }
            }
            public void OnTriggerExit(Collider collision)

            {
                if (Owner == null)
                {
                    Log.Error("Owner is null!");
                }

                if (collision == null)
                {
                    Log.Error("wat");
                }

                if (!collision)
                {
                    Log.Error("water");
                }

                if (collision.gameObject == null)
                {
                    Log.Error("pepehm");
                }

                if (!(collision.gameObject == Owner.gameObject) && Player.TryGet(collision.gameObject, out var p))
                {
                    if (!p2b.ContainsKey(p))
                    {
                        p2b[p] = new List<bunker>();
                    }
                    _ = p2b[p].Remove(Owner);
                }
            }
            public void init(bunker b)
            {
                Owner = b;
            }
        }

        public static void CreateBunker(Vector3 pos, Quaternion rot)
        {
            Primitive p = Primitive.Get(Object.Instantiate(Primitive.Prefab));
            p.Position = pos;
            p.Base.NetworkPrimitiveType = PrimitiveType.Cube;
            p.Rotation = rot;
            p.Scale = new Vector3(2.3f, 2.3f, 0.3f);
            p.Color = Color.gray;
            p.Collidable = true;
            p.Visible = true;
            p.Spawn();

            Primitive i = Primitive.Get(Object.Instantiate(Primitive.Prefab));
            i.Position = pos;
            i.Base.NetworkPrimitiveType = PrimitiveType.Sphere;
            i.Rotation = rot;
            i.Scale = new Vector3(5, 5, 5);
            i.Color = Color.red;
            i.Collidable = false;
            i.Visible = false;
            if (!i.GameObject.TryGetComponent(out SphereCollider boxCollider))
            {
                boxCollider = i.GameObject.AddComponent<SphereCollider>();
            }

            boxCollider.isTrigger = true;
            i.Spawn();
            var BW = p.GameObject.AddComponent<bunker>();
            i.GameObject.AddComponent<ScpDisarmer>().init(BW);
            i.Transform.parent = p.GameObject.transform;
            BW.Health = 200;
        }
        public static RaycastHit CreateRaycastHit(Vector3 from, Vector3 to)
        {
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            if (Physics.Raycast(from, direction, out RaycastHit hit, distance))
            {
                return hit;
            }

            hit.point = to;
            hit.normal = Vector3.up;
            hit.distance = distance;

            return hit;
        }
        public class bunker : NetworkBehaviour, IDestructible, IBlockStaticBatching
        {
            public uint NetworkId => base.netId;

            public Vector3 CenterOfMass => base.transform.position;
            private void ServerSendImpactDecal(RaycastHit hit, Vector3 origin, DecalPoolType decalType, ImpactEffectsModule impactEffectsModule)
            {
                _ = typeof(ImpactEffectsModule).GetMethod("ServerSendImpactDecal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(impactEffectsModule, new object[] { hit, origin, decalType });
            }
            public bool Damage(float damage, DamageHandlerBase handler, Vector3 pos)
            {
                if (handler is not AttackerDamageHandler attackerDamageHandler)
                {
                    ServerDamageWindow(damage);
                    return true;
                }
                if (!CheckDamagePerms(attackerDamageHandler.Attacker.Role))
                {
                    return false;
                }
                LastAttacker = attackerDamageHandler.Attacker;
                Player attacker = Player.Get(attackerDamageHandler.Attacker);
                ServerDamageWindow(damage);
                if (handler is MicroHidDamageHandler)
                {
                    return true;
                }

                if (attacker.CurrentItem != null)
                {
                    if (attacker.CurrentItem is Exiled.API.Features.Items.Firearm firearm)
                    {
                        {
                            if (firearm.HitscanHitregModule is HitscanHitregModuleBase hitscan)
                            {
                                var mod = (ImpactEffectsModule)typeof(HitscanHitregModuleBase).GetField("_impactEffectsModule", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hitscan);
                                if (mod != null)
                                {
                                    var r = CreateRaycastHit(attackerDamageHandler.Attacker.Hub.GetPosition(), pos);
                                    ServerSendImpactDecal(r, attacker.Position, DecalPoolType.Bullet, mod);
                                }
                            }
                        }
                    }
                }
                return true;
            }
            private void Update()
            {
                if (!IsBroken || _prevStatus)
                {
                    return;
                }
                _ = base.StartCoroutine(BreakWindow());
                _prevStatus = true;
            }

            private IEnumerator BreakWindow()
            {
                GameObject.Destroy(base.gameObject);
                yield break;
            }

            private bool CheckDamagePerms(RoleTypeId roleType)
            {
                return !_preventScpDamage || (PlayerRoleLoader.TryGetRoleTemplate<PlayerRoleBase>(roleType, out PlayerRoleBase playerRoleBase) && playerRoleBase.Team > Team.SCPs);
            }

            [ServerCallback]
            private void ServerDamageWindow(float damage)
            {
                if (!NetworkServer.active)
                {
                    return;
                }
                Health -= damage;
                if (Health <= 0f)
                {
                    NetworkIsBroken = true;
                }
            }
            public bool NetworkIsBroken
            {
                get => IsBroken; set => IsBroken = value;
            }
            public Footprint LastAttacker;
            public float Health = 30f;
            public bool IsBroken;
            private readonly bool _preventScpDamage = false;
            private bool _prevStatus;
        }
        public static void OnRoundStart()
        {
        }

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStart;

        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStart;
        }
    }

    public class SBIConfig : Core.ModuleConfigBase
    {
    }
}
