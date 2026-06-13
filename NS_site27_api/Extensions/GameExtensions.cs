using Exiled.API.Features;
using System.Collections.Generic;
using System.Linq;
using MapGeneration;
using PlayerRoles;
using Exiled.API.Enums;

namespace NS_site27_api.Extensions
{
    public static class GameExtensions
    {
        public static string ZoneToString(this ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.LightContainment: return "轻收容";
                case ZoneType.HeavyContainment: return "重收容";
                case ZoneType.Entrance: return "办公";
                case ZoneType.Surface: return "地表";
                case ZoneType.Pocket: return "口袋空间";
                default: return "未知";
            }
        }

        public static string RoomToString(this Room room)
        {
            if (room == null) return "未知房间";

            string zone = room.Zone != null ? room.Zone.ZoneToString() + " " : "";
            string name = zone;

            switch (room.Type)
            {
                case RoomType.HczIntersectionJunk:
                    name += "管道房";
                    break;
                default:
                    name += room.Name;
                    break;
            }

            return name.Replace("(Clone)","");
        }

        public static List<Player> GetServerPlayers()
        {
            return Player.Enumerable.ToList();
        }
    }
}
