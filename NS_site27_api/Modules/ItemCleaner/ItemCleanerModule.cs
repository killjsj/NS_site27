using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.Abilities;
using NS_site27_api.Modules.EventHandle.Handlers;
using NS_site27_api.Modules.MySQL;
using NS_site27_api.Modules.PlayerManagement;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NS_site27_api.Modules.ItemCleaner
{
    public class ItemCleanerConfig : ModuleConfigBase
    {
        public string StartingClean { get; set; } = "<color=yellow><size=22>Site27扫地机将在{second}s后开始清理</size></color>";
        public string DoneClean { get; set; } = "<color=green><size=22>Site27扫地机清理完成~</size></color>";
        public int startCountDownTime { get; set; } = 10;
        public int CleanTime { get; set; } = 300;
        public float DoneCleanShowTime { get; set; } = 5;
    }

    public class ItemCleanerModule : ModuleBase<ItemCleanerConfig>
    {
        public override string ModuleName => "ItemCleaner";
        public static ItemCleanerModule Ins { get; private set; }
        public override void OnEnable()
        {

            Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified += OnVerified;
            Exiled.Events.Handlers.Server.RoundEnded += OnRoundEnded;
            Ins = this;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.Verified -= OnVerified;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
        }
        private static CoroutineHandle _handle;
        private static bool _stop;
        private static void OnVerified(VerifiedEventArgs ev)
        {
            if (!_stop) { 
                if(ev.Player != null)
                {
                    ev.Player.AddMessage("ItemCleaner", GetDisplayText, -1, 0,930);
                }
            }
        }
        public static string[] GetDisplayText(Player p)
        {
            return ShowingStr;
        }
        public static void OnWaitingForPlayers()
        {
            _stop = false;
            _handle = Timing.RunCoroutine(Cleaner());
        }

        public static void OnRoundEnded(RoundEndedEventArgs ev)
        {
            _stop = true;
            if (_handle.IsRunning) Timing.KillCoroutines(_handle);
            foreach (var player in Player.Enumerable)
                player.RemoveMessage("ItemCleaner");
        }
        public static string[] ShowingStr = new[] { "" };
        private static IEnumerator<float> Cleaner()
        {
            var module = ItemCleanerModule.Ins;
            if (module == null) yield break;

            float counter = 0;
            var cfg = module.Config;

            while (!_stop)
            {
                yield return Timing.WaitForSeconds(0.4f);
                counter +=0.4f;
                if (counter <= cfg.CleanTime - cfg.startCountDownTime)
                {
                    ShowingStr[0] = "";  // 清理期静默
                }
                else if (counter <= cfg.CleanTime)
                {
                    ShowingStr[0] = cfg.StartingClean.Replace("{second}", (cfg.CleanTime - counter).ToString("F0"));
                }
                else if (counter <= cfg.CleanTime + cfg.DoneCleanShowTime)
                {
                    ShowingStr[0] = cfg.DoneClean;
                }
                else
                {
                    ShowingStr[0] = "";
                    counter = 0;
                }
            }
        }
        public static void CleanItem()
        {
            try
            {
                foreach (var item in Ragdoll.List)
                {
                    var clean = true;
                    foreach (var s049 in PlayerHUDManager.Scp.Where(x=>x.Role.Type == RoleTypeId.Scp049))
                    {
                        if(Vector3.Distance(s049.Position, item.Position) < 20)
                        {
                            clean = false;
                            break;
                        }
                    }
                    if (clean)
                    {
                        item.Destroy();
                    }
                }
                foreach (var item in Pickup.List)
                {
                    var clean = true;
                    foreach (var player in Player.Enumerable)
                    {
                        if (Vector3.Distance(player.Position, item.Position) < 20)
                        {
                            clean = false;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
