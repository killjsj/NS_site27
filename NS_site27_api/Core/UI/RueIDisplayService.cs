using Exiled.API.Features;
using MEC;
using NS_site27_api.Core.UI;
using ProjectMER.Commands.Modifying.Position;
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

        public void AddMessage(Player player, string id, Func<Player, string[]> getter,UIPosition position)
        {
            
            var r = RueDisplay.Get(player);
            var e = new DynamicElement(position.Y,x =>
            {
                if (x == null) return "";
                var re = getter(Player.Get(x));
                List<string> strings = new();
                string rf = "<line-height=70%>";
                if(position.margin > 0)
                    rf += $"<margin={position.margin}>";
                foreach (var item in re)
                {
                    rf += item + "\n";
                }
                if(position.margin > 0)
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

        public void AddMessage(Player player, string id, string message,  UIPosition position)
        {
            var r = RueDisplay.Get(player);
            r.Show(new RueI.API.Elements.Tag(id), new BasicElement(position.Y, BuildHintString(message, position.margin)) {
                ResolutionBasedAlign = true
            });
        }

        public void RemoveMessage(Player player, string id)
        {
            var r = RueDisplay.Get(player);
            r.Remove(new Tag(id));
        }

        private string BuildHintString(string message, float xPosition)
        {
            string result = "<line-height=70%>";
            List<string> strings = new();
            string rf = "<line-height=70%>";
            strings.AddRange(message.Split('\n'));
            
            foreach (var item in strings)
            {
                rf += $"<margin={xPosition}>";
                rf += item + "\n";
            }
            rf += "</margin>";
            rf += "</line-height>";
            return rf;
        }

        public void CleanupPlayer(Player player)
        {
        }
    }
}
