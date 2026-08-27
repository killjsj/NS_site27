using DisplayKit.Elements;
using DisplayKit.Enums;
using Exiled.API.Features;
using NS_site27_api.Core.UI.DisplayKit;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NS_site27_api.Modules.Chat
{
    public class ChatLayer : DisplayLayer
    {
        public override string Id { get; set; } = "ChatLayer";

        public override void InitNodes(Player target, DisplayCanvas canvas)
        {
                        DisplayElement VisualElement = canvas.AddElement();
            VisualElement.BaseElement.name = "VisualElement";
            VisualElement.Position.Position = Position.Absolute;
            VisualElement.Size.Width = Length.Percent(100f);
            VisualElement.Size.Height = Length.Percent(100f);
            VisualElement.Position.Top = 0f;
            VisualElement.Position.Left = 0f;
            VisualElement.Position.Right = 0f;

                        DisplayElement chat = VisualElement.AddElement();
            chat.BaseElement.name = "chat";
            chat.Flex.Grow = 1f;
            chat.Size.Width = Length.Percent(14.5f);
            chat.Size.Height = Length.Percent(28f);
            chat.Position.Position = Position.Absolute;
            chat.Position.Top = Length.Percent(15f);

                        DisplayText ChatText = chat.AddText("");
            ChatText.BaseElement.name = "ChatText";
            ChatText.Text.Font = FontType.LiberationSans;
            ChatText.Text.FontSize = 23f;
            ChatText.Text.Color = new Color(0.03529412f, 0.3176471f, 1f, 1f);

        }

        public override void Update(Player player, DisplayCanvas canvas)
        {
            try
            {
                foreach (var item in canvas.Children)
                {
                    if (item.BaseElement.name == "ChatText" && item is DisplayText t)
                    {
                        var res = "";
                        var _cfg = ChatManager._cfg;
                        if (_cfg == null || player == null || !player.IsConnected)
                        {
                        }
                        else
                        {
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
                            if (!ChatManager.TeamList.ContainsKey(team))
                            {
                                team = Team.OtherAlive;
                            }

                            string ServerContent = GetChannelDisplay(ChatManager.ServerList, _cfg.MaxServerBroadcastLines, ChatMode.ServerBroadcast);
                            string teamContent = GetChannelDisplay(ChatManager.TeamList[team], _cfg.MaxTeamChatLines, ChatMode.Team);
                            string publicContent = GetChannelDisplay(ChatManager.ChatList, _cfg.MaxPublicChatLines, ChatMode.Global);
                            string adminContent = string.Empty;
                            if (player.RemoteAdminAccess)
                            {
                                adminContent = GetChannelDisplay(ChatManager.AdminList, _cfg.MaxAdminChatLines, ChatMode.Admin);
                            }

                            List<string> parts = new();
                            if (!string.IsNullOrEmpty(ServerContent))
                            {
                                parts.Add("<color=red>" + ServerContent + "</color>");
                            }

                            if (!string.IsNullOrEmpty(publicContent))
                            {
                                parts.Add("<color=white>公告聊天消息:\n" + publicContent + "</color>");
                            }

                            if (!string.IsNullOrEmpty(teamContent))
                            {
                                parts.Add($"<color={ChatManager.GetTeamColor(team)}>团队聊天消息:\n" + teamContent + "</color>");
                            }

                            if (!string.IsNullOrEmpty(adminContent))
                            {
                                parts.Add("<color=red>反馈:\n" + adminContent + "</color>");
                            }

                            string combined = string.Join("\n", parts);
                            res = combined;
                        }

                        break;

                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        private static string GetChannelDisplay(List<ChatMessage> list, int maxLines, ChatMode mode)
        {
                        var outTime = 3f;
            switch (mode)
            {
                case ChatMode.Global:
                    outTime = ChatManager._cfg.PublicChatDuration;
                    break;
                case ChatMode.Team:
                    outTime = ChatManager._cfg.TeamChatDuration;
                    break;
                case ChatMode.Admin:
                    outTime = ChatManager._cfg.AdminChatDuration;
                    break;
                case ChatMode.ServerBroadcast:
                    outTime = ChatManager._cfg.ServerBroadcastDuration;
                    break;

            }
            _ = list.RemoveAll(msg => msg.StartTime + outTime <= Time.time);
            list.Sort(ChatManager._timeSorter);
            while (list.Count > maxLines)
            {
                list.RemoveAt(0);
            }

            if (list.Count == 0)
            {
                return string.Empty;
            }

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
                        string teamName = item.player.Role.Team switch
                        {
                            Team.FoundationForces => "基金会",
                            Team.ChaosInsurgency => "混沌",
                            Team.Scientists => "基金会",
                            Team.ClassD => "混沌",
                            Team.OtherAlive => "教程",
                            Team.SCPs => "SCP",
                            _ => "死人"
                        };
                        displayText = $"<color=red>{(item.player == null ? "" : $"{item.player.Nickname}({teamName})")}: {item.InputText}</color>";
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
                    ChatManager.FirstProcesses.Add(item);
                }
            }
            foreach (var item in ChatManager.FirstProcesses)
            {
                _ = list.Remove(item);
                var i = new ChatMessage() { StartTime = item.StartTime, InputText = item.InputText, player = item.player, isFirstProcess = false };
                list.Add(i);
                if (item.player != null && Plugin.Instance?.connect != null)
                {
                    _ = Plugin.Instance.connect.InsertChatLogAsync(
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
                                        if (team is Team.Scientists or Team.FoundationForces)
                                        {
                                            pass = true;
                                        }
                                        break;
                                    case Team.ClassD:
                                    case Team.ChaosInsurgency:
                                        if (team is Team.ClassD or Team.ChaosInsurgency)
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
                        string teamName = item.player.Role.Team switch
                        {
                            Team.FoundationForces => "基金会",
                            Team.ChaosInsurgency => "混沌",
                            Team.Scientists => "基金会",
                            Team.ClassD => "混沌",
                            Team.OtherAlive => "教程",
                            Team.SCPs => "SCP",
                            _ => "死人"
                        };
                        displayText = $"<color=red>{(item.player == null ? "" : $"{item.player.Nickname}({teamName})")}: {item.InputText}</color>";
                        foreach (var item1 in Player.Enumerable.Where(x => x.RemoteAdminAccess && x != item.player))
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
            ChatManager.FirstProcesses.Clear();
            return str;
        }
    }
}
