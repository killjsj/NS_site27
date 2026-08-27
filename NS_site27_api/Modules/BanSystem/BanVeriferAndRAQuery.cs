using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
using RemoteAdmin;
using System;

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
                                                                                                                                            }

        public async void OnPreVerifer(PreAuthenticatingEventArgs ev)
        {
            try
            {
                if (GetSQL() != null)
                {
                    var re = await GetSQL().QueryBanAsync(ev.UserId);

                    if (re != null && re.HasValue)
                    {
                        bool thisServer = re.Value.port == "0" || re.Value.port == Server.Port.ToString();

                        if (re?.end > DateTime.UtcNow && thisServer)
                        {
                            ev.RejectBanned(re?.reason, re.Value.end, true);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Error($"[BanVerifer] OnPreVerifer: {ex}"); }
        }
        private static MySQLConnect GetSQL()
        {
            return CorePlugin.Instance?.connect;
        }
    }
}
