using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.Abilities;
using NS_site27_api.Modules.EventHandle.Handlers;
using NS_site27_api.Modules.MySQL;
using PlayerRoles;
using System;
using System.Collections.Generic;

namespace NS_site27_api.Modules.EventHandle
{
    public class EventHandleConfig : ModuleConfigBase
    {
        public int BroadcastWaitTime { get; set; } = 180;
        public int BroadcastShowTime { get; set; } = 5;
        public int BroadcastSize { get; set; } = 27;
        public string BroadcastColor { get; set; } = "yellow";
        public List<string> BroadcastContext { get; set; } = new List<string> { "示范用1", "示范用2" };
        public string WelcomeContext { get; set; } = "Welcome {player}";
        public bool RoundEndFF { get; set; } = true;
        public string RoundEndFFText { get; set; } = "<size=22><color=#F5FFFA>FF enabled</color></size>";
    }

    public class EventHandleModule : ModuleBase<EventHandleConfig>
    {
        public override string ModuleName => "EventHandle";

        private MySQLConnect _sql;

        public Dictionary<string, List<(bool enable, string card, string text, string holder, string color, string permColor, string displayName, int? rank, bool applyToAll)>> CachedCards
            = new Dictionary<string, List<(bool, string, string, string, string, string, string, int?, bool)>>();

        public Dictionary<ushort, ItemType> CachedCardTypes = new Dictionary<ushort, ItemType>();
        public List<string> NotTodayScp = new List<string>();
        public IFFManager CurrentFFManager;
        public static EventHandleModule Ins { get; private set; }
        public override void OnEnable()
        {

            Exiled.Events.Handlers.Server.WaitingForPlayers += RoundEventHandler.OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted += RoundEventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RespawningTeam += RoundEventHandler.OnRespawningTeam;
            Exiled.Events.Handlers.Server.RoundEnded += RoundEventHandler.OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound += RoundEventHandler.OnRestartingRound;

            Exiled.Events.Handlers.Player.Verified += PlayerEventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned += PlayerEventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Escaping += PlayerEventHandler.OnEscaping;
            Exiled.Events.Handlers.Player.Escaped += PlayerEventHandler.OnEscaped;
            Exiled.Events.Handlers.Player.Left += PlayerEventHandler.OnPlayerLeave;
            Ins = this;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Server.WaitingForPlayers -= RoundEventHandler.OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted -= RoundEventHandler.OnRoundStarted;
            Exiled.Events.Handlers.Server.RespawningTeam -= RoundEventHandler.OnRespawningTeam;
            Exiled.Events.Handlers.Server.RoundEnded -= RoundEventHandler.OnRoundEnded;
            Exiled.Events.Handlers.Server.RestartingRound -= RoundEventHandler.OnRestartingRound;
            Exiled.Events.Handlers.Player.Verified -= PlayerEventHandler.OnVerified;
            Exiled.Events.Handlers.Player.Spawned -= PlayerEventHandler.OnSpawned;
            Exiled.Events.Handlers.Player.Escaping -= PlayerEventHandler.OnEscaping;
            Exiled.Events.Handlers.Player.Escaped -= PlayerEventHandler.OnEscaped;
            Exiled.Events.Handlers.Player.Left -= PlayerEventHandler.OnPlayerLeave;

            BroadcastHandler.Stop();
            CachedCards.Clear();
            CachedCardTypes.Clear();
        }

        public void SetSQL(MySQLConnect sql) => _sql = sql;
    }

    public interface IFFManager
    {
        bool IsDamaging(Player attacker, Player victim);
        float GetFF(Player attacker, Player victim);
    }
}
