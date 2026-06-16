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
        public string OutProtect { get; set; } = "<size=27><color=#00FFFF>保护已取消 - 因开枪</color></size>";
        public string InProtect { get; set; } = "<size=24><color=#4DFFB8>保护剩余 {remainingTime} 秒\n开枪将取消保护</color></size>";
    }

    public class SpawnProtectionModule : ModuleBase<SpawnProtectionConfig>
    {
        public static Dictionary<Player, (bool lost,float time)> LoseProtectAt = new();
        public override string ModuleName => "SpawnProtection";
        public override void OnEnable()
        {
            ServerHandlers.EndingRound += OnRoundEnd;
            PlayerHandlers.Left += OnPlayerLeave;
            PlayerHandlers.Verified += OnVerified;
            PlayerHandlers.ChangingRole += ChangingRole;
            PlayerHandlers.Shot += Shot;
            ServerHandlers.RespawnedTeam += RespawnedTeam;
        }

        public override void OnDisable()
        {
            ServerHandlers.EndingRound -= OnRoundEnd;
            PlayerHandlers.Left -= OnPlayerLeave;
            PlayerHandlers.Verified -= OnVerified;
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
                LoseProtectAt[player] = (false, Time.time);

            }
            catch (Exception ex)
            {
                Log.Error($"[SpawnProtection] Error: {ex.Message}");
            }
        }
        private string[] FrontEnd(Player player)
        {
            var spawnProtectedEffect = player.GetEffect(EffectType.SpawnProtected);
            if (spawnProtectedEffect == null || spawnProtectedEffect.TimeLeft <= 0 || !spawnProtectedEffect.IsEnabled)
                return null;
            var remainingTime = spawnProtectedEffect.TimeLeft;
            var text = "";
            if (remainingTime > 0 && spawnProtectedEffect.IsEnabled)
            {
                text = Config.InProtect.Replace("{remainingTime}", $"{remainingTime:F0}");
            } else
            if (LoseProtectAt.TryGetValue(player,out var t) && t.lost)
            {
                if (Time.time - t.time >= 5f)
                {
                    text = Config.OutProtect;
                }
            }
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
                        LoseProtectAt[ev.Player] = (true, Time.time);
                        if (effect.Intensity != 0)
                        {
                            effect.ServerDisable();
                            effect.TimeLeft = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SpawnProtection] Shot cancel error: {ex.Message}");
                    }
                }
                else
                {
                    
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
        }
        private void OnVerified(VerifiedEventArgs ev)
        {
            if (ev.Player != null) ev.Player.AddMessage("ProtectionMessage", FrontEnd, -1, ScreenPosition.Top);
        }
    }
}
