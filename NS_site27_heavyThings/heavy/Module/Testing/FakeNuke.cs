using Achievements;
using AudioManagerAPI.Defaults;
using Exiled.API.Features;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.WarheadEvents;
using LabApi.Events.Handlers;
using Mirror;
using Next_generationSite_27.UnionP;
using NS_site27_api.Core;
using NS_site27_heavy.Core;
using Subtitles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Utils.NonAllocLINQ;
#if false
namespace NS_site27_heavy.heavy.Module.Testing
{
    internal class FakeNuke : Core.ModuleBase<Core.NullModuleConfig>
    {
        public static FakeNuke Instance;
        public int SessionId = 0;

        public override string ModuleName => "FakeNuke";

        public void Start()
        {
            panel.overridePanel();
        }

        private void StaticUnityMethods_OnUpdate()
        {
            UpdateTime();
        }
        public bool IsManuallyRunning { get; set; }
        public bool AlreadyDetonated { get; set; }
        public bool IsRunning { get; set; }
        public float StartTime { get; set; }
        public float TotalTime { get; set; }
        public float TimeUntilDetonation
        {
            get
            {
                return Mathf.Max(0f, (float)(StartTime + TotalTime - NetworkTime.time));
            }
        }

        private bool _doorsAlreadyOpen;

        private bool _blastDoorsShut;
        public void Stop()
        {
            if (!IsRunning || TimeUntilDetonation <= 10f || IsLocked)
            {
                return;
            }
            AlphaWarheadSyncInfo alphaWarheadSyncInfo = this.Info;
            WarheadStoppingEventArgs e = new WarheadStoppingEventArgs((disabler == null) ? ReferenceHub.HostHub : disabler, alphaWarheadSyncInfo);
            WarheadEvents.OnStopping(e);
            if (!e.IsAllowed)
            {
                return;
            }
            alphaWarheadSyncInfo = e.WarheadState;
            disabler = e.Player.ReferenceHub;
            ServerLogs.AddLog(ServerLogs.Modules.Warhead, "Detonation cancelled.", ServerLogs.ServerLogType.GameEvent, false);
            if (AlphaWarheadController.TimeUntilDetonation <= 15f && disabler != null)
            {
                AchievementHandlerBase.ServerAchieve(disabler.connectionToClient, AchievementName.ThatWasClose);
            }
            alphaWarheadSyncInfo.StartTime = 0.0;
            int num = (int)Mathf.Min(AlphaWarheadController.TimeUntilDetonation, (float)this.CurScenario.TimeToDetonate);
            int num2 = int.MaxValue;
            alphaWarheadSyncInfo.ScenarioType = WarheadScenarioType.Resume;
            byte b = 0;
            while ((int)b < this.ResumeScenarios.Length)
            {
                int num3 = this.ResumeScenarios[(int)b].TimeToDetonate - num;
                if (num3 >= 0 && num3 <= num2)
                {
                    num2 = num3;
                    alphaWarheadSyncInfo.ScenarioId = b;
                }
                b += 1;
            }
            this.NetworkInfo = alphaWarheadSyncInfo;
            this.NetworkCooldownEndTime = NetworkTime.time + (double)this._cooldown;
            DoorEventOpenerExtension.TriggerAction(DoorEventOpenerExtension.OpenerEventType.WarheadCancel);
            if (!NetworkServer.active)
            {
                return;
            }
            this._isAutomatic = false;
            new SubtitleMessage(new SubtitlePart[]
            {
        new SubtitlePart(SubtitleType.AlphaWarheadCancelled, null)
            }).SendToAuthenticated(0);
            WarheadEvents.OnStopped(new WarheadStoppedEventArgs((disabler == null) ? ReferenceHub.HostHub : disabler, alphaWarheadSyncInfo));
        }
        private void UpdateTime()
        {
            if (!NetworkServer.active || !IsRunning || !IsManuallyRunning)
            {
                return;
            }
            if (!_blastDoorsShut && AlphaWarheadController.TimeUntilDetonation < 2f)
            {
                _blastDoorsShut = true;
                BlastDoor.Instances.ForEach<BlastDoor>(delegate (BlastDoor x)
                {
                    x.ServerSetTargetState(false);
                });
            }
            if (!_doorsAlreadyOpen && AlphaWarheadController.TimeUntilDetonation < (float)this.CurScenario.TimeToDetonate)
            {
                _doorsAlreadyOpen = true;
                DoorEventOpenerExtension.TriggerAction(DoorEventOpenerExtension.OpenerEventType.WarheadStart);
            }
            if (AlreadyDetonated || TimeUntilDetonation > 0f)
            {
                return;
            }
            Warhead.Detonate();
        }
        public static AlphaWarheadSyncInfo LastInfo;

        private void AlphaWarheadControllerPatch_InfoChanged(ref AlphaWarheadSyncInfo Info, ref AlphaWarheadSyncInfo prevInfo)
        {
            LastInfo = Info;
            var isDet = Info.InProgress;
            IsRunning = isDet;
            UpdateLight(isDet);
        }
        private void UpdateLight(bool isDet)
        {
            Color newC = Color.white;
            if (isDet)
            {
                newC = new Color(1f, 0.2f, 0.2f);
            }
            foreach (var i in Room.List)
            {
                i.Color = newC;
            }
        }

        public override void OnEnable()
        {
            DefaultAudioManager.Instance.RegisterAudio("", () => File.OpenRead(""));
            AlphaWarheadControllerPatch.InfoChanged += AlphaWarheadControllerPatch_InfoChanged;
            StaticUnityMethods.OnUpdate += StaticUnityMethods_OnUpdate;
        }

        public override void OnDisable()
        {
            AlphaWarheadControllerPatch.InfoChanged -= AlphaWarheadControllerPatch_InfoChanged;
            StaticUnityMethods.OnUpdate -= StaticUnityMethods_OnUpdate;
        }
    }
    [HarmonyPatch(typeof(AlphaWarheadController))]
    public static class AlphaWarheadControllerPatch
    {
        public static AlphaWarheadSyncInfo LastSyncedInfo;
        public delegate void OnAlphaInfoChanged(ref AlphaWarheadSyncInfo Info,ref AlphaWarheadSyncInfo prevInfo);
        public static event OnAlphaInfoChanged InfoChanged;
        [HarmonyPatch("set_NetworkInfo")]
        [HarmonyPrefix]
        public static bool SetAlphaInfo(AlphaWarheadSyncInfo value)
        {
            LastSyncedInfo = value;
            InfoChanged(ref value,ref AlphaWarheadController.Singleton.Info);
            AlphaWarheadController.Singleton.Info = value;
            return false;
        }
        [HarmonyPatch("get_NetworkInfo")]
        [HarmonyPrefix]
        public static bool GetAlphaInfo(ref AlphaWarheadSyncInfo __result)
        {
            __result = LastSyncedInfo;
            return false;
        }
        [HarmonyPatch("OnInfoUpdated")]
        [HarmonyPrefix]
        public static bool OnInfoUpdated()
        {
            return false;
        }
    }
}
#endif