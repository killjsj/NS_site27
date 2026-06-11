using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using PlayerRoles;
using UnityEngine;
using Player = Exiled.API.Features.Player;

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

        // 冷却
        public int MaxMessagesPerCooldown { get; set; } = 3;
        public float CooldownWindow { get; set; } = 10f;
        public float CooldownDuration { get; set; } = 10f;
    }

    // ==================== 消息结构 ====================
    public struct ChatMessage
    {
        public string DisplayText;  // 已格式化好（含颜色）的完整行
        public float ExpireTime;    // 过期时间（Time.time + duration）

        public ChatMessage(string displayText, float duration)
        {
            DisplayText = displayText;
            ExpireTime = Time.time + duration;
        }
    }

    // ==================== 管理器 ====================
    public static class ChatManager
    {
        private static ChatConfig _cfg;

        // 显示列表
        public static List<ChatMessage> ChatList = new List<ChatMessage>();
        public static List<ChatMessage> AdminList = new List<ChatMessage>();
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

        // 阵营颜色
        private static readonly Dictionary<Team, string> teamColors = new Dictionary<Team, string>()
        {
            { Team.SCPs, "#FF0000" },
            { Team.FoundationForces, "#0096FF" },
            { Team.Scientists, "#0096FF" },        // 与MTF同色
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
        private static string GetChannelDisplay(List<ChatMessage> list, int maxLines)
        {
            // 移除过期消息
            list.RemoveAll(msg => msg.ExpireTime <= Time.time);
            // 保持最多 maxLines 行（删除旧的消息）
            while (list.Count > maxLines)
                list.RemoveAt(0);

            if (list.Count == 0)
                return string.Empty;

            return string.Join("\n", list.Select(msg => $"{msg.DisplayText}"));
        }

        // ---------- 组合回调 ----------
        public static string[] UpdateLoopCombined(Player player)
        {
            if (_cfg == null || player == null || !player.IsConnected)
                return new[] { "" };

            // 1. 团队部分
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
            string teamContent = GetChannelDisplay(TeamList[team], _cfg.MaxTeamChatLines);

            // 2. 公共部分
            string publicContent = GetChannelDisplay(ChatList, _cfg.MaxPublicChatLines);

            // 3. 管理员部分（仅 RA 玩家可见）
            string adminContent = string.Empty;
            if (player.RemoteAdminAccess)
                adminContent = GetChannelDisplay(AdminList, _cfg.MaxAdminChatLines);

            // 拼接所有非空部分
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(publicContent)) parts.Add("<color=white>公告聊天消息:" + publicContent + "</color>");
            if (!string.IsNullOrEmpty(teamContent)) parts.Add($"<color={GetTeamColor(team)}>团队聊天消息:\n"+teamContent + "</color>");
            if (!string.IsNullOrEmpty(adminContent)) parts.Add("<color=red>反馈:\n" + adminContent + "</color>");

            if (parts.Count == 0)
                return new[] { "" };

            string combined = string.Join("\n", parts);
            // 整体左对齐，统一字号
            return new[] { $"<align=left><indent=-350><size={_cfg.ChatFontSize}>{combined}</size></indent></align>" };
        }

        // ---------- 初始化（合并为一个 Hint） ----------
        public static void SetupPlayer(Player player)
        {
            // 固定在屏幕顶部附近（Y=800），可根据需求调整
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

        // ---------- 工具 ----------
        public static string GetTeamColor(Team team) => teamColors.TryGetValue(team, out var color) ? color : "#FFFFFF";
    }

    // ==================== 模块入口 ====================
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

    // ==================== 命令（冷却已集成） ====================
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
            string displayText = $"{player.Nickname}: {message}";   // 公共白色，不加颜色标签

            ChatManager.ChatList.Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.PublicChatDuration ?? 3f));
            ChatManager.RecordMessageSend(player);

            response = "消息已发送";
            return true;
        }

        // 辅助获取配置（可在 ChatManager 里加静态方法，此处简写）
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

            // 管理员不受冷却限制
            string message = string.Join(" ", arguments);
            string displayText = $"<color=red>{player.Nickname}: {message}</color>";

            ChatManager.AdminList.Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.AdminChatDuration ?? 5f));

            response = "管理员消息已发送";
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
            Team team = player.Role.Team;
            string color = ChatManager.GetTeamColor(team);
            string displayText = $"<color={color}>{player.Nickname}: {message}</color>";

            // 根据队伍添加到对应列表
            switch (team)
            {
                case Team.SCPs:
                    ChatManager.TeamList[Team.SCPs].Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.TeamChatDuration ?? 3f));
                    break;
                case Team.Scientists:
                case Team.FoundationForces:
                    ChatManager.TeamList[Team.FoundationForces].Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.TeamChatDuration ?? 3f));
                    break;
                case Team.ChaosInsurgency:
                case Team.ClassD:
                    ChatManager.TeamList[Team.ChaosInsurgency].Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.TeamChatDuration ?? 3f));
                    break;
                default:
                    ChatManager.TeamList[team].Add(new ChatMessage(displayText, ChatModule.Ins.GetConfig()?.TeamChatDuration ?? 3f));
                    break;
            }

            ChatManager.RecordMessageSend(player);
            response = "队伍消息已发送";
            return true;
        }
    }
}