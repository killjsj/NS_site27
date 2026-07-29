using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;

namespace NS_site27_api.Modules.Admin
{
    public class AdminLoggerConfig : ModuleConfigBase
    {

    }
    public class AdminLoggerModule : ModuleBase<AdminLoggerConfig>
    {
        public static MySQLConnect sql => Plugin.Instance?.connect;
        public override string ModuleName => "AdminLoggerModule";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.SentValidCommand -= SentValidCommand;

        }

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Player.SentValidCommand += SentValidCommand;
        }
        public static void SentValidCommand(SentValidCommandEventArgs ev)
        {
            if (ev.Player.RemoteAdminAccess && ev.Type == LabApi.Features.Enums.CommandType.RemoteAdmin)
            {
                _ = ev.Player.Group;
                if (AdminAssignModule.CachedGroups.ContainsKey(ev.Player))
                {
                    _ = AdminAssignModule.CachedGroups[ev.Player];
                }
                _ = sql?.LogAdminPermissionAsync(ev.Player.UserId, ev.Player.DisplayNickname, Exiled.API.Features.Server.Port, ev.Query, ev.Response, group: ev.Player.Group.Name);
            }
        }
    }
}
