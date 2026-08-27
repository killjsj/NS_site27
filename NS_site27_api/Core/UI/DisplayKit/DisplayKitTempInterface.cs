using DisplayKit.Elements;
using Mirror;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Align = UnityEngine.UIElements.Align;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;
using FlexDirection = UnityEngine.UIElements.FlexDirection;
using Justify = UnityEngine.UIElements.Justify;
using Length = UnityEngine.UIElements.Length;
using Overflow = UnityEngine.UIElements.Overflow;
using Position = UnityEngine.UIElements.Position;
using StyleColor = UnityEngine.UIElements.StyleColor;
using StyleFloat = UnityEngine.UIElements.StyleFloat;
using StyleKeyword = UnityEngine.UIElements.StyleKeyword;
using StyleLength = UnityEngine.UIElements.StyleLength;
using StyleRotate = UnityEngine.UIElements.StyleRotate;
using StyleScale = UnityEngine.UIElements.StyleScale;
using StyleTextShadow = UnityEngine.UIElements.StyleTextShadow;
using StyleTransformOrigin = UnityEngine.UIElements.StyleTransformOrigin;
using StyleTranslate = UnityEngine.UIElements.StyleTranslate;
using TextOverflow = UnityEngine.UIElements.TextOverflow;
using UIElements = UnityEngine.UIElements;
using Visibility = UnityEngine.UIElements.Visibility;
using WhiteSpace = UnityEngine.UIElements.WhiteSpace;
using Wrap = UnityEngine.UIElements.Wrap;

namespace DisplayKit.Enums
{
    public enum FontStyle { Normal, Bold, Italic, BoldItalic }

    public enum FontType
    {
        Default, LiberationSans,
        RobotoRegular, RobotoItalic, RobotoBold, RobotoBoldItalic,
        RobotoLight, RobotoLightItalic, RobotoMedium, RobotoMediumItalic,
        RobotoThin, RobotoThinItalic,
        RobotoMonoRegular, RobotoMonoItalic, RobotoMonoBold, RobotoMonoBoldItalic,
        RobotoMonoLight, RobotoMonoLightItalic, RobotoMonoMedium, RobotoMonoMediumItalic,
        RobotoMonoThin, RobotoMonoThinItalic
    }

    public enum CanvasVisibility { Visible, Hidden }
}

namespace DisplayKit
{
    public interface IDisplayElement
    {
        int? Id { get; }
        DisplayCanvas Root { get; }
        IDisplayElement Parent { get; }
        List<IDisplayElement> Children { get; }
        VisualElement BaseElement { get; }
        bool IsLoaded { get; }
        bool HasChanges { get; }
    }

    public interface IDisplayStyleTarget
    {
        BackgroundData Background { get; }
        FlexData Flex { get; }
        AlignData Align { get; }
        SizeData Size { get; }
        SpacingData Spacing { get; }
        BorderData Border { get; }
        PositionData Position { get; }
        TransformData Transform { get; }
        DisplayData Display { get; }
        TextData Text { get; }
    }

    public struct ObserverStatus { }

    public class RecyclableIdGenerator { }

    internal static class ExternObjectCache
    {
        private static readonly Dictionary<string, object> _cache = new();

        public static T GetOrAdd<T>(string key, System.Func<T> factory)
        {
            return (T)_cache.GetOrAdd(key, () => factory());
        }

        public static void Set(string key, object value)
        {
            _cache[key] = value;
        }

        public static bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var o) && o is T t)
            {
                value = t;
                return true;
            }
            value = default!;
            return false;
        }
    }


    public class BackgroundData { public StyleColor Color { get; set; } }

    public class FlexData
    {
        public StyleFloat Grow { get; set; }
        public StyleFloat Shrink { get; set; }
        public StyleLength Basis { get; set; }
        public UIElements.StyleEnum<FlexDirection> Direction { get; set; }
        public UIElements.StyleEnum<Wrap> Wrap { get; set; }
    }

    public class AlignData
    {
        public UIElements.StyleEnum<Align> AlignItems { get; set; }
        public UIElements.StyleEnum<Justify> JustifyContent { get; set; }
        public UIElements.StyleEnum<Align> AlignSelf { get; set; }
        public UIElements.StyleEnum<Align> AlignContent { get; set; }
    }

    public class SizeData
    {
        public StyleLength Width { get; set; }
        public StyleLength Height { get; set; }
        public StyleLength MinWidth { get; set; }
        public StyleLength MinHeight { get; set; }
        public StyleLength MaxWidth { get; set; }
        public StyleLength MaxHeight { get; set; }
    }

    public class SpacingData
    {
        public StyleLength MarginTop { get; set; }
        public StyleLength MarginBottom { get; set; }
        public StyleLength MarginLeft { get; set; }
        public StyleLength MarginRight { get; set; }
        public StyleLength PaddingTop { get; set; }
        public StyleLength PaddingBottom { get; set; }
        public StyleLength PaddingLeft { get; set; }
        public StyleLength PaddingRight { get; set; }
    }

    public class BorderData
    {
        public StyleColor Color { get; set; }
        public StyleColor TopColor { get; set; }
        public StyleColor BottomColor { get; set; }
        public StyleColor LeftColor { get; set; }
        public StyleColor RightColor { get; set; }
        public StyleFloat Width { get; set; }
        public StyleFloat TopWidth { get; set; }
        public StyleFloat BottomWidth { get; set; }
        public StyleFloat LeftWidth { get; set; }
        public StyleFloat RightWidth { get; set; }
        public StyleLength Radius { get; set; }
        public StyleLength TopLeftRadius { get; set; }
        public StyleLength TopRightRadius { get; set; }
        public StyleLength BottomLeftRadius { get; set; }
        public StyleLength BottomRightRadius { get; set; }
    }

    public class PositionData
    {
        public UIElements.StyleEnum<Position> Position { get; set; }
        public StyleLength Top { get; set; }
        public StyleLength Bottom { get; set; }
        public StyleLength Left { get; set; }
        public StyleLength Right { get; set; }
    }

    public class TransformData
    {
        public StyleTranslate Translate { get; set; }
        public StyleScale Scale { get; set; }
        public StyleRotate Rotate { get; set; }
        public StyleTransformOrigin TransformOrigin { get; set; }
    }

    public class DisplayData
    {
        public UIElements.StyleEnum<DisplayStyle> Display { get; set; }
        public UIElements.StyleEnum<Visibility> Visibility { get; set; }
        public StyleFloat Opacity { get; set; }
        public UIElements.StyleEnum<Overflow> Overflow { get; set; }
    }

    public class TextData
    {
        public Enums.FontType? Font { get; set; }
        public UIElements.StyleEnum<Enums.FontStyle> FontStyle { get; set; }
        public StyleLength FontSize { get; set; }
        public StyleColor Color { get; set; }
        public TextAnchor? Align { get; set; }
        public UIElements.StyleEnum<WhiteSpace> Wrap { get; set; }
        public UIElements.StyleEnum<TextOverflow> Overflow { get; set; }
        public StyleLength LetterSpacing { get; set; }
        public StyleLength WordSpacing { get; set; }
        public StyleLength ParagraphSpacing { get; set; }
        public StyleFloat OutlineWidth { get; set; }
        public StyleColor OutlineColor { get; set; }
        public StyleTextShadow TextShadow { get; set; }
    }
}

namespace DisplayKit.Elements
{
    public class DisplayCanvas : IDisplayElement, IDisplayStyleTarget
    {
        public static DisplayCanvas Create()
        {
            return ExternObjectCache.GetOrAdd($"DisplayCanvas.Create_{Time.time}", () => new DisplayCanvas());
        }

        public DisplayCanvas()
        {
            Children = new List<IDisplayElement>();
            Observers = new Dictionary<int, ObserverStatus>();
            IdToElement = new Dictionary<int, IDisplayElement>();
            IdGenerator = new RecyclableIdGenerator();

            Background = new BackgroundData();
            Flex = new FlexData();
            Align = new AlignData();
            Size = new SizeData();
            Spacing = new SpacingData();
            Border = new BorderData();
            Position = new PositionData();
            Transform = new TransformData();
            Display = new DisplayData();
            Text = new TextData();

            BaseElement = new VisualElement();
            Root = this;
            IsLoaded = true;
            HasChanges = false;
        }

        public DisplayElement AddElement()
        {
            var key = $"DisplayCanvas.AddElement.{GetHashCode()}.{Guid.NewGuid()}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var el = new DisplayElement
                {
                    Parent = this,
                    Root = this,
                    BaseElement = new VisualElement()
                };
                Children.Add(el);
                return el;
            });
        }

        public DisplayText AddText(string text = "")
        {
            var key = $"DisplayCanvas.AddText.{GetHashCode()}.{Guid.NewGuid()}.{text}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var t = new DisplayText
                {
                    Parent = this,
                    Root = this,
                    BaseElement = new VisualElement(),
                    Content = text
                };
                Children.Add(t);
                return t;
            });
        }

        public void Spawn()
        {
            ExternObjectCache.Set($"DisplayCanvas.Spawned.{GetHashCode()}", true);
        }

        public void Spawn(int connectionId)
        {
            ExternObjectCache.Set($"DisplayCanvas.Spawned.{GetHashCode()}.{connectionId}", true);
        }

        public void Spawn(ReferenceHub hub)
        {
            ExternObjectCache.Set($"DisplayCanvas.Spawned.{GetHashCode()}.{hub?.GetHashCode().ToString() ?? "null"}", true);
        }

        public void Show()
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}", true);
        }

        public void Hide()
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}", false);
        }

        public void Show(int connectionId)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{connectionId}", true);
        }

        public void Hide(int connectionId)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{connectionId}", false);
        }

        public void Show(ReferenceHub hub)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{hub?.GetHashCode().ToString() ?? "null"}", true);
        }

        public void Hide(ReferenceHub hub)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{hub?.GetHashCode().ToString() ?? "null"}", false);
        }

        public void SetVisibility(bool visible)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}", visible);
        }

        public void SetVisibility(int connectionId, bool visible)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{connectionId}", visible);
        }

        public void SetVisibility(ReferenceHub hub, bool visible)
        {
            ExternObjectCache.Set($"DisplayCanvas.Visibility.{GetHashCode()}.{hub?.GetHashCode().ToString() ?? "null"}", visible);
        }

        public void Destroy()
        {
            ExternObjectCache.Set($"DisplayCanvas.Destroyed.{GetHashCode()}", true);
        }

        public int? Id { get; internal set; }
        public int? SortOrder { get; set; }
        public Enums.CanvasVisibility DefaultVisibility { get; set; }
        public Dictionary<int, ObserverStatus> Observers { get; internal set; }
        public Dictionary<int, IDisplayElement> IdToElement { get; internal set; }
        public bool IsGloballySpawned { get; internal set; }
        public RecyclableIdGenerator IdGenerator { get; internal set; }

        public DisplayCanvas Root { get; internal set; }
        public IDisplayElement Parent { get; internal set; }
        public List<IDisplayElement> Children { get; internal set; }
        public VisualElement BaseElement { get; internal set; }
        public bool IsLoaded { get; internal set; }
        public bool HasChanges { get; internal set; }

        public BackgroundData Background { get; internal set; }
        public FlexData Flex { get; internal set; }
        public AlignData Align { get; internal set; }
        public SizeData Size { get; internal set; }
        public SpacingData Spacing { get; internal set; }
        public BorderData Border { get; internal set; }
        public PositionData Position { get; internal set; }
        public TransformData Transform { get; internal set; }
        public DisplayData Display { get; internal set; }
        public TextData Text { get; internal set; }

        private Action<NetworkConnection> _onPlayerCanvasConstructed;
        public event Action<NetworkConnection> OnPlayerCanvasConstructed
        {
            add => _onPlayerCanvasConstructed += value;
            remove => _onPlayerCanvasConstructed -= value;
        }
    }

    public class DisplayElement : IDisplayElement, IDisplayStyleTarget
    {
        public DisplayElement()
        {
            Children = new List<IDisplayElement>();
            Background = new BackgroundData();
            Flex = new FlexData();
            Align = new AlignData();
            Size = new SizeData();
            Spacing = new SpacingData();
            Border = new BorderData();
            Position = new PositionData();
            Transform = new TransformData();
            Display = new DisplayData();
            Text = new TextData();
            BaseElement = new VisualElement();
            IsLoaded = true;
            HasChanges = false;
        }

        public DisplayElement AddElement()
        {
            var key = $"DisplayElement.AddElement.{GetHashCode()}.{Guid.NewGuid()}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var el = new DisplayElement
                {
                    Parent = this,
                    Root = Root,
                    BaseElement = new VisualElement()
                };
                Children.Add(el);
                return el;
            });
        }

        public DisplayText AddText(string text = "")
        {
            var key = $"DisplayElement.AddText.{GetHashCode()}.{Guid.NewGuid()}.{text}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var t = new DisplayText
                {
                    Parent = this,
                    Root = Root,
                    BaseElement = new VisualElement(),
                    Content = text
                };
                Children.Add(t);
                return t;
            });
        }

        public void Remove()
        {
            ExternObjectCache.Set($"DisplayElement.Removed.{GetHashCode()}", true);
            if (Parent is DisplayCanvas dc)
            {
                _ = dc.Children.Remove(this);
            }
            else if (Parent is DisplayElement de)
            {
                _ = de.Children.Remove(this);
            }
        }

        public int? Id { get; internal set; }
        public DisplayCanvas Root { get; internal set; }
        public IDisplayElement Parent { get; internal set; }
        public List<IDisplayElement> Children { get; internal set; }
        public VisualElement BaseElement { get; internal set; }
        public bool IsLoaded { get; internal set; }
        public bool HasChanges { get; internal set; }

        public BackgroundData Background { get; }
        public FlexData Flex { get; }
        public AlignData Align { get; }
        public SizeData Size { get; }
        public SpacingData Spacing { get; }
        public BorderData Border { get; }
        public PositionData Position { get; }
        public TransformData Transform { get; }
        public DisplayData Display { get; }
        public TextData Text { get; }
    }

    public class DisplayText : IDisplayElement, IDisplayStyleTarget
    {
        public DisplayText()
        {
            Children = new List<IDisplayElement>();
            Background = new BackgroundData();
            Flex = new FlexData();
            Align = new AlignData();
            Size = new SizeData();
            Spacing = new SpacingData();
            Border = new BorderData();
            Position = new PositionData();
            Transform = new TransformData();
            Display = new DisplayData();
            Text = new TextData();
            BaseElement = new VisualElement();
            IsLoaded = true;
            HasChanges = false;
        }

        public DisplayElement AddElement()
        {
            var key = $"DisplayText.AddElement.{GetHashCode()}.{Guid.NewGuid()}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var el = new DisplayElement
                {
                    Parent = this,
                    Root = Root,
                    BaseElement = new VisualElement()
                };
                Children.Add(el);
                return el;
            });
        }

        public DisplayText AddText(string text = "")
        {
            var key = $"DisplayText.AddText.{GetHashCode()}.{Guid.NewGuid()}.{text}";
            return ExternObjectCache.GetOrAdd(key, () =>
            {
                var t = new DisplayText
                {
                    Parent = this,
                    Root = Root,
                    BaseElement = new VisualElement(),
                    Content = text
                };
                Children.Add(t);
                return t;
            });
        }

        public void Remove()
        {
            ExternObjectCache.Set($"DisplayText.Removed.{GetHashCode()}", true);
            if (Parent is DisplayCanvas dc)
            {
                _ = dc.Children.Remove(this);
            }
            else if (Parent is DisplayElement de)
            {
                _ = de.Children.Remove(this);
            }
        }

        public string Content { get; set; }

        public int? Id { get; internal set; }
        public DisplayCanvas Root { get; internal set; }
        public IDisplayElement Parent { get; internal set; }
        public List<IDisplayElement> Children { get; internal set; }
        public VisualElement BaseElement { get; internal set; }
        public bool IsLoaded { get; internal set; }
        public bool HasChanges { get; internal set; }

        public BackgroundData Background { get; }
        public FlexData Flex { get; }
        public AlignData Align { get; }
        public SizeData Size { get; }
        public SpacingData Spacing { get; }
        public BorderData Border { get; }
        public PositionData Position { get; }
        public TransformData Transform { get; }
        public DisplayData Display { get; }
        public TextData Text { get; }
    }
}


namespace DisplayKit
{
    public static class StyleParser
    {
        public static void ParseAndApply(string cssStyle, IDisplayStyleTarget element)
        {
            var styles = Parse(cssStyle);
            ExternObjectCache.Set($"StyleParser.ParseAndApply.{cssStyle}.{element?.GetHashCode().ToString() ?? "null"}", styles);
        }

        public static Dictionary<string, string> Parse(string cssStyle)
        {
            var key = $"StyleParser.Parse.{cssStyle}";
            return ExternObjectCache.GetOrAdd(key, () => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    public static class StyleCodeGen
    {
        public static void WriteAssignments(Dictionary<string, string> styles, string varName, StringBuilder sb)
        {
            if (styles == null)
            {
                return;
            }

            var concatenated = string.Join(";", System.Linq.Enumerable.Select(styles, kv => $"{kv.Key}:{kv.Value}"));
            var key = $"StyleCodeGen.WriteAssignments.{varName}.{concatenated}";
            var cached = ExternObjectCache.GetOrAdd(key, () => concatenated);
            _ = sb.Append(cached);
        }
    }

    public static class StyleIStyleConverter
    {
        public static void Apply(UIElements.IStyle s, IDisplayStyleTarget e)
        {
            var dict = ToDictionary(s);
            ExternObjectCache.Set($"StyleIStyleConverter.Apply.{e?.GetHashCode().ToString() ?? "null"}", dict);
        }

        public static Dictionary<string, string> ToDictionary(UIElements.IStyle s)
        {
            var key = $"StyleIStyleConverter.ToDictionary.IStyle.{s?.GetHashCode().ToString() ?? "null"}";
            return ExternObjectCache.GetOrAdd(key, () => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        public static Dictionary<string, string> ToDictionary(UIElements.IResolvedStyle rs)
        {
            var key = $"StyleIStyleConverter.ToDictionary.IResolvedStyle.{rs?.GetHashCode().ToString() ?? "null"}";
            return ExternObjectCache.GetOrAdd(key, () => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    internal static class CssParse
    {
        public static string StripSuffix(string s, string suffix)
        {
            return string.IsNullOrEmpty(s) || string.IsNullOrEmpty(suffix)
                ? s
                : s.EndsWith(suffix, StringComparison.Ordinal) ? s.Substring(0, s.Length - suffix.Length) : s;
        }

        public static bool TryParseKeyword(string raw, out StyleKeyword keyword)
        {
            keyword = default;
            return false;
        }

        public static StyleFloat ParseFloat(string raw)
        {
            return default;
        }

        public static StyleColor ParseColor(string raw)
        {
            return default;
        }

        public static StyleLength ParseLength(string raw)
        {
            return default;
        }

        public static Length ParseLengthRaw(string s)
        {
            return default;
        }

        public static StyleScale ParseScale(string raw)
        {
            return default;
        }

        public static StyleRotate ParseRotate(string raw)
        {
            return default;
        }

        public static StyleTranslate ParseTranslate(string raw)
        {
            return default;
        }

        public static StyleTransformOrigin ParseTransformOrigin(string raw)
        {
            return default;
        }

        public static StyleTextShadow ParseTextShadow(string raw)
        {
            return default;
        }

        public static T ParseEnum<T>(string raw) where T : struct, Enum
        {
            return default;
        }

        public static Enums.FontType? ParseFontDef(string raw)
        {
            return null;
        }
    }
}
