using Exiled.API.Features;
using MEC;
using NS_site27_api.Extensions;
using NS_site27_heavy.Core;
using PlayerRoles.Spectating;
using System;
using System.Collections.Generic;

namespace NS_site27_heavy.heavy.Module.Testing
{
    public class DummyPairs
    {
        public Player Master;
        public Player Dummy;
        public bool isSwaped = false;
    }
    public enum SwapMode
    {
        Toggle,
        ToMaster,
        ToDummy,
    }
    internal class DummySwap : IModule
    {
        public static Dictionary<Player, DummyPairs> playerToDummys = new();
        public static DummyPairs CreateOrGetRemoteDummy(Player master)
        {
            if (playerToDummys.TryGetValue(master, out var ds))
            {
                return ds;
            }
            var np = Npc.Spawn($"dummy of {master.Nickname}", master.Role.Type, true, master.Position);
            _ = Timing.CallDelayed(0.5f, () =>
            {
                _ = np.TryLookDirection(master.CameraTransform.forward);
                np.ReferenceHub.serverRoles.NetworkHideFromPlayerList = true;
                SpectatableVisibilityManager.SetHidden(np.ReferenceHub, true);
            });
            var dp = new DummyPairs() { Master = master, Dummy = np };
            playerToDummys[master] = dp;
            return dp;
        }
        public static bool HaveSwapDummy(Player master)
        {
            return playerToDummys.ContainsKey(master);
        }
        public static bool TryToSwap(Player master, SwapMode swapMode = SwapMode.Toggle)
        {
            try
            {
                if (HaveSwapDummy(master))
                {
                    var dp = CreateOrGetRemoteDummy(master);
                    if (dp.isSwaped && swapMode == SwapMode.ToDummy)
                    {
                        return false;
                    }

                    if (!dp.isSwaped && swapMode == SwapMode.ToMaster)
                    {
                        return false;
                    }

                    SwapAB(master, dp.Dummy);
                    dp.isSwaped = !dp.isSwaped;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }
        }
        public string ModuleName => "Swaper";

        public bool IsEnabled => true;

        public static IEnumerator<float> Update()
        {
            while (true)
            {
                try
                {
                    foreach (var item in playerToDummys)
                    {
                        item.Value.Dummy.CurrentItem = item.Value.Master.CurrentItem;
                        item.Value.Dummy.RankColor = item.Value.Master.RankColor;
                        item.Value.Dummy.RankName = item.Value.Master.RankName;
                        item.Value.Dummy.Wearables = item.Value.Master.Wearables;
                        item.Value.Dummy.DisplayNickname = item.Value.Master.DisplayNickname;
                        item.Value.Dummy.CustomInfo = item.Value.Master.CustomInfo;
                        item.Value.Dummy.InfoArea = item.Value.Master.InfoArea;
                        item.Value.Dummy.Health = item.Value.Master.Health;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                yield return Timing.WaitForSeconds(0.5f);
            }
        }
        public static void SwapAB(Player a, Player b)
        {
            var prevRot = a.Rotation;
            var prevPos = a.Position;

            a.Position = b.Position;
            a.Rotation = b.Rotation;

            b.Position = prevPos;
            b.Rotation = prevRot;
        }
        public static CoroutineHandle ch;
        public void OnEnable()
        {
            Exiled.Events.Handlers.Server.RestartingRound += Restart;
            Exiled.Events.Handlers.Server.WaitingForPlayers += Wait;
        }

        public void OnDisable()
        {
            Exiled.Events.Handlers.Server.RestartingRound -= Restart;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= Wait;
        }
        public static void Restart()
        {
            playerToDummys.Clear();
            if (ch.IsRunning)
            {
                _ = Timing.KillCoroutines(ch);
            }
        }
        public static void Wait()
        {
            ch = Timing.RunCoroutine(Update());
        }
        public void OnReloadConfig()
        {
        }
    }
}
