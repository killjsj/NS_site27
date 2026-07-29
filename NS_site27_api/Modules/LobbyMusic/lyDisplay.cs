using DisplayKit.Elements;
using Exiled.API.Features;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Modules.LobbyMusic;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NS_site27_api.Modules.Lobby
{
    public class lyDisplay : DisplayLayer
    {
        public override string Id { get; set; } = "lyDisplay";

        public override void InitNodes(Player target, DisplayCanvas canvas)
        {
            // start define of VisualElement
            DisplayElement VisualElement = canvas.AddElement();
            VisualElement.BaseElement.name = "VisualElement";
            VisualElement.Position.Position = Position.Absolute;
            VisualElement.Size.Width = Length.Percent(100f);
            VisualElement.Size.Height = Length.Percent(100f);
            VisualElement.Position.Top = 0f;
            VisualElement.Position.Left = 0f;
            VisualElement.Position.Right = 0f;

            // start define of LobbySong
            DisplayElement LobbySong = VisualElement.AddElement();
            LobbySong.BaseElement.name = "LobbySong";
            LobbySong.Flex.Grow = 1f;
            LobbySong.Position.Position = Position.Absolute;
            LobbySong.Position.Right = Length.Percent(0f);
            LobbySong.Position.Bottom = Length.Percent(95f);
            LobbySong.Position.Left = Length.Percent(85f);
            LobbySong.Position.Top = Length.Percent(0f);

            // start define of Althrougth_it_not_be_long_here_but_how_cares
            DisplayElement Althrougth_it_not_be_long_here_but_how_cares = LobbySong.AddElement();
            Althrougth_it_not_be_long_here_but_how_cares.BaseElement.name = "Althrougth_it_not_be_long_here_but_how_cares";
            Althrougth_it_not_be_long_here_but_how_cares.Flex.Grow = 1f;
            Althrougth_it_not_be_long_here_but_how_cares.Position.Position = Position.Absolute;

            // start define of lyric
            DisplayText lyric = LobbySong.AddText("");
            lyric.BaseElement.name = "lyric";
            lyric.Text.Color = new Color(0f, 1f, 0.9843137f, 1f);
            lyric.Position.Position = Position.Relative;
            lyric.Flex.Direction = FlexDirection.Column;
            lyric.Align.AlignItems = Align.FlexEnd;


        }

        public override void Update(Player target, DisplayCanvas canvas)
        {
            try
            {
                foreach (var item in canvas.Children)
                {
                    if (item.BaseElement.name == "lyric" && item is DisplayText t)
                    {
                        var res = "";
                        if (!(string.IsNullOrEmpty(LobbyMusicManager.Instance.CurrentSongName) || LobbyMusicManager.Instance.sessionId == 0))
                        {
                            string lrcText = "";
                            if (LobbyMusicManager.Instance._lrcLines != null)
                            {
                                for (int i = LobbyMusicManager.Instance._lrcLines.Count - 1; i >= 0; i--)
                                {
                                    if (LobbyMusicManager.Instance._lrcLines[i].Time <= LobbyMusicManager.Instance.current)
                                    {
                                        lrcText = LobbyMusicManager.Instance._lrcLines[i].Text;
                                        break;
                                    }
                                }
                            }
                            string timeStr = LobbyMusicManager.Instance.TotalTime > 0
                                ? $"{LobbyMusicManager.FormatTime(LobbyMusicManager.Instance.current)}/{LobbyMusicManager.FormatTime((float)LobbyMusicManager.Instance.TotalTime)}"
                                : LobbyMusicManager.FormatTime(LobbyMusicManager.Instance.current);

                            res = $"<align=right><size=14><line-height=45%><color=#00FFFF50>[{timeStr}]:{LobbyMusicManager.Instance.CurrentSongName}({LobbyMusicManager.Instance.GetSourceName()})\n{lrcText}</color></line-height></size></align>";
                        }
                        else
                        {
                            res = "";
                        }
                        if (t.Content != res)
                        {
                            t.Content = res;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }
}
