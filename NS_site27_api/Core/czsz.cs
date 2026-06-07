using AutoEvent;
using AutoEvent.API;
using AutoEvent.API.Enums;
using AutoEvent.Interfaces;
using CustomRendering;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using PlayerRoles;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace NS_site27_api.Core
{
    public enum pointType
    {
        A,
        B,
        C
    }
    public enum TeamType
    {
        A,
        B,

    }
    class czsz : Event<czszConfig, czszTranslation>, IEventMap,IModule
    {
        public override string Name { get; set; } = "占领";
        public override string Description { get; set; } = "testing";
        public override string Author { get; set; } = "killjsj";
        public override string CommandName { get; set; } = "zhanl";
        public MapInfo MapInfo { get; set; } = new MapInfo()
        {
            MapName = "czsz",
            Position = Vector3.zero
        };
        public List<Player> BTeam = new List<Player>();
        public List<Player> ATeam = new List<Player>();
        internal GameObject ASpawnPoint { get; set; }
        internal GameObject BSpawnPoint { get; set; }
        internal GameObject ATexts { get; set; }
        internal GameObject BTexts { get; set; }
        internal GameObject CTexts { get; set; }
        public static Dictionary<pointType, HashSet<Player>> InPoint { get; set; } = new() {
            {  pointType.A, new HashSet<Player>()  },
            { pointType.B, new HashSet<Player>()  },
            { pointType.C, new HashSet<Player>() }
        };
        public static Dictionary<pointType, (float A, float B)> TPoint { get; set; } = new() {
            {  pointType.A, (0,0) },
            { pointType.B, (0,0) },
            { pointType.C, (0,0)}
        };

        public string ModuleName => "czsz";

        public bool IsEnabled { get; set; } = true;

        public int Apoints = 0;
        public int Bpoints = 0;
        public int ALives = 0;
        public int BLives = 0;
        public bool stop = false;
        public List<CoroutineHandle> coroutines = new();
        public Stopwatch time = new();
        protected override bool IsRoundDone()
        {
            return Apoints >= Config.TargetPoint || Bpoints >= Config.TargetPoint || stop;
        }

        protected override void OnFinished()
        {
            if (stop) stop = false;
            if (coroutines != null)
            {
                foreach (var item in coroutines)
                {
                    Timing.KillCoroutines(item);
                }

            }
            bool Awin = false;
            bool Bwin = false;
            if (Apoints >= Config.TargetPoint)
            {
                Awin = true;
            }
            else if (Bpoints >= Config.TargetPoint)
            {
                Bwin = true;
            }
            else if (Apoints > Bpoints)
            {
                Awin = true;
            }
            else if (Bpoints > Apoints)
            {
                Bwin = true;
            }
            else if (ALives > BLives)
            {
                Awin = true;
            }
            else if (BLives > ALives)
            {
                Bwin = true;
            }
            else
            {
            }
            if (Awin)
            {
               AutoEvent.API.Extensions.ServerBroadcast("A队获得胜利!", 3);
            }
            else if (Bwin)
            {
               AutoEvent.API.Extensions.ServerBroadcast("B队获得胜利!", 3);
            }
            else
            {
                AutoEvent.API.Extensions.ServerBroadcast("平局!", 3);

            }
        }
        protected override void OnCleanup()
        {
            if (p != null)
            {
                p.Stop();
                p = null;
            }
            foreach (var item in Player.Enumerable)
            {
                item.RemoveMessage("czszPoint");
            }
            base.OnCleanup();
        }
        public TimeSpan RemainTime;
        public string[] shower(Player player)
        {
            var pointMess = "";
            foreach (var pair in InPoint)
            {
                var point = pair.Key;
                var players = pair.Value;
                var Aplayers = players.Intersect(ATeam).ToList();
                var Bplayers = players.Intersect(BTeam).ToList();
                var p = TPoint[point];
                var pl = player;
                var color = "white";
                if (pl != null)
                {
                    if (ATeam.Contains(pl))
                    {
                        color = p.A > p.B ? "green" : p.B > p.A ? "red" : "yellow";
                    }
                    else if (BTeam.Contains(pl))
                    {
                        color = p.B > p.A ? "green" : p.A > p.B ? "red" : "yellow";
                    }
                    else
                        color = "yellow";
                }
                pointMess += $"<size=18><color={color}>{point}点 A队占领:{p.A:F1}% B队占领:{p.B:F1}% A队人数:{Aplayers.Count} B队人数:{Bplayers.Count}</color></size=18>\n";
            }
            return new[] { $"<size=22>{pointMess}A队积分:{Apoints} 剩余命数:{Math.Max(0, Config.TotalLives - ALives)}\nB队积分:{Bpoints} 剩余命数:{Math.Max(0, Config.TotalLives - BLives)}\n目标:{Config.TargetPoint} 时间:{RemainTime.TotalSeconds:F0}</size=22>" };
        }
        protected override void ProcessFrame()
        {

            base.ProcessFrame();
        }
        protected override void RegisterEvents()
        {
            base.RegisterEvents();
            Exiled.Events.Handlers.Player.Died += Handlers_Player_Died;
        }
        protected override void UnregisterEvents()
        {
            base.UnregisterEvents();
            Exiled.Events.Handlers.Player.Died -= Handlers_Player_Died;
        }
        public void Handlers_Player_Died(DiedEventArgs ev)
        {
            bool IsAteam = ATeam.Contains(ev.Player);
            if (IsAteam)
            {
                Bpoints += 5;
                if (Config.TotalLives - ALives > 0)
                {
                    ev.Player.Ex2LabPly().GiveLoadout(Config.ALoadouts);
                    ALives += 1;
                }
                else
                {
                    stop = true;
                }
                ev.Player.Position = ASpawnPoint.transform.position;
            }
            else
            {
                Apoints += 5;
                if (Config.TotalLives - BLives > 0)
                {
                    ev.Player.Ex2LabPly().GiveLoadout(Config.BLoadouts);
                    BLives += 1;
                }
                else
                {
                    stop = true;
                }
                ev.Player.Position = BSpawnPoint.transform.position;
            }

        }
        public IEnumerator<float> StayUpdate()
        {
            while (!stop)
            {
                foreach (var point in InPoint.Keys.ToList()) // 避免枚举修改问题
                {
                    var playersInPoint = InPoint[point];
                    var aPlayers = playersInPoint.Intersect(ATeam).Count();
                    var bPlayers = playersInPoint.Intersect(BTeam).Count();

                    var progress = TPoint[point];  // 拷贝值

                    // 双方人数相等 → 不动（可选：缓慢衰减）
                    if (aPlayers == bPlayers && aPlayers > 1)
                    {
                        if (progress.A > 0) { progress.A-=UnityEngine.Random.Range(-1,2);; }
                        if (progress.B > 0){ progress.B-= UnityEngine.Random.Range(-1,2); }
                        TPoint[point] = progress;  // 如果有修改，再赋值
                        continue;
                    }

                    int advantage = aPlayers - bPlayers;
                    float speed = Math.Min(5f, Math.Abs(advantage*1.5f)); // 人数差距越大，速度越快（可调）

                    if (advantage > 0) // A 队优势
                    {
                        if (progress.B > 0)
                        {
                            progress.B -= speed;
                            if (progress.B < 0) progress.B = 0;
                        }
                        if (progress.A < 100)
                        {
                            progress.A += speed;
                            if (progress.A > 100) progress.A = 100;
                        }
                    }
                    if (advantage < 0) // B 队优势
                    {
                        if (progress.A > 0)
                        {
                            progress.A -= speed;
                            if (progress.A < 0) progress.A = 0;
                        }
                        else if (progress.B < 100)
                        {
                            progress.B += speed;
                            if (progress.B > 100) progress.B = 100;
                        }
                    }

                    TPoint[point] = progress;  // 写回字典
                }
                RemainTime = TimeSpan.FromSeconds(Config.TotalTime - time.Elapsed.TotalSeconds);
                if( RemainTime.TotalSeconds <= 0)
                {
                    stop = true;
                }
                yield return Timing.WaitForSeconds(0.2f);
            }
        }
        public IEnumerator<float> PointUpdate()
        {
            while (true)
            {
                foreach (var item in InPoint)
                {
                    var point = item.Key;
                    var players = item.Value;
                    var Aplayers = players.Intersect(ATeam).ToList();
                    var Bplayers = players.Intersect(BTeam).ToList();
                    var p = TPoint[point];
                    if (p.A > p.B && p.A >= 80)
                    {
                        if (Bplayers.Count > 0)
                        {
                            Apoints += 2;
                        }
                        else
                        {
                            Apoints += 3;
                        }
                    }
                    if (p.B > p.A && p.B >= 80)
                    {
                        if (Aplayers.Count > 0)
                        {
                            Bpoints += 2;
                        }
                        else
                        {
                            Bpoints += 3;
                        }

                    }
                }
                yield return Timing.WaitForSeconds(1f);
            }
        }
        IAudioHandle p;
        new czszConfig Config = new();
        protected override void OnStart()
        {
            Config = new czszConfig();
            InPoint = new() {
            {  pointType.A, new HashSet<Player>()  },
            { pointType.B, new HashSet<Player>()  },
            { pointType.C, new HashSet<Player>() }
        };
            TPoint = new() {
            {  pointType.A, (0,0) },
            { pointType.B, (0,0) },
            { pointType.C, (0,0)}
        };
             Apoints = 0;
             Bpoints = 0;
             ALives = 0;
             BLives = 0;
             stop = false;
            foreach (var item in MapInfo.Map.AttachedBlocks)
            {
                switch (item.name)
                {
                    case "ATrigger":
                        {
                            var ac = item.AddComponent<PointTrigger>();
                            ac.pointtype = pointType.A;
                            if (!item.TryGetComponent(out BoxCollider boxCollider))
                                boxCollider = item.AddComponent<BoxCollider>();

                            boxCollider.isTrigger = true;
                            break;
                        }
                    case "BTrigger":
                        {
                            var ac = item.AddComponent<PointTrigger>();
                            ac.pointtype = pointType.B;
                            if (!item.TryGetComponent(out BoxCollider boxCollider))
                                boxCollider = item.AddComponent<BoxCollider>();

                            boxCollider.isTrigger = true;
                            break;
                        }
                    case "CTrigger":
                        {
                            var ac = item.AddComponent<PointTrigger>();
                            ac.pointtype = pointType.C;
                            if (!item.TryGetComponent(out BoxCollider boxCollider))
                                boxCollider = item.AddComponent<BoxCollider>();

                            boxCollider.isTrigger = true;
                            break;
                        }
                    case "killarea":
                        {
                            item.AddComponent<DiedTriggert>();
                            break;
                        }
                    case "Bspawnpoint":
                        {
                            BSpawnPoint = item;
                            break;
                        }
                    case "Aspawnpoint":
                        {
                            ASpawnPoint = item;
                            break;
                        }
                    case "Atexts":
                        {
                            ATexts = item;
                            break;
                        }
                    case "Btexts":
                        {
                            BTexts = item;
                            break;
                        }
                    case "Ctexts":
                        {
                            CTexts = item;
                            break;
                        }
                    case "":
                        {
                            break;
                        }

                }
            }
            time.Restart();
            int i = 0;
            var list3 = (from _ in Player.Enumerable
                         orderby UnityEngine.Random.value
                         select _);
            foreach (var item in list3)
            {
                if (i % 2 == 1)
                {
                    item.Ex2LabPly().GiveLoadout(Config.ALoadouts);
                    ATeam.Add(item);
                    item.Position = ASpawnPoint.transform.position;
                }
                else
                {
                    item.Ex2LabPly().GiveLoadout(Config.BLoadouts);
                    BTeam.Add(item);
                    item.Position = BSpawnPoint.transform.position;
                }
                i++;
                item.AddMessage("czszPoint", shower, -1, UIPosition.FromEnum(ScreenPosition.MiddleRight));
            }
            p = AutoEvent.API.Extensions.PlayAudio("czsz1.ogg");
            Timing.CallDelayed(20, () => { p = AutoEvent.API.Extensions.PlayAudio("czsz2.ogg", true); });
            coroutines.Add(Timing.RunCoroutine(StayUpdate()));
            coroutines.Add(Timing.RunCoroutine(PointUpdate()));
        }

        public void OnEnable()
        {
            var result = EventManager.RegisterEvent(this);

            if (result != EventRegistrationResult.Success)
                Log.Warn($"Failed to register event: {result}");
        }

        public void OnDisable()
        {
                    EventManager.UnregisterEvent(this);
        }

        public void OnReloadConfig()
        {
        }
    }
    public class PointTrigger : MonoBehaviour
    {
        private BoxCollider _collider;

        private void Start()
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
            gameObject.layer = LayerMask.NameToLayer("InvisibleCollider");
        }

        public pointType pointtype = pointType.A;
        public void OnCollisionEnter(Collision collision)
        {

            Player? player = Player.Get(collision.gameObject);
            if (player is null)
            {
                return;
            }
            czsz.InPoint[pointtype].Add(player);
        }
        public void OnTriggerEnter(Collider collision)
        {
            Player? player = Player.Get(collision.gameObject);
            if (player is null)
            {
                return;
            }
            czsz.InPoint[pointtype].Add(player);
        }
        public void OnCollisionExit(Collision collision)
        {
            Player? player = Player.Get(collision.gameObject);
            if (player is null)
                return;
            czsz.InPoint[pointtype].Remove(player);
        }
        public void OnTriggerExit(Collider collision)
        {
            Player? player = Player.Get(collision.gameObject);
            if (player is null)
                return;
            czsz.InPoint[pointtype].Remove(player);
        }
    }
    public class DiedTriggert : MonoBehaviour
    {
        private BoxCollider _collider;

        private void Start()
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
        }

        public void OnTriggerEnter(Collider other)
        {
            Player? player = Player.Get(other.gameObject);
            if (player is null)
                return;
            player.Kill(DamageType.Falldown);
        }
    }
    public class czszTranslation : EventTranslation
    {
    }

    public class czszConfig : EventConfig
    {
        // Keep properties as simple auto-properties (remove the = new ... part)
        public int TotalTime { get; set; } = 450;
        public int TotalLives { get; set; } = 65;
        public int TargetPoint { get; set; } = 1000;

        public List<Loadout> ALoadouts { get; set; }
        public List<Loadout> BLoadouts { get; set; }

        // ←←← Add constructor
        public czszConfig()
        {
            ALoadouts = new List<Loadout>
        {
            new Loadout
            {
                Roles = new Dictionary<RoleTypeId, int>
                {
                    { RoleTypeId.NtfCaptain, 300 }
                },
                Items = new List<ItemType>
                {
                    ItemType.Jailbird,
                    ItemType.ParticleDisruptor,
                    ItemType.GunFRMG0,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeFlash,
                    ItemType.ArmorCombat,
                    ItemType.Medkit
                },
                Effects = new List<EffectData>
                {
                    new EffectData { Type = "FogControl", Intensity = 1 },   // ⚠️ Check if FogControl exists!
                    new EffectData { Type = "Scp207", Intensity = 1 },
                    new EffectData { Type = "Scp1853", Intensity = 1 }
                },
                InfiniteAmmo = AmmoMode.InfiniteAmmo,
                ArtificialHealth = new ArtificialHealth
                {
                    InitialAmount = 300f,
                    MaxAmount = 300f,
                    RegenerationAmount = 5,
                    Permanent = false,
                    Duration = 10f
                }
            }
        };

            BLoadouts = new List<Loadout>
        {
            new Loadout
            {
                Roles = new Dictionary<RoleTypeId, int>
                {
                    { RoleTypeId.ClassD, 300 }
                },
                Items = new List<ItemType>
                {
                    ItemType.Jailbird,
                    ItemType.ParticleDisruptor,
                    ItemType.GunFRMG0,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeFlash,
                    ItemType.ArmorCombat,
                    ItemType.Medkit

                },
                Effects = new List<EffectData>
                {
                    new EffectData { Type = "FogControl", Intensity = 1 },   // ⚠️ Check if FogControl exists!
                    new EffectData { Type = "Scp207", Intensity = 1 },
                    new EffectData { Type = "Scp1853", Intensity = 1 }
                },
                InfiniteAmmo = AmmoMode.InfiniteAmmo,
                ArtificialHealth = new ArtificialHealth
                {
                    InitialAmount = 300f,
                    MaxAmount = 300f,
                    RegenerationAmount = 5,
                    Permanent = false,
                    Duration = 10f
                }
            }
        };
        }
    }
    public static class Ex2Lab
    {
        public static LabApi.Features.Wrappers.Player Ex2LabPly(this Exiled.API.Features.Player player)
        {
            return LabApi.Features.Wrappers.Player.Get(player.ReferenceHub);
        }
    }
}
