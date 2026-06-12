using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using PlayerRoles;
using PlayerRoles.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoiceChat.Codec;
using YamlDotNet.Serialization;

namespace NS_site27_api.Modules.PlayerManagement
{
    class ScpToPlayerChat : ModuleBase<ScpToPlayerChatConfig>
    {
        public static ScpToPlayerChat Instance { get; private set; }
        public static Dictionary<Player, bool> isEnabledForwading = new Dictionary<Player, bool>();
        public static Dictionary<Player, int> sessionIds = new();
        public static Dictionary<Player, OpusDecoder> Decoders = new();
        private static float[] _receiveBuffer;
        private static bool _receiveBufferSet;
        public override string ModuleName => "ScpToPlayerChat";

        public override void OnDisable()
        {
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            Exiled.Events.Handlers.Player.Left -= Left;
            LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage -= SendingVoiceMessage;
            VoiceSetting = null;
        }

        public override void OnEnable()
        {
            VoiceSetting = new KeybindSetting(this.Config.SettingId,"SCP对人类语音",UnityEngine.KeyCode.V,false,false, "按下此键可以让SCP的语音对人类可听",255, null, (p, sb) =>
            {
                if(p != null && p.IsScp && sb != null && sb.Id == Config.SettingId)
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
                    p.AddMessage("ScpTalkToPlayerHint", str,3,0,325);

                }
            });
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Player.Left += Left;
            LabApi.Events.Handlers.PlayerEvents.SendingVoiceMessage += SendingVoiceMessage;
        }
        public static void Left(LeftEventArgs ev)
        {
            if (isEnabledForwading.ContainsKey(ev.Player))
            {
                isEnabledForwading.Remove(ev.Player);
            }
            int sessionId = -1;
            if (sessionIds.TryGetValue(ev.Player, out sessionId))
            {
                DefaultAudioManager.Instance.DestroySession(sessionId);
                sessionIds.Remove(ev.Player);
            }
            if (Decoders.TryGetValue(ev.Player, out var decoder))
            {
                decoder.Dispose();
                Decoders.Remove(ev.Player);
            }
        }
        public static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev.IsAllowed && ev.NewRole.GetTeam() != Team.SCPs && ev.Player.Role.Team == Team.SCPs)
            {
                Plugin.Unregister(ev.Player, VoiceSetting);
                isEnabledForwading[ev.Player] = false;
                int sessionId = -1;
                if (sessionIds.TryGetValue(ev.Player, out sessionId))
                {
                    DefaultAudioManager.Instance.DestroySession(sessionId);
                }
                if (Decoders.TryGetValue(ev.Player, out var decoder))
                {
                    decoder.Dispose();
                }
            }
            if (ev.IsAllowed && ev.NewRole.GetTeam() == Team.SCPs && ev.Player.Role.Team != Team.SCPs)
            {
                Plugin.Register(ev.Player, VoiceSetting);
                isEnabledForwading[ev.Player] = false;
                int sessionId = -1;
                if (sessionIds.TryGetValue(ev.Player, out sessionId))
                {
                    DefaultAudioManager.Instance.SetSessionPosition(sessionId, ev.Player.Position);
                }
                else
                {
                    sessionId = DefaultAudioManager.Instance.CreateStreamSession(
    position: ev.Player.Position,
    isSpatial: true,
    minDistance: 0.05f,
    maxDistance: 20f,
    volume: 1f,
    priority: AudioPriority.High,
    validPlayersFilter: p => p.PlayerId != ev.Player.Id
);
                }
                if (!Decoders.TryGetValue(ev.Player, out var decoder))
                {
                    decoder = new OpusDecoder();
                    Decoders[ev.Player] = decoder;
                }
            }
        }
        public static SettingBase VoiceSetting { get; private set; }
        public static void SendingVoiceMessage(PlayerSendingVoiceMessageEventArgs ev)
        {
            if (isEnabledForwading.TryGetValue(ev.Player, out var isEnabled) && isEnabled && ev.Player.Team == Team.SCPs)
            {
                int sessionId = -1;
                if (sessionIds.TryGetValue(ev.Player, out sessionId))
                {
                    DefaultAudioManager.Instance.SetSessionPosition(sessionId, ev.Player.Position);
                }
                else
                {
                    sessionId = DefaultAudioManager.Instance.CreateStreamSession(
    position: ev.Player.Position,
    isSpatial: true,
    minDistance: 0.05f,
    maxDistance: 20f,
    volume: 1f,
    priority: AudioPriority.High,
    validPlayersFilter: p => p.PlayerId != ev.Player.PlayerId
);

                }
                if (!Decoders.TryGetValue(ev.Player, out var decoder))
                {
                    decoder = new OpusDecoder();
                    Decoders[ev.Player] = decoder;
                }
                if (!_receiveBufferSet)
                {
                    _receiveBufferSet = true;
                    _receiveBuffer = new float[24000];
                }
                decoder.Decode(ev.Message.Data, ev.Message.DataLength, _receiveBuffer);
                DefaultAudioManager.Instance.AppendPcmData(sessionId, _receiveBuffer);
            }
        }
    }

    class ScpToPlayerChatConfig : ModuleConfigBase
    {
        [YamlMember(Description = "语音设置ID")]
        public int SettingId { get; set; } = 12332;
    }
}
