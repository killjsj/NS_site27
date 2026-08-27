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
        private const float BaseDamage = 500f;                  private const float DoorBaseDamage = 100f;
        private const float MinDamage = 0.1f;           
                                public static void Explode(Footprint attacker, Vector3 position, ExplosionType explosionType)
        {
                        SetHostHitboxes(true);

                        HashSet<uint> processedDestructibles = HashSetPool<uint>.Shared.Rent();
            HashSet<uint> processedDoors = HashSetPool<uint>.Shared.Rent();
            try
            {
                float radius = ExplosionRad; 
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
                                bool isServer = NetworkServer.active;

                foreach (Collider collider in hitColliders)
                {
                    if (isServer)
                    {
                                                if (collider.TryGetComponent<IExplosionTrigger>(out var trigger))
                        {
                            trigger.OnExplosionDetected(attacker, position, radius);
                        }

                                                if (collider.TryGetComponent<IDestructible>(out var destructible))
                        {
                            if (!processedDestructibles.Contains(destructible.NetworkId) &&
                                ExplodeDestructible(destructible, attacker, position, explosionType, radius))
                            {
                                _ = processedDestructibles.Add(destructible.NetworkId);
                            }
                        }
                                                else if (collider.TryGetComponent<InteractableCollider>(out var interactable) &&
                                 interactable.Target is DoorVariant door &&
                                 processedDoors.Add(door.netId))
                        {
                            ExplodeDoor(door, position, attacker, radius);
                        }
                    }

                                        if (collider.attachedRigidbody != null)
                    {
                        ExplodeRigidbody(collider.attachedRigidbody, position, radius);
                    }
                }
            }
            finally
            {
                                HashSetPool<uint>.Shared.Return(processedDestructibles);
                HashSetPool<uint>.Shared.Return(processedDoors);
            }

            SetHostHitboxes(false);
        }
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
                    Destroy(gameObject, 0.05f);                 }
            }
        }
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

                                private static void ExplodeRigidbody(Rigidbody rb, Vector3 pos, float radius)
        {
            if (rb.isKinematic)
            {
                return;
            }

                        if (Physics.Linecast(rb.gameObject.transform.position, pos, ThrownProjectile.HitBlockerMask))
            {
                return;
            }

                        float massFactor = (Mathf.Clamp01(Mathf.InverseLerp(0.5f, 10f, rb.mass)) * radius) + 1f;
            float force = 10f / massFactor;

                        rb.AddExplosionForce(force, pos, radius, force, ForceMode.VelocityChange);
        }

                                private static bool ExplodeDestructible(IDestructible dest, Footprint attacker, Vector3 pos, ExplosionType explosionType, float radius)
        {
            Vector3 delta = dest.CenterOfMass - pos;
            float magnitude = delta.magnitude;

                        if (magnitude < 0.001f || magnitude > radius)
            {
                return false;
            }

                        if (Physics.Linecast(dest.CenterOfMass, pos, ThrownProjectile.HitBlockerMask))
            {
                return false;
            }

                        float distanceFactor = Mathf.Clamp01(1f - (magnitude / radius));
            float damage = BaseDamage * distanceFactor;

            if (damage < MinDamage)
            {
                return false;
            }

                        Vector3 impulse = (delta / magnitude * distanceFactor * 10f) + Vector3.up;

                        bool damaged = dest.Damage(damage, new ExplosionDamageHandler(attacker, impulse, damage, 50, explosionType), dest.CenterOfMass);

                        if (damaged && ReferenceHub.TryGetHubNetID(dest.NetworkId, out ReferenceHub targetHub))
            {
                bool isSelf = attacker.Hub == targetHub;

                                bool shouldApplyEffects = isSelf || HitboxIdentity.IsDamageable(attacker.Role, targetHub.GetRoleId());
                if (shouldApplyEffects)
                {
                    float duration = 0.3f;
                    float minimal = 0.2f;
                    TriggerEffect<Burned>(targetHub, duration, minimal);
                    TriggerEffect<Deafened>(targetHub, duration, minimal);
                    TriggerEffect<Concussed>(targetHub, duration, minimal);
                }

                                if (!isSelf && attacker.Hub != null)
                {
                    Hitmarker.SendHitmarkerDirectly(attacker.Hub, 1f, true, HitmarkerType.Regular);
                }
            }

            return damaged;
        }

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
