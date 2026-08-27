using AudioManagerAPI.Features.Enums;
using AudioManagerAPI.Speakers.State;
using Exiled.API.Extensions;
using Exiled.API.Features.Core.UserSettings;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using NS_site27_api.Core;
using NS_site27_api.Modules.MessageModule;
using NS_site27_api.Modules.SettingManagement;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp079;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VoiceChat.Networking;
using YamlDotNet.Serialization;
using Player = Exiled.API.Features.Player;

namespace NS_site27_api.Modules.PlayerManagement
{
    internal class ScpToPlayerChat : ModuleBase<ScpToPlayerChatConfig>
    {
        public static ScpToPlayerChat Instance { get; private set; }
        public override string ModuleName => "ScpToPlayerChat";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            VoiceSetting = null;
                        LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage -= VoiceChatting;
        }

        public override void OnEnable()
        {
            VoiceSetting = new KeybindSetting(Config.SettingId, "SCP对人类语音", UnityEngine.KeyCode.V, false, false, "按下此键可以让SCP的语音对人类可听", 255, null, (p, sb) =>
            {
                if (p != null && p.IsScp && sb != null && sb.Id == Config.SettingId && sb is KeybindSetting keybind && keybind.IsPressed)
                {
                    if (!TalkTohumanScp.Contains(p))
                    {
                        TalkTohumanScp.Add(p);
                    }
                    else
                    {
                        _ = TalkTohumanScp.Remove(p);
                    }
                    string str = TalkTohumanScp.Contains(p) ? "<color=green><size=20>已开启 SCP对人类语音</size></color>" : "<color=red><size=20>已关闭 SCP对人类语音</size></color>";
                    p.RemoveHint("scphumantalk");
                    p.AddHint("scphumantalk", 3, x => new MsgUpdateResult() { Content = str, Title = "scpTalkToHuman" });

                }
            });
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage += VoiceChatting;
        }
        public static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.IsAllowed && !ev.NewRole.IsScp() && ev.Player.Role.Team == Team.SCPs)
            {
                SettingManager.Instance.UnregisterForPlayer(ev.Player, VoiceSetting);
                _ = TalkTohumanScp.Remove(ev.Player);
                if (ScpToSpeaker.TryGetValue(ev.Player, out var speakerToy))
                {
                    AudioManagerAPI.Controllers.ControllerIdManager.ReleaseController(speakerToy.ControllerId);
                    ScpToSpeaker[ev.Player].Destroy();
                    ScpToSpeaker[ev.Player] = null;
                }
            }
            if (ev.IsAllowed && ev.NewRole.IsScp() && ev.Player.Role.Team != Team.SCPs && ev.NewRole != RoleTypeId.Scp079)
            {
                _ = TalkTohumanScp.Remove(ev.Player);
                SettingManager.Instance.RegisterForPlayer(ev.Player, VoiceSetting);
            }
        }
        public static HashSet<Player> TalkTohumanScp = new();
        public static HashSet<ReferenceHub> Scp079AllowIntercom = new();
        public static Dictionary<Player, LabApi.Features.Wrappers.SpeakerToy> ScpToSpeaker = new();
        private static readonly SpeakerToy _speakerPrefab;
        public static SettingBase VoiceSetting { get; private set; }

        public static void VoiceChatting(PlayerSendingVoiceMessageEventArgs ev)
        {
            if (ev.Player.Role.IsScp() && TalkTohumanScp.Contains(ev.Player))
            {
                if (!ScpToSpeaker.TryGetValue(ev.Player, out var sp))
                {
                    var AS = new SpeakerState()
                    {
                        Volume = 1,
                    };
                    if (AudioManagerAPI.Controllers.ControllerIdManager.TryAllocate(AudioPriority.High, null, AS, out var _, out var id))
                    {
                        var newInstance = SpeakerToy.Create(ev.Player.GameObject.transform, false);
                        newInstance.ControllerId = id;
                        newInstance.Volume = 1f;
                        newInstance.IsSpatial = true;
                        newInstance.MinDistance = 0f;
                        newInstance.MaxDistance = 20f;
                        newInstance.Spawn();
                        ScpToSpeaker.Add(ev.Player, newInstance);
                        sp = newInstance;
                    }
                }
                if (sp != null)
                {
                    sp.Transform.position = ev.Player.Position;
                    sp.MaxDistance = 20f;
                    sp.MinDistance = 0f;

                    var vm = new AudioMessage()
                    {
                        ControllerId = sp.ControllerId,
                        Data = ev.Message.Data,
                        DataLength = ev.Message.DataLength,
                    };

                    foreach (var hub in ReferenceHub.AllHubs.Where(x => x != null &&
                        Vector3.Distance(x.GetPosition(), ev.Player.Position) <= 20 && x != ev.Player.ReferenceHub && x.roleManager.CurrentRole.Team != Team.SCPs))
                    {
                        hub.connectionToClient.Send(vm, 0);
                    }
                }

            }
            else if(ev.Player.Role == RoleTypeId.Scp079)
            {
                if(ev.Message.Channel == VoiceChat.VoiceChatChannel.Proximity)
                {
                    if(ev.Player.ReferenceHub.roleManager.CurrentRole is Scp079Role s079)
                    {
                        if(s079.CurrentCamera?.Room.Name == MapGeneration.RoomName.EzIntercom && (s079?.CurrentCamera?.Label?.ToLower().Contains("panel") ?? false))
                        {
                            Scp079AllowIntercom.Add(ev.Player.ReferenceHub);
                        }
                        else
                        {
                            Scp079AllowIntercom.Remove(ev.Player.ReferenceHub);
                        }
                    }
                }
            }
        }
    }

    internal class ScpToPlayerChatConfig : ModuleConfigBase
    {
        [YamlMember(Description = "语音设置ID")]
        public int SettingId { get; set; } = 12332;
    }
}
