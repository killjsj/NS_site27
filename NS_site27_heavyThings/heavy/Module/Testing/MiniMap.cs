using CommandSystem;
using MapGeneration;
using NS_site27_heavy.heavy.Module.testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utils;

namespace NS_site27_heavy.heavy.Module.Testing
{
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class PrintMapCommand : ICommand
    {
        public string Command => "PMC";

        public string[] Aliases => new[] { "" };

        public string Description => "";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "done!\n";
            foreach (var item in RoomIdentifier.AllRoomIdentifiers)
            {
                if(item == null) continue;
                response += $"{item}: {item?.Name}({item?.Shape}), {item?.MainCoords},{item?.Icon}({item?.Icon?.name ?? "null"},{item?.Icon?.bounds})\n";
            }
            return true;
        }
    }
}
