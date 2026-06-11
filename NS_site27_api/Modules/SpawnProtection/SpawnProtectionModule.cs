using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using PlayerHandlers = Exiled.Events.Handlers.Player;


namespace NS_site27_api.Modules.SpawnProtection
{
    public class SpawnProtectionConfig : ModuleConfigBase
    {
        public int ProtectTime { get; set; } = 30;
        public bool NoProtectWhenShoot { get; set; } = true;
        public string InProtectColor { get; set; } = "4DFFB8";
        public string OutProtectColor { get; set; } = "00FFFF";
    }

    public class SpawnProtectionModule : ModuleBase<SpawnProtectionConfig>
    {
        public override string ModuleName => "SpawnProtection";
        public override void OnEnable()
        {
            ServerHandlers.EndingRound += OnRoundEnd;
            PlayerHandlers.Left += OnPlayerLeave;
            PlayerHandlers.ChangingRole += ChangingRole;
            PlayerHandlers.Shot += Shot;
            ServerHandlers.RespawnedTeam += RespawnedTeam;
        }

        public override void OnDisable()
        {
            ServerHandlers.EndingRound -= OnRoundEnd;
            PlayerHandlers.Left -= OnPlayerLeave;
            PlayerHandlers.ChangingRole -= ChangingRole;
            PlayerHandlers.Shot -= Shot;
            ServerHandlers.RespawnedTeam -= RespawnedTeam;
        }

        private void RespawnedTeam(RespawnedTeamEventArgs ev)
        {
            if (Round.IsEnded) return;

            foreach (var player in ev.Players)
            {
                ApplySpawnProtection(player);
            }
        }

        private void ApplySpawnProtection(Player player)
        {
            try
            {

                player.EnableEffect(EffectType.SpawnProtected, Config.ProtectTime);
                if(player.HasMessage("lossingProtection")) player.RemoveMessage("lossingProtection");

                player.AddMessage("ProtectionMessaging", FrontEnd, 7, ScreenPosition.Top);

            }
            catch (Exception ex)
            {
                Log.Error($"[SpawnProtection] Error: {ex.Message}");
            }
        }
        private string[] FrontEnd(Player player)
        {
            var spawnProtectedEffect = player.GetEffect(EffectType.SpawnProtected);
            if (spawnProtectedEffect == null || spawnProtectedEffect.TimeLeft > 0 || !spawnProtectedEffect.IsEnabled)
                return null;
            var remainingTime = spawnProtectedEffect.TimeLeft;
            var text = $"<size=27><color=#{Config.InProtectColor}>保护剩余 {remainingTime:F0} 秒\n开枪将取消保护</color></size>";
            return new[ ]{ text };
        }

        private void Shot(ShotEventArgs ev)
        {
            if (ev.Player.GetEffect(EffectType.SpawnProtected) != null)
            {
                var effect = ev.Player.GetEffect(EffectType.SpawnProtected);

                if (Config.NoProtectWhenShoot && effect.IsEnabled && effect.TimeLeft > 0)
                {
                    try
                    {
                        effect.TimeLeft = 0;
                        ev.Player.DisableEffect(EffectType.SpawnProtected);
                        effect.ServerDisable();
                            ev.Player.RemoveMessage("ProtectionMessaging");

                        var text = $"<size=27><color=#{Config.OutProtectColor}>保护已取消 - 因开枪</color></size>";
                        ev.Player.AddMessage("lossingProtection",text,7,ScreenPosition.Top);
                        Timing.CallDelayed(7f, ()=>
                        {
                            ev.Player.RemoveMessage("lossingProtection");
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SpawnProtection] Shot cancel error: {ex.Message}");
                    }
                }
                else
                {
                    if (effect.Intensity != 0)
                        effect.ServerDisable();
                }
            }
        }

        private void OnPlayerLeave(LeftEventArgs ev)
        {
        }

        private void OnRoundEnd(EndingRoundEventArgs ev)
        {
        }

        private void ChangingRole(ChangingRoleEventArgs ev)
        {
            ev.Player.DisableEffect(EffectType.SpawnProtected);
            ev.Player.RemoveMessage("lossingProtection");
            ev.Player.RemoveMessage("ProtectionMessaging");

        }
    }
}
