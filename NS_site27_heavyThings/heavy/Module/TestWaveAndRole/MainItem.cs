using Exiled.API.Features.Attributes;
using Exiled.API.Features.Spawn;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerStatsSystem;

namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    [CustomItem(ItemType.Coin)]
    internal class WhipS : CustomItemPlus
    {
        public static WhipS Ins;
        public static uint WhipId = 411;
        public override uint Id { get => WhipId; set { } }
        public override string Name { get => "test-"; set { } }
        public override string Description { get => "corn"; set { } }
        public override float Weight { get => 3f; set { } }

        public override SpawnProperties SpawnProperties { get => new(); set { } }
        public override void Init()
        {
            Type = ItemType.Coin;
            abilities.Add(new TestAbility1());
            Ins = this;
        }
        public override void Destroy()
        {
            Ins = null;
            base.Destroy();
        }
        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.Hurting += OnHurt;
            base.SubscribeEvents();
        }
        protected override void UnsubscribeEvents()
        {
            base.UnsubscribeEvents();
            Exiled.Events.Handlers.Player.Hurting -= OnHurt;

        }
        public void OnHurt(HurtingEventArgs ev)
        {
            if (ev.DamageHandler.Base is JailbirdDamageHandler)
            {
                if (ev.Player.CurrentItem != null && Check(ev.Player.CurrentItem))
                {
                    ev.Amount = 2;
                }
            }

        }
            }
    [CustomItem(ItemType.ArmorLight)]
    internal class AM : CustomArmor
    {
        public static AM Ins;
        public static uint AMId = 12333;
        public override uint Id { get => AMId; set { } }
        public override string Name { get => "test-amror"; set { } }
        public override string Description { get => "123"; set { } }
        public override float Weight { get => 3f; set { } }

        public override SpawnProperties SpawnProperties { get => new(); set { } }
        public override void Init()
        {
            Type = ItemType.ArmorLight;
            abilities.Add(new TestAbility2());
            base.Init();
            Ins = this;
        }
        public override void Destroy()
        {
            Ins = null;
            base.Destroy();
        }
        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.Hurting += OnHurt;
            base.SubscribeEvents();
        }
        protected override void UnsubscribeEvents()
        {
            base.UnsubscribeEvents();
            Exiled.Events.Handlers.Player.Hurting -= OnHurt;

        }
        public void OnHurt(HurtingEventArgs ev)
        {
        }
            }
}
