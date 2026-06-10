using Exiled.API.Features;
using MEC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace NS_site27_api.Core.UI
{
    public enum ScreenPosition
    {
        Top,
        CenterTop,
        Center,
        CenterBottom,
        Bottom,
        Custom,
        TopLeft,
        TopRight,
        MiddleLeft,
        MiddleRight,
        BottomLeft,
        BottomRight,
    }

    public struct UIPosition
    {
        public float margin;
        public float Y;
        public bool UseEnum;
        public ScreenPosition Position;

        public UIPosition(float margin, float y)
        {
            this.margin = margin;
            Y = y;
            UseEnum = false;
            Position = ScreenPosition.Custom;
        }

        public UIPosition(ScreenPosition pos)
        {
            UseEnum = true;
            Position = pos;
            var res = ResolveEnum(pos);
            margin = res.margin;
            Y = res.Y;
        }

        public static UIPosition FromEnum(ScreenPosition pos) => new UIPosition(pos);
        public static UIPosition FromXY(float margin, float y) => new UIPosition(margin, y);

        public void Resolve()
        {
            if (UseEnum)
            {
                var res = ResolveEnum(Position);
                margin = res.margin;
                Y = res.Y;
            }
        }

        private static (float margin, float Y) ResolveEnum(ScreenPosition location)
        {
            switch (location)
            {
                case ScreenPosition.Top:
                    return (-100, 800);
                case ScreenPosition.CenterTop:
                    return (0, 900);
                case ScreenPosition.Center:
                    return (50, 300);
                case ScreenPosition.CenterBottom:
                    return (0, 0);
                case ScreenPosition.Bottom:
                    return (-100, 50);
                case ScreenPosition.TopLeft:
                    return (-500, 900);
                case ScreenPosition.TopRight:
                    return (500, 900);
                case ScreenPosition.MiddleLeft:
                    return (-100, 500);
                case ScreenPosition.MiddleRight:
                    return (100, 500);
                case ScreenPosition.BottomLeft:
                    return (-500, 50);
                case ScreenPosition.BottomRight:
                    return (500, 50);
                default:
                    return (0, 0);
            }
        }
    }

    public interface IUIService
    {
        void AddMessage(Player player, string id, Func<Player, string[]> getter, UIPosition position);
        void AddMessage(Player player, string id, string message,  UIPosition position);
        void RemoveMessage(Player player, string id);
        void CleanupPlayer(Player player);
    }

    public static class UIManager
    {
        private static readonly Dictionary<Player, Dictionary<string, (float startAt, float duration, bool isDynamic, Func<Player, string[]> getter, string staticText)>> _playerMessages
    = new Dictionary<Player, Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>>();

        private static CoroutineHandle _updater;

        private static IUIService _service;
        private static bool _initialized;
        private static IEnumerator<float> Updater()
        {
            List<string> waitingforRemove = new();
            while (true)
            {
                
                foreach (var item in _playerMessages)
                {
                    waitingforRemove.Clear();
                    foreach (var item2 in item.Value.Where(x=>x.Value.duration > 0).ToArray())
                    {
                        if(Time.fixedTime - item2.Value.startAt > item2.Value.duration)
                        {
                            RemoveMessage(item.Key, item2.Key);
                        }
                    }
                }
                yield return Timing.WaitForSeconds(0.1f);
            }
        }
        public static void Initialize(IUIService service)
        {
            _service = service;
            _updater = Timing.RunCoroutine(Updater());
            _initialized = true;
        }
        public static void Finish()
        {
            if (_initialized)
            {
                Timing.KillCoroutines(_updater);
                _service = null;
                _playerMessages.Clear();
                _initialized = false;
            }
        }
        public static bool HasMessage(this Player player, string messageId)
        {
            if (_service == null) return false;
            bool re = false;
            if (_playerMessages.TryGetValue(player, out var msgs) &&
                   msgs.TryGetValue(messageId, out var message))
            {
                if (Time.fixedTime - message.startAt <= message.duration && message.duration > 0)
                {
                    re = true;
                }
            }
            return re;
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration = 5, ScreenPosition position = ScreenPosition.Center)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, true, getter, null);
            _service?.AddMessage(player, id, getter, UIPosition.FromEnum(position));
        }

        public static void AddMessage(this Player player, string id, string message, float duration = 5, ScreenPosition position = ScreenPosition.Center)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, false, null, message);
            _service?.AddMessage(player, id, message, UIPosition.FromEnum(position));
        }

        public static void AddMessage(this Player player, string id, string message, float duration, float x, float y)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, false, null, message);
            _service?.AddMessage(player, id, message, UIPosition.FromXY(x, y));
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration, float x, float y)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, true, getter, null);
            _service?.AddMessage(player, id, getter, UIPosition.FromXY(x, y));
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration, UIPosition uIPosition)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, true, getter, null);
            _service?.AddMessage(player, id, getter, uIPosition);
        }

        public static void AddMessage(this Player player, string id, string message, float duration, UIPosition uIPosition)
        {
            if (player == null) return;

            if (!_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs = new Dictionary<string, (float, float, bool, Func<Player, string[]>, string)>();
                _playerMessages[player] = msgs;
            }

            msgs[id] = (Time.fixedTime, duration, false, null, message);
            _service?.AddMessage(player, id, message, uIPosition);
        }

        public static void RemoveMessage(this Player player, string id)
        {
            if (player == null) return;
            if (_playerMessages.TryGetValue(player, out var msgs))
            {
                msgs.Remove(id);
                if (msgs.Count == 0)
                    _playerMessages.Remove(player);
            }
            _service?.RemoveMessage(player, id);
        }
        public static void CleanupPlayer(this Player player)
        {
            if (player != null)
                _playerMessages.Remove(player);
        }
    }
}
