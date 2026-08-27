using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.MessageModule;
using NS_site27_api.Modules.PlayerManagement;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;

namespace NS_site27_api.Modules.ItemCleaner
{
    public class ItemCleanerConfig : ModuleConfigBase
    {
        public string StartingClean { get; set; } = "<color=yellow><size=23.5>Site27扫地机将在{second}s后开始清理</size></color>";
        public string DoneClean { get; set; } = "<color=green><size=23.5>Site27扫地机清理完成 共{body}个尸体和{item}个物品</size></color>";
        public int startCountDownTime { get; set; } = 10;
        public int CleanTime { get; set; } = 300;
        [YamlMember(Description = "决定扫地机提示y轴 范围: 0-1000 值越高 位置越高")]
        public int yPos { get; set; } = 865;
        public float DoneCleanShowTime { get; set; } = 5;
        public ItemType[] WhiteList = (ItemType[])Enum.GetValues(typeof(ItemType));
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
            Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
            Ins = this;
        }

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.Verified -= OnVerified;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundEnded -= OnRoundEnded;
            Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
        }
        private static CoroutineHandle _handle;
        private static bool _stop;
        private void OnVerified(VerifiedEventArgs ev)
        {
        }
        public static void RoundStarted()
        {
            _stop = false;
            _handle = Timing.RunCoroutine(Cleaner());
        }
        public static void OnWaitingForPlayers()
        {

        }

        public static void OnRoundEnded(RoundEndedEventArgs ev)
        {
            _stop = true;
            if (_handle.IsRunning)
            {
                _ = Timing.KillCoroutines(_handle);
            }
        }
        public static string ShowingStr = "";
        public static bool countdownstart = false;
        private static IEnumerator<float> Cleaner()
        {
            var module = ItemCleanerModule.Ins;
            if (module == null)
            {
                yield break;
            }

            var cfg = module.Config;
            float counter = -cfg.DoneCleanShowTime;

            while (!_stop)
            {
                yield return Timing.WaitForSeconds(0.4f);
                counter += 0.4f;
                if (counter <= cfg.CleanTime - cfg.startCountDownTime)
                {
                }
                else if (counter <= cfg.CleanTime)
                {
                    if (!countdownstart)
                    {
                        foreach (var item in Player.Enumerable)
                        {
                            item.AddHint("clean_startcountdown", cfg.CleanTime - counter, x => new MsgUpdateResult() { Content = ShowingStr, Title = "Clean!", NoticeCircleColor = Color.red }, PriorityLevel.High);
                        }
                        countdownstart = true;
                    }
                    ShowingStr = cfg.StartingClean.Replace("{second}", (cfg.CleanTime - counter).ToString("F0"));
                }
                else
                {
                    countdownstart = false;
                    foreach (var item in Player.Enumerable)
                    {
                        item.RemoveHint("clean_startcountdown");
                    }
                    ShowingStr = "";
                    counter = -cfg.DoneCleanShowTime;
                    _ = CleanItem();
                }
            }
        }
        public static bool showstart = false;
        public static async Awaitable CleanItem()
        {
            await Awaitable.MainThreadAsync();
            try
            {
                var module = ItemCleanerModule.Ins;
                var cfg = module.Config;
                int cleanedItemCount = 0;
                int cleanedBodyCount = 0;
                foreach (var item in Ragdoll.List.ToArray())
                {
                    var clean = true;
                    foreach (var s049 in PlayerHUDManager.Scp.Where(x => x.Role.Type == RoleTypeId.Scp049))
                    {
                        if (Vector3.Distance(s049.Position, item.Position) < 20)
                        {
                            clean = false;
                            break;
                        }
                    }
                    if (clean)
                    {
                        item.Destroy();
                        cleanedBodyCount++;
                    }
                    if (cleanedBodyCount % 10 == 9)
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }
                foreach (var item in Pickup.List.ToArray())
                {
                    var clean = true;
                    foreach (var player in Player.Enumerable.Where(x => x.IsAlive))
                    {
                        if (Vector3.Distance(player.Position, item.Position) < 20)
                        {
                            clean = false;
                            break;
                        }
                    }
                    if (cfg.WhiteList.Contains(item.Type))
                    {
                        clean = false;
                        continue;
                    }
                    if (clean)
                    {
                        item.Destroy();
                        cleanedItemCount++;
                    }
                    if (cleanedItemCount % 20 == 19)
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }
                foreach (var item in Player.Enumerable)
                {

                    item.AddHint("DoneCleanShow", cfg.DoneCleanShowTime, x => new MsgUpdateResult() { Content = ShowingStr, Title = "Cleaned", NoticeCircleColor = Color.green });
                }

                ShowingStr = cfg.DoneClean.Replace("{body}", cleanedBodyCount.ToString()).Replace("{item}", cleanedItemCount.ToString());
                await Awaitable.WaitForSecondsAsync(cfg.DoneCleanShowTime);
                foreach (var item in Player.Enumerable)
                {
                    item.RemoveHint("DoneCleanShow");
                }
                ShowingStr = "";
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
