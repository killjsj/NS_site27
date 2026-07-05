using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerRoles.Subroutines;
using NS_site27_api.Core;
using NS_site27_api.Modules.SettingManagement;
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

        // �Ƿ���ָ�����/��Ʒ����¿��ã�Ĭ�Ͽ��ã�
        public virtual bool IsAvailable(Player player, Exiled.API.Features.Items.Item item) => true;
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

    public abstract class ItemKeyAbility : ItemCoolDownAbility, IRegisiterNeeded<ItemAbilityBase>
    {
        public SettingBase setting = null;
        public Player player;
        public abstract KeyCode KeyCode { get; }
        public static Dictionary<Player, List<ItemKeyAbility>> activeAbilities = new Dictionary<Player, List<ItemKeyAbility>>();

        public ItemKeyAbility() : base()
        {
        }

        public ItemKeyAbility(ushort serial) : base(serial)
        {

        }

        private void InitSetting()
        {
            if (CorePlugin.Instance == null) return;

            int keyId = id + (int)KeyCode * 7919;
            setting = SettingManager.Instance?.GetOrCreateKeybindSetting(
                keyId, Name, KeyCode, Des,
                pressedPlayer =>
                {
                    if (activeAbilities.TryGetValue(pressedPlayer, out var abilities))
                    {
                        foreach (var a in abilities.Where(x => x.KeyCode == KeyCode).ToList())
                            a.OnTriggerInternal(pressedPlayer);
                    }
                });
        }

        public ItemAbilityBase Register(Player player)
        {
            // create per-player instance (try to use internal/public constructors taking ushort, fallback to parameterless)
            ItemKeyAbility a = null;
            try
            {
                a = Activator.CreateInstance(this.GetType(), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { (ushort)0 }, null) as ItemKeyAbility;
            }
            catch { }
            if (a == null)
            {
                a = (ItemKeyAbility)Activator.CreateInstance(this.GetType());
            }
            a.player = player;
            a.InternalRegister(player);
            return a;
        }

        public void InternalRegister(Player panel)
        {
            player = panel;
            // ensure setting initialized and registered to player UI
            if (setting == null) InitSetting();

            SettingManager.Instance?.RegisterForPlayer(player, setting);

            if (!activeAbilities.ContainsKey(player))
                activeAbilities.Add(player, new List<ItemKeyAbility> { this });
            else
                activeAbilities[player].Add(this);

            CorePlugin.RunCoroutine(CooldownReset());
        }

        public virtual void Unregister(Player player)
        {
            SettingManager.Instance?.UnregisterForPlayer(player, setting);
            if (activeAbilities.TryGetValue(player, out var list))
                list.Remove(this);
        }
    }
}

