using CommandSystem;
using DisplayKit.Elements;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using NS_site27_api.Core.UI.DisplayKit;
using NS_site27_api.Modules.Duel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;
using Utils.NonAllocLINQ;
using static RemoteAdmin.Communication.RaPlayerList;

namespace NS_site27_api.Modules.MessageModule
{
    public class PlayerMessageLayer : DisplayLayer
    {
        public static PlayerMessageLayer Instance;
        public override string Id { get; set; } = "msgLayer";
        public enum PlayerMessageArea
        {
            Container,
            MsgOutSide,
            MsgText
        }
        public override void OnEnable()
        {
            Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
            Exiled.Events.Handlers.Player.Verified += OnPlayerJoined;
            base.OnEnable();
            Instance = this;
        }
        public override void OnDisable()
        {
            base.OnDisable();
            Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
            Exiled.Events.Handlers.Player.Verified -= OnPlayerJoined;
            Instance = null;
        }

        private void OnPlayerLeft(LeftEventArgs ev)
        {
            try {
                var player = ev.Player;
                if (msgs.TryGetValue(player, out var msg))
                {
                    foreach (var item in msg.ToArray())
                    {
                        RemoveMsg(player, item.id);
                    }
                    msgs.Remove(player);
                }
                player.RemoveLayer(this);
                msgQueues.Remove(player);
                containers.Remove(player);
                
            }
            catch (Exception e)
            {
                Log.Error($"When left {e}");
            }
        }
        private void OnPlayerJoined(VerifiedEventArgs ev)
        {
            try
            {
                var player = ev.Player;
                player.AddLayer(this);
            }
            catch (Exception e)
            {
                Log.Error($"When OnPlayerJoined {e}");
            }
        }
        public override void InitNodes(Player target, DisplayCanvas canvas)
        {
            // start define of Container
            DisplayElement Container = canvas.AddElement();
            Container.BaseElement.name = "Container";
            Container.Position.Position = Position.Absolute;
            Container.Position.Right = Length.Percent(0f);
            Container.Position.Bottom = Length.Percent(14f);
            Container.Flex.Direction = FlexDirection.Column;
            Container.Display.Overflow = Overflow.Hidden;
            Container.Flex.Wrap = Wrap.NoWrap;
            Container.Size.MaxHeight = Length.Percent(48f);
            Container.Flex.Shrink = 0f;

            containers[target] = Container;
        }
        public static DisplayElement CreateMsgNode(DisplayElement Container)
        {
            DisplayElement MsgOutSide = Container.AddElement();
            MsgOutSide.BaseElement.name = "MsgOutSide";
            MsgOutSide.Flex.Grow = 0f;
            MsgOutSide.Background.Color = new Color(0f, 0f, 0f, 0.671f);
            MsgOutSide.Border.BottomLeftRadius = Length.Percent(8f);
            MsgOutSide.Border.BottomRightRadius = Length.Percent(8f);
            MsgOutSide.Border.TopRightRadius = Length.Percent(8f);
            MsgOutSide.Border.TopLeftRadius = Length.Percent(8f);
            MsgOutSide.Position.Position = Position.Relative;
            MsgOutSide.Position.Top = Length.Percent(0f);
            MsgOutSide.Position.Bottom = Length.Percent(0f);
            MsgOutSide.Position.Right = 0f;
            MsgOutSide.Position.Left = Length.Percent(0f);
            MsgOutSide.Size.Width = 220f;
            MsgOutSide.Size.Height = 103f;
            MsgOutSide.Flex.Shrink = 0f;

            /*
            canvas(UXML - id:0, Root) -> Container(VisualElement - id:1, 1th child of canvas) -> MsgOutSide(VisualElement - id:2, 1th child of Container) -> MsgText(Label - id:3, 1th child of MsgOutSide) 
            */
            // start define of MsgText
            DisplayText MsgText = MsgOutSide.AddText("Label");
            MsgText.BaseElement.name = "MsgText";
            MsgText.Spacing.PaddingTop = 0f;
            MsgText.Spacing.PaddingRight = 0f;
            MsgText.Spacing.PaddingBottom = 0f;
            MsgText.Spacing.PaddingLeft = 0f;
            MsgText.Text.Color = new Color(0.09019608f, 1f, 0f, 1f);
            MsgText.Position.Position = Position.Absolute;
            MsgText.Position.Top = Length.Percent(7f);
            MsgText.Position.Bottom = Length.Percent(8f);
            MsgText.Position.Right = Length.Percent(0f);
            MsgText.Position.Left = Length.Percent(0f);
            MsgText.Text.Wrap = WhiteSpace.Normal;

            return MsgOutSide;
        }
        private static IEnumerator<float> SlideIn(DisplayElement element, float duration = 0.75f, float startX = 220)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                float currentX = Mathf.Lerp(startX, 0f, t);
                element.Transform.Translate = new Translate(currentX, 0, 0);
                yield return Timing.WaitForSeconds(0.05f);
            }
            element.Transform.Translate = new Translate(0, 0, 0);
        }
        public bool AddMsg(Player player, MsgArgs arg)
        {
            if (CanAddMsg(player))
            {
                if (!msgQueues.TryGetValue(player, out var msg))
                {
                    msg = new();
                    msgQueues.Add(player, msg);
                }
                msg.Enqueue(arg);

            }
            return true;
        }
        public bool CanAddMsg(Player player)
        {
            if (!msgs.TryGetValue(player, out var msg))
            {
                msg = new();
                msgs.Add(player, msg);
                return true;
            }
            //if (msg.Count >= 6)
            //{
            //    return false;
            //}
            return true;
        }
        public void RemoveMsg(Player player, string id)
        {
            if (msgs.TryGetValue(player, out var msg))
            {
                foreach (var item in msg.Where(x => x.id == id).ToArray())
                {
                    if (item.animCH.IsRunning)
                        Timing.KillCoroutines(item.animCH);
                    item.MsgOutSide.Remove();
                    msg.Remove(item);
                }
            }
        }
        public void RemoveMsg(Player player, Msg msg)
        {
            if (msgs.TryGetValue(player, out var Pmsgs))
            {
                    if (msg.animCH.IsRunning)
                        Timing.KillCoroutines(msg.animCH);
                msg.MsgOutSide.Remove();
                    Pmsgs.Remove(msg);
                
            }
        }
        public static Dictionary<Player, List<Msg>> msgs = new();
        public static Dictionary<Player, DisplayElement> containers = new();
        private void internalAddMsg(DisplayElement container, Player player, MsgArgs args)
        {
            Msg m = new Msg();
            m.id = args.id;
            if (msgs.TryGetValue(player, out var existing))
            {
                var old = existing.FirstOrDefault(m => m.id == args.id);
                if (!old.Equals(default(Msg)))   // 因为 Msg 是 struct，需要判断是否找到
                    RemoveMsg(player, old);
            }
            m.MsgOutSide = CreateMsgNode(container);
            foreach (var item in m.MsgOutSide.Children)
            {
                if (item.BaseElement.name == "MsgText" && item is DisplayText t)
                {
                    m.MsgText = t;
                    t.Content = args.Updater?.Invoke(player);
                    break;
                }
            }
            m.Updater = args.Updater;
            m.startTime = Time.time;
            m.lifetime = args.lifetime;
            m.animCH = Timing.RunCoroutine(SlideIn(m.MsgOutSide));
            msgs[player].Add(m);
        }
        public static Dictionary<Player, Queue<MsgArgs>> msgQueues = new();

        public override void Update(Player player, DisplayCanvas canvas)
        {
            try
            {
                if (containers.TryGetValue(player, out var container))
                {
                    if (msgs.TryGetValue(player, out var msg))
                    {
                        // process reqs
                        while(msg.Count >= 6)
                        {
                            RemoveMsg(player, msg[0]);
                        }
                        if (msgQueues.TryGetValue(player, out var re))
                        {
                            while (msg.Count < 6 && re.TryDequeue(out var arg))
                            {
                                internalAddMsg(container, player, arg);
                            }
                        }
                        string res = "";
                        foreach (var item in msg.ToArray())
                        {
                            res = "";
                            try
                            {
                                res = item.Updater?.Invoke(player);
                                res = $"[{item.startTime + item.lifetime - Time.time:F0}]" + res;
                                if (res != item.MsgText.Content)
                                {
                                    item.MsgText.Content = res;
                                }
                                if (Time.time > item.startTime + item.lifetime)
                                {
                                    RemoveMsg(player, item);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error($"When updatingmsg:{player}'s {item.id} {e}");
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
    public static class PlayerMessageHelper
    {
        public static void AddHint(this Player player, string id, float time, Func<Player, string> getter)
        {
            if (player == null) return;
            if (PlayerMessageLayer.Instance == null) return;
            MsgArgs args = new MsgArgs()
            {
                Updater = getter,
                lifetime = time,
                id = id,
            };
            PlayerMessageLayer.Instance.AddMsg(player, args);
        }
        public static void RemoveHint(this Player player, string id)
        {
            if (player == null) return;
            if (PlayerMessageLayer.Instance == null) return;
            PlayerMessageLayer.Instance.RemoveMsg(player, id);
        }
    }
    public struct Msg
    {
        public string id;
        public Func<Player, string> Updater;
        public float startTime;
        public float lifetime;
        public DisplayElement MsgOutSide;
        public DisplayText MsgText;
        public CoroutineHandle animCH;
        public override bool Equals(object obj)
        {
            return obj is Msg other && id == other.id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }
    public struct MsgArgs
    {
        public string id;
        public Func<Player, string> Updater;
        public float lifetime;
        public override bool Equals(object obj)
        {
            return obj is MsgArgs other && id == other.id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class SendPrivateTip : ICommand
    {
        public string Command => "SendPrivateTip";

        public string[] Aliases => new[] { "SPT" };

        public string Description => "SPT playerid time message";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "player == null"; return false; }
            if (PlayerMessageLayer.Instance == null) { response = "PlayerMessageLayer.Instance == null"; return false; }
            if (arguments.Count < 3) { response = $"用法: {Description}"; return false; }

            string[] na; var list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out na);
            if (list == null || list.Count == 0) { response = "目标无效"; return false; }
            var time = float.Parse(na.First());
            string message = string.Join(" ", na.Skip(1).Take(na.Length - 1));
            var er = 0;
            var su = 0;
            foreach (var target in list)
            {
                try
                {
                    Player.Get(target).AddHint($"SendPrivateTip_{player.Id}_{target.PlayerId}_{Time.time:F1}", time, x => $"{player.Nickname}发送消息:{message}");
                    su++;
                }catch (Exception e)
                {
                    er++;
                    Log.Error(e);
                }
            }
            response = $"已发送 成功:{su} 失败:{er}"; return true;
        }
    }
}
