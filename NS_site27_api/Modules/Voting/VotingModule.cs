using Exiled.API.Features;
using MEC;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using System.Collections.Generic;
using UnityEngine;

namespace NS_site27_api.Modules.Voting
{
    public class VotingConfig : ModuleConfigBase
    {
        public int DefaultVoteTime { get; set; } = 30;
    }

    public class VotingModule : ModuleBase<VotingConfig>
    {
        public override string ModuleName => "Voting";
        public static List<List<Player>> VoteControl = new List<List<Player>>();
        public static bool IsVoting = false;

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
            IsVoting = false;
        }

        public static void StartVote(string voteName, long voteTime)
        {
            CorePlugin.RunCoroutine(VoteCoroutine(voteName, voteTime));
        }
        public static string[] VoteHint(Player p)
        {
            if (VoteControl[0].Contains(p) || VoteControl[1].Contains(p))
                return null;

            return new[]{ $"管理员发起了投票:{CurrentVoteName} 时间:{remainingTime} 在控制台输入.voteyes 或 .voteno 弃权不投票"};
        }
        public static long remainingTime = 0;
        public static string CurrentVoteName = "";
        private static IEnumerator<float> VoteCoroutine(string voteName, long voteTime)
        {
            VoteControl = new List<List<Player>> { new List<Player>(), new List<Player>() };
            IsVoting = true;
            CurrentVoteName = voteName;
            foreach (var player in Player.List)
            {
                player.AddMessage("VoteHint",VoteHint,voteTime,ScreenPosition.Top);
            }
            remainingTime = voteTime;
            for (; remainingTime != 0; remainingTime--)
            {
                yield return Timing.WaitForSeconds(1);
            }
            foreach (var player in Player.List)
            {
                player.RemoveMessage("VoteHint");
            }
            int yes = VoteControl[0].Count;
            int no = VoteControl[1].Count;

            double percentage = (yes / (double)Mathf.Max(1, yes + no)) * 100;
            //Map.Broadcast((ushort)8f, );
            foreach (var player in Player.List)
            {
                player.AddMessage("VoteResultHint", $"投票:{voteName} 结果: 同意率:{percentage:F2}% 同意:{yes} 不同意:{no}", 7f, ScreenPosition.Top);
            }
            yield return Timing.WaitForSeconds(7);

            foreach (var player in Player.List)
            {
                player.RemoveMessage("VoteResultHint");
            }
            IsVoting = false;
            VoteControl = new List<List<Player>>();
        }
    }
}
