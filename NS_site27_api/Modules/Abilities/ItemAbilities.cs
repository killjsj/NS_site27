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
    public abstract class ItemAbilityBase : AbilityBase
    {
        public ushort OwnerId { get; set; }
        public static Dictionary<ushort, List<ItemAbilityBase>> ItemABs = new();

        public ItemAbilityBase() { }
        public ItemAbilityBase(ushort ownerId)
        {
            OwnerId = ownerId;
        }
    }

    public abstract class ItemCoolDownAbility : ItemAbilityBase, ICounted, ITiming
    {
        public virtual double Time { get; } = 30;
        public virtual float WaitForDoneTime { get; } = 0;
        public virtual int TotalCount { get; set; } = 1;
        public int count { get; set; } = 1;

        public AbilityCooldown cooldown = new AbilityCooldown();
        public AbilityCooldown DoneCooldown = new AbilityCooldown();
        public virtual float CoolDownRemaining { get => cooldown.Remaining; set => cooldown.Remaining = value; }
        public virtual float DoneRemaining { get => DoneCooldown.Remaining; set => DoneCooldown.Remaining = value; }
        public virtual bool Done { get => DoneCooldown.IsReady; }

        public ItemCoolDownAbility() { }
        public ItemCoolDownAbility(ushort serial) : base(serial) { }

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
    }

    public abstract class ItemKeyAbility : ItemCoolDownAbility
    {
        public SettingBase setting = null;
        public Player player;
        public abstract KeyCode KeyCode { get; }
        public static Dictionary<Player, List<ItemKeyAbility>> activeAbilities = new Dictionary<Player, List<ItemKeyAbility>>();

        public ItemKeyAbility() : base()
        {
            InitSetting();
        }

        public ItemKeyAbility(ushort serial) : base(serial)
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

        public void InternalRegister(Player panel)
        {
            try
            {
                Plugin.Register(player, setting);
            }
            catch { }

            if (!activeAbilities.ContainsKey(player))
                activeAbilities.Add(player, new List<ItemKeyAbility> { this });
            else
                activeAbilities[player].Add(this);

            CorePlugin.RunCoroutine(CooldownReset());
        }

        public virtual void Unregister(Player player) { }
    }
}
