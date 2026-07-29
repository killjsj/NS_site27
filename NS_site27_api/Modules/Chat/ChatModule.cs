using CommandSystem;
using Exiled.Events.EventArgs.Player;
using NS_site27_api.Core;
using NS_site27_api.Core.UI.DisplayKit;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public static ChatConfig _cfg;
        public class TimeSorter : IComparer<ChatMessage>
        {
            public int Compare(ChatMessage x, ChatMessage y)
            {
                return x.StartTime.CompareTo(y.StartTime);
            }
        }
        public static TimeSorter _timeSorter = new();
        // 显示列表
        public static List<ChatMessage> ChatList = new();
        public static List<ChatMessage> AdminList = new();
        public static List<ChatMessage> ServerList = new();
        public static Dictionary<Team, List<ChatMessage>> TeamList = new()
        {
            { Team.Dead, new List<ChatMessage>() },
            { Team.FoundationForces, new List<ChatMessage>() },
            { Team.Flamingos, new List<ChatMessage>() },
            { Team.SCPs, new List<ChatMessage>() },
            { Team.ChaosInsurgency, new List<ChatMessage>() },
            { Team.OtherAlive, new List<ChatMessage>() },
        };

        // 冷却相关
        public static readonly Dictionary<string, float> cooldownEndTimes = new();
        public static readonly Dictionary<string, List<float>> recentMessageTimes = new();
        public static readonly List<ChatMessage> FirstProcesses = new();

        // 阵营颜色
        public static readonly Dictionary<Team, string> teamColors = new()
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

        public static void SetConfig(ChatConfig config)
        {
            _cfg = config;
        }

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
            _ = times.RemoveAll(t => now - t > _cfg.CooldownWindow);

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
        public static void Cleanup()
        {
            ChatList.Clear();
            AdminList.Clear();
            foreach (var key in TeamList.Keys)
            {
                TeamList[key].Clear();
            }

            cooldownEndTimes.Clear();
            recentMessageTimes.Clear();
        }
        public static string GetTeamColor(Team team)
        {
            return teamColors.TryGetValue(team, out var color) ? color : "#FFFFFF";
        }
    }
    public class ChatModule : ModuleBase<ChatConfig>
    {
        public override string ModuleName => "Chat";
        public static ChatModule Ins { get; private set; }

        public void Verified(VerifiedEventArgs ev)
        {
            if (ev.Player != null)
            {
                ev.Player.AddLayer("ChatLayer");
            }
        }

        public override void OnEnable()
        {
            ChatManager.SetConfig(Config);
            Exiled.Events.Handlers.Player.Verified += Verified;
            Ins = this;
        }

        public override void OnDisable()
        {
            ChatManager.Cleanup();
            Exiled.Events.Handlers.Player.Verified -= Verified;
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
            _ = ChatManager.GetTeamColor(team);
            switch (team)
            {
                case Team.SCPs:
                    ChatManager.TeamList[Team.SCPs].Add(new ChatMessage(message, player));
                    break;
                case Team.Scientists:
                case Team.FoundationForces:
                    ChatManager.TeamList[Team.FoundationForces].Add(new ChatMessage(message, player));
                    break;
                case Team.ChaosInsurgency:
                case Team.ClassD:
                    ChatManager.TeamList[Team.ChaosInsurgency].Add(new ChatMessage(message, player));
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