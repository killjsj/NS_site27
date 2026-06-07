using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.API.Features.Items;
using MEC;
using NS_site27_api.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using Scp914Handlers = Exiled.Events.Handlers.Scp914;
using Item = Exiled.API.Features.Items.Item;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Scp914;

namespace NS_site27_api.Modules.Weapons.BombGun
{
    public class BombGunConfig : ModuleConfigBase
    {
        public int MaxBombs { get; set; } = 100;
        public float ShootForce { get; set; } = 25f;
    }

    public class BombGunModule : ModuleBase<BombGunConfig>
    {
        public override string ModuleName => "BombGun";
        public static List<ushort> BombGunSerials = new List<ushort>();
        public static int ActiveGrenades = 0;
        public static BombGunModule Ins { get; private set;  }
        public static int MaxActiveGrenades => Ins?.Config.MaxBombs + 200 ?? 300;

        public BombHandle Handler = new BombHandle();

        public override void OnEnable()
        {
            Ins = this;
            Exiled.Events.Handlers.Player.Shot += Handler.OnPlayerShotWeapon;
            Exiled.Events.Handlers.Scp914.UpgradingPickup += Handler.OnUpgradingPickup;
            Exiled.Events.Handlers.Scp914.UpgradingInventoryItem += Handler.OnUpgradingInventoryItem;
        }

        public override void OnDisable()
        {
            Ins = null;
            Exiled.Events.Handlers.Player.Shot -= Handler.OnPlayerShotWeapon;
            Exiled.Events.Handlers.Scp914.UpgradingPickup -= Handler.OnUpgradingPickup;
            Exiled.Events.Handlers.Scp914.UpgradingInventoryItem -= Handler.OnUpgradingInventoryItem;
            BombGunSerials.Clear();
        }
    }

    public class BombHandle
    {
        public void OnPlayerShotWeapon(ShotEventArgs ev)
        {
            var player = ev.Player;
            var gun = ev.Firearm;

            if (!BombGunModule.BombGunSerials.Contains(gun.Serial))
                return;

            if (BombGunModule.ActiveGrenades >= BombGunModule.MaxActiveGrenades)
            {
                player.ShowHint("Server Max Grenade", 3);
                return;
            }

            var pos = player.CameraTransform.position + player.CameraTransform.forward * UnityEngine.Random.value;
            var grenade = Item.Create(ItemType.GrenadeHE);
            var pickup = grenade.CreatePickup(pos, Quaternion.identity, true) as GrenadePickup;

            if (pickup?.Rigidbody != null)
            {
                pickup.Rigidbody.AddForce(player.CameraTransform.forward * 25, ForceMode.Impulse);
                BombGunModule.ActiveGrenades++;
            }
        }

        public void OnUpgradingPickup(UpgradingPickupEventArgs ev)
        {
            if (ev.Pickup != null && BombGunModule.BombGunSerials.Contains(ev.Pickup.Serial))
                ev.IsAllowed = false;
        }

        public void OnUpgradingInventoryItem(UpgradingInventoryItemEventArgs ev)
        {
            if (ev.Item != null && BombGunModule.BombGunSerials.Contains(ev.Item.Serial))
                ev.IsAllowed = false;
        }

        public static void RegisterGun(Item gun)
        {
            if (!BombGunModule.BombGunSerials.Contains(gun.Serial))
                BombGunModule.BombGunSerials.Add(gun.Serial);
        }

        public static void RegisterGun(Pickup gun)
        {
            if (!BombGunModule.BombGunSerials.Contains(gun.Serial))
                BombGunModule.BombGunSerials.Add(gun.Serial);
        }
    }
}
