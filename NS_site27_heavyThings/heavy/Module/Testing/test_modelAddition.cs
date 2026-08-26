using AdminToys;
using CommandSystem;
using Exiled.API.Features;
using HarmonyLib;
using Mirror;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.Thirdperson;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Utils;

namespace NS_site27_heavy.heavy.Module.testing
{
    public static class ModelAdd
    {
        public static MethodInfo anim_get;
        public static Dictionary<Player, List<(SchematicObject, List<AdminToyBase>)>> m = new();

        public static void start(Player player, string SchematicName = "test-armor")
        {
            if (anim_get == null)
            {
                anim_get = typeof(AnimatedCharacterModel).PropertyGetter("Animator");
            }

            var ss = new SerializableSchematic() { SchematicName = SchematicName };
            var gb = ss.SpawnOrUpdateObject();
            var a = gb.GetComponent<AdminToys.PrimitiveObjectToy>();
            if (a != null)
            {
                a.NetworkMovementSmoothing = 0;
                a.syncInterval = 0;
                if (gb.TryGetComponent<SchematicObject>(out var so))
                {
                    gb.transform.parent = player.GameObject.transform;
                    if (!m.TryGetValue(player, out var primitives))
                    {
                        primitives = new();
                        m[player] = primitives;
                    }
                    var i2 = new List<AdminToyBase>();
                    primitives.Add((so, i2));
                    if (player.Role.Base is IFpcRole fpc)
                    {
                        if (fpc.FpcModule.CharacterModelInstance is not AnimatedCharacterModel animatedCharacterModel)
                        {
                            return;
                        }
                        if (anim_get != null)
                        {
                            Animator animator = (Animator)anim_get.Invoke(animatedCharacterModel, null);
                            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                            {
                                Log.Info("animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman");
                                return;
                            }
                            foreach (var item in so.AdminToyBases)
                            {
                                item.NetworkMovementSmoothing = 0;
                                item.syncInterval = 0;
                                if (Enum.TryParse<HumanBodyBones>(item.name, true, out var bonetype))
                                {
                                    Transform boneTransform = animator.GetBoneTransform(bonetype);
                                    if (boneTransform != null)
                                    {
                                        i2.Add(item);
                                        var f = item.gameObject.AddComponent<follower>();
                                        f.TargetFollower = boneTransform;
                                        f.ThisFollower = item.CachedTransform;

                                    }
                                    else
                                    {
                                        Log.Info("boneTransform null");
                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void Clear(Player player, string name = "")
        {
            if (m.TryGetValue(player, out var primitives))
            {
                if (primitives.Any(x => x.Item1.Name == name))
                {
                    var i = primitives.Find(x => x.Item1.Name == name);
                    foreach (var item in i.Item2)
                    {
                        if (item.gameObject.TryGetComponent<follower>(out var follower))
                        {
                            GameObject.Destroy(follower);
                        }
                    }
                    i.Item1.Destroy();
                    _ = primitives.Remove(i);
                }
                else
                {
                    foreach (var i in primitives)
                    {
                        foreach (var item in i.Item2)
                        {
                            if (item.gameObject.TryGetComponent<follower>(out var follower))
                            {
                                GameObject.Destroy(follower);
                            }
                        }
                        i.Item1.Destroy();
                    }
                    primitives.Clear();
                }
            }
        }
    }
    public class follower : MonoBehaviour
    {
        public Transform TargetFollower;
        public Transform ThisFollower;
        public Vector3 offset = new(0, 0, 0);
        public void LateUpdate()
        {
            if (ThisFollower == null)
            {
                ThisFollower = transform;
            }
            if (TargetFollower != null)
            {
                ThisFollower.position = TargetFollower.position + offset;
                ThisFollower.rotation = TargetFollower.rotation;
                //Log.Info($"pos:{ThisFollower.position}");
            }
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class TryAttachModelOnBodyCommand : ICommand
    {
        public string Command => "TAMOB";

        public string[] Aliases => new[] { "" };

        public string Description => "";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "done!\n";
            var p = Player.Get(sender);
            if (arguments.Count < 1)
            {
                response = "Usage: TAMOB [model] <player>";
                return false;
            }

            var targets = RAUtils.ProcessPlayerIdOrNamesList(arguments, 1, out _);
            if (targets == null || targets.Count == 0)
            {
                response = "Player not found. Using yourself";
                targets = new List<ReferenceHub>() { p.ReferenceHub };
            }
            foreach (var item in targets)
            {
                ModelAdd.start(Player.Get(item), arguments.First());
            }
            return true;
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class ClearAttachedModelOnBodyCommand : ICommand
    {
        public string Command => "CAMOB";

        public string[] Aliases => new[] { "" };

        public string Description => "";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "done!\n";
            var p = Player.Get(sender);
            var targets = RAUtils.ProcessPlayerIdOrNamesList(arguments, 1, out _);
            if (targets == null || targets.Count == 0)
            {
                response = "Player not found. Using yourself";
                targets = new List<ReferenceHub>() { p.ReferenceHub };
            }
            foreach (var item in targets)
            {
                ModelAdd.Clear(Player.Get(item));
            }
            return true;
        }
    }
}
