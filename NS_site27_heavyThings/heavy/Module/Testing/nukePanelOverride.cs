using AdminToys;
using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using PlayerRoles.Subroutines;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable.Schematics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Utils;
using Utils.NonAllocLINQ;
namespace Next_generationSite_27.UnionP
{
    public static class panel
    {
        public static SerializableSchematic ss;
        public static SchematicObject so;
        public static TextToy hczText;
        public static TextToy surfaceText;
        public static void DestroyPanel()
        {
            if (so != null)
            {
                so.Destroy();
                so = null;
            }
        }
        public static void overridePanel()
        {
            if(ss == null)
            {
                ss = new SerializableSchematic() { SchematicName = "nuke-panel",Position = Vector3.zero};
            }
            if (so == null)
            {
                so = ss.SpawnOrUpdateObject()?.GetComponent<SchematicObject>() ?? null;
                if (so == null)
                {
                    return;
                }
            }
            foreach (var item in so.AdminToyBases)
            {
                var s = true;
                switch (item.name)
                {
                    case "rot":
                        {
                            var n = Room.Get(Exiled.API.Enums.RoomType.HczNuke);
                            if (n != null)
                            {
                                item.CachedTransform.position = n.transform.position;
                                item.CachedTransform.rotation = n.transform.rotation;
                                Log.Info($"item:{item.CachedTransform} {item.CachedTransform.position},n:{n}");
                            }
                            break;
                        }
                    case "surfaceText":
                        {
                            s = false;
                            surfaceText = item as TextToy;
                            break;
                        }
                    case "hczText":
                        {
                            s = false;
                            hczText = item as TextToy;
                            break;
                        }
                }
                    item.NetworkMovementSmoothing = 0;
                if (s && item is not TextToy)
                {
                }
                else
                {
                    item.syncInterval = 0;
                }
            }
        }
        [CommandHandler(typeof(RemoteAdminCommandHandler))]
        internal class ChangeNukeCommand : ICommand
        {
            string ICommand.Command { get; } = "CN";

            string[] ICommand.Aliases { get; } = new[] { "ChangeNuke" };

            string ICommand.Description { get; } = "!!! cn [Text(可选)]";

            bool ICommand.Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
            {
                response = $"done!";
                if (arguments.Count > 0) { 
                    if(panel.surfaceText == null || panel.hczText == null)
                    {
                        response += "gening,try again next frame!...\n";
                        panel.overridePanel();
                        return false;
                    }
                    string message = "<size=1.71>" +  string.Join(" ", arguments) + "</size>";

                    panel.surfaceText.Network_textFormat= message;
                    panel.hczText.Network_textFormat = message;
                    response += $" hcz:{hczText.CachedTransform.position},room:{Room.Get(Exiled.API.Enums.RoomType.HczNuke)}";
                }
                else
                {
                    panel.DestroyPanel();
                    response += "Destroyed!";
                }
                return true;
            }
        }
    }
}

