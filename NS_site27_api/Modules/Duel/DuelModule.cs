using CommandSystem;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using PlayerRoles;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Utils;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace NS_site27_api.Modules.Duel
{
    public class DuelConfig : ModuleConfigBase
    {
        public int RequestTimeoutSeconds { get; set; } = 30;
        public string WinnerBadgeFormat { get; set; } = "{winner}猫娘喵";
        public string BadgeColor { get; set; } = "yellow";
    }

    public struct BattleReq
    {
        public Player From; public string FromBackup;
        public Player To; public string ToBackup;
        public BattleType Type; public Stopwatch stopwatch;
    }

    public struct CurrentBattle { public Player From; public Player To; public BattleType Type; }

    public enum BattleType { JailBird, Gun }

    public static class DuelManager
    {
        public static CurrentBattle ActiveBattle;
        public static bool IsBattling, TempFlag;
        public static List<BattleReq> BattleReqs = new List<BattleReq>();
        public static List<BattleReq> WaitingBattleReqs = new List<BattleReq>();
        public static Dictionary<string, string> PlayerBadges = new Dictionary<string, string>();
        public static List<CoroutineHandle> BadgeCoroutines = new List<CoroutineHandle>();
        public static List<CoroutineHandle> BattleCoroutines = new List<CoroutineHandle>();

        private const string REQUEST_KEY = "FlightRequest";

        private static void Bc(Player p, string msg, ushort dur) => p?.Broadcast(new Exiled.API.Features.Broadcast(msg, dur, true, default), true);

        public static IEnumerator<float> BattleLoop()
        {
            while (Round.IsStarted)
            {
                try
                {
                    for (int i = BattleReqs.Count - 1; i >= 0; i--)
                    {
                        var req = BattleReqs[i];
                        if (req.stopwatch != null && req.stopwatch.Elapsed.TotalSeconds >= 30)
                        { CancelRequest(req, "超时"); BattleReqs.RemoveAt(i); }
                    }
                    if (!TempFlag && WaitingBattleReqs.Count > 0)
                    {
                        var next = WaitingBattleReqs[0]; WaitingBattleReqs.RemoveAt(0);
                        TryStartBattle(next);
                    }
                }
                catch (Exception ex) { Log.Error($"[Duel] {ex}"); }
                yield return Timing.WaitForSeconds(1f);
            }
            Cleanup();
        }

        public static void Cleanup()
        {
            foreach (var h in BadgeCoroutines) Timing.KillCoroutines(h);
            BadgeCoroutines.Clear();
            foreach (var h in BattleCoroutines) Timing.KillCoroutines(h);
            BattleCoroutines.Clear();
            BattleReqs.Clear(); WaitingBattleReqs.Clear();
            ActiveBattle = new CurrentBattle(); PlayerBadges.Clear();
            IsBattling = TempFlag = false;
        }

        private static void CancelRequest(BattleReq req, string reason)
        {
            var from = req.From ?? (string.IsNullOrEmpty(req.FromBackup) ? null : Player.Get(req.FromBackup));
            var to = req.To ?? (string.IsNullOrEmpty(req.ToBackup) ? null : Player.Get(req.ToBackup));
            from?.RemoveMessage(REQUEST_KEY); to?.RemoveMessage(REQUEST_KEY);
            Bc(from, $"<size=20>{reason}</size>", 3);
            Bc(to, $"<size=20>{reason}</size>", 3);
        }

        private static void TryStartBattle(BattleReq req)
        {
            var from = req.From ?? (string.IsNullOrEmpty(req.FromBackup) ? null : Player.Get(req.FromBackup));
            var to = req.To ?? (string.IsNullOrEmpty(req.ToBackup) ? null : Player.Get(req.ToBackup));
            if (from == null || to == null) { CancelRequest(req, "玩家已离开"); return; }
            if (from.IsAlive || to.IsAlive) { Bc(from, "<size=20>无法开始:双方中有人还活着</size>", 3); Bc(to, "<size=20>无法开始:双方中有人还活着</size>", 3); return; }

            TempFlag = true;
            ActiveBattle = new CurrentBattle { From = from, To = to, Type = req.Type };
            from.RemoveMessage(REQUEST_KEY); to.RemoveMessage(REQUEST_KEY);
            Bc(from, $"<size=25>与{to.DisplayNickname}的决斗开始!类型:{req.Type}</size>", 5);
            Bc(to, $"<size=25>与{from.DisplayNickname}的决斗开始!类型:{req.Type}</size>", 5);
            SetupBattle(from, to, req.Type);
        }

        private static void SetupBattle(Player from, Player to, BattleType type)
        {
            from.Role.Set(RoleTypeId.Tutorial);
            to.Role.Set(RoleTypeId.Tutorial);
            Timing.CallDelayed(0.1f, () =>
            {
                IsBattling = true;
                int hp = type == BattleType.Gun ? 500 : 300;
                from.Health = hp; to.Health = hp;
                from.SetFriendlyFire(RoleTypeId.Tutorial, 1);
                to.SetFriendlyFire(RoleTypeId.Tutorial, 1);
                switch (type)
                {
                    case BattleType.JailBird: for (int i = 0; i < 3; i++) { from.AddItem(ItemType.Jailbird); to.AddItem(ItemType.Jailbird); } break;
                    case BattleType.Gun: from.AddItem(ItemType.GunE11SR); from.AddItem(ItemType.Ammo556x45); from.AddItem(ItemType.Ammo556x45); from.AddItem(ItemType.Ammo556x45); to.AddItem(ItemType.GunE11SR); to.AddItem(ItemType.Ammo556x45); to.AddItem(ItemType.Ammo556x45); to.AddItem(ItemType.Ammo556x45); break;
                }
                from.AddItem(ItemType.Medkit); to.AddItem(ItemType.Medkit);
            });
        }

        public static void EndBattle(Player winner, Player loser)
        {
            if (winner == null || loser == null) { ActiveBattle = new CurrentBattle(); IsBattling = TempFlag = false; return; }
            var badge = DuelBadgeGen(winner);
            Bc(winner, $"<size=27>赢了{loser.DisplayNickname}!对方将获得称号{badge}</size>", 10);
            Bc(loser, $"<size=27>输了{winner.DisplayNickname}!获得称号{badge}</size>", 10);
            Exiled.API.Features.Cassie.MessageTranslated("pitch_0.2 " + winner.DisplayNickname + " 击败了 " + loser.DisplayNickname, "");
            PlayerBadges[loser.UserId] = DuelBadgeGen(winner, false);
            ActiveBattle = new CurrentBattle(); IsBattling = TempFlag = false;
            try { winner.ClearItems(); loser.ClearItems(); } catch { }
            try { winner.Role.Set(RoleTypeId.Spectator); loser.Role.Set(RoleTypeId.Spectator); } catch { }
            try { winner.TryRemoveFriendlyFire(RoleTypeId.Tutorial); loser.TryRemoveFriendlyFire(RoleTypeId.Tutorial); } catch { }
            BadgeCoroutines.Add(Timing.RunCoroutine(BadgeShowLoop(loser)));
        }

        public static string DuelBadgeGen(Player winner, bool html = true)
        {
            if (winner == null) return "猫娘喵";
            return html ? $"<color=yellow>我是{winner.Nickname}的猫娘喵</color>" : $"我是{winner.Nickname}的猫娘喵";
        }

        public static IEnumerator<float> BadgeShowLoop(Player player)
        {
            if (player == null) yield break;
            if (PlayerBadges.TryGetValue(player.UserId, out string badgeText))
            {
                try { player.ReferenceHub.serverRoles.SetText(badgeText); player.ReferenceHub.serverRoles.Network_myText = badgeText; player.ReferenceHub.serverRoles.SetColor("yellow"); player.ReferenceHub.serverRoles.Network_myColor = "yellow"; } catch { }
            }
            while (player != null && player.IsConnected && Round.IsStarted)
            {
                if (PlayerBadges.TryGetValue(player.UserId, out badgeText))
                {
                    if (!(player.ReferenceHub?.serverRoles?.Network_myText ?? "").Contains(badgeText))
                    {
                        try { player.ReferenceHub.serverRoles.SetText(badgeText); player.ReferenceHub.serverRoles.Network_myText = badgeText; player.ReferenceHub.serverRoles.SetColor("yellow"); player.ReferenceHub.serverRoles.Network_myColor = "yellow"; } catch { }
                    }
                }
                else
                {
                    try { player.Group.BadgeText = null; player.ReferenceHub.serverRoles.Network_myText = null; } catch { }
                    yield break;
                }
                yield return Timing.WaitForSeconds(2f);
            }
        }

        public static void OnDied(DyingEventArgs ev)
        {
            if (!IsBattling || ev.Player == null || ev.Attacker == null) return;
            if (ev.Player == ActiveBattle.From || ev.Player == ActiveBattle.To)
            {
                Player winner = ev.Attacker, loser = ev.Player;
                ev.IsAllowed = false;
                EndBattle(winner, loser);
            }
        }

        public static void OnLeft(LeftEventArgs ev)
        {
            if (!IsBattling || ev.Player == null) return;
            if (ev.Player == ActiveBattle.From || ev.Player == ActiveBattle.To)
            {
                Player loser = ev.Player, winner = loser == ActiveBattle.From ? ActiveBattle.To : ActiveBattle.From;
                var badge = DuelBadgeGen(winner);
                Exiled.API.Features.Cassie.MessageTranslated("pitch_0.2 " + loser.DisplayNickname + " 逃跑了", "");
                Bc(winner, $"<size=27>赢了{loser.DisplayNickname}!对方将获得称号{badge}</size>", 10);
                PlayerBadges[loser.UserId] = DuelBadgeGen(winner, false);
                ActiveBattle = new CurrentBattle(); IsBattling = TempFlag = false;
                try { winner.TryRemoveFriendlyFire(RoleTypeId.Tutorial); winner.Role.Set(RoleTypeId.Spectator); } catch { }
            }
        }

        public static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (!IsBattling || ev.Player == null) return;
            if (ev.Player == ActiveBattle.From || ev.Player == ActiveBattle.To)
            {
                Player loser = ev.Player, winner = loser == ActiveBattle.From ? ActiveBattle.To : ActiveBattle.From;
                Bc(winner, $"<size=27>{loser.DisplayNickname}切换角色,决斗取消</size>", 5);
                Bc(loser, "<size=27>你切换了角色,决斗取消</size>", 5);
                ActiveBattle = new CurrentBattle(); IsBattling = TempFlag = false;
                try { winner.TryRemoveFriendlyFire(RoleTypeId.Tutorial); loser.TryRemoveFriendlyFire(RoleTypeId.Tutorial); } catch { }
                try { winner.Role.Set(RoleTypeId.Spectator); loser.Role.Set(RoleTypeId.Spectator); } catch { }
            }
        }

        public static void OnVerify(VerifiedEventArgs ev)
        {
            if (ev.Player != null && PlayerBadges.ContainsKey(ev.Player.UserId))
                BadgeCoroutines.Add(Timing.RunCoroutine(BadgeShowLoop(ev.Player)));
        }
    }

    public class DuelModule : ModuleBase<DuelConfig>
    {
        public override string ModuleName => "Duel";
        public override void OnEnable()
        {
            
            PlayerHandlers.Dying += DuelManager.OnDied;
            PlayerHandlers.Left += DuelManager.OnLeft;
            PlayerHandlers.ChangingRole += DuelManager.OnChangingRole;
            PlayerHandlers.Verified += DuelManager.OnVerify;
            DuelManager.BattleCoroutines.Add(CorePlugin.RunCoroutine(DuelManager.BattleLoop()));
        }

        public override void OnDisable()
        {
            PlayerHandlers.Dying -= DuelManager.OnDied;
            PlayerHandlers.Left -= DuelManager.OnLeft;
            PlayerHandlers.ChangingRole -= DuelManager.OnChangingRole;
            PlayerHandlers.Verified -= DuelManager.OnVerify;
            DuelManager.Cleanup();
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class StartBattleCommand : ICommand
    {
        public string Command => "startBattle";
        public string[] Aliases => new[] { "startB" };
        public string Description => "发起决斗";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null || player.IsAlive) { response = "不能发起决斗"; return false; }
            if (!Round.IsStarted) { response = "未开始"; return false; }
            if (DuelManager.PlayerBadges.ContainsKey(player.UserId)) { response = "你已经是猫娘了喵"; return false; }
            if (arguments.Count < 1) { response = "用法: startBattle <目标> [0|1]"; return false; }

            string[] na; var list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out na);
            if (list == null || list.Count == 0) { response = "目标无效"; return false; }
            var target = Player.Get(list[0]);
            if (target == null || target.IsAlive || target == player) { response = "目标无效/活着/自己"; return false; }

            var bt = BattleType.JailBird;
            if (arguments.Count > 1 && (arguments.At(1) == "1" || arguments.At(1).ToLower() == "gun")) bt = BattleType.Gun;

            //BUG: possible duplicate request
            DuelManager.BattleReqs.Add(new BattleReq { From = player, FromBackup = player.UserId, To = target, ToBackup = target.UserId, Type = bt, stopwatch = Stopwatch.StartNew() });
            target.AddMessage("FlightRequest", $"<size=27>{player.Nickname}发起决斗!类型:{bt}\n.acceptBattle .refuseBattle</size>", 10f);
            response = $"已发送,类型:{bt}"; return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class AcceptBattleCommand : ICommand
    {
        public string Command => "acceptBattle"; public string[] Aliases => new[] { "AB" }; public string Description => "同意决斗";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null || !Round.IsStarted) { response = "failed"; return false; }

            BattleReq found = default; bool ok = false;
            for (int i = 0; i < DuelManager.BattleReqs.Count; i++)
                if (DuelManager.BattleReqs[i].To == player || DuelManager.BattleReqs[i].ToBackup == player.UserId) { found = DuelManager.BattleReqs[i]; ok = true; break; }
            if (!ok) { response = "无人找你决斗"; return false; }
            if (player.IsAlive) { response = "你还活着"; DuelManager.BattleReqs.Remove(found); return false; }
            DuelManager.BattleReqs.Remove(found); DuelManager.WaitingBattleReqs.Add(found);
            response = "成功"; return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class RefuseBattleCommand : ICommand
    {
        public string Command => "refuseBattle"; public string[] Aliases => new[] { "RB" }; public string Description => "拒绝决斗";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "failed"; return false; }
            for (int i = DuelManager.BattleReqs.Count - 1; i >= 0; i--)
            {
                if (DuelManager.BattleReqs[i].To == player || DuelManager.BattleReqs[i].ToBackup == player.UserId)
                {
                    var from = DuelManager.BattleReqs[i].From;
                    from?.RemoveMessage("FlightRequest");
                    from?.Broadcast(new Exiled.API.Features.Broadcast($"<size=27>{player.DisplayNickname}拒绝了你的决斗</size>", 3, true, default), true);
                }
            }
            DuelManager.BattleReqs.RemoveAll(x => x.To == player || x.ToBackup == player.UserId);
            response = "成功"; return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ForceBattleCommand : ICommand
    {
        public string Command => "ForceBattle"; public string[] Aliases => new[] { "fB" }; public string Description => "强制度斗(RA)";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null || player.KickPower < 4) { response = "权限不足"; return false; }
            string[] na; var list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out na);
            if (list == null || list.Count == 0) { response = "目标无效"; return false; }
            var target = Player.Get(list[0]);
            if (target == null || target.IsAlive || target == player) { response = "目标无效"; return false; }

            var bt = BattleType.JailBird;
            if (arguments.Count > 1 && arguments.At(1) == "1") bt = BattleType.Gun;
            DuelManager.WaitingBattleReqs.Add(new BattleReq { From = player, To = target, ToBackup = target.UserId, Type = bt, stopwatch = Stopwatch.StartNew() });
            response = "成功"; return true;
        }
    }
}
