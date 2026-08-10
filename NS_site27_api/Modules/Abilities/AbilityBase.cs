using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Modules.SettingManagement;
using PlayerRoles.Subroutines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace NS_site27_api.Modules.Abilities
{
    public interface IRegisiterNeeded<T> where T : AbilityBase
    {
        T Register(Player player);
        void Unregister(Player player);
    }
    public interface IitemRegisiterNeeded<T>
    {
        T Register(ushort serial);
        void Unregister(ushort serial);
    }
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

    public class PlayerAbilitySet
    {
        public List<AbilityBase> RoleAbilities = new();
        public List<ItemAbilityBase> ItemAbilities = new();

        public bool HasAny()
        {
            return RoleAbilities.Count > 0 || ItemAbilities.Count > 0;
        }

        public IEnumerable<AbilityBase> AllAbilities()
        {
            foreach (var a in RoleAbilities)
            {
                yield return a;
            }

            foreach (var a in ItemAbilities)
            {
                yield return a;
            }
        }

        public bool HasVisible()
        {
            foreach (var ability in AllAbilities())
            {
                try
                {
                    if (ability is ICounted c && (c.count > 0 || c.TotalCount > 0))
                    {
                        return true;
                    }

                    if (ability is ITiming t && (!t.Done || t.CoolDownRemaining > 0))
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(ability.CustomInfoToShow))
                    {
                        return true;
                    }
                }
                catch { return true; }
            }
            return false;
        }

        public PlayerAbilitySet GetOrCreate(Player player)
        {
            if (!PlayerAbilities.TryGetValue(player, out var set))
            {
                set = new PlayerAbilitySet();
                PlayerAbilities[player] = set;
            }
            return set;
        }

        public static Dictionary<Player, PlayerAbilitySet> PlayerAbilities = new();
    }

    public abstract class AbilityBase
    {
        public readonly int offset = 5000;

        public static bool RegisterForPlayer(Player player, AbilityBase ab)
        {
            if (player == null)
            {
                return false;
            }

            var set = new PlayerAbilitySet().GetOrCreate(player);
            set.RoleAbilities.Add(ab);
            return true;
        }

        public static bool RegisterForPlayer(Player player, IEnumerable<AbilityBase> abs)
        {
            if (player == null)
            {
                return false;
            }

            var set = new PlayerAbilitySet().GetOrCreate(player);
            set.RoleAbilities.AddRange(abs);
            return true;
        }

        public static bool UnregisterForPlayer(Player player, AbilityBase ab)
        {
            if (player == null)
            {
                return false;
            }

            if (PlayerAbilitySet.PlayerAbilities.TryGetValue(player, out var set))
            {
                _ = set.RoleAbilities.Remove(ab);
            }

            return true;
        }

        public static PlayerAbilitySet GetPlayerAbilitySet(Player player)
        {
            if (player == null)
            {
                return null;
            }

            _ = PlayerAbilitySet.PlayerAbilities.TryGetValue(player, out var set);
            return set;
        }

        public abstract string Name { get; }
        public abstract string Des { get; }
        public virtual string CustomInfoToShow { get; set; }
        public virtual int id => GetStableHash(GetType().FullName) + offset;

        public bool AppendCustomInfoAfterNormalInfo { get; set; }

        private static int GetStableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = (hash * 31) + c;
                }

                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityBase ab && ab.id == id;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

    public abstract class CoolDownAbility : AbilityBase, ICounted, ITiming
    {
        public virtual double time { get; } = 30;
        public virtual float WaitForDoneTime { get; } = 0;
        public virtual int TotalCount { get; set; } = 1;
        public int count { get; set; } = 1;

        public AbilityCooldown cooldown = new();
        public AbilityCooldown DoneCooldown = new();

        public virtual Player player { get; set; }
        public virtual float CoolDownRemaining { get => cooldown.Remaining; set => cooldown.Remaining = value; }
        public virtual float DoneRemaining { get => DoneCooldown.Remaining; set => DoneCooldown.Remaining = value; }
        public virtual bool Done => DoneCooldown.IsReady;

        public CoolDownAbility() { }
        public CoolDownAbility(Player player)
        {
            this.player = player;
            count = TotalCount;
        }

        public void OnTriggerInternal(Player player)
        {
            if (count <= 0 || !DoneCooldown.IsReady)
            {
                return;
            }

            if (!OnTrigger())
            {
                return;
            }

            if (cooldown.IsReady)
            {
                cooldown.Trigger(time + WaitForDoneTime);
            }

            count--;
            DoneCooldown.Trigger(WaitForDoneTime);
            _ = CorePlugin.RunCoroutine(CooldownStart());
        }

        public IEnumerator<float> CooldownStart()
        {
            while (true)
            {
                if (DoneCooldown.IsReady)
                {
                    break;
                }

                yield return Timing.WaitForSeconds(0.2f);
            }
            if (cooldown.IsReady)
            {
                cooldown.Trigger(time);
            }
        }

        public IEnumerator<float> CooldownReset()
        {
            while (true)
            {
                if (cooldown.IsReady && count < TotalCount)
                {
                    count++;
                    if (count < TotalCount)
                    {
                        cooldown.Trigger(time);
                    }
                }
                yield return Timing.WaitForSeconds(0.3f);
            }
        }

        public abstract bool OnTrigger();
        public virtual AbilityBase Register(Player player)
        {
            var ctor = GetType().GetConstructor(new[] { typeof(Player) });
            if (ctor == null)
                return this;

            var a = (CoolDownAbility)ctor.Invoke(new object[] { player });
            a.InternalRegister();
            return a;
        }
        public virtual void InternalRegister()
        {
            _ = CorePlugin.RunCoroutine(CooldownReset());
        }

        public virtual void Unregister(Player player) { }
    }

    public abstract class KeyAbility : CoolDownAbility, IRegisiterNeeded<AbilityBase>
    {
        public SettingBase setting = null;
        public abstract KeyCode KeyCode { get; }
        public static Dictionary<Player, List<KeyAbility>> activeAbilities = new();

        public KeyAbility() : base()
        {
        }

        public KeyAbility(Player player) : base(player)
        {
            InitSetting();
        }

        private void InitSetting()
        {
            if (CorePlugin.Instance == null)
            {
                return;
            }

            int keyId = id + ((int)KeyCode * 7919);
            setting = SettingManager.Instance?.GetOrCreateKeybindSetting(
                keyId, Name, KeyCode, Des,
                pressedPlayer =>
                {
                    if (activeAbilities.TryGetValue(pressedPlayer, out var abilities))
                    {
                        foreach (var a in abilities.Where(x => x.KeyCode == KeyCode).ToList())
                        {
                            a.OnTriggerInternal(pressedPlayer);
                        }
                    }
                });
        }

        public override AbilityBase Register(Player player)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = GetType();
            var ctor = type.GetConstructor(flags, null, new[] { typeof(Player) }, null);
            if (ctor != null)
            {
                var a = (KeyAbility)ctor.Invoke(new object[] { player });
                a.InternalRegisterPlayer(player);
                return a;
            }

            // 退回无参构造函数
            var parameterless = type.GetConstructor(flags, null, Type.EmptyTypes, null);
            if (parameterless == null)
            {
                Log.Error($"{type.FullName} 缺少 (Player) 或无参构造函数，无法注册。");
                return null;
            }

            var tmp = (KeyAbility)parameterless.Invoke(null);
            tmp.player = player;
            tmp.InitSetting();
            tmp.InternalRegisterPlayer(player);
            return tmp;
        }

        public void InternalRegisterPlayer(Player player)
        {

            if (setting == null)
            {
                InitSetting();
            }

            if (CorePlugin.Instance == null)
            {
                return;
            }

            SettingManager.Instance?.RegisterForPlayer(player, setting);

            if (!activeAbilities.ContainsKey(player))
            {
                activeAbilities.Add(player, new List<KeyAbility> { this });
            }
            else
            {
                activeAbilities[player].Add(this);
            }

            base.InternalRegister();
        }

        public override void Unregister(Player player)
        {
            SettingManager.Instance?.UnregisterForPlayer(player, setting);

            if (activeAbilities.TryGetValue(player, out var list))
            {
                _ = list.Remove(this);
            }
        }
    }

    public abstract class PassAbility : AbilityBase, IRegisiterNeeded<AbilityBase>
    {
        //public static bool _initialized;
        public Player pass_player;
        public static Dictionary<Player, List<PassAbility>> activeAbilities = new();
        public virtual float checktime => 0.2f;
        public void Init()
        {
            _ = CorePlugin.RunCoroutine(Refresher());
        }

        public IEnumerator<float> Refresher()
        {
            while (true)
            {
                        try { this.OnCheck(this.pass_player); }
                        catch (Exception ex) { Log.Warn($"PassAbility error: {ex}"); }
                yield return Timing.WaitForSeconds(checktime);
            }
        }

        public virtual void OnCheck(Player player) { }
        public virtual AbilityBase Register(Player player)
        {
            var ctor = GetType().GetConstructor(new[] { typeof(Player) });
            if (ctor == null)
                return this;

            var a = (PassAbility)ctor.Invoke(new object[] { player });
            a.InternalRegister(player);
            return a;
        }
        public void InternalRegister(Player panel)
        {
            pass_player = panel;
            if (!activeAbilities.ContainsKey(pass_player))
            {
                activeAbilities.Add(pass_player, new List<PassAbility> { this });
            }
            else
            {
                activeAbilities[pass_player].Add(this);
            }

            Init();
        }

        public virtual void Unregister(Player player)
        {
            if (activeAbilities.TryGetValue(player, out var list))
            {
                _ = list.Remove(this);
            }
        }

        public PassAbility() { }
        public PassAbility(Player player)
        {
            this.pass_player = player;
            Init();
        }
    }
}
