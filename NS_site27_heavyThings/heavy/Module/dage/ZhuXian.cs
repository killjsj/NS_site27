using AdminToys;
using Exiled.API.Features;
using MEC;
using NS_site27_heavy.heavy.Module.FpcSpoofing;
using PlayerRoles.FirstPersonControl;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_heavy.heavy.Module.dage
{
    public static class ZhuXian
    {
        public enum LightnLayer
        {
            None, FirstLightn, SecondLightn
        }
        public static Animator anim = null;
        public static PrimitiveObjectToy status = null;
        public static PrimitiveObjectToy cam = null;
        public static SchematicObject so = null;
                                                private const float StrikePhase = 2f;

        private const float EndPhase = 5f;

                                                public static float phase => status == null ? 0f : status.transform.localScale.x;

        public static bool isPlaying => status != null && phase < EndPhase;
        public static readonly Dictionary<LightnLayer, Dictionary<int, LightningBolt>> bolts = new();
        public static List<Player> guas = new();
        public static CoroutineHandle handle;
        public static void start()
        {
            if (isPlaying)
            {
                return;
            }

            var ss = new SerializableSchematic() { SchematicName = "ZhuXian", Position = new Vector3(0, 301, -40) };
            var gb = ss.SpawnOrUpdateObject();
            if (gb.TryGetComponent<SchematicObject>(out so))
            {
                bolts.Clear();
                Dictionary<LightnLayer, Dictionary<int, AdminToyBase>> endsMapper = new();
                Dictionary<LightnLayer, Dictionary<int, AdminToyBase>> boltsMapper = new();
                killed = false;
                foreach (var item in so.AdminToyBases)
                {
                    if (item.name == "animateroot" && !item.TryGetComponent<Animator>(out anim))
                    {
                        Log.Error($"Failed to get animator!");
                    }
                    switch (item.name)
                    {
                        case "guabiCamera":
                            _ = item.TryGetComponent<PrimitiveObjectToy>(out cam);
                            break;
                        case "bridge":
                            break;
                        case "status":
                            _ = item.TryGetComponent<PrimitiveObjectToy>(out status);
                            break;
                    }

                    var layer = LayerOf(item.transform);

                    if (TryParseId(item.name, "Lightn End", out var endId))
                    {
                        Map(endsMapper, layer, endId, item);
                    }
                    else if (TryParseId(item.name, "Lightning Bolt", out var boltId))
                    {
                        Map(boltsMapper, layer, boltId, item);
                    }
                }

                foreach (var layer in boltsMapper)
                {
                    foreach (var pair in layer.Value)
                    {
                        if (!endsMapper.TryGetValue(layer.Key, out var ends) || !ends.TryGetValue(pair.Key, out var end))
                        {
                            Log.Error($"Lightning Bolt{pair.Key} of {layer.Key} has no matching Lightn End{pair.Key}!");
                            continue;
                        }

                        var bolt = LightningBolt.Attach(pair.Value.gameObject, end.transform);
                        if (bolt == null)
                        {
                            continue;
                        }
                        if (layer.Key == LightnLayer.FirstLightn)
                        {
                            bolt.Segments = 100;
                        }
                        else if (layer.Key == LightnLayer.SecondLightn)
                        {
                            bolt.Segments = 30;
                        }

                        bolt.Thickness = 0.05f;
                        bolt.StepSize = 0.35f;
                        bolt.Color = new Color(0.75f, 0.85f, 1f);
                        bolt.Collidable = false;
                        bolt.FlickerInterval = 0.3f;
                        bolt.FollowInterval = 0.05f;

                        if (!bolts.TryGetValue(layer.Key, out var registry))
                        {
                            registry = new();
                            bolts[layer.Key] = registry;
                        }

                        registry[pair.Key] = bolt;
                    }
                }
            }
            if (status == null)
            {
                                                Log.Error($"Failed to get the status block! The schematic will not play.");
            }

            foreach (var item in guas)
            {
                FakeYawSpinController.Stop(item.ReferenceHub);
            }
            handle = Timing.RunCoroutine(animer());
        }
        public static bool killed = false;
        private static float _lastPhase = float.NaN;
        public static IEnumerator<float> animer()
        {
            while (isPlaying)
            {
                Update();
                yield return Timing.WaitForSeconds(0.2f);
            }

                        Stop();
        }
        public static void Update()
        {
            if (!isPlaying)
            {
                return;
            }

                                    if (phase != _lastPhase)
            {
                _lastPhase = phase;
                Log.Info($"ZhuXian phase -> {phase} (strike at {StrikePhase}, end at {EndPhase})");
            }
            if (phase >= StrikePhase)
            {
                if (!killed)
                {
                    Log.Info("Kill!");
                    killed = true;
                    Exiled.API.Features.Map.ExplodeEffect(cam.transform.position, Exiled.API.Enums.ProjectileType.FragGrenade);
                    Exiled.API.Features.Map.ExplodeEffect(cam.transform.position, Exiled.API.Enums.ProjectileType.Flashbang);
                    int c = 0;
                    foreach (var item in guas)
                    {
                        c++;
                        item.Kill("封10年");
                        if (item != null && item.IsAlive && item.Role.Base is IFpcRole fpc)
                        {
                            fpc.FpcModule.Motor.GravityController.Gravity = FpcGravityController.DefaultGravity;
                        }
                    }
                    Exiled.API.Features.Cassie.MessageTranslated("", $"{c}个玩家 被暂时封号,共10年");
                }
            }
            else
            {
                foreach (var item in guas)
                {
                    if (item == null || !item.IsAlive)
                    {
                        continue;
                    }

                    item.Position = cam.transform.position;
                    if (item.Role.Base is IFpcRole fpc)
                    {
                        fpc.FpcModule.Motor.GravityController.Gravity = Vector3.zero;
                    }
                }
            }
        }

                public static void Stop()
        {

            guas.Clear();
            bolts.Clear();
            so?.Destroy();
            so = null;

            status = null;
            cam = null;
            anim = null; killed = false;
            _lastPhase = float.NaN;
        }
        public static void SetLayer(LightnLayer layer, bool visible)
        {
            if (!bolts.TryGetValue(layer, out var registry))
            {
                return;
            }

            foreach (var bolt in registry.Values)
            {
                if (bolt == null)
                {
                    continue;
                }

                if (visible)
                {
                    bolt.Rebuild();
                }
                else
                {
                    bolt.Clear();
                }
            }
        }

        private static void Map(Dictionary<LightnLayer, Dictionary<int, AdminToyBase>> mapper, LightnLayer layer, int id, AdminToyBase toy)
        {
            if (!mapper.TryGetValue(layer, out var m))
            {
                m = new();
                mapper[layer] = m;
            }

            m[id] = toy;
        }
        private static LightnLayer LayerOf(Transform block)
        {
            for (Transform t = block; t != null; t = t.parent)
            {
                if (Enum.TryParse(t.name, out LightnLayer layer) && layer != LightnLayer.None)
                {
                    return layer;
                }
            }

            return LightnLayer.None;
        }
        private static bool TryParseId(string name, string prefix, out int id)
        {
            id = 0;
            if (name == null || !name.StartsWith(prefix))
            {
                return false;
            }

            var rest = name.Substring(prefix.Length).Trim().Trim('(', ')').Trim();

            return rest.Length == 0 || int.TryParse(rest, out id);
        }
    }
}
