using DisplayKit.Elements;
using Exiled.API.Features;
using NS_site27_api.Core.UI.DisplayKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static NS_site27_api.Modules.PlayerManagement.PlayerManagerDisplayKitHUD;

namespace NS_site27_api.Modules.Lobby
{
    class LobbyLayer : DisplayLayer
    {
        public override string Id { get; set; } = "PlayerCountLayer";

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

            // start define of PlayerCount
            DisplayElement PlayerCount = VisualElement.AddElement();
            PlayerCount.BaseElement.name = "PlayerCount";
            PlayerCount.Position.Position = Position.Absolute;
            PlayerCount.Position.Left = Length.Percent(40f);
            PlayerCount.Position.Right = 0f;
            PlayerCount.Position.Top = Length.Percent(22f);
            PlayerCount.Size.Height = Length.Percent(5f);
            PlayerCount.Size.Width = Length.Percent(20f);
            PlayerCount.Text.FontSize = 27f;
            PlayerCount.Text.Color = new Color(1f, 0.7529412f, 0f, 1f);

            // start define of PlayerCountText
            DisplayText PlayerCountText = PlayerCount.AddText("");
            PlayerCountText.BaseElement.name = "PlayerCountText";
            PlayerCountText.Text.Color = new Color(0.4666667f, 0f, 1f, 1f);

            canvas.SortOrder = 255;
        }

        public override void Update(Player target, DisplayCanvas canvas)
        {
            try {
                foreach (var item in canvas.Children)
                {
                    if (item.BaseElement.name == "PlayerCountText" && item is DisplayText t)
                    {
                        var res = "";
                        res = Round.IsLobby ? LobbyModule.ShowingString : "";
                        if (t.Content != res)
                        {
                            t.Content = res;
                        }
                        break;
                    }
                }
            }catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
