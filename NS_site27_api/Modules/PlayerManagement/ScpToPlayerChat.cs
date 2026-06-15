using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using AudioManagerAPI.Speakers.State;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Mirror;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.Sig;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VoiceChat.Codec;
using VoiceChat.Networking;
using YamlDotNet.Serialization;
using Player = Exiled.API.Features.Player;

namespace NS_site27_api.Modules.PlayerManagement
{
    class ScpToPlayerChat : ModuleBase<ScpToPlayerChatConfig>
    {
        public static ScpToPlayerChat Instance { get; private set; }
        public override string ModuleName => "ScpToPlayerChat";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            VoiceSetting = null;
            //Exiled.Events.Handlers.Player.VoiceChatting -= VoiceChatting/;
            LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage -= VoiceChatting;
        }

        public override void OnEnable()
        {
            VoiceSetting = new KeybindSetting(this.Config.SettingId, "SCP对人类语音", UnityEngine.KeyCode.V, false, false, "按下此键可以让SCP的语音对人类可听", 255, null, (p, sb) =>
            {
                if (p != null && p.IsScp && sb != null && sb.Id == Config.SettingId && sb is KeybindSetting keybind && keybind.IsPressed)
                {
                    if (!TalkTohumanScp.Contains(p))
                    {
                        TalkTohumanScp.Add(p);
                    }
                    else
                    {
                        TalkTohumanScp.Remove(p);
                    }
                    string str = TalkTohumanScp.Contains(p) ? "<color=green><size=20>已开启 SCP对人类语音</size></color>" : "<color=red><size=20>已关闭 SCP对人类语音</size></color>";
                    if (p.HasMessage("ScpTalkToPlayerHint"))
                    {
                        p.RemoveMessage("ScpTalkToPlayerHint");
                    }
                    p.AddMessage("ScpTalkToPlayerHint", str, 3, 0, 305);

                }
            });
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage += VoiceChatting;
        }
        public static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.IsAllowed && !ev.NewRole.IsScp() && ev.Player.Role.Team == Team.SCPs)
            {
                Plugin.Unregister(ev.Player, VoiceSetting);
                TalkTohumanScp.Remove(ev.Player);
                if (ScpToSpeaker.TryGetValue(ev.Player, out var speakerToy))
                {
                    AudioManagerAPI.Controllers.ControllerIdManager.ReleaseController(speakerToy.ControllerId);
                    ScpToSpeaker[ev.Player].Destroy();
                    ScpToSpeaker[ev.Player] = null;
                }
            }
            if (ev.IsAllowed && ev.NewRole.IsScp() && ev.Player.Role.Team != Team.SCPs)
            {
                TalkTohumanScp.Remove(ev.Player);
                Plugin.Register(ev.Player, VoiceSetting);
            }
        }
        public static List<Player> TalkTohumanScp = new List<Player>();
        public static Dictionary<Player, LabApi.Features.Wrappers.SpeakerToy> ScpToSpeaker = new Dictionary<Player, LabApi.Features.Wrappers.SpeakerToy>();
        private static SpeakerToy _speakerPrefab;
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
        }
    }

    class ScpToPlayerChatConfig : ModuleConfigBase
    {
        [YamlMember(Description = "语音设置ID")]
        public int SettingId { get; set; } = 12332;
    }
}
