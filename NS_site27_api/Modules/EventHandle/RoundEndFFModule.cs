using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using GameCore;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.EventHandle
{
    public class RoundEndFFConfig : ModuleConfigBase
    {
        public bool EnableFFOnRoundEnd { get; set; } = true;
        public string FFEnabledMessage { get; set; } = "<size=22><color=#F5FFFA>FF enabled</color></size>";
    }

    /// <summary>
    /// Enables friendly fire when the round ends
    /// </summary>
    public class RoundEndFFModule : ModuleBase
    {
        public override string ModuleName => "RoundEndFF";

        private RoundEndFFConfig _config;

        public override void OnEnable()
        {
            _config = GetConfig<RoundEndFFConfig>();
            if (!_config.IsEnabled) return;

            if (_config.EnableFFOnRoundEnd)
                ServerHandlers.RoundEnded += OnRoundEnded;
        }

        public override void OnDisable()
        {
            ServerHandlers.RoundEnded -= OnRoundEnded;
        }

        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            ServerConsole.FriendlyFire = true;
            ServerConfigSynchronizer.RefreshAllConfigs();
            foreach (var player in Player.List)
            {
                player.AddMessage("RoundEndFF", _config.FFEnabledMessage, 2, ScreenPosition.CenterTop);
            }
        }
    }
}
