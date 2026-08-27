using CommandSystem;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp079.Pinging;
using PlayerRoles.Subroutines;
using RelativePositioning;
using System;
using UnityEngine;
using Utils.Networking;

namespace NS_site27_heavy.heavy.Module.Testing
{
    internal class test_Fake079Ind
    {
        public static void Send(Vector3 pos, Player player)
        {
            if (RoleTypeId.Scp079.TryGetRoleTemplate<Scp079Role>(out var r))
            {
                if (r is ISubroutinedRole isr)
                {
                    SubroutineBase[] allSubroutines = isr.SubroutineModule.AllSubroutines;
                    for (int i = 0; i < allSubroutines.Length; i++)
                    {
                        if (allSubroutines[i].GetType() == typeof(Scp079PingAbility))
                        {
                            byte index = (byte)(i + 1);
                            var writer = NetworkWriterPool.Get();
                            writer.WriteUShort(NetworkMessageId<SubroutineMessage>.Id);
                            writer.WriteByte(index);
                            {
                                bool f = false;
                                ReferenceHub h = null;
                                foreach (var item in ReferenceHub.AllHubs)
                                {
                                    if (item.roleManager.CurrentRole.RoleTypeId == RoleTypeId.Scp079)
                                    {
                                        f = true;
                                        h = item;
                                        break;
                                    }
                                }
                                if (!f)
                                {
                                    h = Spawn("", RoleTypeId.Scp079, true, Vector3.zero).ReferenceHub;
                                    h.roleManager.ServerSetRole(RoleTypeId.Scp079, RoleChangeReason.None);
                                    _ = Timing.CallDelayed(0.3f, () =>
                                    {
                                        writer.WriteReferenceHub(h);
                                        writer.WriteRoleType(RoleTypeId.Scp079);
                                        NetworkWriterPooled networkWriterPooled = NetworkWriterPool.Get();
                                        networkWriterPooled.WriteByte(1);
                                        networkWriterPooled.WriteRelativePosition(new RelativePositioning.RelativePosition(pos));
                                        networkWriterPooled.WriteVector3(pos);
                                        int num = networkWriterPooled.Position;
                                        if (num > 65790)
                                        {
                                            num = 0;
                                        }
                                        writer.WriteByte((byte)Math.Min(num, 255));
                                        if (num >= 255)
                                        {
                                            writer.WriteUShort((ushort)(num - 255));
                                        }
                                        writer.WriteBytes(networkWriterPooled.buffer, 0, num);
                                        player.Connection.Send(writer.ToArraySegment());
                                        NetworkWriterPool.Return(writer);
                                        NetworkServer.Destroy(h.gameObject);
                                    });
                                    break;
                                }
                                writer.WriteReferenceHub(h);
                                writer.WriteRoleType(RoleTypeId.Scp079);
                                NetworkWriterPooled networkWriterPooled = NetworkWriterPool.Get();
                                networkWriterPooled.WriteByte(1);
                                networkWriterPooled.WriteRelativePosition(new RelativePositioning.RelativePosition(pos));
                                networkWriterPooled.WriteVector3(pos);
                                int num = networkWriterPooled.Position;
                                if (num > 65790)
                                {
                                    num = 0;
                                }
                                writer.WriteByte((byte)Math.Min(num, 255));
                                if (num >= 255)
                                {
                                    writer.WriteUShort((ushort)(num - 255));
                                }
                                writer.WriteBytes(networkWriterPooled.buffer, 0, num);
                                player.Connection.Send(writer.ToArraySegment());
                                NetworkWriterPool.Return(writer);
                                break;
                            }
                        }
                    }
                }
            }
        }
        public static Npc Spawn(string name, RoleTypeId role = RoleTypeId.None, bool ignored = false, Vector3? position = null)
        {
            Npc npc = new(DummyUtils.SpawnDummy(name));
            _ = Timing.CallDelayed(0.2f, delegate
            {
                npc.Role.Set(role, SpawnReason.ForceClass, position.HasValue ? RoleSpawnFlags.AssignInventory : RoleSpawnFlags.All);
            });
            if (ignored)
            {
                _ = Round.IgnoredPlayers.Add(npc.ReferenceHub);
            }
            npc.ReferenceHub.serverRoles.NetworkHideFromPlayerList = true;
            Player.Dictionary.Add(npc.GameObject, npc);
            return npc;
        }
        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        public class Spawn079IndCommand : ICommand
        {
            public string Command => "S79ID";

            public string[] Aliases => new[] { "" };

            public string Description => "";

            public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                response = "done!\n";
                var p = Player.Get(sender);
                Send(p.Position, p);
                return true;
            }
        }
    }
}
