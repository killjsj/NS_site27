using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using InventorySystem.Items.Keycards;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Exiled.API.Features.Items;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using Scp914Handlers = Exiled.Events.Handlers.Scp914;
using InventorySystem;
using Interactables.Interobjects.DoorUtils;
namespace NS_site27_api.Modules._Keycard
{
    public class KeycardConfig : ModuleConfigBase
    {
        public bool EnableCustomKeycards { get; set; } = true;
    }

    public class KeycardModule : ModuleBase<KeycardConfig>
    {
        public override string ModuleName => "Keycard";
        public static MySQLConnect SQL => Plugin.Instance?.connect;


        internal Dictionary<string, List<(bool enable, string card, string text, string holder, string color, string permColor, string displayName, int? rank, bool applyToAll)>> _cachedCards
            = new Dictionary<string, List<(bool, string, string, string, string, string, string, int?, bool)>>();

        internal Dictionary<ushort, ItemType> _cachedCardTypes = new Dictionary<ushort, ItemType>();

        public override void OnEnable()
        {
            

            PlayerHandlers.Verified += OnVerified;
            PlayerHandlers.ChangedItem += OnChangedItem;
            Scp914Handlers.UpgradingPickup += OnUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem += OnUpgradingInventoryItem;
        }

        public override void OnDisable()
        {
            PlayerHandlers.Verified -= OnVerified;
            PlayerHandlers.ChangedItem -= OnChangedItem;
            Scp914Handlers.UpgradingPickup -= OnUpgradingPickup;
            Scp914Handlers.UpgradingInventoryItem -= OnUpgradingInventoryItem;
            _cachedCards.Clear();
            _cachedCardTypes.Clear();
        }
        private void OnVerified(VerifiedEventArgs ev)
        {
            if (SQL != null && !_cachedCards.ContainsKey(ev.Player.UserId))
            {
                var cards = QueryPlayerCards(ev.Player.UserId);
                _cachedCards[ev.Player.UserId] = cards;
            }
        }

        private void OnChangedItem(ChangedItemEventArgs ev)
        {
            if (ev.Item == null || !ev.Item.IsKeycard || _cachedCardTypes.ContainsKey(ev.Item.Serial))
                return;

            if (!_cachedCards.TryGetValue(ev.Player.UserId, out var cards))
                return;

            var keycard = ev.Item as Keycard;
            if (keycard == null) return;

            foreach (var card in cards)
            {
                if (!card.enable) continue;

                string targetCard = ResolveTargetCard(card.applyToAll, card.card, keycard);
                if (string.IsNullOrEmpty(targetCard)) continue;

                if (card.applyToAll || targetCard == card.card)
                {
                    ApplyCustomKeycard(keycard, ev.Player, card, targetCard);
                    break;
                }
            }
        }

        private string ResolveTargetCard(bool applyToAll, string card, Keycard keycard)
        {
            if (applyToAll) return card;

            return keycard.Identifier.TypeId switch
            {
                ItemType.KeycardJanitor => "KeycardCustomSite02",
                ItemType.KeycardContainmentEngineer => "KeycardCustomSite02",
                ItemType.KeycardScientist => "KeycardCustomSite02",
                ItemType.KeycardResearchCoordinator => "KeycardCustomSite02",
                ItemType.KeycardGuard => "KeycardCustomMetalCase",
                ItemType.KeycardMTFCaptain => "KeycardCustomTaskForce",
                ItemType.KeycardMTFPrivate => "KeycardCustomTaskForce",
                ItemType.KeycardMTFOperative => "KeycardCustomTaskForce",
                ItemType.KeycardFacilityManager => "KeycardCustomManagement",
                ItemType.KeycardZoneManager => "KeycardCustomManagement",
                ItemType.KeycardO5 => "KeycardCustomManagement",
                _ => null,
            };
        }

        private void ApplyCustomKeycard(Keycard keycard, Player player, (bool enable, string card, string text, string holder, string color, string permColor, string displayName, int? rank, bool applyToAll) card, string targetCard)
        {
            if (!Enum.TryParse<ItemType>(targetCard, out var targetType)) return;
            if (!targetType.TryGetTemplate(out KeycardItem template) || !template.Customizable) return;

            Color color;
            ColorUtility.TryParseHtmlString(card.color, out color);
            if (color == default) color = Color.cyan;

            string displayText = card.text?.Replace(" ", "_") ?? "Default_Text";
            string holderName = card.holder?.Replace(" ", "_") ?? "Unknown_Holder";
            string permColor = string.IsNullOrEmpty(card.permColor) ? "cyan" : card.permColor;

            foreach (DetailBase detail in template.Details)
            {
                if (detail is ICustomizableDetail cd)
                {
                    switch (cd)
                    {
                        case CustomItemNameDetail nameDetail:
                            nameDetail.SetArguments(new ArraySegment<object>(new object[] { displayText }));
                            break;
                        case CustomLabelDetail labelDetail:
                            labelDetail.SetArguments(new ArraySegment<object>(new object[] { displayText, color }));
                            break;
                        case NametagDetail nametag:
                            nametag.SetArguments(new ArraySegment<object>(new object[] { targetCard == "KeycardCustomTaskForce" ? displayText : holderName }));
                            break;
                        case CustomSerialNumberDetail serialDetail:
                            serialDetail.SetArguments(new ArraySegment<object>(new object[] { holderName }));
                            break;
                        case CustomWearDetail wearDetail:
                            wearDetail.SetArguments(new ArraySegment<object>(new object[] { (byte)(card.rank.GetValueOrDefault(2)) }));
                            break;
                        case CustomTintDetail tintDetail:
                            tintDetail.SetArguments(new ArraySegment<object>(new object[] { color }));
                            break;
                        case CustomRankDetail rankDetail:
                            rankDetail.SetArguments(new ArraySegment<object>(new object[] { card.rank.GetValueOrDefault(2) }));
                            break;
                        case CustomPermsDetail perms:
                            var levels = new KeycardLevels(keycard.Base.GetPermissions(null));
                            perms.ParseArguments(new ArraySegment<string>(new string[] {
                                levels.Containment.ToString(), levels.Armory.ToString(), levels.Admin.ToString(), permColor }));
                            break;
                    }
                }
            }

            Timing.CallDelayed(0.1f, () =>
            {
                var origType = keycard.Identifier.TypeId;
                player.RemoveItem(keycard);
                player.AddItem(targetType);
                var current = player.CurrentItem;
                if (current != null)
                {
                    _cachedCardTypes[current.Serial] = origType;
                }
                
            });
        }

        private void OnUpgradingPickup(UpgradingPickupEventArgs ev)
        {
            if (_cachedCardTypes.ContainsKey(ev.Pickup.Serial))
            {
                ev.IsAllowed = false;
            }
        }

        private void OnUpgradingInventoryItem(UpgradingInventoryItemEventArgs ev)
        {
            if (ev.Item != null && _cachedCardTypes.ContainsKey(ev.Item.Serial))
            {
                ev.IsAllowed = false;
            }
        }

        private List<(bool, string, string, string, string, string, string, int?, bool)> QueryPlayerCards(string userId)
        {
            return new List<(bool, string, string, string, string, string, string, int?, bool)>();
        }
    }
}
