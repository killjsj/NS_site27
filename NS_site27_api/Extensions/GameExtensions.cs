using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;

namespace NS_site27_api.Extensions
{
    public static class GameExtensions
    {
        public static string ZoneToString(this ZoneType zone)
        {
            return zone switch
            {
                ZoneType.LightContainment => "轻收容",
                ZoneType.HeavyContainment => "重收容",
                ZoneType.Entrance => "办公",
                ZoneType.Surface => "地表",
                ZoneType.Pocket => "口袋空间",
                _ => "未知",
            };
        }

        public static string RoomToString(this Room room)
        {
            if (room == null)
            {
                return "未知房间";
            }

            string zone = room.Zone != null ? room.Zone.ZoneToString() + " " : "";
            string name = zone;

            name += room.Type switch
            {
                RoomType.HczIntersectionJunk => "管道房",
                _ => room.Name,
            };
            return name.Replace("(Clone)", "");
        }

        public static List<Player> GetServerPlayers()
        {
            return Player.Enumerable.ToList();
        }
        public static Dictionary<RoleTypeId, string> RoleTrans = new() {
                {RoleTypeId.Scp049, "Scp049" },
                {RoleTypeId.Scp096, "Scp096" },
                {RoleTypeId.Scp3114, "Scp3114" },
                {RoleTypeId.Scp173, "Scp173" },
                {RoleTypeId.Scp939, "Scp939" },
                {RoleTypeId.Scp0492, "小僵尸" },
                {RoleTypeId.Scp079, "Scp079" },
                {RoleTypeId.Scp106, "Scp106" },

                {RoleTypeId.NtfCaptain, "九尾狐队长" },
                {RoleTypeId.NtfPrivate, "九尾狐列兵" },
                {RoleTypeId.NtfSergeant, "九尾狐中士" },
                {RoleTypeId.NtfSpecialist, "九尾狐收容专家" },
                {RoleTypeId.FacilityGuard, "保安" },
                {RoleTypeId.Scientist, "科学家" },

                {RoleTypeId.ChaosRifleman, "混沌步兵" },
                {RoleTypeId.ChaosMarauder, "混沌掠夺" },
                {RoleTypeId.ChaosRepressor, "混沌机枪" },
                {RoleTypeId.ChaosConscript, "混沌招募" },
                {RoleTypeId.ClassD, "ClassD " },

                {RoleTypeId.None, "None" },
                {RoleTypeId.Overwatch, "Overwatch" },
                {RoleTypeId.Spectator, "观察者" },
                {RoleTypeId.Tutorial, "教程角色" },
            };
        public static string RoleToString(this RoleTypeId role)
        {
            return RoleTrans.TryGetValue(role, out var name) ? name : role.ToString();
        }
    }
}
