using CommandSystem;
using Exiled.API.Features;
using NS_site27_api.Modules.Voting;

namespace NS_site27_api.Modules.Commands
{
    using System;
    [CommandHandler(typeof(ClientCommandHandler))]
    public class VoteYesCommand : ICommand
    {
        public string Command => "voteyes";
        public string[] Aliases => new[] { "vyes", "yes", ".vyes" };
        public string Description => "Vote yes";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null || !VotingModule.IsVoting)
            {
                response = "No active vote.";
                return false;
            }

            if (VotingModule.VoteControl[0].Contains(player) || VotingModule.VoteControl[1].Contains(player))
            {
                response = "Already voted.";
                return false;
            }

            VotingModule.VoteControl[0].Add(player);
            response = "Voted YES.";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class VoteNoCommand : ICommand
    {
        public string Command => "voteno";
        public string[] Aliases => new[] { "vno", "no", ".vno" };
        public string Description => "Vote no";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null || !VotingModule.IsVoting)
            {
                response = "No active vote.";
                return false;
            }

            if (VotingModule.VoteControl[0].Contains(player) || VotingModule.VoteControl[1].Contains(player))
            {
                response = "Already voted.";
                return false;
            }

            VotingModule.VoteControl[1].Add(player);
            response = "Voted NO.";
            return true;
        }
    }
}
