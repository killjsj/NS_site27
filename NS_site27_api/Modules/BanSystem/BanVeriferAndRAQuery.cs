using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_api.Modules.BanSystem
{
    public class BanSystemConfig : Core.ModuleConfigBase
    {
        public bool EnableCustomBan { get; set; } = true;
    }

    public class BanVeriferAndRAQuery : ModuleBase<BanSystemConfig>
    {
        public override string ModuleName => "BanVeriferAndRAQuery";
        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.PreAuthenticating -= OnPreVerifer;
            LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo -= PlayerEvents_RequestedRaPlayerInfo;

        }

        public override void OnEnable()
        {
            LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo += PlayerEvents_RequestedRaPlayerInfo;
            Exiled.Events.Handlers.Player.PreAuthenticating += OnPreVerifer;
        }

        private void PlayerEvents_RequestedRaPlayerInfo(LabApi.Events.Arguments.PlayerEvents.PlayerRequestedRaPlayerInfoEventArgs ev)
        {
            if(Player.TryGet(ev.Target,out var p))
            {
                if(CommandProcessor.RemoteAdminCommandHandler.TryGetCommand("cban", out var command) && ev.Player.ReferenceHub.queryProcessor.TryGetSender(out var s))
                {
                    if (command.Execute(new ArraySegment<string>(new[] {ev.Player.UserId } ),s,out var re))
                    {
                        ev.InfoBuilder.AppendLine("\n");
                        ev.InfoBuilder.AppendLine(re);
                    }
                }
            }
        }

        public void OnPreVerifer(PreAuthenticatingEventArgs ev) { 
            if(GetSQL() != null)
            {
                var re = GetSQL().QueryBan(ev.UserId);

                if (re != null && re.HasValue) {
                    bool thisServer = re.Value.port != "0" ? re.Value.port == Server.Port.ToString() : true;

                    if (re?.end > DateTime.UtcNow && thisServer)
                    {
                        ev.RejectBanned(re?.reason, re.Value.end,true);
                    }
                }
            }
        }
        private static MySQLConnect GetSQL()
        {
            return CorePlugin.Instance?.connect;
        }
    }
}
