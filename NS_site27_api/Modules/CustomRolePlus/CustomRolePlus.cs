using DisplayKit.Elements;
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
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Armor;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Pickups;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using MEC;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Modules.Abilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NS_site27_api.Modules.CustomRolePlus
{
    public class abilitiesLayer : DisplayLayer
    {
        public override string Id { get; set; } = "abilitiesShower";

        public override void InitNodes(Player target, DisplayCanvas canvas)
        {

            /*
            canvas(UXML - id:0, Root) -> VisualElement(VisualElement - id:1, 1th child of canvas) 
            */
                        DisplayElement VisualElement = canvas.AddElement();
            VisualElement.BaseElement.name = "VisualElement";
            VisualElement.Flex.Grow = 1f;
            VisualElement.Position.Position = Position.Absolute;
            VisualElement.Position.Top = Length.Percent(70f);
            VisualElement.Size.MaxWidth = Length.Percent(15f);
            VisualElement.Align.AlignSelf = Align.FlexStart;
            VisualElement.Position.Right = Length.Percent(16.5f);

            /*
            canvas(UXML - id:0, Root) -> VisualElement(VisualElement - id:1, 1th child of canvas) -> specUiText(Label - id:2, 1th child of VisualElement) 
            */
                        DisplayText specUiText = VisualElement.AddText("");
            specUiText.BaseElement.name = "specUiText";
            specUiText.Background.Color = new Color(0.6509804f, 1f, 0.6666667f, 0.471f);
            specUiText.Text.Color = Color.black;
            specUiText.Text.Wrap = WhiteSpace.Normal;
            specUiText.Text.Overflow = TextOverflow.Ellipsis;
            specUiText.Spacing.PaddingTop = 0f;
            specUiText.Spacing.PaddingRight = 0f;
            specUiText.Spacing.PaddingBottom = 0f;
            specUiText.Spacing.PaddingLeft = 0f;


        }

        public override void Update(Player p, DisplayCanvas canvas)
        {
            foreach (var Eitem in canvas.Children)
            {
                if (Eitem.BaseElement.name == "specUiText" && Eitem is DisplayText t)
                {
                    string showing = "<align=right><size=24><color=white>\n";
                    if (p != null)
                    {
                        if (!string.IsNullOrEmpty(p.UniqueRole))
                        {
                            var r = CustomRole.Get(p.UniqueRole);
                                                        if (r != null)
                            {
                                showing += $"你是: {p.UniqueRole}\n{r.Description}\n";
                            }
                            else
                            {
                                showing += $"你是: {p.UniqueRole}\n";
                            }
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
                                        showing += $">{c.Name}:{c.GetUIDescription(p)}\n";
                                    }
                                    else
                                    {
                                        showing += $"{c.Name}\n";
                                    }
                                }
                            }
                        }
                        var set = AbilityBase.GetPlayerAbilitySet(p);
                        if (set != null)
                        {
                            foreach (var item in set.AllAbilities())
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
                                if (!string.IsNullOrEmpty(CustomInfo))
                                {
                                    show = true;
                                    if (item.AppendCustomInfoAfterNormalInfo) { 
                                        nS += $"{CustomInfo}";

                                    }
                                    else
                                    {
                                        nS = $"{CustomInfo}";
                                    }
                                }
                                nS += "\n";
                                if (show)
                                {
                                    showing += nS;
                                }
                            }
                        }
                    }
                    showing += "</color></size></align>";
                    if (showing != t.Content)
                    {
                        t.Content = showing;
                    }
                }
            }
        }
    }
    public abstract class CustomRolePlus : CustomRole
    {
                public List<AbilityBase> abilities = new();

        protected override void ShowMessage(Player player)
        {
        }
        public static void AddAbilityMessage(Player player)
        {
            player.AddLayer("abilitiesShower");
        }

        public static void RemoveAbilityMessage(Player player)
        {
            player.RemoveLayer("abilitiesShower");
        }

        private static bool HasVisibleAbilitiesOrItems(Player player)
        {
            if (player == null)
            {
                return false;
            }

            if (CustomItemPlus.PlayerItems.TryGetValue(player, out var pItems) && pItems.Count > 0)
            {
                return true;
            }

            var set = AbilityBase.GetPlayerAbilitySet(player);
            return set != null && set.HasVisible();
        }

        private static void RefreshAbilityMessage(Player player)
        {
            if (player == null)
            {
                return;
            }

            RemoveAbilityMessage(player);
            if (HasVisibleAbilitiesOrItems(player))
            {
                AddAbilityMessage(player);
            }
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ChangingRole += OnPlayerChangingRole;
            base.SubscribeEvents();
        }
        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnPlayerChangingRole;
            base.UnsubscribeEvents();
        }

        private static void ClearStaleRoleAbilities(Player player)
        {
            if (player == null)
            {
                return;
            }

            var set = AbilityBase.GetPlayerAbilitySet(player);
            if (set == null || set.RoleAbilities.Count == 0)
            {
                return;
            }

            foreach (var ability in set.RoleAbilities.ToList())
            {
                if (ability is IRegisiterNeeded<AbilityBase> reg)
                {
                    reg.Uninit(player);
                }
            }

            set.RoleAbilities.Clear();

            if (!set.HasAny())
            {
                RemoveAbilityMessage(player);
            }
        }
        private static void OnPlayerChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.Player == null)
            {
                return;
            }

            ClearStaleRoleAbilities(ev.Player);
            RefreshAbilityMessage(ev.Player);
        }

        protected override void RoleAdded(Player player)
        {
            ClearStaleRoleAbilities(player);
            base.RoleAdded(player);
            AddAbilityMessage(player);

            if (abilities == null || abilities.Count == 0)
            {
                return;
            }

            var set = new PlayerAbilitySet().GetOrCreate(player);

            foreach (var template in abilities)
            {
                AbilityBase instance = template;

                if (template is IRegisiterNeeded<AbilityBase> reg)
                {
                    Log.Info($"{instance.GetType().FullName} template is IRegisiterNeeded<AbilityBase> reg");
                    instance = reg.Register(player);
                }

                set.RoleAbilities.Add(instance);
            }
        }

        protected override void RoleRemoved(Player player)
        {
            base.RoleRemoved(player);

            var set = AbilityBase.GetPlayerAbilitySet(player);
            if (set == null || set.RoleAbilities.Count == 0)
            {
                return;
            }

            foreach (var ability in set.RoleAbilities.ToList())
            {
                if (ability is IRegisiterNeeded<AbilityBase> reg)
                {
                    reg.Uninit(player);
                }
            }

            set.RoleAbilities.Clear();

            if (!set.HasAny())
            {
                RemoveAbilityMessage(player);
            }
        }
    }

    public static class CustomItemEx
    {
        public static CustomItemPlus GetItemsCustom(this Item item)
        {
            return item == null
                ? null
                : CustomItemPlus.ItemMapping.TryGetValue(item, out var map)
                ? map
                : CustomItemPlus.ItemMappingBySerial.TryGetValue(item.Serial, out var map2) ? map2 : null;
        }
    }

    public abstract class CustomWeapon : CustomItemPlus
    {
        public virtual AttachmentName[] Attachments { get; set; } = new AttachmentName[0];

        public override ItemType Type
        {
            get => base.Type;
            set
            {
                if (!value.IsWeapon(checkNonFirearm: false) && value != ItemType.None)
                {
                    throw new ArgumentOutOfRangeException("Type", value, "Invalid weapon type.");
                }

                base.Type = value;
            }
        }

        public virtual float Damage { get; set; } = -1f;
        public virtual byte ClipSize { get; set; }
        public virtual bool FriendlyFire { get; set; }

        public override Pickup? Spawn(Vector3 position, Exiled.API.Features.Player? previousOwner = null)
        {
            if (Item.Create(Type) is not Firearm firearm)
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
            if (previousOwner != null)
            {
                pickup.PreviousOwner = previousOwner;
            }

            _ = TrackedSerials.Add(pickup.Serial);
            return pickup;
        }

        public override Pickup? Spawn(Vector3 position, Item item, Exiled.API.Features.Player? previousOwner = null)
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

                Pickup pickup = firearm.CreatePickup(position);
                pickup.Scale = Scale;
                if (previousOwner != null)
                {
                    pickup.PreviousOwner = previousOwner;
                }

                _ = TrackedSerials.Add(pickup.Serial);
                return pickup;
            }
            return base.Spawn(position, item, previousOwner);
        }

        public override void Give(Exiled.API.Features.Player player, bool displayMessage = true)
        {
            Item item = player.AddItem(Type);
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

            Log.Debug($"{Name}: Adding {item.Serial} to tracker.");
            _ = TrackedSerials.Add(item.Serial);
            OnAcquired(player, item, displayMessage);
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ReloadingWeapon += OnInternalReloading;
            Exiled.Events.Handlers.Player.ReloadedWeapon += OnInternalReloaded;
            Exiled.Events.Handlers.Player.Shooting += OnInternalShooting;
            Exiled.Events.Handlers.Player.Shot += OnInternalShot;
            Exiled.Events.Handlers.Player.Hurting += OnInternalHurting;
            Exiled.Events.Handlers.Item.ChangingAttachments += OnInternalChangingAttachment;
            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.ReloadingWeapon -= OnInternalReloading;
            Exiled.Events.Handlers.Player.ReloadedWeapon -= OnInternalReloaded;
            Exiled.Events.Handlers.Player.Shooting -= OnInternalShooting;
            Exiled.Events.Handlers.Player.Shot -= OnInternalShot;
            Exiled.Events.Handlers.Player.Hurting -= OnInternalHurting;
            Exiled.Events.Handlers.Item.ChangingAttachments -= OnInternalChangingAttachment;
            base.UnsubscribeEvents();
        }

        protected virtual void OnReloading(ReloadingWeaponEventArgs ev) { }
        protected virtual void OnReloaded(ReloadedWeaponEventArgs ev) { }
        protected virtual void OnShooting(ShootingEventArgs ev) { }
        protected virtual void OnShot(ShotEventArgs ev) { }

        protected virtual void OnHurting(HurtingEventArgs ev)
        {
            if (ev.IsAllowed && Damage >= 0f)
            {
                ev.Amount = Damage;
            }
        }

        protected virtual void OnChangingAttachment(ChangingAttachmentsEventArgs ev) { }

                private void OnInternalReloading(ReloadingWeaponEventArgs ev)
        {
            if (!Check(ev.Item))
            {
                return;
            }

            if (ClipSize > 0)
            {
                ev.IsAllowed = false; 
                int currentMag = ev.Firearm.MagazineAmmo;
                int ammoAvailable = ev.Player.GetAmmo(ev.Firearm.AmmoType);
                int needed = ClipSize - currentMag;

                if (needed > 0 && ammoAvailable > 0)
                {
                    int loadAmount = Mathf.Min(needed, ammoAvailable);
                    ev.Firearm.MagazineAmmo = (byte)(currentMag + loadAmount);
                    ev.Player.SetAmmo(ev.Firearm.AmmoType, (ushort)(ammoAvailable - loadAmount));
                    OnReloading(ev);                 }
            }
            else
            {
                OnReloading(ev);
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
                int ammoStored = (ev.Firearm.Base.Modules.FirstOrDefault(x => x is AutomaticActionModule) as AutomaticActionModule)?.AmmoStored ?? 0;
                int neededForClip = ClipSize - ammoStored;
                AmmoType ammoType = ev.Firearm.AmmoType;
                int magAmmo = ev.Firearm.MagazineAmmo;
                int totalAvailable = ev.Player.GetAmmo(ammoType) + magAmmo;

                if (neededForClip < totalAvailable)
                {
                    ev.Firearm.MagazineAmmo = neededForClip;
                    int ammoToSet = ev.Player.GetAmmo(ammoType) - (ClipSize - magAmmo - ammoStored);
                    ev.Player.SetAmmo(ammoType, (ushort)Mathf.Max(0, ammoToSet));
                }
                else
                {
                    ev.Firearm.MagazineAmmo = totalAvailable;
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
            if (ev.Attacker == null || ev.Player == null)
            {
                return;
            }

            if (ev.Attacker == ev.Player)
            {
                return;             }

            if (ev.DamageHandler == null)
            {
                return;
            }

            if (!ev.DamageHandler.CustomBase.BaseIs<FirearmDamageHandler>(out var param))
            {
                return;
            }

            if (!Check(param.Item))
            {
                return;
            }

            if (!FriendlyFire && ev.Attacker.Role.Team == ev.Player.Role.Team)
            {
                return;
            }

            OnHurting(ev);
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
        public List<ItemAbilityBase> abilities = new();

        private static bool _availabilityCheckerStarted = false;
        private static bool _staticEventsSubscribed = false;

                private static void OnRoundRestart()
        {
            ItemMapping.Clear();
            PlayerAbilitySet.PlayerAbilities.Clear();
            ItemAbilityBase.ItemABs.Clear();
            PlayerItems.Clear();
        }
        private static IEnumerator<float> AvailabilityChecker()
        {
            while (true)
            {
                try
                {
                    foreach (var kv in PlayerItems.ToArray())
                    {
                        var player = kv.Key;
                        var items = kv.Value.ToList();
                        foreach (var tup in items)
                        {
                            ushort serial = tup.Item1;
                            var custom = tup.Item2;
                            var exItem = Item.Get(serial);
                            if (exItem == null)
                            {
                                continue;
                            }

                            foreach (var template in custom.abilities.ToArray())
                            {
                                bool shouldBe;
                                try
                                {
                                    shouldBe = custom.IsAvailable(player, exItem);
                                }
                                catch
                                {
                                    shouldBe = true;
                                }

                                var exists = ItemAbilities.TryGetValue(serial, out var created) && created.Any(x => x.id == template.id);

                                if (exists && !shouldBe)
                                {
                                    var inst = created.FirstOrDefault(x => x.id == template.id);
                                    if (inst != null)
                                    {
                                        UnregisterInstanceForTemplate(template, player, serial);
                                        _ = created.RemoveAll(x => x.id == template.id);
                                        if (PlayerAbilities.TryGetValue(player, out var pal))
                                        {
                                            _ = pal.ItemAbilities.RemoveAll(x => x.id == template.id);
                                        }
                                    }
                                }
                                else if (!exists && shouldBe)
                                {
                                    ItemAbilityBase instance = template;
                                    instance = CreateInstanceFromTemplate(template, player, serial);
                                    if (!ItemAbilities.TryGetValue(serial, out var list))
                                    {
                                        list = new List<ItemAbilityBase>();
                                        ItemAbilities[serial] = list;
                                    }
                                    list.Add(instance);

                                    var set = new PlayerAbilitySet().GetOrCreate(player);
                                    if (!set.ItemAbilities.Any(x => x.id == instance.id))
                                    {
                                        set.ItemAbilities.Add(instance);
                                    }

                                    CustomRolePlus.AddAbilityMessage(player);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Log.Warn($"AvailabilityChecker error: {ex}"); }
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        private static ItemAbilityBase CreateInstanceFromTemplate(ItemAbilityBase template, Player player, ushort serial)
        {
            ItemAbilityBase instance = template;
            try
            {
                if (template is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                {
                    instance = reg1.Register(serial);
                }
                else if (template is IRegisiterNeeded<ItemAbilityBase> reg)
                {
                    instance = reg.Register(player);
                }
            }
            catch (Exception ex) { Log.Warn($"CreateInstanceFromTemplate error: {ex}"); }
            return instance;
        }

        private static void RegisterInstance(ItemAbilityBase instance, Player player, ushort serial)
        {
            if (instance == null)
            {
                return;
            }

            if (!ItemAbilities.TryGetValue(serial, out var list))
            {
                list = new List<ItemAbilityBase>();
                ItemAbilities[serial] = list;
            }
            if (!list.Any(x => x.id == instance.id))
            {
                list.Add(instance);
            }

            var set = new PlayerAbilitySet().GetOrCreate(player);
            if (!set.ItemAbilities.Any(x => x.id == instance.id))
            {
                set.ItemAbilities.Add(instance);
            }

            CustomRolePlus.AddAbilityMessage(player);
        }

        private static void UnregisterInstanceForTemplate(ItemAbilityBase template, Player player, ushort serial)
        {
            if (!ItemAbilities.TryGetValue(serial, out var list))
            {
                return;
            }

            var inst = list.FirstOrDefault(x => x.id == template.id);
            if (inst == null)
            {
                return;
            }

            try
            {
                if (template is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                {
                    reg1.Unregister(serial);
                }
                else if (template is IRegisiterNeeded<ItemAbilityBase> reg)
                {
                    reg.Uninit(player);
                }
            }
            catch (Exception ex) { Log.Warn($"UnregisterInstanceForTemplate error: {ex}"); }

            _ = list.RemoveAll(x => x.id == template.id);
            if (PlayerAbilities.TryGetValue(player, out var pal))
            {
                _ = pal.ItemAbilities.RemoveAll(x => x.id == template.id);
            }

            if (list.Count == 0)
            {
                _ = ItemAbilities.Remove(serial);
            }

            CustomRolePlus.AddAbilityMessage(player);
        }

        public static Dictionary<Player, List<(ushort, CustomItemPlus)>> PlayerItems = new();
        public static Dictionary<Item, CustomItemPlus> ItemMapping = new();
        public static Dictionary<ushort, CustomItemPlus> ItemMappingBySerial = new();
        public static Dictionary<Player, PlayerAbilitySet> PlayerAbilities => PlayerAbilitySet.PlayerAbilities;
        public static Dictionary<ushort, List<ItemAbilityBase>> ItemAbilities => ItemAbilityBase.ItemABs;

        protected override void ShowPickedUpMessage(Player player) { }

                public virtual bool IsAvailable(Player player, Item item)
        {
            return Check(item);
        }

        protected void OnFlipingCoin(PlayerFlippedCoinEventArgs ev)
        {
            OnUsed(ev.Player, Item.Get(ev.CoinItem.Base));
        }

        protected void OnUsingItem(PlayerUsedItemEventArgs ev)
        {
            OnUsed(ev.Player, Item.Get(ev.UsableItem.Base));
        }

        protected virtual void OnUsed(Player player, Item item) { }

        protected override void SubscribeEvents()
        {
            base.SubscribeEvents();

                        if (!_staticEventsSubscribed)
            {
                Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestart;
                _staticEventsSubscribed = true;
            }

            ItemPickupBase.OnPickupDestroyed += ItemPickupBase_OnPickupDestroyed;
            InventoryExtensions.OnItemRemoved += ItemBase_OnItemRemoved;
            PlayerEvents.PickingUpItem += PlayerEvents_PickingUpItem;
            PlayerEvents.UsedItem += OnUsingItem;
            PlayerEvents.FlippedCoin += OnFlipingCoin;
        }

        protected override void UnsubscribeEvents()
        {
            base.UnsubscribeEvents();
                        ItemPickupBase.OnPickupDestroyed -= ItemPickupBase_OnPickupDestroyed;
            InventoryExtensions.OnItemRemoved -= ItemBase_OnItemRemoved;
            PlayerEvents.PickingUpItem -= PlayerEvents_PickingUpItem;
            PlayerEvents.UsedItem -= OnUsingItem;
            PlayerEvents.FlippedCoin -= OnFlipingCoin;
        }

        private static readonly HashSet<ushort> PickedItem = new();

        private void PlayerEvents_PickingUpItem(LabApi.Events.Arguments.PlayerEvents.PlayerPickingUpItemEventArgs ev)
        {
            _ = PickedItem.Add(ev.Pickup.Serial);
            RefreshPlayersItems(Player.Get(ev.Player));
        }

        private void ItemBase_OnItemRemoved(ReferenceHub hub, ItemBase it, ItemPickupBase itb)
        {
            if (it == null)
            {
                return;
            }

            OnDestroyedInternal(it.ItemId.SerialNumber, hub);
            RefreshPlayersItems(Player.Get(hub));
        }

        protected override void ShowSelectedMessage(Player player)
        {
            CustomRolePlus.AddAbilityMessage(player);
        }

        private void ItemPickupBase_OnPickupDestroyed(ItemPickupBase obj)
        {
            if (PickedItem.Contains(obj.ItemId.SerialNumber))
            {
                _ = PickedItem.Remove(obj.ItemId.SerialNumber);
                return;
            }
            OnDestroyedInternal(obj.ItemId.SerialNumber);
        }

        private static bool HasVisibleAbilitiesOrItems(Player player)
        {
            if (player == null)
            {
                return false;
            }

            if (PlayerItems.TryGetValue(player, out var pItems) && pItems.Any())
            {
                return true;
            }

            var set = AbilityBase.GetPlayerAbilitySet(player);
            return set != null && set.HasVisible();
        }

        private static void RefreshAbilityMessage(Player player)
        {
            if (player == null)
            {
                return;
            }

            CustomRolePlus.RemoveAbilityMessage(player);
            if (HasVisibleAbilitiesOrItems(player))
            {
                CustomRolePlus.AddAbilityMessage(player);
            }
        }

        private static void RemoveSerialFromAllPlayerItems(ushort serial)
        {
            foreach (var kv in PlayerItems.ToArray())
            {
                if (kv.Value.RemoveAll(x => x.Item1 == serial) > 0)
                {
                    if (kv.Value.Count == 0)
                    {
                        _ = PlayerItems.Remove(kv.Key);
                    }

                    RefreshAbilityMessage(kv.Key);
                }
            }
        }

        protected virtual void OnDestroyedInternal(ushort serial, ReferenceHub referenceHub = null)
        {
            if (!ItemMappingBySerial.ContainsKey(serial))
            {
                return;
            }

            var player = Player.Get(referenceHub);
            if (player != null)
            {
                RefreshPlayersItems(player);
            }
            else
            {
                RemoveSerialFromAllPlayerItems(serial);
            }

            if (ItemAbilities.TryGetValue(serial, out var itemAbilities))
            {
                List<ItemAbilityBase> toRemove = new(itemAbilities);
                foreach (var ability in toRemove)
                {
                    try
                    {
                        if (ability is IRegisiterNeeded<ItemAbilityBase> reg)
                        {
                            reg.Uninit(player);
                        }

                        if (ability is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                        {
                            reg1.Unregister(serial);
                        }
                    }
                    catch { }
                }
                if (PlayerAbilities.TryGetValue(player, out var set))
                {
                    _ = set.ItemAbilities.RemoveAll(x => toRemove.Any(y => y.id == x.id));
                }

                _ = ItemAbilities.Remove(serial);
            }


            try
            {
                var keys = ItemMapping.Where(kv => kv.Key?.Serial == serial).Select(kv => kv.Key).ToList();
                foreach (var k in keys)
                {
                    _ = ItemMapping.Remove(k);
                }
            }
            catch { }
            _ = ItemMappingBySerial.Remove(serial);

            OnDestroyed(serial, player);
            _ = TrackedSerials.Remove(serial);

            RefreshAbilityMessage(player);
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
            ItemMappingBySerial[item.Serial] = this;
            RefreshPlayersItems(player);

            if (abilities == null || abilities.Count == 0)
            {
                return;
            }

            if (!_availabilityCheckerStarted)
            {
                _availabilityCheckerStarted = true;
                _ = NS_site27_api.Core.CorePlugin.RunCoroutine(AvailabilityChecker());
            }

            CustomRolePlus.AddAbilityMessage(player);

            foreach (var template in abilities)
            {
                bool available;
                try { available = template.IsAvailable(player, item) && IsAvailable(player, item); }
                catch { available = true; }
                if (!available)
                {
                    continue;
                }

                var instance = CreateInstanceFromTemplate(template, player, item.Serial);
                RegisterInstance(instance, player, item.Serial);
            }
        }
        protected override void OnDroppingItem(DroppingItemEventArgs ev)
        {
            base.OnDroppingItem(ev);
            if (!Check(ev.Item))
            {
                return;
            }

                        if (ItemAbilities.TryGetValue(ev.Item.Serial, out var itemAbilities))
            {
                List<ItemAbilityBase> toRemove = new(itemAbilities);
                foreach (var ability in toRemove)
                {
                    try
                    {
                        if (ability is IRegisiterNeeded<ItemAbilityBase> reg)
                        {
                            reg.Uninit(ev.Player);
                        }

                        if (ability is IitemRegisiterNeeded<ItemAbilityBase> reg1)
                        {
                            reg1.Unregister(ev.Item.Serial);
                        }
                    }
                    catch { }
                }
                if (PlayerAbilities.TryGetValue(ev.Player, out var set))
                {
                    _ = set.ItemAbilities.RemoveAll(x => toRemove.Any(y => y.id == x.id));
                }

                _ = ItemAbilities.Remove(ev.Item.Serial);
            }

            RefreshPlayersItems(ev.Player);
            RemoveSerialFromAllPlayerItems(ev.Item.Serial);

                        bool hasVisible = false;
            if (PlayerItems.TryGetValue(ev.Player, out var pItems) && pItems.Any(t => t.Item1 != ev.Item.Serial))
            {
                hasVisible = true;
            }
            else if (PlayerAbilities.TryGetValue(ev.Player, out var pal) && pal.HasVisible())
            {
                hasVisible = true;
            }

                        CustomRolePlus.RemoveAbilityMessage(ev.Player);
            if (hasVisible)
            {
                CustomRolePlus.AddAbilityMessage(ev.Player);
            }

                        _ = Timing.CallDelayed(0.1f, () =>
            {
                RefreshPlayersItems(ev.Player);
                RefreshAbilityMessage(ev.Player);
            });
        }

        public virtual string GetUIDescription(Player p)
        {
            return Description;
        }
    }

    public abstract class CustomArmor : CustomItemPlus
    {
        public override bool IsAvailable(Player player, Item item)
        {
            return true;
        }

        public override ItemType Type
        {
            get => base.Type;
            set
            {
                if (value != ItemType.None && !value.IsArmor())
                {
                    throw new ArgumentOutOfRangeException("Type", value, "Invalid armor type.");
                }

                base.Type = value;
            }
        }

        [Description("The value must be above 1 and below 2")]
        public virtual float StaminaUseMultiplier { get; set; } = 1.15f;

        [Description("The value must be above 0 and below 100")]
        public virtual int HelmetEfficacy { get; set; } = 80;

        [Description("The value must be above 0 and below 100")]
        public virtual int VestEfficacy { get; set; } = 80;

        public virtual List<ArmorAmmoLimit> AmmoLimits { get; set; } = new List<ArmorAmmoLimit>();
        public virtual List<BodyArmor.ArmorCategoryLimitModifier> CategoryLimits { get; set; } = new List<BodyArmor.ArmorCategoryLimitModifier>();

        public override void Give(Exiled.API.Features.Player player, bool displayMessage = true)
        {
            Armor armor = (Armor)Item.Create(Type);
            armor.Weight = Weight;
            armor.StaminaUseMultiplier = StaminaUseMultiplier;
            armor.VestEfficacy = VestEfficacy;
            armor.HelmetEfficacy = HelmetEfficacy;

            if (AmmoLimits.Count != 0)
            {
                armor.AmmoLimits = AmmoLimits;
            }

                        if (CategoryLimits.Count != 0)
            {
                armor.CategoryLimits = CategoryLimits;
            }

            player.AddItem(armor);
            _ = TrackedSerials.Add(armor.Serial);
            _ = Timing.CallDelayed(0.1f, delegate
            {
                OnAcquired(player, armor, displayMessage);
                RefreshPlayersItems(player);
            });
            if (displayMessage)
            {
                ShowPickedUpMessage(player);
            }
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Player.PickingUpItem += OnInternalPickingUpItem;
            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Player.PickingUpItem -= OnInternalPickingUpItem;
            base.UnsubscribeEvents();
        }

        private void OnInternalPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (Check(ev.Pickup) && ev.Player.Items.Count < 8)
            {
                OnPickingUp(ev);
                if (ev.IsAllowed)
                {
                    ev.IsAllowed = false;
                    _ = TrackedSerials.Remove(ev.Pickup.Serial);
                    ev.Pickup.Destroy();
                    Give(ev.Player);
                }
            }
        }
    }
}