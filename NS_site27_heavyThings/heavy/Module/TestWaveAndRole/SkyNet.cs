using CustomPlayerEffects;
using CustomRendering;
using DrawableLine;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using NS_site27_api.Modules.Abilities;
using PlayerRoles.Subroutines;
using UnityEngine;
using Utils.Networking;
namespace NS_site27_heavy.heavy.Module.TestWaveAndRole
{
    public class DebuggersAbility1 : KeyAbility
    {
        public override KeyCode KeyCode => KeyCode.Mouse3;

        public override string Name => "房间放毒";

        public override string Des => "房间放毒7秒";

        public override int id => 106;
        public override double Time => 120;
        public override float WaitForDoneTime => 7;
        public static readonly CachedLayerMask HitregMask = new(new string[]
{
            "Default",
            "Hitbox",
            "Glass",
            "CCTV",
            "Door"
});
        public override bool OnTrigger()
        {
            var r = new Ray(player.CameraTransform.position + (player.CameraTransform.forward * 0.8f), player.CameraTransform.forward);
            if (Physics.Raycast(r, out var raycast, 45, HitregMask.Mask))
            {
                var o = Room.Get(raycast.point);
                if (o && o.Type != RoomType.Surface)
                {
                    o.LockDown(7);
                    foreach (var item in o.Players)
                    {
                        item.EnableEffect(EffectType.Decontaminating, 7f);
                        _ = item.EnableEffect<FogControl>(7f);

                        item.GetEffect<FogControl>().SetFogType(FogType.Decontamination);
                    }
                }
                else
                {
                    return false;
                }
            }
            return true;
        }
        internal DebuggersAbility1(Player player) : base(player)
        {
            TotalCount = 1;
        }
        public DebuggersAbility1() : base()
        {
            TotalCount = 1;
        }
    }
    public class DebuggersAbility2 : KeyAbility
    {
        public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "房间锁门";

        public override string Des => "指向目标锁定7秒";

        public override int id => 104;
        public override double Time => 75;
        public override float WaitForDoneTime => 12;
        public static readonly CachedLayerMask HitregMask = new(new string[]
{
            "Default",
            "Hitbox",
            "Glass",
            "CCTV",
            "Door"
});
        public override bool OnTrigger()
        {
            var r = new Ray(player.CameraTransform.position + (player.CameraTransform.forward * 0.8f), player.CameraTransform.forward);
            if (Physics.Raycast(r, out var raycast, 45, HitregMask.Mask))
            {
                var o = Room.Get(raycast.point);
                if (o && o.Type != RoomType.Surface)
                {
                    o.Blackout(12);
                    return true;
                }
                else
                {
                    var d = Door.GetClosest(raycast.point, out float distance);
                    if (d != null && distance <= 3f)
                    {
                        d.Lock(12f, DoorLockType.Regular079);
                        return true;
                    }
                }
            }
            return false;
        }
        internal DebuggersAbility2(Player player) : base(player)
        {
            TotalCount = 1;
        }
        public DebuggersAbility2() : base()
        {
            TotalCount = 1;
        }
    }

    public class TPAbility : KeyAbility
    {
        public override KeyCode KeyCode => KeyCode.Mouse1;

        public override string Name => "传送";

        public override string Des => "45m内传送";

        public override int id => 102;
        public override double Time => 3;
        public static readonly CachedLayerMask HitregMask = new(new string[]
{
            "Default",
            "Hitbox",
            "Glass",
            "CCTV",
            "Door"
});

        public override bool OnTrigger()
        {
            var r = new Ray(player.CameraTransform.position + (player.CameraTransform.forward * 0.8f), player.CameraTransform.forward);
            if (Physics.Raycast(r, out var raycast, 45, HitregMask.Mask))
            {
                if (raycast.collider.TryGetComponent<IDestructible>(out var destructible))
                {
                    //destructibles.Add(destructible);
                    if (destructible is HitboxIdentity HI)
                    {
                        var p = Player.Get(HI.TargetHub);
                        if (p == player)
                        {
                            return false;
                        }
                        (player.Position, p.Position) = (p.Position, player.Position);
                        return true;
                    }
                    else
                    {
                        player.Position = raycast.point + (raycast.normal * 0.3f);
                        return true;
                    }

                }
                else
                {
                    player.Position = raycast.point + (raycast.normal * 0.3f);
                    return true;
                }
            }
            return false;
        }
        internal TPAbility(Player player) : base(player)
        {
            TotalCount = 3;
        }
        public TPAbility() : base()
        {
            TotalCount = 3;
        }
    }
    public class DebuggersAbility3 : PassAbility, ITiming
    {
        //public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "范围扫描";

        public override string Des => "扫描周围35m内的敌人";

        public override int id => 105;
        //public override double Time => 75;
        //public override float WaitForDoneTime => 12;
        public static AbilityCooldown cd = new();
        public override AbilityBase Register(Player player)
        {
            var a = new DebuggersAbility3(player);
            a.InternalRegister(player);
            return a;
        }

        float ITiming.CoolDownRemaining { get => cd.Remaining; set => cd.Remaining = value; }
        float ITiming.DoneRemaining { get => 0; set { } }

        bool ITiming.Done => true;

        public override void OnCheck(Player player)
        {
            //base.OnCheck(player);
            if (cd.IsReady)
            {
                cd.Trigger(2.5);
                foreach (var p in Player.Enumerable)
                {
                    if (player != p && Vector3.Distance(player.Position, p.Position) <= 35f)
                    {
                        if (HitboxIdentity.IsEnemy(player.ReferenceHub, p.ReferenceHub))
                        {
                            new DrawableLineMessage(0.7f, Color.red * new Color(1, 1, 1, 1 - (Vector3.Distance(player.Position, p.Position) / 150) + 0.01f), new Vector3[2] { p.CameraTransform.position + (0.2f * Vector3.down), player.Position }).SendToHubsConditionally(x => x == player.ReferenceHub);
                        }
                    }
                }
            }
        }
        internal DebuggersAbility3(Player player) : base(player)
        {
            //TotalCount = 1;
        }
        public DebuggersAbility3() : base()
        {
            //TotalCount = 1;
        }
    }
    public class TestAbility1 : ItemKeyAbility
    {
        //public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "物品-伤害";

        public override string Des => "对周围敌人造成伤害50f";

        public override int id => 188;
        public override double Time => 3;
        public override int TotalCount { get; set; } = 6;
        public override float WaitForDoneTime => 0;
        public static AbilityCooldown cd = new();

        public override KeyCode KeyCode => KeyCode.Mouse0;

        public override bool OnTrigger()
        {
            foreach (var p in Player.Enumerable)
            {
                if (player != p && Vector3.Distance(player.Position, p.Position) <= 35f)
                {
                    if (HitboxIdentity.IsEnemy(player.ReferenceHub, p.ReferenceHub))
                    {
                        p.Hurt(50f, DamageType.A7);
                    }
                }
            }
            return true;
        }
        public TestAbility1() : base()
        {
            //TotalCount = 1;
        }
    }


    public class TestAbility2 : ItemKeyAbility
    {
        //public override KeyCode KeyCode => KeyCode.Mouse2;

        public override string Name => "物品-扫描";

        public override string Des => "扫描周围35m内的所有人";

        public override int id => 189;
        public override double Time => 10;
        public override int TotalCount { get; set; } = 6;
        public override float WaitForDoneTime => 0;
        public static AbilityCooldown cd = new();

        public override KeyCode KeyCode => KeyCode.Mouse0;

        public override bool OnTrigger()
        {
            foreach (var p in Player.Enumerable)
            {
                if (player != p && Vector3.Distance(player.Position, p.Position) <= 35f)
                {
                    //if (HitboxIdentity.IsEnemy(player.ReferenceHub, p.ReferenceHub))
                    {
                        new DrawableLineMessage(0.5f, Color.yellow * new Color(1, 1, 1, 1 - (Vector3.Distance(player.Position, p.Position) / 150) + 0.01f), new Vector3[2] { p.CameraTransform.position + (0.2f * Vector3.down), player.Position }).SendToHubsConditionally(x => x == player.ReferenceHub);
                    }
                }
            }
            return true;
        }
        public TestAbility2() : base()
        {
            //TotalCount = 1;
        }
    }
}
