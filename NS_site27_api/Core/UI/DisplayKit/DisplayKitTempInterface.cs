// ai
// ============================================================================
// DisplayKitTempInterface.cs
// 从 DisplayKit*.md 文档中提取的所有 API 接口/类型签名（仅供参考，非编译代码）
// ============================================================================
// 来源文件:
//   DisplayKit.md, DisplayKit-CreatingElements.md, DisplayKit-DeletingElements.md,
//   DisplayKit-ElementReference.md, DisplayKit-Examples.md,
//   DisplayKit-ModifyingElements.md, DisplayKit-SendingToPlayers.md,
//   DisplayKit-StyleProperties.md
// ============================================================================

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
using LengthUnit = UnityEngine.UIElements.LengthUnit;
using Overflow = UnityEngine.UIElements.Overflow;
using Position = UnityEngine.UIElements.Position;
using Rotate = UnityEngine.UIElements.Rotate;
using Scale = UnityEngine.UIElements.Scale;
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
using TextShadow = UnityEngine.UIElements.TextShadow;
using TransformOrigin = UnityEngine.UIElements.TransformOrigin;
using Translate = UnityEngine.UIElements.Translate;
using UIElements = UnityEngine.UIElements;
using Visibility = UnityEngine.UIElements.Visibility;
using WhiteSpace = UnityEngine.UIElements.WhiteSpace;
using Wrap = UnityEngine.UIElements.Wrap;

// ============================================================================
// DisplayKit.Enums
// ============================================================================

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

// ============================================================================
// DisplayKit — 基础接口
// ============================================================================

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

    // ============================================================================
    // 样式数据类
    // ============================================================================

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

// ============================================================================
// DisplayKit.Elements — 核心元素类
// ============================================================================

namespace DisplayKit.Elements
{
    public class DisplayCanvas : IDisplayElement, IDisplayStyleTarget
    {
        public static extern DisplayCanvas Create();
        public extern DisplayElement AddElement();
        public extern DisplayText AddText(string text = "");

        public extern void Spawn();
        public extern void Spawn(int connectionId);
        public extern void Spawn(ReferenceHub hub);

        public extern void Show();
        public extern void Hide();
        public extern void Show(int connectionId);
        public extern void Hide(int connectionId);
        public extern void Show(ReferenceHub hub);
        public extern void Hide(ReferenceHub hub);
        public extern void SetVisibility(bool visible);
        public extern void SetVisibility(int connectionId, bool visible);
        public extern void SetVisibility(ReferenceHub hub, bool visible);

        public extern void Destroy();

        public int? Id { get; }
        public int? SortOrder { get; set; }
        public Enums.CanvasVisibility DefaultVisibility { get; set; }
        public Dictionary<int, ObserverStatus> Observers { get; }
        public Dictionary<int, IDisplayElement> IdToElement { get; }
        public bool IsGloballySpawned { get; }
        public RecyclableIdGenerator IdGenerator { get; }

        public DisplayCanvas Root { get; }
        public IDisplayElement Parent { get; }
        public List<IDisplayElement> Children { get; }
        public VisualElement BaseElement { get; }
        public bool IsLoaded { get; }
        public bool HasChanges { get; }

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

        public extern event Action<NetworkConnection> OnPlayerCanvasConstructed;
    }

    public class DisplayElement : IDisplayElement, IDisplayStyleTarget
    {
        public extern DisplayElement AddElement();
        public extern DisplayText AddText(string text = "");
        public extern void Remove();

        public int? Id { get; }
        public DisplayCanvas Root { get; }
        public IDisplayElement Parent { get; }
        public List<IDisplayElement> Children { get; }
        public VisualElement BaseElement { get; }
        public bool IsLoaded { get; }
        public bool HasChanges { get; }

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
        public extern DisplayElement AddElement();
        public extern DisplayText AddText(string text = "");
        public extern void Remove();

        public string Content { get; set; }

        public int? Id { get; }
        public DisplayCanvas Root { get; }
        public IDisplayElement Parent { get; }
        public List<IDisplayElement> Children { get; }
        public VisualElement BaseElement { get; }
        public bool IsLoaded { get; }
        public bool HasChanges { get; }

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

// ============================================================================
// DisplayKit — 工具类
// ============================================================================

namespace DisplayKit
{
    public static class StyleParser
    {
        public static extern void ParseAndApply(string cssStyle, IDisplayStyleTarget element);
        public static extern Dictionary<string, string> Parse(string cssStyle);
    }

    public static class StyleCodeGen
    {
        public static extern void WriteAssignments(Dictionary<string, string> styles, string varName, StringBuilder sb);
    }

    public static class StyleIStyleConverter
    {
        public static extern void Apply(UIElements.IStyle s, IDisplayStyleTarget e);
        public static extern Dictionary<string, string> ToDictionary(UIElements.IStyle s);
        public static extern Dictionary<string, string> ToDictionary(UIElements.IResolvedStyle rs);
    }

    internal static class CssParse
    {
        public static extern string StripSuffix(string s, string suffix);
        public static extern bool TryParseKeyword(string raw, out StyleKeyword keyword);
        public static extern StyleFloat ParseFloat(string raw);
        public static extern StyleColor ParseColor(string raw);
        public static extern StyleLength ParseLength(string raw);
        public static extern Length ParseLengthRaw(string s);
        public static extern StyleScale ParseScale(string raw);
        public static extern StyleRotate ParseRotate(string raw);
        public static extern StyleTranslate ParseTranslate(string raw);
        public static extern StyleTransformOrigin ParseTransformOrigin(string raw);
        public static extern StyleTextShadow ParseTextShadow(string raw);
        public static extern T ParseEnum<T>(string raw) where T : struct, Enum;
        public static extern Enums.FontType? ParseFontDef(string raw);
    }
}
