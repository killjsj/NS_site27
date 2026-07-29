using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using NS_site27_api.Modules.PlayerManagement;
using System;
using System.Collections.Generic;

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
        public static async void Player_Verified(VerifiedEventArgs ev)
        {
            try
            {
                if (sql == null)
                {
                    return;
                }

                var PB = await sql.QueryBadgeAsync(userid: ev.Player.UserId);
                if (PB != null)
                {
                    if (PB.Count > 0)
                    {

                        foreach (var (player_name, badge, color, expiration_date, is_permanent, notes) in PB)
                        {
                            if (is_permanent || expiration_date <= DateTime.Now)
                            {
                                var text = badge;
                                List<string> colors = new();
                                color.Split(',').ForEach(colors.Add);
                                PlayerStateManager.badges[ev.Player.UserId] = (player_name, text, colors, expiration_date, is_permanent, notes);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[BadgeAssign] Player_Verified: {ex}"); }
        }
    }
}
