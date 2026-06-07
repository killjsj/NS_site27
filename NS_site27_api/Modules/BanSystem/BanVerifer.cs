using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Modules.MySQL;
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

    public class BanVerifer : ModuleBase<BanSystemConfig>
    {
        public override string ModuleName => "BanVerifier";
        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.PreAuthenticating -= OnPreVerifer;

        }

        public override void OnEnable()
        {
                
            Exiled.Events.Handlers.Player.PreAuthenticating += OnPreVerifer;
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
