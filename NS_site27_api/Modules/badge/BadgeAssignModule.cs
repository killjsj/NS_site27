using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using NS_site27_api.Modules.PlayerManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_api.Modules.Badge
{
    public class BadgeAssignConfig : ModuleConfigBase
    {

    }
    public class BadgeAssignModule : ModuleBase<BadgeAssignConfig>
    {
        public static MySQLConnect sql => Plugin.Instance?.connect;
        public override string ModuleName => "BadgeAssignModule";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.Verified -= Player_Verified;

        }

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Player.Verified += Player_Verified;
        }
        public static void Player_Verified(VerifiedEventArgs ev)
        {
            var PB = sql.QueryBadge(userid: ev.Player.UserId);
            if (PB != null)
            {
                if (PB.Count > 0)
                {
                    foreach (var item in PB)
                    {
                        if (item.is_permanent || item.expiration_date <= DateTime.Now)
                        {
                            var text = item.badge;
                            List<string> colors = new List<string>();
                            item.color.Split(',').ForEach(c => colors.Add(c));
                            PlayerStateManager.badges[ev.Player.UserId] = (item.player_name, text, colors, item.expiration_date, item.is_permanent, item.notes);
                            break;
                        }
                    }
                }
            }
        }
    }
}
