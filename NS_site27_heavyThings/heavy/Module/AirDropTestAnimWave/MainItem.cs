using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.API.Features.Spawn;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp1509;
using MEC;
using NS_site27_api.Modules.CustomRolePlus;
using NS_site27_heavy.heavy.Module.testing;
using PlayerRoles.PlayableScps.Scp1507;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    [CustomItem(ItemType.SCP1509)]
    internal class chixiao : CustomItemPlus
    {
        public static chixiao Ins;
        public static uint ChixiaoId = 49951;
        public override uint Id { get => ChixiaoId; set { } }
        public override string Name { get => "test-Chixiao"; set { } }
        public override string Description { get => "corn"; set { } }
        public override float Weight { get => 3f; set { } }
        public static int cuseId = 0;
        public Dictionary<int, List<Player>> HasDamage = new();
        public override SpawnProperties SpawnProperties { get => new(); set { } }
        public override void Init()
        {
            Exiled.Events.Handlers.Scp1509.TriggeringAttack += TriggeringAttack;
            Exiled.Events.Handlers.Player.ChangingItem += ChangingItem;
            Type = ItemType.SCP1509;
            Ins = this;
        }
        public override void Destroy()
        {
            Exiled.Events.Handlers.Scp1509.TriggeringAttack -= TriggeringAttack;
            Exiled.Events.Handlers.Player.ChangingItem -= ChangingItem;
            Ins = null;
            base.Destroy();
        }
        protected void ChangingItem(ChangingItemEventArgs ev)
        {
            if (!Check(ev.Player.CurrentItem))
            {
                ModelAdd.Clear(ev.Player, "chixiao");
            }
            if (Check(ev.Item))
            {
                ModelAdd.start(ev.Player, "chixiao");
            }
        }
        protected void TriggeringAttack(TriggeringAttackEventArgs ev)
        {
            if (!Check(ev.Item))
            {
                return;
            }

            var ss = new SerializableSchematic() { SchematicName = "chixiao-child", Position = ev.Player.Position, Rotation = ev.Player.Rotation.eulerAngles };
            var gb = ss.SpawnOrUpdateObject();
            if (gb.TryGetComponent<SchematicObject>(out var so))
            {
                foreach (var item1 in so.AdminToyBases)
                {
                    if (item1.name == "ren")
                    {
                        HasDamage[cuseId] = new();
                        foreach (var item2 in item1.GetComponentsInChildren<AdminToyBase>())
                        {
                            var t = item2.gameObject.AddComponent<Trigger>();
                            t.Onwer = ev.Player;
                            t.useId = cuseId;
                        }
                        cuseId++;
                    }
                    item1.NetworkMovementSmoothing = 0;
                    item1.syncInterval = 0;
                }
                _ = Timing.CallDelayed(1f, so.Destroy);
            }
        }
    }
    public class Trigger : MonoBehaviour
    {
        private BoxCollider _collider;
        public Player Onwer;
        public int useId;
        private void Start()
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
        }

        public void OnTriggerEnter(Collider other)
        {
            Player? player = Player.Get(other.gameObject);
            if (player == null || Onwer == player)
            {
                return;
            }
            if (!chixiao.Ins.HasDamage[useId].Contains(player))
            {
                if (HitboxIdentity.IsDamageable(Onwer.ReferenceHub, player.ReferenceHub))
                {
                    player.Hurt(new Scp1507DamageHandler(Onwer.Footprint, 33.4f));
                    Onwer.ShowHitMarker(1.3f);
                    chixiao.Ins.HasDamage[useId].Add(player);
                }
            }
        }
    }
}
