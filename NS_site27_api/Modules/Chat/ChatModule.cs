using CommandSystem;
using Exiled.API.Features;
using Exiled.Events.Commands.Config;
using MEC;
using NS_site27_api.Core;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.Chat
{
    public class ChatConfig : ModuleConfigBase
    {
        public int MaxQueueSize { get; set; } = 6;
        public float ChatMessageDuration { get; set; } = 4f;
        public float AdminMessageDuration { get; set; } = 7f;
        public float ChatFontSize { get; set; } = 23f;
    }

    public class ChatModule : ModuleBase<ChatConfig>
    {
        public override string ModuleName => "Chat";

        private CoroutineHandle _updateHandle;
        public override void OnReloadConfig()
        {
            base.OnReloadConfig();
            ChatManager.SetConfig(Config);
        }
        public override void OnEnable()
        {

            ChatManager.SetConfig(Config);
            _updateHandle = CorePlugin.RunCoroutine(ChatManager.UpdateLoop());
        }

        public override void OnDisable()
        {
            ChatManager.Cleanup();
        }
    }

    public struct ChatMessage
    {
        public string text;
        public float exp;

        public ChatMessage(string text) { this.text = text; exp = 0; }
    }

    public static class ChatManager
    {
        private static ChatConfig _cfg;

        public static string ChatDisplayString = "";
        public static string AdminDisplayString = "";

        public static Dictionary<Team, string> TeamDisplayStrings = new Dictionary<Team, string>()
        {
            { Team.Dead, "" }, { Team.FoundationForces, "" }, { Team.Flamingos, "" },
            { Team.SCPs, "" }, { Team.ChaosInsurgency, "" }, { Team.OtherAlive, "" },
        };

        public static List<ChatMessage> ChatList = new List<ChatMessage>();
        public static List<ChatMessage> AdminList = new List<ChatMessage>();
        public static Dictionary<Team, List<ChatMessage>> TeamList = new Dictionary<Team, List<ChatMessage>>()
        {
            { Team.Dead, new List<ChatMessage>() }, { Team.FoundationForces, new List<ChatMessage>() },
            { Team.Flamingos, new List<ChatMessage>() }, { Team.SCPs, new List<ChatMessage>() },
            { Team.ChaosInsurgency, new List<ChatMessage>() }, { Team.OtherAlive, new List<ChatMessage>() },
        };

        public static void SetConfig(ChatConfig config)
        {
            _cfg = config;
        }

        public static IEnumerator<float> UpdateLoop()
        {
            while (true)
            {
                try
                {
                    ProcessChatList(ChatList, ref ChatDisplayString, _cfg.ChatMessageDuration, "<align=left><size=" + _cfg.ChatFontSize + ">", false);
                    ProcessChatList(AdminList, ref AdminDisplayString, _cfg.AdminMessageDuration, "<align=right><size=" + _cfg.ChatFontSize + ">", true);

                    foreach (var team in TeamList.Keys.ToArray())
                    {
                        var teamList = TeamList[team];
                        if (teamList.Count > 0)
                            TeamDisplayStrings[team] = BuildTeamString(teamList, _cfg.ChatMessageDuration);
                        else
                            TeamDisplayStrings[team] = "";
                    }

                    foreach (var player in Player.List)
                    {
                        if (player == null || !player.IsConnected) continue;

                        string display = "";

                        if (!string.IsNullOrEmpty(ChatDisplayString))
                            display += ChatDisplayString + "\n";

                        string teamStr = "";
                        switch (player.Role.Team)
                        {
                            case Team.SCPs: teamStr = TeamDisplayStrings[Team.SCPs]; break;
                            case Team.Scientists: case Team.FoundationForces: teamStr = TeamDisplayStrings[Team.FoundationForces]; break;
                            case Team.ChaosInsurgency: case Team.ClassD: teamStr = TeamDisplayStrings[Team.ChaosInsurgency]; break;
                            case Team.Dead: teamStr = TeamDisplayStrings[Team.Dead]; break;
                            case Team.OtherAlive: teamStr = TeamDisplayStrings[Team.OtherAlive]; break;
                            case Team.Flamingos: teamStr = TeamDisplayStrings[Team.Flamingos]; break;
                        }
                        if (!string.IsNullOrEmpty(teamStr))
                            display += teamStr + "\n";

                        if (!string.IsNullOrEmpty(display))
                            player.ShowHint(display, 1.5f);
                    }
                }
                catch (Exception e)
                {
                    Log.Info(e.ToString());
                }
                yield return Timing.WaitForSeconds(0.2f);
            }
        }

        private static void ProcessChatList(List<ChatMessage> list, ref string displayString, float duration, string prefix, bool isAdmin)
        {
            int maxSize = _cfg.MaxQueueSize;
            if (list.Count > maxSize)
            {
                while (list.Count > maxSize)
                    list.RemoveAt(0);
            }

            if (list.Count > 0)
            {
                displayString = prefix;
                for (int i = 0; i < list.Count; i++)
                {
                    var msg = list[i];
                    if (msg.exp <= 0f)
                    {
                        msg.exp = Time.time + duration;
                        list[i] = msg;
                    }
                    if (msg.exp <= Time.time)
                    {
                        list.RemoveAt(i);
                        i--;
                        continue;
                    }

                    string colorTag = isAdmin ? "<color=red>" : "";
                    string colorEnd = isAdmin ? "</color>" : "";
                    displayString += $"{colorTag}[{(msg.exp - Time.time):F0}] {msg.text}{colorEnd}\n";
                }
                displayString += "</size></align>";
            }
            else
            {
                displayString = "";
            }
        }

        private static string BuildTeamString(List<ChatMessage> list, float duration)
        {
            string result = "<align=center><size=" + _cfg.ChatFontSize + ">";
            int maxSize = _cfg.MaxQueueSize;

            if (list.Count > maxSize)
            {
                while (list.Count > maxSize)
                    list.RemoveAt(0);
            }

            for (int i = 0; i < list.Count; i++)
            {
                var msg = list[i];
                if (msg.exp <= 0f)
                {
                    msg.exp = Time.time + duration;
                    list[i] = msg;
                }
                if (msg.exp <= Time.time)
                {
                    list.RemoveAt(i);
                    i--;
                    continue;
                }
                result += $"<color=yellow>[{(msg.exp - Time.time):F0}] {msg.text}</color>\n";
            }
            result += "</size></align>";
            return result;
        }

        public static void Cleanup()
        {
            ChatList.Clear();
            AdminList.Clear();
            foreach (var k in TeamList.Keys)
                TeamList[k].Clear();
            ChatDisplayString = "";
            AdminDisplayString = "";
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class BroadcastChatCommand : ICommand
    {
        public string Command => "bc";
        public string[] Aliases => new[] { "cc" };
        public string Description => "公屏聊天";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "failed to find player"; return false; }
            if (arguments.Count == 0) { response = "空空如也"; return false; }

            string message = string.Join(" ", arguments.ToArray());
            message = $"{player.Nickname}💭:{message}";
            ChatManager.ChatList.Add(new ChatMessage(message));
            response = "Done!";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class AdminChatCommand : ICommand
    {
        public string Command => "ac";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "管理聊天";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "failed to find player"; return false; }
            if (arguments.Count == 0) { response = "空空如也"; return false; }

            string message = string.Join(" ", arguments.ToArray());
            message = $"(反馈){player.Nickname}💭:{message}";
            ChatManager.AdminList.Add(new ChatMessage(message));
            response = "Done!";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class TeamChatCommand : ICommand
    {
        public string Command => "c";
        public string[] Aliases => Array.Empty<string>();
        public string Description => "队伍聊天";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "failed to find player"; return false; }
            if (arguments.Count == 0) { response = "空空如也"; return false; }

            string message = string.Join(" ", arguments.ToArray());
            message = $"(队伍){player.Nickname}💭:{message}";

            switch (player.Role.Team)
            {
                case Team.SCPs:
                    ChatManager.TeamList[Team.SCPs].Add(new ChatMessage(message));
                    break;
                case Team.Scientists:
                case Team.FoundationForces:
                    ChatManager.TeamList[Team.FoundationForces].Add(new ChatMessage(message));
                    break;
                case Team.ChaosInsurgency:
                case Team.ClassD:
                    ChatManager.TeamList[Team.ChaosInsurgency].Add(new ChatMessage(message));
                    break;
                case Team.Dead:
                    ChatManager.TeamList[Team.Dead].Add(new ChatMessage(message));
                    break;
                case Team.OtherAlive:
                    ChatManager.TeamList[Team.OtherAlive].Add(new ChatMessage(message));
                    break;
                case Team.Flamingos:
                    ChatManager.TeamList[Team.Flamingos].Add(new ChatMessage(message));
                    break;
            }
            response = "Done!";
            return true;
        }
    }
}
