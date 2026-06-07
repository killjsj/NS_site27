using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using MEC;
using NS_site27_api.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerRoles.Subroutines;
namespace NS_site27_api.Modules.Abilities
{
    public interface ICounted
    {
        int TotalCount { get; set; }
        int count { get; set; }
    }

    public interface ITiming
    {
        float CoolDownRemaining { get; set; }
        float DoneRemaining { get; set; }
        bool Done { get; }
    }

    public abstract class AbilityBase
    {
        public static Dictionary<Player, List<AbilityBase>> PlayerAbilities = new Dictionary<Player, List<AbilityBase>>();
        public readonly int offset = 5000;

        public static bool RegisterForPlayer(Player player, AbilityBase ab)
        {
            if (player == null) return false;
            if (!PlayerAbilities.TryGetValue(player, out var list))
            {
                list = new List<AbilityBase>();
                PlayerAbilities.Add(player, list);
            }
            list.Add(ab);
            return true;
        }

        public static bool RegisterForPlayer(Player player, IEnumerable<AbilityBase> abs)
        {
            if (player == null) return false;
            if (!PlayerAbilities.TryGetValue(player, out var list))
            {
                list = new List<AbilityBase>();
                PlayerAbilities.Add(player, list);
            }
            list.AddRange(abs);
            return true;
        }

        public static bool UnregisterForPlayer(Player player, AbilityBase ab)
        {
            if (player == null) return false;
            if (PlayerAbilities.TryGetValue(player, out var list))
            {
                list.Remove(ab);
            }
            return true;
        }

        public abstract string Name { get; }
        public abstract string Des { get; }
        public virtual string CustomInfoToShow { get; set; }
        public virtual int id => GetStableHash(this.GetType().FullName) + offset;

        private static int GetStableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is AbilityBase ab)
                return ab.id == id;
            return false;
        }

        public override int GetHashCode() => id.GetHashCode();
    }

    public abstract class CoolDownAbility : AbilityBase, ICounted, ITiming
    {
        public virtual double Time { get; } = 30;
        public virtual float WaitForDoneTime { get; } = 0;
        public virtual int TotalCount { get; set; } = 1;
        public int count { get; set; } = 1;

        public AbilityCooldown cooldown = new AbilityCooldown();
        public AbilityCooldown DoneCooldown = new AbilityCooldown();

        public virtual Player player { get; set; }
        public virtual float CoolDownRemaining { get => cooldown.Remaining; set => cooldown.Remaining = value; }
        public virtual float DoneRemaining { get => DoneCooldown.Remaining; set => DoneCooldown.Remaining = value; }
        public virtual bool Done { get => DoneCooldown.IsReady; }

        public CoolDownAbility() { }
        public CoolDownAbility(Player player)
        {
            this.player = player;
            count = TotalCount;
        }

        public void OnTriggerInternal(Player player)
        {
            if (count <= 0 || !DoneCooldown.IsReady)
                return;

            if (!OnTrigger())
                return;

            count--;

            if (cooldown.IsReady) cooldown.Trigger(WaitForDoneTime);
            DoneCooldown.Trigger(WaitForDoneTime);
            CorePlugin.RunCoroutine(CooldownStart());
        }

        public IEnumerator<float> CooldownStart()
        {
            while (true)
            {
                if (DoneCooldown.IsReady) break;
                yield return Timing.WaitForSeconds(0.2f);
            }
            if (cooldown.IsReady) cooldown.Trigger(Time);
        }

        public IEnumerator<float> CooldownReset()
        {
            while (true)
            {
                if (cooldown.IsReady && count < TotalCount)
                {
                    count++;
                    if (count < TotalCount) cooldown.Trigger(Time);
                }
                yield return Timing.WaitForSeconds(0.3f);
            }
        }

        public abstract bool OnTrigger();
        public abstract AbilityBase Register(Player player);

        public virtual void InternalRegister()
        {
            CorePlugin.RunCoroutine(CooldownReset());
        }

        public virtual void Unregister(Player player) { }
    }

    public abstract class KeyAbility : CoolDownAbility
    {
        public SettingBase setting = null;
        public abstract KeyCode KeyCode { get; }
        public static Dictionary<Player, List<KeyAbility>> activeAbilities = new Dictionary<Player, List<KeyAbility>>();

        public KeyAbility() : base()
        {
            InitSetting();
        }

        public KeyAbility(Player player) : base(player)
        {
            InitSetting();
        }

        private void InitSetting()
        {
            if (CorePlugin.Instance == null) return;

            if (Plugin.MenuCache.Any(x => x.Id == id))
            {
                setting = Plugin.MenuCache.FirstOrDefault(x => x.Id == id);
            }
            else
            {
                try
                {
                    setting = new KeybindSetting(id, Name, KeyCode, true, hintDescription: Des, onChanged: (p, sb) =>
                    {
                        if (sb is KeybindSetting kbs && kbs.IsPressed)
                        {
                            if (activeAbilities.TryGetValue(p, out var abilities))
                            {
                                var a = abilities.FirstOrDefault(x => x.id == kbs.Id);
                                a?.OnTriggerInternal(p);
                            }
                        }
                    });
                    Plugin.MenuCache.Add(setting);
                }
                catch { }
            }
        }

        public override AbilityBase Register(Player player)
        {
            var a = (KeyAbility)Activator.CreateInstance(this.GetType(), player);
            a.InternalRegisterPlayer(player);
            return a;
        }

        public void InternalRegisterPlayer(Player player)
        {
            if (CorePlugin.Instance == null) return;

            try
            {
                Plugin.Register(player, setting);
            }
            catch { }

            if (!activeAbilities.ContainsKey(player))
                activeAbilities.Add(player, new List<KeyAbility> { this });
            else
                activeAbilities[player].Add(this);

            base.InternalRegister();
        }

        public override void Unregister(Player player)
        {
            try
            {
                Plugin.Unregister(player, setting);
            }
            catch { }

            if (activeAbilities.TryGetValue(player, out var list))
                list.Remove(this);
        }
    }

    public abstract class PassAbility : AbilityBase
    {
        public static bool _initialized;
        public Player player;
        public static Dictionary<Player, List<PassAbility>> activeAbilities = new Dictionary<Player, List<PassAbility>>();

        public static void Init()
        {
            if (!_initialized)
            {
                _initialized = true;
                CorePlugin.RunCoroutine(Refresher());
            }
        }

        public static IEnumerator<float> Refresher()
        {
            while (true)
            {
                foreach (var kv in activeAbilities.ToArray())
                {
                    foreach (var ability in kv.Value.ToArray())
                    {
                        try { ability.OnCheck(kv.Key); }
                        catch (Exception ex) { Log.Warn($"PassAbility error: {ex}"); }
                    }
                    yield return Timing.WaitForOneFrame;
                }
                yield return Timing.WaitForSeconds(0.3f);
            }
        }

        public abstract void OnCheck(Player player);
        public abstract AbilityBase Register(Player player);

        public void InternalRegister(Player panel)
        {
            player = panel;
            if (!activeAbilities.ContainsKey(player))
                activeAbilities.Add(player, new List<PassAbility> { this });
            else
                activeAbilities[player].Add(this);
        }

        public virtual void Unregister(Player player)
        {
            if (activeAbilities.TryGetValue(player, out var list))
                list.Remove(this);
        }

        public PassAbility() { }
        public PassAbility(Player player)
        {
            this.player = player;
        }
    }
}
