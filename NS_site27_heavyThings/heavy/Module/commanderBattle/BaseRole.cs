using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Attributes;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Modules.CustomRolePlus;
using PlayerRoles;
using PlayerRoles.Spectating;
using ProjectMER.Commands.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Utils.Networking;
using VoiceChat.Networking;

namespace NS_site27_heavy.heavy.Module.commanderBattle
{
    public static class CommanderGlobalVar
    {
        public static List<ReferenceHub> DoNotRespawn = new();
        public static Dictionary<Player,bool> SpeakingToGlobal = new();
    }
    [CustomRole(PlayerRoles.RoleTypeId.Spectator)]
    public abstract class BaseCommander : CustomRolePlus
    {
        public override uint Id { get; set; } = 83471;
        public override int MaxHealth { get; set; } = 100;
        public override RoleTypeId Role { get; set; } = RoleTypeId.Spectator;
        //public static MainRole r;
        public override void Init()
        {
            base.Init();
            Exiled.Events.Handlers.Player.ChangingRole += cr;
            Exiled.Events.Handlers.Player.VoiceChatting += vc;
        }
        private Type serverRoleType = typeof(ServerRoles);
        protected override void RoleAdded(Player player)
        {
            base.RoleAdded(player);
            CommanderGlobalVar.DoNotRespawn.Add(player.ReferenceHub);
            foreach (var item in Player.Enumerable.Where(x=>!SpectatableVisibilityManager.IsHidden(x.ReferenceHub)))
            {
                player.Connection.Send(new SpectatableVisibilityMessages.SpectatableVisibilityMessage(item.ReferenceHub, CheckVisable(player, item)));
            }
        }
        public void vc(VoiceChattingEventArgs ev)
        {
            if (Check(ev.Player))
            {
                ev.IsAllowed = false;
                var TalkToAll = false;
                if (!CommanderGlobalVar.SpeakingToGlobal.TryGetValue(ev.Player,out TalkToAll))
                {
                    CommanderGlobalVar.SpeakingToGlobal[ev.Player] = false;
                }
                var vm = ev.VoiceMessage;
                if (TalkToAll)
                {
                    vm.Channel = VoiceChat.VoiceChatChannel.Intercom;
                    foreach (var item in Player.Enumerable)
                    {
                        if( CheckVisable(ev.Player, item))
                        {
                            item.Connection.Send<VoiceMessage>(vm);
                        }
                    }
                }
                else
                {
                    vm.Channel = VoiceChat.VoiceChatChannel.Spectator;
                    if(ev.Player.Role.Base is SpectatorRole sr)
                    {
                        if (ReferenceHub.TryGetHubNetID(sr.SyncedSpectatedNetId, out var hub)) {
                            if (Player.TryGet(hub,out var player))
                            {
                                player.Connection.Send(vm);
                            }
                        }
                    }
                }
            }
        }
        public virtual bool CheckVisable(Player player, Player target)
        {
            return true;
        }
        public void cr(ChangingRoleEventArgs ev)
        {
            foreach (var item in this.TrackedPlayers)
            {
                if (ev.Player == item) continue;
                item.Connection.Send(new SpectatableVisibilityMessages.SpectatableVisibilityMessage(item.ReferenceHub, CheckVisable(item, ev.Player)));

            }
            if (Check(ev.Player))
            {
                CommanderGlobalVar.DoNotRespawn.Remove(ev.Player.ReferenceHub);
                CommanderGlobalVar.SpeakingToGlobal.Remove(ev.Player);
                foreach (var item in Player.Enumerable.Where(x => !SpectatableVisibilityManager.IsHidden(x.ReferenceHub)))
                {
                    ev.Player.Connection.Send(new SpectatableVisibilityMessages.SpectatableVisibilityMessage(item.ReferenceHub, true));

                }
            }
        }
        public override void Destroy()
        {
            base.Destroy();
            Exiled.Events.Handlers.Player.ChangingRole -= cr;
            Exiled.Events.Handlers.Player.VoiceChatting -= vc;
        }
    }
}
