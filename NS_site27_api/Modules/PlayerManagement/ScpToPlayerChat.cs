using AdminToys;
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using Mirror;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.Sig;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VoiceChat.Codec;
using VoiceChat.Networking;
using YamlDotNet.Serialization;

namespace NS_site27_api.Modules.PlayerManagement
{
    class ScpToPlayerChat : ModuleBase<ScpToPlayerChatConfig>
    {
        public static ScpToPlayerChat Instance { get; private set; }
        public static Dictionary<Player, bool> isEnabledForwading = new Dictionary<Player, bool>();
        public override string ModuleName => "ScpToPlayerChat";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            VoiceSetting = null;
            Exiled.Events.Handlers.Player.VoiceChatting -= VoiceChatting;
        }

        public override void OnEnable()
        {
            VoiceSetting = new KeybindSetting(this.Config.SettingId,"SCP对人类语音",UnityEngine.KeyCode.V,false,false, "按下此键可以让SCP的语音对人类可听",255, null, (p, sb) =>
            {
                if(p != null && p.IsScp && sb != null && sb.Id == Config.SettingId && sb is KeybindSetting keybind && keybind.IsPressed)
                {
                    if (!isEnabledForwading.TryGetValue(p, out var isEnabled))
                    {
                        isEnabledForwading[p] = true;
                    }
                    else
                    {
                        isEnabledForwading[p] = !isEnabled;
                    }
                    string str = isEnabledForwading[p] ? "<color=green><size=20>已开启 SCP对人类语音</size></color>" : "<color=red><size=20>已关闭 SCP对人类语音</size></color>";
                    if (p.HasMessage("ScpTalkToPlayerHint")){
                        p.RemoveMessage("ScpTalkToPlayerHint");
                    }
                    p.AddMessage("ScpTalkToPlayerHint", str,3,0,305);

                }
            });
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Player.VoiceChatting += VoiceChatting;
        }
        public static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.IsAllowed && ev.NewRole.GetTeam() != Team.SCPs && ev.Player.Role.Team == Team.SCPs)
            {
                Plugin.Unregister(ev.Player, VoiceSetting);
                isEnabledForwading[ev.Player] = false;
                ScpToSpeaker[ev.Player] = null;
            }
            if (ev.IsAllowed && ev.NewRole.GetTeam() == Team.SCPs && ev.Player.Role.Team != Team.SCPs)
            {
                Plugin.Register(ev.Player, VoiceSetting);
                isEnabledForwading[ev.Player] = false;
            }
        }
        public static Dictionary<Player, SpeakerToy> ScpToSpeaker = new Dictionary<Player, SpeakerToy>();
        private static SpeakerToy _speakerPrefab;
        public static SettingBase VoiceSetting { get; private set; }

        private static SpeakerToy GetSpeakerPrefab()
        {
            if (_speakerPrefab != null) return _speakerPrefab;
            foreach (var prefab in NetworkClient.prefabs.Values)
            {
                if (prefab.TryGetComponent(out SpeakerToy toy))
                {
                    _speakerPrefab = toy;
                    break;
                }
            }
            return _speakerPrefab;
        }

        public void VoiceChatting(VoiceChattingEventArgs ev)
        {
            if (ev.Player.IsScp && isEnabledForwading.ContainsKey(ev.Player) && isEnabledForwading[ev.Player])
            {
                var id = (byte)(120 + ev.Player.Id);
                for (byte b = 0; ; b++)
                {
                    if (LabApi.Features.Wrappers.SpeakerToy.List.Any(x => x.ControllerId == b))
                    {
                        continue;
                    }
                    id = b;
                    break;
                }
                if (!ScpToSpeaker.TryGetValue(ev.Player, out var sp))
                {
                    var prefab = GetSpeakerPrefab();
                    if (prefab == null) return;

                    var newInstance = GameObject.Instantiate(prefab, ev.Player.Position, Quaternion.identity);
                    newInstance.NetworkControllerId = id;
                    newInstance.NetworkVolume = 1f;
                    newInstance.IsSpatial = false;
                    newInstance.MinDistance = 0f;
                    newInstance.MaxDistance = 20f;
                    newInstance.transform.parent = ev.Player.Transform;
                    sp.transform.position = ev.Player.Position;
                    sp.MaxDistance = 20f;
                    sp.MinDistance = 0f;
                    NetworkServer.Spawn(newInstance.gameObject);

                    ScpToSpeaker.Add(ev.Player, newInstance);
                    sp = newInstance;
                }



                var vm = new AudioMessage()
                {
                    ControllerId = id,
                    Data = ev.VoiceMessage.Data,
                    DataLength = ev.VoiceMessage.DataLength,
                };

                foreach (var hub in ReferenceHub.AllHubs.Where(x =>
                    x.roleManager.CurrentRole is FpcStandardRoleBase i &&
                    Vector3.Distance(i.CameraPosition, ev.Player.Position) <= 20 && x != ev.Player.ReferenceHub && x.roleManager.CurrentRole.Team != Team.SCPs))
                {
                    hub.connectionToClient.Send(vm, 0);
                }
            }
        }


        public static void CleanupPlayer(Player player)
        {
            ScpToSpeaker.Remove(player);
        }
    }

    class ScpToPlayerChatConfig : ModuleConfigBase
    {
        [YamlMember(Description = "语音设置ID")]
        public int SettingId { get; set; } = 12332;
    }
}
