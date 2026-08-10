using DisplayKit.Elements;
using Exiled.API.Features;
using NS_site27_api.Core.UI;
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
    /// <summary>
    /// Outlines nearby players through walls, one box per target.
    ///
    /// <para>
    /// Unlike world-space markers, UI Toolkit elements are not part of the 3D scene, so nothing can
    /// occlude them — that is what makes this an x-ray rather than an outline.
    /// </para>
    /// <para>
    /// DisplayKit positions elements in screen space only — <c>PositionData</c> is
    /// Top/Bottom/Left/Right as <c>StyleLength</c>, with no world-space anchoring — so the server
    /// projects. Both inputs are available per viewer: vertical FOV from
    /// <c>70f / IZoomModifyingItem.ZoomAmount</c> (so scopes and ADS are exact), and aspect ratio
    /// from <c>AspectRatioSync.AspectRatio</c>, which the client reports. Both are re-read every
    /// tick so a scope going up or a resolution change is picked up immediately.
    /// </para>
    /// <para>
    /// Boxes use <see cref="Length.Percent"/> against the canvas, so no pixel resolution is needed
    /// on either side.
    /// </para>
    /// </summary>
    public class XrayLayer : DisplayLayer
    {
        public override string Id { get; set; } = "xray";

        public override TimeSpan updateTime => TimeSpan.FromSeconds(0.05f);

        /// <summary>Targets beyond this are not revealed.</summary>
        public float Range { get; set; } = 60f;

        /// <summary>When false, allies are outlined too, in their own colour.</summary>
        public bool EnemiesOnly { get; set; } = true;

        /// <summary>Maximum simultaneous boxes. Elements are pooled up to this count.</summary>
        public int MaxMarkers { get; set; } = 16;

        /// <summary>Extra margin around each target, as a fraction of the projected box.</summary>
        public float Padding { get; set; } = 0.08f;

        public float BorderWidth { get; set; } = 2f;

        /// <summary>Used only until the viewer's client has reported its real aspect ratio.</summary>
        public const float FallbackAspectRatio = 16f / 9f;

        // One pool per canvas: the runner creates a canvas per (player, layer) pair, and the same
        // layer instance is shared across all of them.
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
            if (viewer == null || !(viewer.roleManager.CurrentRole is IFpcRole viewerFpc))
            {
                HideFrom(pool, 0);
                return;
            }

            Vector3 origin = viewerFpc.FpcModule.Position;
            float sqrRange = Range * Range;

            // Both resolved once per tick rather than cached, so a scope going up or a mid-round
            // resolution change is picked up on the next refresh.
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

                if (!(hub.roleManager.CurrentRole is IFpcRole otherFpc))
                {
                    continue;
                }

                // No line-of-sight check on purpose: seeing through geometry is the feature.
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

                // Fully off-screen: skip rather than clamp, so markers do not pile up at the edges.
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

        /// <summary>
        /// Elements are created once and reused. Creating and removing them every tick would defeat
        /// DisplayKit's caching and churn the client's visual tree.
        /// </summary>
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

            // Percent resolves against the parent's box, which is the full-screen canvas — so these
            // are literally viewport fractions and no pixel resolution is ever needed.
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
            if (!enemy)
            {
                return new Color(0.31f, 0.78f, 0.47f);
            }

            return target.GetTeam() switch
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
