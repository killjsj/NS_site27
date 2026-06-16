using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using Org.BouncyCastle.Asn1.X509;
using PlayerRoles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Subtitles.SubtitleCategory;
using Player = Exiled.API.Features.Player;
using Time = UnityEngine.Time;

namespace NS_site27_api.Modules.Chat
{
    // ==================== 配置 ====================
    public class ChatConfig : ModuleConfigBase
    {
        // 原有
        public float ChatFontSize { get; set; } = 23f;

        // 公共聊天
        public int MaxPublicChatLines { get; set; } = 3;
        public float PublicChatDuration { get; set; } = 3f;

        // 团队聊天
        public int MaxTeamChatLines { get; set; } = 3;
        public float TeamChatDuration { get; set; } = 3f;

        // 管理反馈
        public int MaxAdminChatLines { get; set; } = 5;
        public float AdminChatDuration { get; set; } = 5f;

        public int MaxServerBroadcastLines { get; set; } = 3;
        public float ServerBroadcastDuration { get; set; } = 10f;

        // 冷却
        public int MaxMessagesPerCooldown { get; set; } = 3;
        public float CooldownWindow { get; set; } = 10f;
        public float CooldownDuration { get; set; } = 10f;
    }

    // ==================== 消息结构 ====================
    public struct ChatMessage
    {
        public string InputText;  // 已格式化好（含颜色）的完整行
        public float StartTime;
        public Player player;
        public bool isFirstProcess = true;
        public ChatMessage(string text, Player player)
        {
            InputText = text;
            StartTime = Time.time;
            this.player = player;
        }
    }
    public enum ChatMode
    {
        Global,
        Team,
        Admin,
        ServerBroadcast
    }
    // ==================== 管理器 ====================
    public static class ChatManager
    {
        private static ChatConfig _cfg;
        private class TimeSorter : IComparer<ChatMessage>
        {
            public int Compare(ChatMessage x, ChatMessage y)
            {
                return x.StartTime.CompareTo(y.StartTime);
            }
        }
        private static TimeSorter _timeSorter = new();
        // 显示列表
        public static List<ChatMessage> ChatList = new List<ChatMessage>();
        public static List<ChatMessage> AdminList = new List<ChatMessage>();
        public static List<ChatMessage> ServerList = new List<ChatMessage>();
        public static Dictionary<Team, List<ChatMessage>> TeamList = new Dictionary<Team, List<ChatMessage>>()
        {
            { Team.Dead, new List<ChatMessage>() },
            { Team.FoundationForces, new List<ChatMessage>() },
            { Team.Flamingos, new List<ChatMessage>() },
            { Team.SCPs, new List<ChatMessage>() },
            { Team.ChaosInsurgency, new List<ChatMessage>() },
            { Team.OtherAlive, new List<ChatMessage>() },
        };

        // 冷却相关
        private static readonly Dictionary<string, float> cooldownEndTimes = new Dictionary<string, float>();
        private static readonly Dictionary<string, List<float>> recentMessageTimes = new Dictionary<string, List<float>>();
        private static readonly List<ChatMessage> FirstProcesses = new();

        // 阵营颜色
        private static readonly Dictionary<Team, string> teamColors = new Dictionary<Team, string>()
        {
            { Team.SCPs, "#FF0000" },
            { Team.FoundationForces, "#0096FF" },
            { Team.Scientists, "#00FFFF" },        // 与MTF同色
            { Team.ChaosInsurgency, "#00AA00" },
            { Team.ClassD, "#FF8C00" },
            { Team.Dead, "#808080" },
            { Team.Flamingos, "#FF69B4" },
            { Team.OtherAlive, "#FFFFFF" },
        };

        public static void SetConfig(ChatConfig config) => _cfg = config;

        // ---------- 冷却检查 ----------
        public static bool CanSendMessage(Player player, out float cooldownRemaining)
        {
            string userId = player.UserId;
            float now = Time.time;

            // 检查是否在硬冷却期
            if (cooldownEndTimes.TryGetValue(userId, out float endTime) && now < endTime)
            {
                cooldownRemaining = endTime - now;
                return false;
            }

            // 清理过期记录并统计最近窗口内的次数
            if (!recentMessageTimes.TryGetValue(userId, out var times))
            {
                times = new List<float>();
                recentMessageTimes[userId] = times;
            }
            times.RemoveAll(t => now - t > _cfg.CooldownWindow);

            if (times.Count >= _cfg.MaxMessagesPerCooldown)
            {
                float earliest = times.Min();
                float blockEnd = earliest + _cfg.CooldownWindow;
                if (now < blockEnd)
                {
                    cooldownEndTimes[userId] = blockEnd;
                    cooldownRemaining = blockEnd - now;
                    return false;
                }
            }

            cooldownRemaining = 0f;
            return true;
        }

        public static void RecordMessageSend(Player player)
        {
            string userId = player.UserId;
            if (!recentMessageTimes.TryGetValue(userId, out var times))
            {
                times = new List<float>();
                recentMessageTimes[userId] = times;
            }
            times.Add(Time.time);
        }
        private static string GetChannelDisplay(List<ChatMessage> list, int maxLines, ChatMode mode)
        {
            // 移除过期消息
            var outTime = 3f;
            switch (mode)
            {
                case ChatMode.Global:
                    outTime = _cfg.PublicChatDuration;
                    break;
                case ChatMode.Team:
                    outTime = _cfg.TeamChatDuration;
                    break;
                case ChatMode.Admin:
                    outTime = _cfg.AdminChatDuration;
                    break;
                case ChatMode.ServerBroadcast:
                    outTime = _cfg.ServerBroadcastDuration;
                    break;

            }
            list.RemoveAll(msg => msg.StartTime + outTime <= Time.time);
            list.Sort(_timeSorter);
            while (list.Count > maxLines)
                list.RemoveAt(0);

            if (list.Count == 0)
                return string.Empty;
            var str = "";
            foreach (var item in list)
            {
                string displayText = "";
                switch (mode)
                {
                    case ChatMode.Global:
                        displayText = $"{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}";
                        break;
                    case ChatMode.Team:
                        Team team = item.player.Role.Team;
                        string color = ChatManager.GetTeamColor(team);
                        displayText = $"<color={color}>{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}</color>";
                        break;
                    case ChatMode.Admin:
                        displayText = $"<color=red>{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}</color>";
                        break;
                    case ChatMode.ServerBroadcast:
                        displayText = $"<color=red>服务器广播: {(item.player == null ? "" : $"[{item.player.Nickname}]:")} {item.InputText}</color>";
                        break;
                    default:
                        break;
                }
                str += displayText + "\n";
                if (item.isFirstProcess)
                {
                    FirstProcesses.Add(item);
                }
            }
            foreach (var item in FirstProcesses)
            {
                list.Remove(item);
                var i = new ChatMessage() { StartTime = item.StartTime, InputText = item.InputText, player = item.player, isFirstProcess = false };
                list.Add(i);
                if (item.player != null && Plugin.Instance?.connect != null)
                {
                    Plugin.Instance.connect.InsertChatLog(
                        item.player.UserId,
                        item.player.Nickname,
                        item.InputText,
                        mode.ToString(),
                        Server.Port.ToString()
                    );
                }
                string displayText = "";
                switch (mode)
                {
                    case ChatMode.Global:
                        displayText = $"{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}";
                        foreach (var item1 in Player.Enumerable)
                        {
                            item1.SendConsoleMessage($"[公共聊天]{displayText}", "white");
                        }
                        break;
                    case ChatMode.Team:
                        Team team = item.player.Role.Team;
                        string color = ChatManager.GetTeamColor(team);
                        displayText = $"<color={color}>{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}</color>";

                        foreach (var item1 in Player.Enumerable.Where(x =>
                        {
                            var Tteam = x.Role.Team;
                            var pass = Tteam == team;
                            if (!pass)
                            {
                                switch (Tteam)
                                {
                                    case Team.Scientists:
                                    case Team.FoundationForces:
                                        if (team == Team.Scientists || team == Team.FoundationForces)
                                        {
                                            pass = true;
                                        }
                                        break;
                                    case Team.ClassD:
                                    case Team.ChaosInsurgency:
                                        if (team == Team.ClassD || team == Team.ChaosInsurgency)
                                        {
                                            pass = true;
                                        }
                                        break;
                                }
                            }
                            return pass;
                        }))
                        {
                            item1.SendConsoleMessage($"[队伍聊天]{displayText}", "yellow");
                        }
                        break;
                    case ChatMode.Admin:
                        displayText = $"<color=red>{(item.player == null ? "" : $"{item.player.Nickname}")}: {item.InputText}</color>";
                        foreach (var item1 in Player.Enumerable.Where(x => x.RemoteAdminAccess))
                        {
                            item1.SendConsoleMessage($"[反馈]{displayText}", "red");
                        }
                        item.player.SendConsoleMessage($"[反馈]{displayText}", "red");
                        break;
                    case ChatMode.ServerBroadcast:
                        displayText = $"<color=red>服务器广播: {(item.player == null ? "" : $"[{item.player.Nickname}]:")} {item.InputText}</color>";
                        foreach (var item1 in Player.Enumerable)
                        {
                            item1.SendConsoleMessage($"[服务器广播] {displayText}", "red");
                        }
                        break;
                    default:
                        break;
                }
            }
            FirstProcesses.Clear();
            return str;
        }
        public static string[] UpdateLoopCombined(Player player)
        {
            if (_cfg == null || player == null || !player.IsConnected)
                return new[] { "" };
            Team team = player.Role.Team;
            switch (team)
            {
                case Team.Scientists:
                    team = Team.FoundationForces;
                    break;
                case Team.ClassD:
                    team = Team.ChaosInsurgency;
                    break;

                default:
                    break;
            }
            if (!TeamList.ContainsKey(team))
                team = Team.OtherAlive;
            string ServerContent = GetChannelDisplay(ServerList, _cfg.MaxServerBroadcastLines, ChatMode.ServerBroadcast);
            string teamContent = GetChannelDisplay(TeamList[team], _cfg.MaxTeamChatLines, ChatMode.Team);
            string publicContent = GetChannelDisplay(ChatList, _cfg.MaxPublicChatLines, ChatMode.Global);
            string adminContent = string.Empty;
            if (player.RemoteAdminAccess)
                adminContent = GetChannelDisplay(AdminList, _cfg.MaxAdminChatLines, ChatMode.Admin);
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(ServerContent)) parts.Add("<color=red>" + ServerContent + "</color>");
            if (!string.IsNullOrEmpty(publicContent)) parts.Add("<color=white>公告聊天消息:\n" + publicContent + "</color>");
            if (!string.IsNullOrEmpty(teamContent)) parts.Add($"<color={GetTeamColor(team)}>团队聊天消息:\n" + teamContent + "</color>");
            if (!string.IsNullOrEmpty(adminContent)) parts.Add("<color=red>反馈:\n" + adminContent + "</color>");

            if (parts.Count == 0)
                return new[] { "" };

            string combined = string.Join("\n", parts);
            return new[] { $"<align=left><indent=-350><size={_cfg.ChatFontSize}>{combined}</size></indent></align>" };
        }
        public static void SetupPlayer(Player player)
        {
            player.AddMessage("ChatCombined", UpdateLoopCombined, -1f, 0, 800);
        }
        public static void Cleanup()
        {
            ChatList.Clear();
            AdminList.Clear();
            foreach (var key in TeamList.Keys)
                TeamList[key].Clear();
            cooldownEndTimes.Clear();
            recentMessageTimes.Clear();
        }
        public static string GetTeamColor(Team team) => teamColors.TryGetValue(team, out var color) ? color : "#FFFFFF";
    }
    public class ChatModule : ModuleBase<ChatConfig>
    {
        public override string ModuleName => "Chat";
        public static ChatModule Ins { get; private set; }

        public override void OnEnable()
        {
            ChatManager.SetConfig(Config);
            Ins = this;
        }

        public override void OnDisable()
        {
            ChatManager.Cleanup();
            Ins = null;
        }

        public override void OnReloadConfig()
        {
            base.OnReloadConfig();
            ChatManager.SetConfig(Config);
        }
    }
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BroadcastChatCommand : ICommand
    {
        public string Command => "bc";
        public string[] Aliases => new[] { "cc" };
        public string Description => "公共聊天 (10秒内最多3条)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "无法获取玩家"; return false; }
            if (arguments.Count == 0) { response = "请输入内容"; return false; }

            if (!ChatManager.CanSendMessage(player, out float cooldown))
            {
                response = $"发言过于频繁，请等待 {cooldown:F1} 秒";
                return false;
            }

            string message = string.Join(" ", arguments);

            ChatManager.ChatList.Add(new ChatMessage(message, player));
            ChatManager.RecordMessageSend(player);
            response = "消息已发送";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class AdminChatCommand : ICommand
    {
        public string Command => "ac";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "管理员反馈";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "无法获取玩家"; return false; }
            if (arguments.Count == 0) { response = "请输入内容"; return false; }
            string message = string.Join(" ", arguments);
            ChatManager.AdminList.Add(new ChatMessage(message, player));
            response = "管理员消息已发送";
            return true;
        }
    }
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ServerBroadcastCommand : ICommand
    {
        public string Command => "sbc";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "服务器广播";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count == 0) { response = "请输入内容"; return false; }
            string message = string.Join(" ", arguments);
            ChatManager.ServerList.Add(new ChatMessage(message, Player.Get(sender)));
            response = "服务器广播已发送";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class TeamChatCommand : ICommand
    {
        public string Command => "c";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "团队聊天 (10秒内最多3条)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "无法获取玩家"; return false; }
            if (arguments.Count == 0) { response = "请输入内容"; return false; }
            if (!ChatManager.CanSendMessage(player, out float cooldown))
            {
                response = $"发言过于频繁，请等待 {cooldown:F1} 秒";
                return false;
            }
            string message = string.Join(" ", arguments);
            var team = player.Role.Team;
            string color = ChatManager.GetTeamColor(team);
            switch (team)
            {
                case Team.SCPs:
                    ChatManager.TeamList[Team.SCPs].Add(new ChatMessage(message, player));
                    team = Team.SCPs;
                    break;
                case Team.Scientists:
                case Team.FoundationForces:
                    ChatManager.TeamList[Team.FoundationForces].Add(new ChatMessage(message, player));
                    team = Team.FoundationForces;
                    break;
                case Team.ChaosInsurgency:
                case Team.ClassD:
                    ChatManager.TeamList[Team.ChaosInsurgency].Add(new ChatMessage(message, player));
                    team = Team.ChaosInsurgency;
                    break;
                default:
                    ChatManager.TeamList[team].Add(new ChatMessage(message, player));
                    break;
            }
            ChatManager.RecordMessageSend(player);
            response = "队伍消息已发送";
            return true;
        }
    }
}