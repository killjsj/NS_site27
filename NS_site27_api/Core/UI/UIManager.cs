using Exiled.API.Features;
using System;

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
        public float X;
        public float Y;
        public bool UseEnum;
        public ScreenPosition Position;

        public UIPosition(float x, float y)
        {
            X = x;
            Y = y;
            UseEnum = false;
            Position = ScreenPosition.Custom;
        }

        public UIPosition(ScreenPosition pos)
        {
            UseEnum = true;
            Position = pos;
            var res = ResolveEnum(pos);
            X = res.X;
            Y = res.Y;
        }

        public static UIPosition FromEnum(ScreenPosition pos) => new UIPosition(pos);
        public static UIPosition FromXY(float x, float y) => new UIPosition(x, y);

        public void Resolve()
        {
            if (UseEnum)
            {
                var res = ResolveEnum(Position);
                X = res.X;
                Y = res.Y;
            }
        }

        private static (float X, float Y) ResolveEnum(ScreenPosition location)
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
        bool HasMessage(Player player, string messageId);
        void AddMessage(Player player, string id, Func<Player, string[]> getter, float duration, UIPosition position);
        void AddMessage(Player player, string id, string message, float duration, UIPosition position);
        void RemoveMessage(Player player, string id);
        void CleanupPlayer(Player player);
    }

    public static class UIManager
    {
        private static IUIService _service;
        private static bool _initialized;

        public static void Initialize(IUIService service)
        {
            _service = service;
            _initialized = true;
        }

        public static bool HasMessage(this Player player, string messageId)
        {
            if (_service == null) return false;
            return _service.HasMessage(player, messageId);
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration = 5, ScreenPosition position = ScreenPosition.Center)
        {
            _service?.AddMessage(player, id, getter, duration, UIPosition.FromEnum(position));
        }

        public static void AddMessage(this Player player, string id, string message, float duration = 5, ScreenPosition position = ScreenPosition.Center)
        {
            _service?.AddMessage(player, id, message, duration, UIPosition.FromEnum(position));
        }

        public static void AddMessage(this Player player, string id, string message, float duration, float x, float y)
        {
            _service?.AddMessage(player, id, message, duration, UIPosition.FromXY(x, y));
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration, float x, float y)
        {
            _service?.AddMessage(player, id, getter, duration, UIPosition.FromXY(x, y));
        }

        public static void AddMessage(this Player player, string id, Func<Player, string[]> getter, float duration, UIPosition uIPosition)
        {
            _service?.AddMessage(player, id, getter, duration, uIPosition);
        }

        public static void AddMessage(this Player player, string id, string message, float duration, UIPosition uIPosition)
        {
            _service?.AddMessage(player, id, message, duration, uIPosition);
        }

        public static void RemoveMessage(this Player player, string id)
        {
            _service?.RemoveMessage(player, id);
        }
    }
}
