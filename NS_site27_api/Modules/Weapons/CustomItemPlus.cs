using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.API.Structs;
using Exiled.CustomItems.API.Features;
using Exiled.CustomRoles.API.Features;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Features;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using MEC;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.Abilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
namespace NS_site27_api.Modules.Weapons
{
    public interface IRegisiterNeeded<T> where T : AbilityBase
    {
        public T Register(Player player);
        public void Unregister(Player player);
    }
    public interface IitemRegisiterNeeded<T>
    {
        public T Register(ushort serial);
        public void Unregister(ushort serial);
    }
    public abstract class CustomRolePlus : CustomRole
    {
        // 所有玩家共享的能力模板
        public List<AbilityBase> abilities = new List<AbilityBase>();

        // 每个玩家自己的技能实例
        public static string[] abilitiesShower(Player p)
        {
            string showing = "<size=24>";
            if (p != null)
            {
                if (AbilityBase.PlayerAbilities.TryGetValue(p, out var list))
                {
                    foreach (var item in list)
                    {
                        var N = item.Name;
                        var CustomInfo = item.CustomInfoToShow;
                        bool show = false;
                        string nS = $"{N}: ";
                        if (item is ICounted CDA)
                        {
                            var Count = CDA.count;
                            var TotalCount = CDA.TotalCount;
                            nS += $"<color={(Count == 0 ? "red" : "green")}>{Count}</color>/{TotalCount} ";
                            show = true;
                        }
                        if (item is ITiming timing)
                        {
                            var RemainTime = timing.CoolDownRemaining;
                            var SkillRemainTime = timing.DoneRemaining;
                            nS += $"{(!timing.Done ? "还剩下:" : "冷却:")}{(!timing.Done ? SkillRemainTime : RemainTime):F0}s ";
                            show = true;
                        }
                        if (!string.IsNullOrEmpty(item.CustomInfoToShow))
                        {
                            show = true;
                            nS += $"{CustomInfo}";
                        }
                        nS += "\n";
                        if (show)
                        {
                            showing += nS;
                        }
                    }
                }
            }
            showing += "</size>";
            return new[] { showing };
        }

        public static string[] RoleShower(Player p)
        {
            string showing = "<size=24>";
            if (p != null)
            {
                if (!string.IsNullOrEmpty(p.UniqueRole))
                {
                    var r = CustomRole.Get(p.UniqueRole);
                    showing += $"你是: {p.UniqueRole}\n{r.Description}\n";
                }
                if (CustomItemPlus.PlayerItems.ContainsKey(p))
                {
                    showing += "物品:\n";
                    foreach (var item in CustomItemPlus.PlayerItems[p])
                    {
                        var c = item.Item2;
                        if (c != null)
                        {
                            if (p.CurrentItem != null && item.Item1 == p.CurrentItem.Serial)
                            {
                                showing += $">{c.Name}:{c.Description}\n";
                            }
                            else
                            {
                                showing += $"{c.Name}\n";
                            }
                        }
                    }
                }

            }
            showing += "</size>";
            return new[] { showing };
        }

        protected override void ShowMessage(Player player)
        {
            player.AddMessage("CustomRolePlus_Role", RoleShower, -1, UIPosition.FromXY(700, 700));
        }

        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);

            if (abilities == null || abilities.Count == 0)
                return;

            player.AddMessage("CustomRolePlus_Abilities", abilitiesShower, -1, UIPosition.FromXY(400, 400));

            foreach (var template in abilities)
            {
                AbilityBase instance = template;

                if (template is IRegisiterNeeded<AbilityBase> reg)
                {
                    instance = reg.Register(player);
                }

                if (!AbilityBase.PlayerAbilities.TryGetValue(player, out var list1))
                {
                    list1 = new List<AbilityBase>();
                    AbilityBase.PlayerAbilities[player] = list1;
                }

                list1.Add(instance);
            }
        }

        protected override void RoleRemoved(Player player)
        {
            base.RoleRemoved(player);

            if (!AbilityBase.PlayerAbilities.TryGetValue(player, out var list))
                return;
            List<AbilityBase> ab = new();
            foreach (var ability in list)
            {
                if (ability is IRegisiterNeeded<AbilityBase> reg)
                {
                    reg.Unregister(player);

                }
                ab.Add(ability);
            }
            list.RemoveAll(x => ab.Any(y => y.id == x.id));
            if (list.Count <= 0)
            {
                player.RemoveMessage("CustomRolePlus_Abilities");
            }
        }
    }
    public static class CustomItemEx
    {
        public static CustomItemPlus GetItemsCustom(this Item item)
        {
            if (item == null) return null;
            if (CustomItemPlus.ItemMapping.TryGetValue(item, out var map))
            {
                return map;
            }
            return null;
        }
    }
    public abstract class CustomWeapon : CustomItemPlus
    {
        //
        // 摘要:
        //     Gets or sets value indicating what InventorySystem.Items.Firearms.Attachments.Components.Attachments
        //     the weapon will have.
        public virtual AttachmentName[] Attachments { get; set; } = new AttachmentName[0];

        public override ItemType Type
        {
            get
            {
                return base.Type;
            }
            set
            {
                if (!value.IsWeapon(checkNonFirearm: false) && value != ItemType.None)
                {
                    throw new ArgumentOutOfRangeException("Type", value, "Invalid weapon type.");
                }

                base.Type = value;
            }
        }

        //
        // 摘要:
        //     Gets or sets the weapon damage.
        public virtual float Damage { get; set; } = -1f;

        //
        // 摘要:
        //     Gets or sets a value indicating how big of a clip the weapon will have.
        //
        // 言论：
        //     Warning for ItemType.GunShotgun and ItemType.GunRevolver. They are not fully
        //     compatible with this features.
        public virtual byte ClipSize { get; set; }

        //
        // 摘要:
        //     Gets or sets a value indicating whether to allow friendly fire with this weapon
        //     on FF-enabled servers.
        public virtual bool FriendlyFire { get; set; }

        public override Pickup? Spawn(Vector3 position, Exiled.API.Features.Player? previousOwner = null)
        {
            if (!(Exiled.API.Features.Items.Item.Create(Type) is Firearm firearm))
            {
                Log.Debug("Spawn: Item is not Firearm.");
                return null;
            }

            if (!Attachments.IsEmpty())
            {
                firearm.AddAttachment(Attachments);
            }

            Pickup pickup = firearm.CreatePickup(position);
            if (pickup == null)
            {
                Log.Debug("Spawn: Pickup is null.");
                return null;
            }

            if (ClipSize > 0)
            {
                firearm.MagazineAmmo = ClipSize;
            }

            pickup.Weight = Weight;
            pickup.Scale = Scale;
            if ((object)previousOwner != null)
            {
                pickup.PreviousOwner = previousOwner;
            }

            base.TrackedSerials.Add(pickup.Serial);
            return pickup;
        }

        public override Pickup? Spawn(Vector3 position, Exiled.API.Features.Items.Item item, Exiled.API.Features.Player? previousOwner = null)
        {
            if (item is Firearm firearm)
            {
                if (!Attachments.IsEmpty())
                {
                    firearm.AddAttachment(Attachments);
                }

                if (ClipSize > 0)
                {
                    firearm.MagazineAmmo = ClipSize;
                }

                int magazineAmmo = firearm.MagazineAmmo;
                Log.Debug(string.Format("{0}.{1}: Spawning weapon with {2} ammo.", "Name", "Spawn", magazineAmmo));
                Pickup pickup = firearm.CreatePickup(position);
                pickup.Scale = Scale;
                if ((object)previousOwner != null)
                {
                    pickup.PreviousOwner = previousOwner;
                }

                base.TrackedSerials.Add(pickup.Serial);
                return pickup;
            }

            return base.Spawn(position, item, previousOwner);
        }

        public override void Give(Exiled.API.Features.Player player, bool displayMessage = true)
        {
            Exiled.API.Features.Items.Item item = player.AddItem(Type);
            if (item is Firearm firearm)
            {
                if (!Attachments.IsEmpty())
                {
                    firearm.AddAttachment(Attachments);
                }

                if (ClipSize > 0)
                {
                    firearm.MagazineAmmo = ClipSize;
                }
            }

            Log.Debug(string.Format("{0}: Adding {1} to tracker.", "Give", item.Serial));
            base.TrackedSerials.Add(item.Serial);
            OnAcquired(player, item, displayMessage);
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ReloadingWeapon += new CustomEventHandler<ReloadingWeaponEventArgs>(OnInternalReloading);
            Exiled.Events.Handlers.Player.ReloadedWeapon += new CustomEventHandler<ReloadedWeaponEventArgs>(OnInternalReloaded);
            Exiled.Events.Handlers.Player.Shooting += new CustomEventHandler<ShootingEventArgs>(OnInternalShooting);
            Exiled.Events.Handlers.Player.Shot += new CustomEventHandler<ShotEventArgs>(OnInternalShot);
            Exiled.Events.Handlers.Player.Hurting += new CustomEventHandler<HurtingEventArgs>(OnInternalHurting);
            Exiled.Events.Handlers.Item.ChangingAttachments += new CustomEventHandler<ChangingAttachmentsEventArgs>(OnInternalChangingAttachment);
            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ReloadingWeapon -= new CustomEventHandler<ReloadingWeaponEventArgs>(OnInternalReloading);
            Exiled.Events.Handlers.Player.ReloadedWeapon -= new CustomEventHandler<ReloadedWeaponEventArgs>(OnInternalReloaded);
            Exiled.Events.Handlers.Player.Shooting -= new CustomEventHandler<ShootingEventArgs>(OnInternalShooting);
            Exiled.Events.Handlers.Player.Shot -= new CustomEventHandler<ShotEventArgs>(OnInternalShot);
            Exiled.Events.Handlers.Player.Hurting -= new CustomEventHandler<HurtingEventArgs>(OnInternalHurting);
            Exiled.Events.Handlers.Item.ChangingAttachments -= new CustomEventHandler<ChangingAttachmentsEventArgs>(OnInternalChangingAttachment);
            base.UnsubscribeEvents();
        }

        //
        // 摘要:
        //     Handles reloading for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Player.ReloadingWeaponEventArgs.
        protected virtual void OnReloading(ReloadingWeaponEventArgs ev)
        {
        }

        //
        // 摘要:
        //     Handles reloaded for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Player.ReloadedWeaponEventArgs.
        protected virtual void OnReloaded(ReloadedWeaponEventArgs ev)
        {
        }

        //
        // 摘要:
        //     Handles shooting for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Player.ShootingEventArgs.
        protected virtual void OnShooting(ShootingEventArgs ev)
        {
        }

        //
        // 摘要:
        //     Handles shot for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Player.ShotEventArgs.
        protected virtual void OnShot(ShotEventArgs ev)
        {
        }

        //
        // 摘要:
        //     Handles hurting for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Player.HurtingEventArgs.
        protected virtual void OnHurting(HurtingEventArgs ev)
        {
            if (ev.IsAllowed && Damage >= 0f)
            {
                ev.Amount = Damage;
            }
        }

        //
        // 摘要:
        //     Handles attachment changing for custom weapons.
        //
        // 参数:
        //   ev:
        //     Exiled.Events.EventArgs.Item.ChangingAttachmentsEventArgs.
        protected virtual void OnChangingAttachment(ChangingAttachmentsEventArgs ev)
        {
        }

        private void OnInternalReloading(ReloadingWeaponEventArgs ev)
        {
            if (Check(ev.Item))
            {
                if (ClipSize > 0 && ev.Firearm.TotalAmmo >= ClipSize)
                {
                    ev.IsAllowed = false;
                }
                else
                {
                    OnReloading(ev);
                }
            }
        }

        private void OnInternalReloaded(ReloadedWeaponEventArgs ev)
        {
            if (!Check(ev.Item))
            {
                return;
            }

            if (ClipSize > 0)
            {
                int num = ((AutomaticActionModule)ev.Firearm.Base.Modules.FirstOrDefault((ModuleBase x) => x is AutomaticActionModule))?.AmmoStored ?? 0;
                int num2 = ClipSize - num;
                AmmoType ammoType = ev.Firearm.AmmoType;
                int magazineAmmo = ev.Firearm.MagazineAmmo;
                int num3 = -(ClipSize - magazineAmmo - num);
                int num4 = ev.Player.GetAmmo(ammoType) + magazineAmmo;
                if (num2 < num4)
                {
                    ev.Firearm.MagazineAmmo = num2;
                    int num5 = ev.Player.GetAmmo(ammoType) + num3;
                    ev.Player.SetAmmo(ammoType, (ushort)num5);
                }
                else
                {
                    ev.Firearm.MagazineAmmo = num4;
                    ev.Player.SetAmmo(ammoType, 0);
                }
            }

            OnReloaded(ev);
        }

        private void OnInternalShooting(ShootingEventArgs ev)
        {
            if (Check(ev.Item))
            {
                OnShooting(ev);
            }
        }

        private void OnInternalShot(ShotEventArgs ev)
        {
            if (Check(ev.Item))
            {
                OnShot(ev);
            }
        }

        private void OnInternalHurting(HurtingEventArgs ev)
        {
            if ((object)ev.Attacker != null)
            {
                FirearmDamageHandler param;
                if ((object)ev.Player == null)
                {
                    //Log.Debug(Name + ": OnInternalHurting: target null");
                }
                else if (!Check(ev.Attacker.CurrentItem))
                {
                    //Log.Debug(Name + ": OnInternalHurting: !Check()");
                }
                else if (ev.Attacker == ev.Player)
                {
                    //Log.Debug(Name + ": OnInternalHurting: attacker == target");
                }
                else if (ev.DamageHandler == null)
                {
                    //Log.Debug(Name + ": OnInternalHurting: Handler null");
                }
                else if (!ev.DamageHandler.CustomBase.BaseIs<FirearmDamageHandler>(out param))
                {
                    Log.Debug(Name + ": OnInternalHurting: Handler not firearm");
                }
                else if (!Check(param.Item))
                {
                    Log.Debug(Name + ": OnInternalHurting: type != type");
                }
                else if (!FriendlyFire && ev.Attacker.Role.Team == ev.Player.Role.Team)
                {
                    Log.Debug(Name + ": OnInternalHurting: FF is disabled for this weapon!");
                }
                else
                {
                    OnHurting(ev);
                }
            }
        }

        private void OnInternalChangingAttachment(ChangingAttachmentsEventArgs ev)
        {
            if (Check(ev.Player.CurrentItem))
            {
                OnChangingAttachment(ev);
            }
        }
    }
    public abstract class CustomItemPlus : CustomItem
    {
        public List<ItemAbilityBase> abilities = new List<ItemAbilityBase>();

        public static Dictionary<Player, List<(ushort, CustomItemPlus)>> PlayerItems = new Dictionary<Player, List<(ushort, CustomItemPlus)>>();
        public static Dictionary<Item, CustomItemPlus> ItemMapping = new();
        public static Dictionary<Player, List<AbilityBase>> PlayerAbilities => AbilityBase.PlayerAbilities;
        public static Dictionary<ushort, List<ItemAbilityBase>> ItemAbilities => ItemAbilityBase.ItemABs;

        protected void RestartRound()
        {
            ItemMapping.Clear();
            PlayerAbilities.Clear();
            ItemAbilities.Clear();
            PlayerItems.Clear();
        }
        protected void OnFlipingCoin(PlayerFlippedCoinEventArgs ev)
        {
            OnUsed(ev.Player, Item.Get(ev.CoinItem.Base));
        }
        protected void OnUsingItem(PlayerUsedItemEventArgs ev)
        {
            OnUsed(ev.Player, Item.Get(ev.UsableItem.Base));
        }
        protected virtual void OnUsed(Player player,Item item)
        {

        }

        protected override void SubscribeEvents()
        {
            base.SubscribeEvents();
            ItemPickupBase.OnPickupDestroyed += ItemPickupBase_OnPickupDestroyed;
            //ItemPickupBase.OnPickupDestroyed += ItemPickupBase_OnPickupDestroyed;
            Exiled.Events.Handlers.Server.RestartingRound += RestartRound;
            //ItemPickupBase.OnPickupDestroyed += ItemPickupBase_OnPickupDestroyed;
            InventoryExtensions.OnItemRemoved += ItemBase_OnItemRemoved;
            PlayerEvents.PickingUpItem += PlayerEvents_PickingUpItem;
            PlayerEvents.UsedItem += OnUsingItem;
            PlayerEvents.FlippedCoin += OnFlipingCoin;
        }
        protected override void UnsubscribeEvents()
        {
            base.UnsubscribeEvents();
            ItemPickupBase.OnPickupDestroyed -= ItemPickupBase_OnPickupDestroyed;
            Exiled.Events.Handlers.Server.RestartingRound -= RestartRound;
            //ItemPickupBase.OnPickupDestroyed += ItemPickupBase_OnPickupDestroyed;
            InventoryExtensions.OnItemRemoved -= ItemBase_OnItemRemoved;
            PlayerEvents.PickingUpItem -= PlayerEvents_PickingUpItem;
            PlayerEvents.UsedItem -= OnUsingItem;
            PlayerEvents.FlippedCoin -= OnFlipingCoin;
        }
        private static List<ushort> PickedItem = new();
        private void PlayerEvents_PickingUpItem(LabApi.Events.Arguments.PlayerEvents.PlayerPickingUpItemEventArgs ev)
        {
            PickedItem.Add(ev.Pickup.Serial);
                RefreshPlayersItems(Player.Get(ev.Player));
        }

        private void ItemBase_OnItemRemoved(ReferenceHub hub, ItemBase it, ItemPickupBase itb)
        {
            if (itb != null) return;
            if (it == null) return;
            OnDestroyedInternal(it.ItemId.SerialNumber, hub);

                RefreshPlayersItems(Player.Get(hub));
        }
        protected override void ShowSelectedMessage(Player player)
        {
            player.AddMessage("CustomRolePlus_Role", CustomRolePlus.RoleShower, -1, UIPosition.FromEnum(ScreenPosition.Center));
        }
        private void ItemPickupBase_OnPickupDestroyed(ItemPickupBase obj)
        {
            if (PickedItem.Contains(obj.ItemId.SerialNumber))
            {
                PickedItem.Remove(obj.ItemId.SerialNumber);
                return;
            }
            OnDestroyedInternal(obj.ItemId.SerialNumber);
        }

        protected virtual void OnDestroyedInternal(ushort serial, ReferenceHub referenceHub = null)
        {
            if (TrackedSerials.Contains(serial))
            {
                var player = Player.Get(referenceHub);
                RefreshPlayersItems(player);
                if (!ItemAbilities.TryGetValue(serial, out var list1))
                    return;

                List<ItemAbilityBase> ab = new();
                foreach (var ability in list1)
                {
                    if (ability is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                    {
                        reg1.Unregister(serial);

                    } 
                    ab.Add(ability);
                }
                list1.RemoveAll(x => ab.Any(y => y.id == x.id));
                if (player != null)
                {
                    if (!PlayerAbilities.TryGetValue(player, out var list))
                        return;
                    foreach (var ability in list.Where(x => ab.Contains(x)))
                    {
                        if (ability is IRegisiterNeeded<ItemAbilityBase> reg1)
                        {
                            reg1.Unregister(player);
                        }
                    }
                    list.RemoveAll(x => ab.Any(y => y.id == x.id));

                }
                ItemAbilities.Remove(serial);
                OnDestroyed(serial, player);
                TrackedSerials.Remove(serial);

            }

        }
        public static void RefreshPlayersItems(Player player)
        {
            if (!PlayerItems.TryGetValue(player, out var list))
            {
                list = new();
                PlayerItems[player] = list;
            }
            else
            {
                list.Clear();
            }
            foreach (var item in player.Items)
            {
                var c = item.GetItemsCustom();
                if (c != null)
                {
                    list.Add((item.Serial, c));
                }
            }
        }
        protected virtual void OnDestroyed(ushort serial, Player player = null) { }
        protected override void OnAcquired(Player player, Item item, bool displayMessage)
        {
            base.OnAcquired(player, item, displayMessage);
            ItemMapping[item] = this;
            RefreshPlayersItems(player);

            if (abilities == null || abilities.Count == 0)
                return;

            //item.Destroy
            player.AddMessage("CustomRolePlus_Abilities", CustomRolePlus.abilitiesShower, -1, UIPosition.FromXY(400, 400));
            player.AddMessage("CustomRolePlus_Role", CustomRolePlus.RoleShower, -1, UIPosition.FromXY(700, 700));

            if (!ItemAbilities.TryGetValue(item.Serial, out var list))
            {
                list = new List<ItemAbilityBase>();
                ItemAbilities[item.Serial] = list;
                foreach (var template in abilities)
                {
                    // 复制或注册单独实例
                    ItemAbilityBase instance = template;


                    if (template is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                    {
                        instance = reg1.Register(item.Serial);
                    }
                    else if (template is IRegisiterNeeded<ItemAbilityBase> reg)
                    {
                        instance = reg.Register(player);
                    }

                    list.Add(instance);
                }
            }
            Log.Info($"ItemAbilities Count: {list.Count}");
            PlayerAbilities[player].AddRange(list);



        }

        protected override void OnDroppingItem(DroppingItemEventArgs ev)
        {
            base.OnDroppingItem(ev);
            if (Check(ev.Item))
            {
                RefreshPlayersItems(ev.Player);

                foreach (var item in ev.Player.Items)
                {
                    if (item.Serial != ev.Item.Serial)
                    {
                        if (Check(item))
                        {
                            return;
                        }
                    }
                }
                if (!PlayerAbilities.TryGetValue(ev.Player, out var list))
                    return;
                if (!ItemAbilities.TryGetValue(ev.Item.Serial, out var list1))
                    return;

                List<ItemAbilityBase> ab = new();
                foreach (var ability in list1)
                {
                    if (ability is IRegisiterNeeded<ItemAbilityBase> reg)
                    {
                        reg.Unregister(ev.Player);

                    }
                    ab.Add(ability);
                }
                list.RemoveAll(x => ab.Any(y => y.id == x.id));
                if (list.Count <= 0)
                {
                    ev.Player.RemoveMessage("CustomRolePlus_Abilities");
                }
            }
        }

    }
    public abstract class CustomArmor : CustomItemPlus
    {
        //
        // 摘要:
        //     Gets or sets the ItemType to use for this armor.
        public override ItemType Type
        {
            get
            {
                return base.Type;
            }
            set
            {
                if (value != ItemType.None && !value.IsArmor())
                {
                    throw new ArgumentOutOfRangeException("Type", value, "Invalid armor type.");
                }

                base.Type = value;
            }
        }

        //
        // 摘要:
        //     Gets or sets how much faster stamina will drain when wearing this armor.
        [Description("The value must be above 1 and below 2")]
        public virtual float StaminaUseMultiplier { get; set; } = 1.15f;

        //
        // 摘要:
        //     Gets or sets how strong the helmet on the armor is.
        [Description("The value must be above 0 and below 100")]
        public virtual int HelmetEfficacy { get; set; } = 80;

        //
        // 摘要:
        //     Gets or sets how strong the vest on the armor is.
        [Description("The value must be above 0 and below 100")]
        public virtual int VestEfficacy { get; set; } = 80;

        //
        // 摘要:
        //     Gets or sets the Ammunition limit the player have.
        public virtual List<ArmorAmmoLimit> AmmoLimits { get; set; } = new List<ArmorAmmoLimit>();

        //
        // 摘要:
        //     Gets or sets the Item Category limit the player have.
        public virtual List<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits { get; set; } = new List<BodyArmor.ArmorCategoryLimitModifier>();

        public override void Give(Exiled.API.Features.Player player, bool displayMessage = true)
        {
            Armor armor = (Armor)Exiled.API.Features.Items.Item.Create(Type);
            armor.Weight = Weight;
            armor.StaminaUseMultiplier = StaminaUseMultiplier;
            armor.VestEfficacy = VestEfficacy;
            armor.HelmetEfficacy = HelmetEfficacy;
            if (AmmoLimits.Count != 0)
            {
                armor.AmmoLimits = AmmoLimits;
            }

            if (AmmoLimits.Count != 0)
            {
                armor.CategoryLimits = CategoryLimits;
            }

            player.AddItem(armor);
            base.TrackedSerials.Add(armor.Serial);
            Timing.CallDelayed(0.05f, delegate
            {
                OnAcquired(player, armor, displayMessage);
            });
            if (displayMessage)
            {
                ShowPickedUpMessage(player);
            }
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.PickingUpItem += new CustomEventHandler<PickingUpItemEventArgs>(OnInternalPickingUpItem);
            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.PickingUpItem -= new CustomEventHandler<PickingUpItemEventArgs>(OnInternalPickingUpItem);
            base.UnsubscribeEvents();
        }

        private void OnInternalPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (Check(ev.Pickup) && ev.Player.Items.Count < 8 && !(ev.Pickup is Exiled.API.Features.Pickups.BodyArmorPickup))
            {
                OnPickingUp(ev);
                if (ev.IsAllowed)
                {
                    ev.IsAllowed = false;
                    base.TrackedSerials.Remove(ev.Pickup.Serial);
                    ev.Pickup.Destroy();
                    Give(ev.Player);
                }
            }
        }
    }
}
