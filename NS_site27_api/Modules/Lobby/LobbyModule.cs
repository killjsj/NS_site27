using CentralAuth;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using GameCore;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI.DisplayKit;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_api.Modules.Lobby
{
    public class LobbyModule : ModuleBase<LobbyConfig>
    {
        public override string ModuleName => "LobbyModule";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.Verified -= Verified;
            Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
        }

        public override void OnEnable()
        {
            Exiled.Events.Handlers.Player.Verified += Verified;
            Exiled.Events.Handlers.Server.RoundStarted += RoundStarted;
            Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
        }
        public void Verified(VerifiedEventArgs ev)
        {
            ev.Player.RoleManager.ServerSetRole(PlayerRoles.RoleTypeId.Tutorial, PlayerRoles.RoleChangeReason.None, PlayerRoles.RoleSpawnFlags.All);
            ev.Player.AddLayer("PlayerCountLayer");
        }
        public CoroutineHandle handle;
        public void RoundStarted()
        {
            _ = Timing.KillCoroutines(handle);
            foreach (var item in Player.Enumerable)
            {
                item.RemoveLayer("PlayerCountLayer");
            }
        }
        public void WaitingForPlayers()
        {
            GameObject.Find("StartRound").transform.localScale = Vector3.zero;
            handle = Timing.RunCoroutine(PlayerRefreshLoop());
        }
        public static string ShowingString = "";
        private IEnumerator<float> PlayerRefreshLoop()
        {
            while (Round.IsLobby && RoundStart.singleton.Timer != -1)
            {
                try
                {
                    string re = $"<size=23><color=#00FFFF>👥当前玩家数量:{ReferenceHub.GetPlayerCount(ClientInstanceMode.ReadyClient, ClientInstanceMode.Host, ClientInstanceMode.Dummy)} ";
                    if (Round.IsLobbyLocked)
                    {
                        re += "<color=yellow>大厅锁定 ";

                    }
                    else if (RoundStart.singleton.Timer == -2)
                    {
                        re += "<color=red>玩家数量不足! ";
                    }
                    else
                    {
                        re += $"距离开始还剩:{RoundStart.singleton.Timer} 秒! ";
                    }
                    re += "</size></color>";
                    ShowingString = re;
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                yield return Timing.WaitForSeconds(0.2f);
            }
            ShowingString = "";
        }

    }

    public class LobbyConfig : ModuleConfigBase
    {
    }
}
