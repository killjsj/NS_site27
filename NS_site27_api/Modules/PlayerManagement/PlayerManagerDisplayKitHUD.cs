using DisplayKit;
using DisplayKit.Elements;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Roles;
using NS_site27_api.Core;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Extensions;
using NS_site27_api.Modules.Lobby;
using NS_site27_api.Modules.SpawnProtection;
using PlayerRoles;
using Respawning;
using Respawning.Waves;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static NS_site27_api.Modules.PlayerManagement.PlayerHUDManager;

namespace NS_site27_api.Modules.PlayerManagement
{
    public class PlayerManagerDisplayKitHUD : DisplayLayer
    {
        public override string Id { get; set; } = "PlayerManager";

        public override void InitNodes(Player target, DisplayCanvas canvas)
        {

            // start define of VisualElement
            DisplayElement VisualElement = canvas.AddElement();
            VisualElement.BaseElement.name = "VisualElement";
            VisualElement.Position.Position = Position.Absolute;
            VisualElement.Size.Width = Length.Percent(100f);
            VisualElement.Size.Height = Length.Percent(100f);
            VisualElement.Position.Top = 0f;
            VisualElement.Position.Left = 0f;
            VisualElement.Position.Right = 0f;

            // start define of died
            DisplayElement died = VisualElement.AddElement();
            died.BaseElement.name = "died";
            died.Position.Position = Position.Absolute;
            died.Position.Left = 0f;
            died.Position.Right = 0f;
            died.Position.Top = 0f;
            died.Position.Bottom = 0f;
            died.Flex.Direction = FlexDirection.Column;
            died.Transform.TransformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(0f), 0f);
            died.Transform.Translate = new Translate(Length.Percent(0f), Length.Percent(0f));

            // start define of ntfDisplay
            DisplayElement ntfDisplay = died.AddElement();
            ntfDisplay.BaseElement.name = "ntfDisplay";
            ntfDisplay.Flex.Grow = 0f;
            ntfDisplay.Size.Height = Length.Percent(21f);
            ntfDisplay.Size.Width = Length.Percent(15f);
            ntfDisplay.Position.Position = Position.Absolute;
            ntfDisplay.Position.Top = Length.Percent(7f);
            ntfDisplay.Position.Left = Length.Percent(15.5f);
            ntfDisplay.Text.Color = new Color(0.2470588f, 0.772549f, 0.6705883f, 1f);
            ntfDisplay.Text.FontSize = 26f;

            // start define of ntfText
            DisplayText ntfText = ntfDisplay.AddText("");
            ntfText.BaseElement.name = "ntfText";

            // start define of chaosDisplay
            DisplayElement chaosDisplay = died.AddElement();
            chaosDisplay.BaseElement.name = "chaosDisplay";
            chaosDisplay.Flex.Grow = 0f;
            chaosDisplay.Size.Height = Length.Percent(21f);
            chaosDisplay.Size.Width = Length.Percent(15f);
            chaosDisplay.Position.Position = Position.Absolute;
            chaosDisplay.Position.Top = Length.Percent(7f);
            chaosDisplay.Position.Right = Length.Percent(15.5f);
            chaosDisplay.Text.Color = new Color(0.1607843f, 1f, 0.8078431f, 1f);
            chaosDisplay.Text.FontSize = 26f;

            // start define of chaosText
            DisplayText chaosText = chaosDisplay.AddText("");
            chaosText.BaseElement.name = "chaosText";

            // start define of teamDisplay
            DisplayElement teamDisplay = died.AddElement();
            teamDisplay.BaseElement.name = "teamDisplay";
            teamDisplay.Flex.Grow = 0f;
            teamDisplay.Size.Height = Length.Percent(18f);
            teamDisplay.Position.Position = Position.Absolute;
            teamDisplay.Position.Top = Length.Percent(62.3f);
            teamDisplay.Size.Width = Length.Percent(18f);
            teamDisplay.Position.Left = Length.Percent(41f);
            teamDisplay.Text.FontSize = 21f;
            teamDisplay.Text.Color = new Color(0.4666667f, 0f, 1f, 1f);

            // start define of DiedTeamText
            DisplayText DiedTeamText = teamDisplay.AddText("");
            DiedTeamText.BaseElement.name = "DiedTeamText";

            // start define of groupDisplay
            DisplayElement groupDisplay = died.AddElement();
            groupDisplay.BaseElement.name = "groupDisplay";
            groupDisplay.Flex.Grow = 0f;
            groupDisplay.Size.Height = Length.Percent(9f);
            groupDisplay.Position.Position = Position.Absolute;
            groupDisplay.Size.Width = Length.Percent(18f);
            groupDisplay.Position.Top = Length.Percent(79.6f);
            groupDisplay.Position.Right = Length.Percent(15.5f);
            groupDisplay.Text.FontSize = 22f;
            groupDisplay.Text.Color = new Color(0.003921569f, 0f, 1f, 1f);

            // start define of GroupText
            DisplayText GroupText = groupDisplay.AddText("");
            GroupText.BaseElement.name = "GroupText";

            // start define of ServerEventToPlayerDisplay
            DisplayElement ServerEventToPlayerDisplay = VisualElement.AddElement();
            ServerEventToPlayerDisplay.BaseElement.name = "ServerEventToPlayerDisplay";
            ServerEventToPlayerDisplay.Size.Height = Length.Percent(9f);
            ServerEventToPlayerDisplay.Size.Width = Length.Percent(20f);
            ServerEventToPlayerDisplay.Position.Position = Position.Absolute;
            ServerEventToPlayerDisplay.Position.Top = Length.Percent(18.3f);
            ServerEventToPlayerDisplay.Position.Left = Length.Percent(40f);
            ServerEventToPlayerDisplay.Text.FontSize = 26f;
            ServerEventToPlayerDisplay.Text.Color = new Color(0f, 1f, 0.9529412f, 1f);

            // start define of ServerEventToPlayerText
            DisplayText ServerEventToPlayerText = ServerEventToPlayerDisplay.AddText("");
            ServerEventToPlayerText.BaseElement.name = "ServerEventToPlayerText";

            // start define of levelDisplay
            DisplayElement levelDisplay = VisualElement.AddElement();
            levelDisplay.BaseElement.name = "levelDisplay";
            levelDisplay.Position.Position = Position.Absolute;
            levelDisplay.Position.Left = 0f;
            levelDisplay.Position.Right = 0f;
            levelDisplay.Position.Bottom = Length.Percent(0.7f);
            levelDisplay.Size.Height = Length.Percent(6f);
            levelDisplay.Text.FontSize = 23f;
            levelDisplay.Text.Color = new Color(0.3529412f, 0.945098f, 0.3529412f, 1f);

            // start define of LevelText
            DisplayText LevelText = levelDisplay.AddText("");
            LevelText.BaseElement.name = "LevelText";

            // start define of alive
            DisplayElement alive = VisualElement.AddElement();
            alive.BaseElement.name = "alive";
            alive.Position.Position = Position.Absolute;
            alive.Size.Width = Length.Percent(100f);
            alive.Size.Height = Length.Percent(100f);

            // start define of TeamShowDisplay
            DisplayElement TeamShowDisplay = alive.AddElement();
            TeamShowDisplay.BaseElement.name = "TeamShowDisplay";
            TeamShowDisplay.Position.Position = Position.Absolute;
            TeamShowDisplay.Size.Height = Length.Percent(29f);
            TeamShowDisplay.Size.Width = Length.Percent(22f);
            TeamShowDisplay.Position.Right = Length.Percent(14f);
            TeamShowDisplay.Position.Top = Length.Percent(64f);
            TeamShowDisplay.Text.FontSize = 26f;
            TeamShowDisplay.Text.OutlineColor = Color.white;
            TeamShowDisplay.Text.Color = new Color(0.02745098f, 0.8039216f, 0.7764706f, 1f);

            // start define of TeamText
            DisplayText TeamText = TeamShowDisplay.AddText("");
            TeamText.BaseElement.name = "TeamText";

            // start define of SpawnProtect
            DisplayElement SpawnProtect = alive.AddElement();
            SpawnProtect.BaseElement.name = "SpawnProtect";
            SpawnProtect.Size.Width = Length.Percent(16f);
            SpawnProtect.Size.Height = Length.Percent(8f);
            SpawnProtect.Position.Position = Position.Absolute;
            SpawnProtect.Position.Top = Length.Percent(2.5f);
            SpawnProtect.Position.Left = Length.Percent(42f);
            SpawnProtect.Text.FontSize = 27f;
            SpawnProtect.Text.Color = new Color(0.2745098f, 0.9098039f, 0.654902f, 1f);

            // start define of ProtectText
            DisplayText ProtectText = SpawnProtect.AddText("");
            ProtectText.BaseElement.name = "ProtectText";

            // start define of Ammo
            DisplayElement Ammo = alive.AddElement();
            Ammo.BaseElement.name = "Ammo";
            Ammo.Flex.Grow = 1f;
            Ammo.Position.Position = Position.Absolute;
            Ammo.Position.Left = Length.Percent(48.5f);
            Ammo.Position.Top = Length.Percent(51.5f);
            Ammo.Position.Right = Length.Percent(48.5f);
            Ammo.Position.Bottom = Length.Percent(46.8f);
            Ammo.Align.AlignItems = Align.Center;
            Ammo.Align.JustifyContent = Justify.Center;

            // start define of AmmoText
            DisplayText AmmoText = Ammo.AddText("");
            AmmoText.BaseElement.name = "AmmoText";
            AmmoText.Position.Position = Position.Absolute;
            AmmoText.Text.Color = new Color(1f, 0.8588235f, 0f, 1f);



        }
        public enum PlayerManagerUI
        {
            VisualElement,
            died,
            ntfDisplay,
            ntfText,
            chaosDisplay,
            chaosText,
            teamDisplay,
            DiedTeamText,
            groupDisplay,
            GroupText,
            ServerEventToPlayerDisplay,
            ServerEventToPlayerText,
            levelDisplay,
            LevelText,
            alive,
            TeamShowDisplay,
            TeamText,
            SpawnProtect,
            ProtectText,
            Ammo,
            AmmoText
        }

        public override async void Update(Player target, DisplayCanvas canvas)
        {
            try
            {
                foreach (var item in canvas.Children)
                {
                    var name = item.BaseElement.name;
                    if (Enum.TryParse<PlayerManagerUI>(name, true, out var uiElement))
                    {
                        UpdateOneNode(target, item, uiElement);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        public enum UILocate
        {
            Left,
            Right,
            Middle
        }
        private static readonly Dictionary<Player, Dictionary<PlayerManagerUI, string>> _contentCache = new();
        public delegate void OnBuildWaitingSpawnUI(UILocate locate, ref StringBuilder AppendString);
        public static event OnBuildWaitingSpawnUI BuildSpawnUIEvent;
        private bool IsNeedRefresh(Player player, PlayerManagerUI ui, string res)
        {
            return !_contentCache.TryGetValue(player, out var playerCache) || !playerCache.TryGetValue(ui, out var cached) || cached != res;
        }

        private void SetCached(Player player, PlayerManagerUI ui, string content)
        {
            if (!_contentCache.ContainsKey(player))
            {
                _contentCache[player] = new Dictionary<PlayerManagerUI, string>();
            }

            _contentCache[player][ui] = content;
        }
        public static void ClearCacheForPlayer(Player player)
        {
            _ = _contentCache.Remove(player);
        }
        public async void UpdateOneNode(Player player, IDisplayElement element, PlayerManagerUI re)
        {
            var name = element.BaseElement.name;
            try
            {
                if (element is DisplayText t)
                {
                    var res = "";
                    switch (re)
                    {
                        case PlayerManagerUI.ntfText:
                            if (player.IsDead)
                            {
                                if (player == null || player.IsAlive || player.Role is not SpectatorRole) { }
                                else
                                {
                                    StringBuilder result = new("");
                                    if (WaveManager.Waves.FirstOrDefault(x => x is NtfSpawnWave) is NtfSpawnWave big)
                                    {
                                        double left = Math.Max(0, big.Timer.TimeLeft);
                                        _ = result.AppendLine($"<align=left><size=25><color=#0000FFFF>🚁九尾狐: {TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>");
                                    }
                                    if (WaveManager.Waves.FirstOrDefault(x => x is NtfMiniWave) is NtfMiniWave small)
                                    {
                                        double left = Math.Max(0, small.Timer.TimeLeft);
                                        _ = result.AppendLine($"<align=left><size=25><color=#0000FFFF>🚁九尾增援:{TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>");
                                    }
                                    BuildSpawnUIEvent?.Invoke(UILocate.Left, ref result);

                                    res = result.ToString();
                                }
                            }
                            break;
                        case PlayerManagerUI.chaosText:
                            if (player.IsDead)
                            {
                                if (player == null || player.IsAlive || player.Role is not SpectatorRole) { }
                                else
                                {
                                    StringBuilder result = new("");
                                    if (WaveManager.Waves.FirstOrDefault(x => x is ChaosSpawnWave) is ChaosSpawnWave big)
                                    {
                                        double left = Math.Max(0, big.Timer.TimeLeft);
                                        _ = result.AppendLine($"<align=right><size=25><color=#008000FF>🚗混沌: {TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>");
                                    }
                                    if (WaveManager.Waves.FirstOrDefault(x => x is ChaosMiniWave) is ChaosMiniWave small)
                                    {
                                        double left = Math.Max(0, small.Timer.TimeLeft);
                                        _ = result.AppendLine($"<align=right><size=25><color=#008000FF>🚗混沌增援:{TimeSpan.FromSeconds(left):mm\\:ss}</color></size></align>");
                                    }
                                    BuildSpawnUIEvent?.Invoke(UILocate.Right, ref result);
                                    res = result.ToString();
                                }
                            }
                            break;
                        case PlayerManagerUI.DiedTeamText:
                            if (player.IsDead)
                            {
                                string v = "";
                                v += $"<color=yellow>{(string.IsNullOrEmpty(PlayerHUDManager.CurrentTip) ? "" : $"Tip:{PlayerHUDManager.CurrentTip}\n")}";
                                v += $"<color=#00FFFF>博士/九尾数量:{PlayerHUDManager.doc + PlayerHUDManager.gruad + PlayerHUDManager.ntf}</color>\n";
                                v += $"<color=#009900>dd/混沌数量:{PlayerHUDManager.dd + PlayerHUDManager.chaos}</color>\n";
                                v += $"<color=red>scp数量:{PlayerHUDManager.Scp.Count}</color></indent>";
                                res = v;
                            }
                            break;
                        case PlayerManagerUI.GroupText:
                            if (player.IsDead)
                            {
                                string str = "<align=right>";
                                str += PlayerManagementModule.Get().Config.SpecUI;
                                str += "</size></align></line-height></color>";
                                res = str;
                            }
                            else
                            {
                                res = "";
                            }
                            break;
                        case PlayerManagerUI.ServerEventToPlayerText:
                            {
                                res = ServerEventToPlayerTextUpdater(player);
                            }
                            break;
                        case PlayerManagerUI.LevelText:
                            {
                                res = await PlayerHudLVShow(player);
                            }
                            break;
                        case PlayerManagerUI.TeamText:
                            {
                                res = GetTeamRoleString(player);
                            }
                            break;
                        case PlayerManagerUI.ProtectText:
                            res = ProtectTextUpdater(player);
                            break;
                        case PlayerManagerUI.AmmoText:
                            {
                                if (player == null)
                                {
                                    break;
                                }

                                if (player.CurrentItem == null)
                                {
                                    break;
                                }
                                res = AmmoTextUpdater(player);
                                break;
                            }
                        default:
                            break;
                    }

                    if (t.Content != res || IsNeedRefresh(player, re, res))
                    {
                        t.Content = res;
                        SetCached(player, re, res);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to update!{e}");
            }
        }


        public static string ScpText = "<align=right><color=red>SCP{scp}:<color=green>♥ {hp} <color=purple>🔰 {sh} <color=yellow>位于 {pos}</color>";
        public static string Scp079Text = "<align=right><color=red>SCP079:<color=green>LV {lv} <color=yellow>Exp {exp}</color>";
        public static string ZombieText = "<align=right><color=red>SCP049-2:<color=green>{count}个</color>";
        private static async Task<string> PlayerHudLVShow(Player player)
        {
            if (player == null)
            {
                return "";
            }

            Player target = player;
            int specCount = 0;

            if (player.Role is SpectatorRole specRole && specRole.SpectatedPlayer != null)
            {
                target = specRole.SpectatedPlayer;
            }

            if (PlayerStateManager.SpecList.ContainsKey(target))
            {
                specCount = PlayerStateManager.SpecList[target].Count;
            }

            var stats = await PlayerManagementModule.GetOrCreateStats(target);
            bool isSpec = player.Role is SpectatorRole;
            string re = "<align=center><size=20>";
            string upLine = await BuildFirstLine(target, isSpec);
            string downLine = BuildSecondLine(target, stats, specCount, isSpec);
            re += upLine + "\n" + downLine;
            re += "</size></align>";


            return re;
        }
        private static async Task<string> BuildFirstLine(Player player, bool isSpec)
        {
            if (player == null)
            {
                return "";
            }

            string teamName = player.Role.Team switch
            {
                Team.FoundationForces => "基金会",
                Team.ChaosInsurgency => "混沌",
                Team.Scientists => "基金会",
                Team.ClassD => "混沌",
                Team.OtherAlive => "教程",
                Team.SCPs => "SCP",
                _ => "死人"
            };
            string teamColor = player.Role.Team switch
            {
                Team.FoundationForces => "#0000FF",
                Team.ChaosInsurgency => "#00AA00",
                Team.Scientists => "#0000FF",
                Team.ClassD => "#00AA00",
                Team.SCPs => "#FF0000",
                _ => "#FFFFFF"
            };

            var conduct = await ConductManager.GetConduct(player);
            var phase = await PhaseManager.GetPhase(player);

            return $"" +
                   $"<color=#FFFF00>{(isSpec ? "玩家:" : "欢迎回来:")} {player.Nickname}</color> " +
                   $"<color={ConductManager.ConductToColor(conduct)}>品行:{ConductManager.ConductToName(conduct)}</color> " +
                   $"<color={PhaseManager.PhaseToColor(phase)}>阶段:{await PhaseManager.GetPhaseProgressString(player, phase)}</color> " +
                   $"<color={teamColor}>阵营:{teamName}</color>" +
                   $"";
        }
        private static string BuildSecondLine(Player player, PlayerManagementModule.RoundStatistics stats, int specCount, bool isSpec)
        {
            if (player == null | stats == null)
            {
                return "";
            }

            var dur = PlayerDataManager.GetAllTime(player);

            return $"" +
                   $"<color=#FFD700>总得分:{stats.Points}</color> " +
                   $"<color=#00FF00>击杀:{stats.Kills}</color> " +
                   $"<color=#FF0000>死亡:{stats.Deaths}</color> " +
                   (player.LeadingTeam == LeadingTeam.ChaosInsurgency | player.LeadingTeam == LeadingTeam.FacilityForces ? $"<color=yellow>增援:{PlayerHUDManager.GetWaveCount(player)}</color> " : "") +
                   (isSpec ? "" : $"<color=#FF00FF>总时长:{dur.TotalDays:F0}天{dur.Hours:D2}时{dur.Minutes:D2}分</color> ") +
                   $"<color=#87CEEB>观众:{specCount}</color>" +
                   $"";
        }
        private static string GetScpNumber(RoleTypeId role)
        {
            return role switch
            {
                RoleTypeId.Scp049 => "049",
                RoleTypeId.Scp079 => "079",
                RoleTypeId.Scp096 => "096",
                RoleTypeId.Scp106 => "106",
                RoleTypeId.Scp173 => "173",
                RoleTypeId.Scp3114 => "3114",
                RoleTypeId.Scp939 => "939",
                _ => "???"
            };
        }
        private static string GetTeamRoleString(Player player)
        {
            string v = "<b>";
            if (player != null && !player.IsScp)
            {
                v += "<size=19>";
                v += "<align=right>";
                if (player.Role.Team is Team.FoundationForces or Team.Scientists)
                {
                    v += $"<color=#00FFFF>{PlayerHUDManager.doc}:博士数量</color>\n<color=#808080>{PlayerHUDManager.gruad}:保安数量</color>\n<color=#0000FF>{PlayerHUDManager.ntf}:九尾数量</color>";
                }
                else if (player.Role.Team is Team.ChaosInsurgency or Team.ClassD)
                {
                    v += $"<color=yellow>{PlayerHUDManager.dd}:dd数量</color>\n<color=#009900>{PlayerHUDManager.chaos}:混沌数量</color>";
                }
            }
            else if (player != null && player.IsScp)
            {
                v += "<size=17><align=right>";
                var ZombieCount = 0;
                foreach (var item in PlayerHUDManager.Scp)
                {
                    var hp = item.Health;
                    var sh = item.HumeShield;
                    if (item.Role == RoleTypeId.Scp0492)
                    {
                        ZombieCount += 1;
                    }
                    else if (item.Role is Scp079Role scp079)
                    {
                        v += $"<color=red>SCP079:<color=green>LV {scp079.Level} <color=yellow>🔋 {scp079.Energy:F0}/{scp079.MaxEnergy}</color>\n";
                    }
                    else if (item.Role is Scp096Role scp096)
                    {
                        string RageStatuts = "";
                        switch (scp096.RageState)
                        {
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Docile:
                                RageStatuts = "<color=#00FFFF>哭泣</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Distressed:
                                RageStatuts = "<color=green>启动愤怒中</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Enraged:
                                RageStatuts = "<color=yellow>正在愤怒</color>";
                                break;
                            case PlayerRoles.PlayableScps.Scp096.Scp096RageState.Calming:
                                RageStatuts = "<color=red>!!! 无法愤怒 !!!</color>";
                                break;
                        }
                        v += $"<color=red>SCP096:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} {RageStatuts}\n";
                    }
                    else if (item.Role is Scp3114Role scp3114)
                    {
                        var r = scp3114.StolenRole;
                        if (r == RoleTypeId.None || scp3114.DisguiseStatus != PlayerRoles.PlayableScps.Scp3114.Scp3114Identity.DisguiseStatus.Active)
                        {
                            r = RoleTypeId.Scp3114;
                        }

                        v += $"<color=red>SCP3114:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>目前角色:{r.RoleToString()}\n";
                    }
                    else if (item.Role is Scp106Role scp106)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>目前体力:{scp106.Vigor * 100:F0}%\n";

                    }
                    else if (item.Role is Scp173Role scp173)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>{(scp173.BreakneckActive ? "超速移动中" : "")}{(scp173.IsObserved ? " <color=red>!! 被观察 !!" : "")}\n";

                    }
                    else if (item.Role is Scp049Role scp049)
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0} <color=yellow>{(scp049.IsCallActive ? "散发护盾中" : "")}{(scp049.IsRecalling ? " <color=red>!! 复活他人中 !!" : "")}\n";

                    }
                    else
                    {
                        v += $"<color=red>SCP{GetScpNumber(item.Role)}:<color=green>♥ {hp:F0} <color=purple>🔰 {sh:F0}";
                        if (item.Role == RoleTypeId.Scp939)
                        {
                            v += $" <color=yellow>目前体力:{player.Stamina * 100:F0}%";
                        }
                        v += "\n";
                    }
                }
                if (ZombieCount > 0)
                {
                    v += $"<color=red>SCP049-2:<color=green>{ZombieCount}个\n";
                }
            }
            v += "</color></b></size></align>";
            return v;
        }
        private static string ProtectTextUpdater(Player player)
        {
            string res = "";
            var spawnProtectedEffect = player.GetEffect(EffectType.SpawnProtected);
            if (!(spawnProtectedEffect == null || spawnProtectedEffect.TimeLeft <= 0 || !spawnProtectedEffect.IsEnabled))
            {
                var Config = ModuleConfigManager.Get<SpawnProtectionConfig>("SpawnProtection");
                var remainingTime = spawnProtectedEffect.TimeLeft;
                var text = "";
                if (remainingTime > 0 && spawnProtectedEffect.IsEnabled)
                {
                    text = Config.InProtect.Replace("{remainingTime}", $"{remainingTime:F0}");
                }
                else
                {
                    if (SpawnProtectionModule.LoseProtectAt.TryGetValue(player, out var time) && time.lost)
                    {
                        if (Time.time - time.time >= 5f)
                        {
                            text = Config.OutProtect;
                        }
                    }
                }
                res = text;
            }

            return res;
        }

        private static string AmmoTextUpdater(Player player)
        {
            string res = "";
            var i = player.CurrentItem;
            if (i.IsFirearm && i.Type != ItemType.MicroHID && i is Firearm f)
            {
                float RemainPercent = (float)f.TotalAmmo / f.TotalMaxAmmo;
                string str = "<size=15><b>";

                if (f.IsReloading)
                {
                    str += "<color=#00FFFF>换弹中</color>";
                }
                else if (i.Type != ItemType.ParticleDisruptor && RemainPercent < 0.22 && RemainPercent > 0)
                {
                    str += "<color=yellow>低弹药</color>";
                }
                else if (i.Type == ItemType.ParticleDisruptor && f.TotalAmmo <= 2)
                {
                    str += "<color=yellow>低弹药</color>";

                }
                else if (f.TotalAmmo <= 0)
                {
                    str += "<color=red>无弹药</color>";
                }
                str += "</b></size>";
                res = str;
            }
            if (i.Type == ItemType.MicroHID && i is MicroHid hid)
            {
                float RemainPercent = hid.Energy;
                string str = "<size=15><b>";
                if (RemainPercent is < (float)0.20 and > 0)
                {
                    str += "<color=yellow>低电量</color>";
                }
                else if (RemainPercent <= 0)
                {
                    str += "<color=red>请充电</color>";
                }
                else if (hid.IsBroken)
                {
                    str += "<color=red>已损坏</color>";

                }
                str += "</b></size>";
                res = str;
            }

            return res;
        }
        private static string ServerEventToPlayerTextUpdater(Player player)
        {
            string r = "";
            {
                if (player != null && player.CurrentRoom?.Type == RoomType.Lcz914)
                {
                    r += Scp914Str.str;
                }
            }
            {
                bool hasContent = false;

                foreach (var item in PlayerHUDManager.ElevatorInteractions.ToArray().Where(x => Vector3.Distance(x.InteractAt, player.ReferenceHub.transform.position) <= 9f))
                {
                    if (Time.time - item.InteractTime <= 2f)
                    {
                        if (!hasContent) { r = "<size=22><color=#FFFF00>"; hasContent = true; }
                        r += $"{item.Interactor.Nickname}激活电梯\n";
                    }
                    else { _ = PlayerHUDManager.ElevatorInteractions.Remove(item); }
                }
                if (hasContent)
                {
                    r += "</color></size>";
                }
            }
            {
                bool hasContent = false;
                List<NukeInteractInfo> toRemove = new();
                foreach (var item in PlayerHUDManager.NukeInteractions)
                {
                    if (Time.time - item.InteractTime <= 2f)
                    {
                        if (!hasContent) { r = $"<size=22><color={(item.acted ? "red" : "green")}>"; hasContent = true; }
                        r += $"{item.Interactor.Nickname}{(item.acted ? " 已启动核弹" : "已关闭核弹")}\n";
                    }
                    else
                    {
                        toRemove.Add(item);
                    }
                }
                foreach (var item in toRemove)
                {
                    _ = PlayerHUDManager.NukeInteractions.Remove(item);
                }
                if (hasContent)
                {
                    r += "</color></size>";
                }
            }

            return r;
        }

    }
}
