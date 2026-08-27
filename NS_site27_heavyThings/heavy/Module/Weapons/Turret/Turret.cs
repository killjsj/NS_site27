using CommandSystem;
using Decals;
using DrawableLine;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Spawn;
using Exiled.CustomItems.API.Features;
using InventorySystem.Items;
using InventorySystem.Items.Autosync;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Firearms.Modules.Misc;
using MapGeneration.StaticHelpers;
using Mirror;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using RelativePositioning;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utils;
using Utils.Networking;
using Item = Exiled.API.Features.Items.Item;
using Pickup = Exiled.API.Features.Pickups.Pickup;
using Player = Exiled.API.Features.Player;
using Time = UnityEngine.Time;

namespace Next_generationSite_27.UnionP.Turret
{
    internal class Builder : MonoBehaviour
    {
        public Exiled.API.Features.Pickups.Pickup pickup = null;
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
                _ = CalculateBunkerRotation(wallNormal, playerRotation);

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
    public class Turret : MonoBehaviour
    {
        public static Turret Create(Vector3 pos, Vector3 rotate, Player Onwer)
        {
            var sk = new SerializableSchematic
            {
                SchematicName = "Turret",
                Position = pos,
                Rotation = rotate,
            };

            GameObject skg = sk.SpawnOrUpdateObject();
            var turret = skg.AddComponent<Turret>();
            var SO = skg.gameObject.GetComponent<SchematicObject>();
            foreach (var comp in SO.AttachedBlocks)
            {
                switch (comp.name)
                {
                    case "shotpoint":
                        turret.shotPoint = comp;
                        break;
                    case "Damager":
                        turret.Damager = comp;
                        comp.AddComponent<damageReceiver>().turret = turret;
                        break;
                    case "Text":
                        turret.Text = comp.GetComponent<AdminToys.TextToy>();
                        break;
                    case "point":
                        turret.point = comp;
                        break;
                }
            }
            turret.turretModel = SO;
            turret.turretModelpos = pos;
            turret.Onwer = Onwer;
            turret.init = true;
            turret.hp = turret.maxhp;
            try
            {
                turret.Type = ItemType.GunCOM15;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            SO.Rotation = Quaternion.Euler(rotate);
            return turret;
        }
        public Player Onwer;
        public ItemType Type
        {
            get => _Type;
            set
            {
                _Type = value; itemBase = CreateOnwerItem(); c_firerate = 0f;
                var _ = firerate;
                handler = new PlayerStatsSystem.FirearmDamageHandler(firearm: itemBase as InventorySystem.Items.Firearms.Firearm, 0, 1);
                ammoLeft = totalAmmo;
                var fireM = (itemBase as InventorySystem.Items.Firearms.Firearm).Modules.FirstOrDefault(x => x is HitscanHitregModuleBase) as HitscanHitregModuleBase;

                Log.Info($"Turret damage set to {fireM.BaseDamage},1 shot");

            }
        }
        public ItemBase itemBase { get; private set; }
        private ItemType _Type;
        public SchematicObject turretModel;
        private Vector3 turretModelpos;
        public GameObject Damager;
        public AdminToys.TextToy Text;
        public GameObject shotPoint;
        public GameObject point;
        protected Ray ForwardRay
        {
            get
            {
                Transform playerCameraReference = shotPoint.transform;
                return new Ray(playerCameraReference.position, playerCameraReference.forward);
            }
        }

        public HitscanResult ResultNonAlloc = new();
        public void SpawnDecal(Vector3 position, Vector3 startPosition, DecalPoolType type = DecalPoolType.Blood)
        {
            RelativePosition hitPoint = new(position);
            RelativePosition startRaycastPoint = new(startPosition);
            ModularAutosyncItem autoItem = itemBase as ModularAutosyncItem;
            for (byte b = 0; b < autoItem.AllSubcomponents.Length; b++)
            {
                if (autoItem.AllSubcomponents[b] is ImpactEffectsModule)
                {
                    using (new AutosyncRpc(autoItem.ItemId, out var writer))
                    {
                        writer.WriteByte(b);
                        writer.WriteByte(((IConvertible)0).ToByte(null));

                        writer.WriteByte((byte)type);
                        writer.WriteRelativePosition(hitPoint);
                        writer.WriteRelativePosition(startRaycastPoint);
                        return;
                    }
                }
            }
        }
        public void SpawnTrace(RaycastHit Info, Vector3 startPosition, Team team = Team.OtherAlive)
        {
            ModularAutosyncItem autoItem = itemBase as ModularAutosyncItem;
            if (autoItem == null)
            {
                return;
            }
            RelativePosition hitPoint = new(Info.point);
            RelativePosition startRaycastPoint = new(startPosition);
            for (byte b = 0; b < autoItem.AllSubcomponents.Length; b++)
            {
                if (autoItem.AllSubcomponents[b] is ImpactEffectsModule)
                {
                    using (new AutosyncRpc(autoItem.ItemId, out var writer))
                    {
                        writer.WriteByte(b);
                        writer.WriteByte(((IConvertible)2).ToByte(null));

                        writer.WriteRelativePosition(hitPoint);
                        writer.WriteRelativePosition(startRaycastPoint);
                        writer.WriteByte((byte)Onwer.Role.Team);
                        return;
                    }
                }
            }
        }
        public void CreateTracer(RaycastHit Info)
        {
            SpawnTrace(Info, shotPoint.transform.position, Onwer.Role.Team);
            SpawnDecal(Info.point, shotPoint.transform.position, DecalPoolType.Bullet);

            new DrawableLineMessage(0.2f, Color.white, new Vector3[2] { shotPoint.transform.position, Info.point }).SendToAuthenticated();
        }
        protected void Awake()
        {
            PlayerRoleManager.OnRoleChanged += OnRoleChanged;
        }
        public ItemBase CreateOnwerItem()
        {
            var itemSerial = ItemSerialGenerator.GenerateNext();
            ItemBase itemBase2 = Onwer.Inventory.CreateItemInstance(new ItemIdentifier(_Type, itemSerial), true);
            return itemBase2 ?? null;
        }
        protected void OnDestroy()
        {
            PlayerRoleManager.OnRoleChanged -= OnRoleChanged;
            Map.ExplodeEffect(turretModelpos, Exiled.API.Enums.ProjectileType.Flashbang);
        }
        private void OnRoleChanged(ReferenceHub hub, PlayerRoleBase prevrole, PlayerRoleBase newrole)
        {
            if (!NetworkServer.active)
            {
                return;
            }
            if (Onwer.ReferenceHub != hub)
            {
                return;
            }
            turretModel.Destroy();
        }
        public Player Locking;
        protected Ray RandomizeRay(Ray ray, float angle)
        {
            float d = Mathf.Max(UnityEngine.Random.value, UnityEngine.Random.value);
            Vector3 a = UnityEngine.Random.insideUnitSphere * d;
            ray.direction = Quaternion.Euler(angle * a) * ray.direction;
            return ray;
        }
        private RaycastHit hit;

        protected RaycastHit ServerAppendPrescan(Ray targetRay, HitscanResult toAppend)
        {
            float maxDistance = 60;
            if (!Physics.Raycast(targetRay, out hit, maxDistance, HitscanHitregModuleBase.HitregMask))
            {
                return default;
            }
            if (!hit.collider.TryGetComponent<IDestructible>(out IDestructible destructible))
            {
                toAppend.Obstacles.Add(new HitRayPair(targetRay, hit));
                return hit;
            }
            toAppend.Destructibles.Add(new DestructibleHitPair(destructible, hit, targetRay));
            return hit;
        }
        public float hp = 0;
        public float maxhp = 60f;
        private float c_firerate = 0f;
        public int ammoLeft = 0;
        public float firerate
        {
            get
            {

                if (c_firerate == 0f && itemBase is InventorySystem.Items.Firearms.Firearm firearmPickup)
                {
                    try
                    {
                        var item = firearmPickup;
                        var fireM = item.Modules.FirstOrDefault(x => x is AutomaticActionModule) as AutomaticActionModule;
                        if (fireM != null)
                        {
                            c_firerate = 1f / fireM.BaseFireRate;
                            Log.Info($"Turret firerate set to {fireM.BaseFireRate}({c_firerate:F2}s,1 shot)");
                        }
                        else
                        {
                            c_firerate = 0.3f;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        c_firerate = 0.3f;
                    }
                }
                return c_firerate;
            }
        }
        public bool init = false;
        public void OnDamaged(float damage, PlayerStatsSystem.DamageHandlerBase handler, Vector3 pos)
        {
            hp -= damage;
            if (hp <= 0f)
            {
                turretModel.Destroy();
            }
        }
        public float reloadSecond = 5f;
        public int totalAmmo = 60;
        public FullAutoRateLimiter limiter = new();
        public bool reloading = false;
        public void Update()
        {
            if (!init || Onwer == null)
            {
                return;
            }

            limiter.Update();

        }
        public void FixedUpdate()
        {
            if (!init || Onwer == null)
            {
                return;
            }

            Lock();
            if (Locking != null && point != null && Locking.IsAlive
                && Vector3.Distance(shotPoint.transform.position, Locking.Position) <= DistanceToLock)
            {
                Vector3 direction = (Locking.Position - point.transform.position).normalized;
                direction.x = Mathf.Clamp(direction.x, -45f, 45f);

                Quaternion targetRotation = Quaternion.LookRotation(direction);

                float smoothFactor = 5f;
                point.transform.rotation = Quaternion.Slerp(point.transform.rotation, targetRotation, Time.deltaTime * smoothFactor);
            }
            try
            {
                foreach (var player in Player.Enumerable)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    var isfriend = player == null || !player.IsAlive || player == Onwer ||
                        player.Role.Team == Onwer.Role.Team ||
                        !HitboxIdentity.IsDamageable(Onwer.ReferenceHub, player.ReferenceHub);
                    var text = $"<color={(ammoLeft <= 0 ? "yellow" : (isfriend ? "green" : "red"))}>{ammoLeft}/{totalAmmo} {hp}hp";
                    MirrorExtensions.SendFakeSyncVar(player, Text.netIdentity, typeof(AdminToys.TextToy), "Network_textFormat", text);

                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

            if (limiter.Ready)
            {
                if (reloading)
                {
                    ammoLeft = totalAmmo;
                    reloading = false;
                }

                if (Locking != null)
                {
                    if (ammoLeft <= 0)
                    {
                        limiter.Trigger(reloadSecond);
                        reloading = true;
                        return;
                    }
                    limiter.Trigger(firerate);
                    Ray baseRay = ForwardRay;
                    baseRay = RandomizeRay(baseRay, 3f);

                    ResultNonAlloc.Clear();
                    var h = ServerAppendPrescan(baseRay, ResultNonAlloc);

                    foreach (var target in ResultNonAlloc.Destructibles)
                    {
                        ServerApplyDestructibleDamage(target, ResultNonAlloc);
                    }
                    CreateTracer(h);
                    ammoLeft--;

                }
            }
        }
        public float DistanceToLock = 15f;

        private float lockProgress = 0f; private readonly float maxLockTime = 1.5f; private readonly float minLockTime = 0.5f;
        private void Lock()
        {
            if (Locking != null && Locking.IsAlive &&
                Vector3.Distance(shotPoint.transform.position, Locking.Position) <= DistanceToLock)
            {
                return;
            }

            Locking = null;
            foreach (var player in Player.Enumerable)
            {
                if (player == null || !player.IsAlive || player == Onwer ||
                    player.Role.Team == Onwer.Role.Team ||
                    !HitboxIdentity.IsDamageable(Onwer.ReferenceHub, player.ReferenceHub))
                {
                    continue;
                }

                float dist = Vector3.Distance(shotPoint.transform.position, player.Position);

                if (dist <= DistanceToLock)
                {
                    float lockTime = Mathf.Lerp(maxLockTime, minLockTime, dist / DistanceToLock);

                    lockProgress += Time.deltaTime / lockTime;

                    if (lockProgress >= 1f)
                    {
                        Locking = player;
                        lockProgress = 0f; break;
                    }
                }
                else
                {
                    lockProgress = 0f;
                }
            }
        }

        private PlayerStatsSystem.AttackerDamageHandler handler = new PlayerStatsSystem.FirearmDamageHandler();
        protected virtual void ServerApplyDestructibleDamage(DestructibleHitPair target, HitscanResult result)
        {
            if (itemBase == null)
            {
                itemBase = CreateOnwerItem();
            }
            if (itemBase is InventorySystem.Items.Firearms.Firearm item)
            {
                try
                {
                    var fireM = item.Modules.FirstOrDefault(x => x is HitscanHitregModuleBase) as HitscanHitregModuleBase;
                    var damage = fireM.BaseDamage;
                    float dist = Vector3.Distance(shotPoint.transform.position, target.Hit.point);
                    dist -= damage;

                    float num = dist / fireM.DamageFalloffDistance;
                    damage *= Mathf.Clamp01(1f - num);
                    handler.Damage = damage;
                    IDestructible destructible = target.Destructible;
                    HitboxIdentity hitboxIdentity = destructible as HitboxIdentity;
                    if (hitboxIdentity != null)
                    {
                        if (hitboxIdentity.TargetHub.roleManager.CurrentRole.Team == Team.SCPs)
                        {
                            damage = 12;
                            handler.Damage = damage;
                        }
                    }
                    if (hitboxIdentity != null && !hitboxIdentity.TargetHub.IsAlive())
                    {
                        result.RegisterDamage(destructible, damage, handler);
                        return;
                    }
                    Vector3 point = target.Hit.point;

                    if (!destructible.Damage(damage, handler, point))
                    {
                        return;
                    }
                    CreateTracer(target.Hit);
                    Onwer.ShowHitMarker();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }
    }
    public class damageReceiver : NetworkBehaviour, IDestructible, IBlockStaticBatching
    {
        public uint NetworkId => base.netId;

        public Vector3 CenterOfMass => base.transform.position;
        public Turret turret;
        public bool Damage(float damage, PlayerStatsSystem.DamageHandlerBase handler, Vector3 pos)
        {
            turret.OnDamaged(damage, handler, pos);
            return true;
        }
    }

    [CustomItem(ItemType.Coin)]
    public class TurretItem : CustomItemPlus
    {
        public static uint TurretId = 9178;
        public override uint Id { get => TurretId; set => TurretId = value; }
        public override string Name { get => "炮塔"; set { } }
        public override string Description { get => "使用后在5米内表面创建炮台"; set { } }
        public override float Weight { get => 11; set { } }
        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties();
        public static TurretItem Instance;
        public override void Init()
        {
            base.Init();
            Type = ItemType.Coin;
            Instance = this;
        }
        protected override void OnUsed(Player player, Item item)
        {
            base.OnUsed(player, item);
            if (Check(item))
            {
                _ = Turret.Create(player.Position, Vector3.right, player);

            }
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    internal class TurretCommand : ICommand
    {
        string ICommand.Command { get; } = "turret";

        string[] ICommand.Aliases { get; } = new[] { "" };

        string ICommand.Description { get; } = "!!! 使用后产生一个炮塔 turret [PlayerId(主人 可选)]";

        bool ICommand.Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var runner = Player.Get(sender);
            Player Owner;
            if (arguments.Count < 1)
            {
                Owner = runner;
            }
            else
            {
                List<ReferenceHub> list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out _);
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
                response = "你没权 （pass_player.KickPower < 12）";
                return false;
            }
            _ = Turret.Create(Owner.CameraTransform.position, Vector3.right, Owner);
            response = $"done!";
            return true;

        }
    }
}
