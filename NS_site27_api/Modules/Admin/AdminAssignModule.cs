using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Permissions.Features;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_api.Modules.Admin
{
    public class AdminAssignConfig : ModuleConfigBase
    {

    }
    public class AdminAssignModule : ModuleBase<AdminAssignConfig>
    {
        public static MySQLConnect sql => Plugin.Instance?.connect;
        public override string ModuleName => "AdminAssignModule";
        public static Dictionary<Player,UserGroup> CachedGroups = new();
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
            var PA = sql?.QueryAdmin(userid: ev.Player.UserId);
            (string player_name, string port, string permissions, DateTime expiration_date, bool is_permanent, string notes)? target = null;
            if (PA != null)
            {
                if (PA.Count > 0)
                {
                    foreach (var item in PA)
                    {
                        if (item.port == ServerStatic.ServerPort.ToString() || item.port == "0")
                        {

                            target = item;
                            break;
                        }
                    }
                    if (target != null)
                    {
                        var UserGroup = ServerStatic.PermissionsHandler.GetGroup(target.Value.permissions);
                        if (UserGroup != null)
                        {

                            if (ev.Player.Group == null || (ev.Player.Group != null && ev.Player.Group.KickPower < UserGroup.KickPower))
                            {
                                Log.Info($"player {ev.Player} set group:{UserGroup.Name} due AdminSystem");
                                ev.Player.Group = UserGroup.Clone();
                                ev.Player.RankName = $"({UserGroup.Name})";
                            }
                        }
                        else
                        {
                            Log.Info($"failed to Set group! target:{target.Value.permissions}");
                        }
                    }
                }
            }
            if (ev.Player.Group != null)
            {
                CachedGroups[ev.Player] = ev.Player.Group.Clone();
            }
        }
    }
}
