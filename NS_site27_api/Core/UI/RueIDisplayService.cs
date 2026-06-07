using Exiled.API.Features;
using MEC;
using NS_site27_api.Core.UI;
using RueI.API;
using RueI.API.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NS_site27_api.Core.UI
{
    public class RueIHintService : IUIService
    {
        private readonly Dictionary<Player, Dictionary<string, (Stopwatch timer, float duration, bool isDynamic, Func<Player, string[]> getter, string staticText)>> _playerMessages
            = new Dictionary<Player, Dictionary<string, (Stopwatch, float, bool, Func<Player, string[]>, string)>>();

        private CoroutineHandle _updater;

        public RueIHintService()
        {

        }

        public bool HasMessage(Player player, string messageId)
        {
            bool re = false;
            if (_playerMessages.TryGetValue(player, out var msgs) &&
                   msgs.TryGetValue(messageId, out var message))
            {
                if (message.timer.Elapsed.TotalSeconds <= message.duration) {
                    re = true;
                }
                else
                {
                    message.timer.Stop();
                    msgs.Remove(messageId);
                }
            }
            return re;
        }

        public void AddMessage(Player player, string id, Func<Player, string[]> getter, float duration, UIPosition position)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (Stopwatch, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Stopwatch.StartNew(), duration, true, getter, null);
            var r = RueDisplay.Get(player);
            var e = new DynamicElement(position.Y,x =>
            {
                var re = getter(Player.Get(x));
                string rf = "<line-height=70%>";
                rf += $"<margin={position.X}>";
                foreach (var item in re)
                {
                    rf += item + "\n";
                }
                rf += "</margin>";
                rf += "</line-height>";
                return rf;
            })
            {
                UpdateInterval = TimeSpan.FromSeconds(0.2),
                ResolutionBasedAlign = true
            };
            r.Show(new RueI.API.Elements.Tag(id), e);
        }

        public void AddMessage(Player player, string id, string message, float duration, UIPosition position)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (Stopwatch, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Stopwatch.StartNew(), duration, false, null, message);
            var r = RueDisplay.Get(player);
            r.Show(new RueI.API.Elements.Tag(id), new BasicElement(position.Y, BuildHintString(message, position.X)) {
                ResolutionBasedAlign = true
            },duration);
        }

        public void RemoveMessage(Player player, string id)
        {
            if (player == null) return;
            if (_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs.Remove(id);
                if (msgs.Count == 0)
                    _playerMessages.Remove(player);
            }
            var r = RueDisplay.Get(player);
            r.Remove(new Tag(id));
        }

        private string BuildHintString(string message, float xPosition)
        {
            string result = "<line-height=0>";
            if (xPosition != 0)
                result += $"<margin-left={xPosition}>";
            result += message;
            if (xPosition != 0)
                result += "</margin-left>";
            result += "</line-height>";
            return result;
        }

        public void CleanupPlayer(Player player)
        {
            if (player != null)
                _playerMessages.Remove(player);
        }
    }
}
