using CustomPlayerEffects;
using DrawableLine;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Items;
using Exiled.API.Features.Spawn;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using Footprinting;
using Interactables;
using Interactables.Interobjects.DoorUtils;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.ThrowableProjectiles;
using MEC;
using Mirror;
using NorthwoodLib.Pools;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerStatsSystem;
using System.Collections.Generic;
using UnityEngine;
using Utils.Networking;

namespace NS_site27_heavy.heavy.Module.Weapons
{
    [CustomItem(ItemType.Coin)]
    public class OnceTimeRailgun : CustomItemPlus
    {
        public override uint Id { get; set; } = 12334;
        public override ItemType Type { get; set; } = ItemType.Coin;
        public override string Name { get; set; } = "Toaru Kagaku no Railgun";
        public override string Description { get; set; } = "";
        public override float Weight { get; set; } = 1f;
        public override SpawnProperties SpawnProperties { get; set; } = null;
        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);
        }
        protected override void SubscribeEvents()
        {
            base.SubscribeEvents();
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Player.PickingUpItem += PickingUpItem;
        }
        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            Exiled.Events.Handlers.Player.PickingUpItem -= PickingUpItem;
            base.UnsubscribeEvents();
        }
        public void PickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Pickup.Type == ItemType.Coin)
            {
                ev.Player.Inventory.UserInventory.ReserveAmmo[ItemType.Coin] = (ushort)(GetRemainAmmo(ev.Player) + 1);
            }
        }
        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player == null)
            {
                return;
            }

            if (ev.Player.Inventory.UserInventory.ReserveAmmo.ContainsKey(ItemType.Coin))
            {
                _ = ev.Player.Inventory.UserInventory.ReserveAmmo.Remove(ItemType.Coin);
            }
            _ = Timing.CallDelayed(0.2f, () =>
            {
                if (ev.Player == null)
                {
                    return;
                }

                ev.Player.Inventory.UserInventory.ReserveAmmo[ItemType.Coin] = 10;
            });
        }
        public static ushort GetRemainAmmo(Player p)
        {
            if (p != null)
            {
                if (p.Inventory.UserInventory.ReserveAmmo.TryGetValue(ItemType.Coin, out ushort re))
                {
                    return re;
                }
            }
            return 0;
        }
        public override string GetUIDescription(Player p)
        {
            return base.GetUIDescription(p) + $" Remain:{GetRemainAmmo(p)}";
        }
        protected Ray ForwardRay(Player p)
        {
            Transform playerCameraReference = p.CameraTransform;
            return new Ray(playerCameraReference.position + (playerCameraReference.forward * 0.2f), playerCameraReference.forward);

        }
        public void CreateTracer(RaycastHit Info, Player p)
        {
            new DrawableLineMessage(1.1f, new Color(1, 1, 1, 0.5f), new Vector3[2] { p.CameraTransform.position - (Vector3.up * 0.3f) + (p.CameraTransform.forward * 0.2f), Info.point }).SendToAuthenticated();
        }
        public static float ExplosionRad = 1.2f;
        private const float BaseDamage = 500f;          // 基础伤害
        private const float DoorBaseDamage = 100f;
        private const float MinDamage = 0.1f;           // 最小伤害阈值，低于此不造成伤害

        /// <summary>
        /// 爆炸入口方法
        /// </summary>
        public static void Explode(Footprint attacker, Vector3 position, ExplosionType explosionType)
        {
            // 开启本地玩家的碰撞盒（用于伤害检测）
            SetHostHitboxes(true);

            // 从池中租用 HashSet 避免重复处理同一对象
            HashSet<uint> processedDestructibles = HashSetPool<uint>.Shared.Rent();
            HashSet<uint> processedDoors = HashSetPool<uint>.Shared.Rent();
            try
            {
                float radius = ExplosionRad; // 若需要可变，可在此读取配置

                // 获取所有可能受影响的碰撞体
                Collider[] hitColliders = Physics.OverlapSphere(position, radius, HitscanHitregModuleBase.HitregMask);
                var p = Primitive.Create(position, null, new Vector3(radius, radius, radius), false);
                p.Collidable = false;
                p.Color = new Color(1, 1, 1, 0.25f);
                p.Type = PrimitiveType.Sphere;
                p.Spawn();
                var animator = p.GameObject.AddComponent<ScaleAnimator>();
                animator.endScale = Vector3.one * radius * 2f;
                animator.duration = 0.4f;

                _ = Timing.CallDelayed(0.4f, () => { p?.Destroy(); });
                // 提前判断是否在服务器端运行（避免循环内重复检查）
                bool isServer = NetworkServer.active;

                foreach (Collider collider in hitColliders)
                {
                    if (isServer)
                    {
                        // 触发爆炸响应接口
                        if (collider.TryGetComponent<IExplosionTrigger>(out var trigger))
                        {
                            trigger.OnExplosionDetected(attacker, position, radius);
                        }

                        // 处理可破坏物体（IDestructible）
                        if (collider.TryGetComponent<IDestructible>(out var destructible))
                        {
                            if (!processedDestructibles.Contains(destructible.NetworkId) &&
                                ExplodeDestructible(destructible, attacker, position, explosionType, radius))
                            {
                                _ = processedDestructibles.Add(destructible.NetworkId);
                            }
                        }
                        // 处理门（通过 InteractableCollider）
                        else if (collider.TryGetComponent<InteractableCollider>(out var interactable) &&
                                 interactable.Target is DoorVariant door &&
                                 processedDoors.Add(door.netId))
                        {
                            ExplodeDoor(door, position, attacker, radius);
                        }
                    }

                    // 刚体物理（客户端和服务器都需处理）
                    if (collider.attachedRigidbody != null)
                    {
                        ExplodeRigidbody(collider.attachedRigidbody, position, radius);
                    }
                }
            }
            finally
            {
                // 归还池中资源
                HashSetPool<uint>.Shared.Return(processedDestructibles);
                HashSetPool<uint>.Shared.Return(processedDoors);
            }

            SetHostHitboxes(false);
        }
        // 定义一个小脚本
        public class ScaleAnimator : MonoBehaviour
        {
            public float duration = 0.4f;
            public Vector3 endScale;
            private float elapsed = 0f;
            private Vector3 startScale;

            private void Start()
            {
                startScale = transform.localScale;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);
                if (t >= 1f)
                {
                    Destroy(gameObject, 0.05f); // 动画完成后销毁自身
                }
            }
        }
        /// <summary>
        /// 启用/禁用本地玩家碰撞盒（避免自伤）
        /// </summary>
        private static void SetHostHitboxes(bool state)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (!ReferenceHub.TryGetLocalHub(out ReferenceHub localHub))
            {
                return;
            }

            if (localHub.roleManager.CurrentRole is not IFpcRole fpcRole)
            {
                return;
            }

            HitboxIdentity[] hitboxes = fpcRole.FpcModule.CharacterModelInstance.Hitboxes;
            for (int i = 0; i < hitboxes.Length; i++)
            {
                hitboxes[i].SetColliders(state);
            }
        }

        /// <summary>
        /// 对刚体施加爆炸力（带遮挡检测）
        /// </summary>
        private static void ExplodeRigidbody(Rigidbody rb, Vector3 pos, float radius)
        {
            if (rb.isKinematic)
            {
                return;
            }

            // 检测爆炸点与刚体之间是否有遮挡
            if (Physics.Linecast(rb.gameObject.transform.position, pos, ThrownProjectile.HitBlockerMask))
            {
                return;
            }

            // 质量影响冲击力（质量越大受力越小）
            float massFactor = (Mathf.Clamp01(Mathf.InverseLerp(0.5f, 10f, rb.mass)) * radius) + 1f;
            float force = 10f / massFactor;

            // Unity 的 AddExplosionForce 自带距离衰减
            rb.AddExplosionForce(force, pos, radius, force, ForceMode.VelocityChange);
        }

        /// <summary>
        /// 对可破坏物体造成伤害（距离衰减 + 遮挡检测）
        /// </summary>
        private static bool ExplodeDestructible(IDestructible dest, Footprint attacker, Vector3 pos, ExplosionType explosionType, float radius)
        {
            Vector3 delta = dest.CenterOfMass - pos;
            float magnitude = delta.magnitude;

            // 防止除零 && 超出爆炸半径
            if (magnitude < 0.001f || magnitude > radius)
            {
                return false;
            }

            // 遮挡检测（从物体中心到爆炸点）
            if (Physics.Linecast(dest.CenterOfMass, pos, ThrownProjectile.HitBlockerMask))
            {
                return false;
            }

            // 伤害随距离线性衰减
            float distanceFactor = Mathf.Clamp01(1f - (magnitude / radius));
            float damage = BaseDamage * distanceFactor;

            if (damage < MinDamage)
            {
                return false;
            }

            // 计算冲击方向（带随机上方向）
            Vector3 impulse = (delta / magnitude * distanceFactor * 10f) + Vector3.up;

            // 尝试造成伤害
            bool damaged = dest.Damage(damage, new ExplosionDamageHandler(attacker, impulse, damage, 50, explosionType), dest.CenterOfMass);

            // 如果伤害成功且目标是一个 ReferenceHub（玩家）
            if (damaged && ReferenceHub.TryGetHubNetID(dest.NetworkId, out ReferenceHub targetHub))
            {
                bool isSelf = attacker.Hub == targetHub;

                // 施加负面效果（仅当攻击者不是自己，或攻击者与目标在敌对阵营）
                bool shouldApplyEffects = isSelf || HitboxIdentity.IsDamageable(attacker.Role, targetHub.GetRoleId());
                if (shouldApplyEffects)
                {
                    float duration = 0.3f;
                    float minimal = 0.2f;
                    TriggerEffect<Burned>(targetHub, duration, minimal);
                    TriggerEffect<Deafened>(targetHub, duration, minimal);
                    TriggerEffect<Concussed>(targetHub, duration, minimal);
                }

                // 若不是自伤且攻击者有效，发送命中标记
                if (!isSelf && attacker.Hub != null)
                {
                    Hitmarker.SendHitmarkerDirectly(attacker.Hub, 1f, true, HitmarkerType.Regular);
                }
            }

            return damaged;
        }

        /// <summary>
        /// 对门造成伤害（距离衰减）
        /// </summary>
        private static void ExplodeDoor(DoorVariant door, Vector3 pos, Footprint attacker, float radius)
        {
            if (door is not IDamageableDoor damageableDoor)
            {
                return;
            }

            float distance = Vector3.Distance(door.transform.position, pos);
            float factor = Mathf.Clamp01(1f - (distance / radius));
            int damage = (int)(DoorBaseDamage * factor);

            if (damage > 0)
            {
                _ = damageableDoor.ServerDamage(damage, DoorDamageType.Grenade, attacker);
            }
        }

        /// <summary>
        /// 触发状态效果（带最小持续时间检查）
        /// </summary>
        private static void TriggerEffect<T>(ReferenceHub hub, float duration, float minimal) where T : StatusEffectBase
        {
            if (duration < minimal)
            {
                return;
            }

            _ = hub.playerEffectsController.EnableEffect<T>(duration, true);
        }
        protected override void OnUsed(Player player, Item item)
        {
            if (!Check(item))
            {
                return;
            }

            base.OnUsed(player, item);
            if (GetRemainAmmo(player) == 0)
            {
                return;
            }

            Ray ray = ForwardRay(player);
            if (!Physics.Raycast(ray, out var raycastHit, 300, HitscanHitregModuleBase.HitregMask))
            {
                return;
            }
            CreateTracer(raycastHit, player);
            Map.ExplodeEffect(raycastHit.point, Exiled.API.Enums.ProjectileType.Flashbang);
            Explode(player.Footprint, raycastHit.point, ExplosionType.Grenade);
            player.Inventory.UserInventory.ReserveAmmo[ItemType.Coin] -= 1;
            player.Inventory.SendAmmoNextFrame = true;
        }
    }
}
