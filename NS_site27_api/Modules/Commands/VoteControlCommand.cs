using CommandSystem;
using Exiled.API.Features;
using NS_site27_api.Modules.Voting;
using System;

namespace NS_site27_api.Modules.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class StartVoteCommand : ICommand
    {
        public string Command => "start_vote";
        public string[] Aliases => new[] { "startv" };
        public string Description => "Start a server vote";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var runner = Player.Get(sender);
            if (runner == null || runner.KickPower < 3)
            {
                response = "No permission.";
                return false;
            }

            if (arguments.Count < 1)
            {
                response = "Usage: start_vote <vote_name> [time_seconds]";
                return false;
            }

            string voteName = arguments.At(0);
            long voteTime = arguments.Count > 1 && long.TryParse(arguments.At(1), out var t) ? t : 30;

            VotingModule.StartVote(voteName, voteTime);
            response = $"Vote \"{voteName}\" started for {voteTime}s.";
            return true;
        }
    }
}
