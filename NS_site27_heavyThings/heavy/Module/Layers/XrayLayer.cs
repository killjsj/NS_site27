using DisplayKit.Elements;
using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Length = UnityEngine.UIElements.Length;
using Position = UnityEngine.UIElements.Position;

namespace NS_site27_api.Core.UI.DisplayKit.Layers
{
                                                                                    public class XrayLayer : DisplayLayer
    {
        public override string Id { get; set; } = "xray";

        public override TimeSpan updateTime => TimeSpan.FromSeconds(0.05f);

                public float Range { get; set; } = 60f;

                public bool EnemiesOnly { get; set; } = true;

                public int MaxMarkers { get; set; } = 16;

                public float Padding { get; set; } = 0.08f;

        public float BorderWidth { get; set; } = 2f;

                public const float FallbackAspectRatio = 16f / 9f;

                        private readonly Dictionary<DisplayCanvas, List<DisplayElement>> _boxes = new();

        public override void InitNodes(Player target, DisplayCanvas canvas)
        {
            _boxes[canvas] = new List<DisplayElement>();
        }

        public override void DestroyNodes(Player target, DisplayCanvas canvas)
        {
            _ = _boxes.Remove(canvas);
            base.DestroyNodes(target, canvas);
        }

        public override void Update(Player target, DisplayCanvas canvas)
        {
            if (canvas == null || !_boxes.TryGetValue(canvas, out List<DisplayElement> pool))
            {
                return;
            }

            ReferenceHub viewer = target?.ReferenceHub;
            if (viewer == null || viewer.roleManager.CurrentRole is not IFpcRole viewerFpc)
            {
                HideFrom(pool, 0);
                return;
            }

            Vector3 origin = viewerFpc.FpcModule.Position;
            float sqrRange = Range * Range;

                                    float fov = ScreenProjection.GetVerticalFov(viewer);
            float aspect = ScreenProjection.GetAspectRatio(viewer, FallbackAspectRatio);

            int used = 0;

            foreach (Player other in Player.List)
            {
                if (used >= MaxMarkers)
                {
                    break;
                }

                ReferenceHub hub = other?.ReferenceHub;
                if (hub == null || hub == viewer || !other.IsAlive)
                {
                    continue;
                }

                if (hub.roleManager.CurrentRole is not IFpcRole otherFpc)
                {
                    continue;
                }

                                if ((otherFpc.FpcModule.Position - origin).sqrMagnitude > sqrRange)
                {
                    continue;
                }

                bool enemy = HitboxIdentity.IsEnemy(viewer, hub);
                if (EnemiesOnly && !enemy)
                {
                    continue;
                }

                if (!ScreenProjection.TryGetPlayerRect(viewer, hub, out ScreenRect screen, fov))
                {
                    continue;
                }

                Rect rect = Pad(screen.ToUiRect(aspect));

                                if (rect.xMax < 0f || rect.xMin > 1f || rect.yMax < 0f || rect.yMin > 1f)
                {
                    continue;
                }

                Apply(GetBox(canvas, pool, used++), rect, ColorFor(hub, enemy));
            }

            HideFrom(pool, used);
        }

        private Rect Pad(Rect r)
        {
            if (Padding <= 0f)
            {
                return r;
            }

            float px = r.width * Padding;
            float py = r.height * Padding;
            return new Rect(r.x - px, r.y - py, r.width + (px * 2f), r.height + (py * 2f));
        }

                                        private DisplayElement GetBox(DisplayCanvas canvas, List<DisplayElement> pool, int index)
        {
            while (pool.Count <= index)
            {
                DisplayElement box = canvas.AddElement();

                box.Position.Position = new StyleEnum<Position>(Position.Absolute);
                box.Background.Color = new StyleColor(Color.clear);
                box.Border.Width = new StyleFloat(BorderWidth);

                pool.Add(box);
            }

            return pool[index];
        }

        private static void Apply(DisplayElement box, Rect r, Color color)
        {
            box.Display.Display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);

                                    box.Position.Left = new StyleLength(Length.Percent(r.xMin * 100f));
            box.Position.Top = new StyleLength(Length.Percent(r.yMin * 100f));
            box.Size.Width = new StyleLength(Length.Percent(r.width * 100f));
            box.Size.Height = new StyleLength(Length.Percent(r.height * 100f));

            box.Border.Color = new StyleColor(color);
        }

        private static void HideFrom(List<DisplayElement> pool, int index)
        {
            for (int i = index; i < pool.Count; i++)
            {
                pool[i].Display.Display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
        }

        private static Color ColorFor(ReferenceHub target, bool enemy)
        {
            return !enemy
                ? new Color(0.31f, 0.78f, 0.47f)
                : target.GetTeam() switch
                {
                    Team.SCPs => new Color(0.78f, 0.16f, 0.78f),
                    Team.FoundationForces => new Color(0.24f, 0.55f, 1f),
                    Team.ChaosInsurgency => new Color(0.20f, 0.75f, 0.24f),
                    Team.Scientists => new Color(0.94f, 0.90f, 0.55f),
                    Team.ClassD => new Color(1f, 0.55f, 0.16f),
                    _ => new Color(0.90f, 0.24f, 0.24f),
                };
        }
    }
}
