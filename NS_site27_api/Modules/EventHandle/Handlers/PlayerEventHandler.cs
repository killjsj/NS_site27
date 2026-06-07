using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;

namespace NS_site27_api.Modules.EventHandle.Handlers
{
    public static class PlayerEventHandler
    {
        private static Dictionary<ReferenceHub, List<(EffectType, byte, float)>> _effects = new();

        public static void OnVerified(VerifiedEventArgs ev)
        {
        }

        public static void OnSpawned(SpawnedEventArgs ev)
        {
            if (ev.Player.Role.Type == RoleTypeId.ClassD)
            {
                ev.Player.AddItem(ItemType.KeycardJanitor);
            }
        }

        public static void OnEscaping(EscapingEventArgs ev)
        {
            if (ev.Player.Role.Type == RoleTypeId.FacilityGuard)
            {
                ev.EscapeScenario = EscapeScenario.CustomEscape;
                ev.NewRole = RoleTypeId.NtfSergeant;
                ev.IsAllowed = true;
            }
        }

        public static void OnEscaped(EscapedEventArgs ev)
        {
            if (!_effects.ContainsKey(ev.Player.ReferenceHub)) return;

            foreach (var (type, intensity, duration) in _effects[ev.Player.ReferenceHub])
            {
                if (ev.Player.TryGetEffect(type, out var effect))
                {
                    ev.Player.EnableEffect(effect, intensity, duration, false);
                    ev.Player.ReferenceHub.playerEffectsController.ServerSyncEffect(effect);
                }
            }
            _effects.Remove(ev.Player.ReferenceHub);
        }

        public static void OnPlayerLeave(LeftEventArgs ev)
        {
            var module = CorePlugin.Modules.OfType<EventHandleModule>().FirstOrDefault();
            module?.NotTodayScp.Remove(ev.Player.UserId);
        }
    }
}
