using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Interactables.Interobjects.DoorUtils;
using MapGeneration;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FacilityNavigation
{
    public class RoomNode
    {
        public Room Room { get; }
        public Vector3 Position => Room.Position;
        public List<RoomEdge> Edges { get; }

        public RoomNode(Room room)
        {
            Room = room;
            Edges = new List<RoomEdge>(4);
        }

        public override string ToString()
        {
            return Room != null ? $"{Room.Name} ({Room.Type})" : "<null>";
        }
    }

    public enum RoomEdgeType
    {
        Door,
        Connector,
        Elevator,
        Transition,
    }

    public class RoomEdge
    {
        public RoomNode From { get; }
        public RoomNode To { get; }

        public DoorVariant DoorBase { get; }

        public Door Door =>
            DoorBase == null ? null : Door.Get(DoorBase);

        public RoomEdgeType Type { get; }

        public Vector3 ConnectionPoint { get; }

        public int LinkId { get; }

        public RoomEdge(RoomNode from, RoomNode to, DoorVariant door,
                        RoomEdgeType type = RoomEdgeType.Door, Vector3? point = null, int linkId = 0)
        {
            From = from;
            To = to;
            DoorBase = door;
            Type = type;
            LinkId = linkId;
            ConnectionPoint = point
                              ?? (door != null ? door.transform.position
                                               : (from.Position + to.Position) * 0.5f);
        }

        public override string ToString()
        {
            return $"{From} -[{Type}#{LinkId}]-> {To}";
        }
    }

    public readonly struct RoomPair : IEquatable<RoomPair>
    {
        public readonly RoomIdentifier A;
        public readonly RoomIdentifier B;

        public RoomPair(RoomIdentifier a, RoomIdentifier b)
        {
            if (a == null || b == null)
            {
                A = a;
                B = b;
                return;
            }

            if (a.GetInstanceID() <= b.GetInstanceID())
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(RoomPair other)
        {
            return A == other.A && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is RoomPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashA = A == null ? 0 : A.GetInstanceID();
                int hashB = B == null ? 0 : B.GetInstanceID();
                return (hashA * 397) ^ hashB;
            }
        }
    }
}
