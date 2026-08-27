using CommandSystem;
using DisplayKit.Elements;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using NS_site27_api.Core.UI.DisplayKit;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Utils;
using Utils.NonAllocLINQ;

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
            color,
            MsgText,
            title
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
            try
            {
                var player = ev.Player;
                if (msgs.TryGetValue(player, out var msg))
                {
                    foreach (var item in msg.ToArray())
                    {
                        RemoveMsg(player, item, true);
                    }
                    msg.TrimExcess();
                    _ = msgs.Remove(player);
                }
                player.RemoveLayer(this);
                _ = msgQueues.Remove(player);
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
            DisplayElement Container = canvas.AddElement();
            Container.BaseElement.name = "Container";
            Container.Position.Position = Position.Absolute;
            Container.Position.Right = Length.Percent(0f);
            Container.Position.Bottom = Length.Percent(14f);
            Container.Flex.Direction = FlexDirection.Column;
            Container.Display.Overflow = Overflow.Hidden;
            Container.Flex.Wrap = Wrap.NoWrap;
            Container.Flex.Shrink = 0f;
            Container.Size.Height = Length.Percent(67f);
            Container.Size.Width = Length.Percent(13.5f);
            Container.Align.AlignContent = Align.FlexEnd;
            Container.Align.AlignSelf = Align.Center;
            Container.Align.JustifyContent = Justify.FlexEnd;

            canvas.SortOrder = 255;
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
            MsgOutSide.Flex.Shrink = 0f;
            MsgOutSide.Spacing.MarginBottom = 9f;
            MsgOutSide.Size.Width = Length.Percent(100f);
            MsgOutSide.Size.Height = Length.Percent(100f);
            MsgOutSide.Size.MaxHeight = Length.Percent(18f);
            MsgOutSide.Position.Top = Length.Percent(0f);

            DisplayElement color = MsgOutSide.AddElement();
            color.BaseElement.name = "color";
            color.Flex.Grow = 1f;
            color.Position.Position = Position.Relative;
            color.Position.Top = Length.Percent(7f);
            color.Position.Left = Length.Percent(4f);
            color.Size.Width = Length.Percent(4f);
            color.Spacing.MarginTop = Length.Percent(0f);
            color.Spacing.MarginRight = Length.Percent(0f);
            color.Spacing.MarginBottom = Length.Percent(0f);
            color.Spacing.MarginLeft = Length.Percent(0f);
            color.Spacing.PaddingTop = Length.Percent(0f);
            color.Spacing.PaddingRight = Length.Percent(0f);
            color.Spacing.PaddingBottom = Length.Percent(0f);
            color.Spacing.PaddingLeft = Length.Percent(0f);
            color.Border.TopLeftRadius = Length.Percent(50f);
            color.Border.TopRightRadius = Length.Percent(50f);
            color.Border.BottomRightRadius = Length.Percent(50f);
            color.Border.BottomLeftRadius = Length.Percent(50f);
            color.Background.Color = new Color(0f, 1f, 0.9411765f, 1f);
            color.Size.MaxWidth = Length.Percent(18f);
            color.Size.MaxHeight = Length.Percent(18f);

            DisplayText MsgText = MsgOutSide.AddText("");
            MsgText.BaseElement.name = "MsgText";
            MsgText.Spacing.PaddingTop = 0f;
            MsgText.Spacing.PaddingRight = 0f;
            MsgText.Spacing.PaddingBottom = 0f;
            MsgText.Spacing.PaddingLeft = 0f;
            MsgText.Text.Color = new Color(0.09019608f, 1f, 0f, 1f);
            MsgText.Position.Position = Position.Absolute;
            MsgText.Position.Top = Length.Percent(28f);
            MsgText.Position.Bottom = Length.Percent(8f);
            MsgText.Position.Right = Length.Percent(0f);
            MsgText.Position.Left = Length.Percent(4f);
            MsgText.Text.Wrap = WhiteSpace.Normal;

            DisplayText title = MsgOutSide.AddText("");
            title.BaseElement.name = "title";
            title.Position.Position = Position.Relative;
            title.Position.Left = Length.Percent(16f);
            title.Text.Color = new Color(0f, 1f, 0.7960784f, 1f);
            title.Position.Top = -20f;
            title.Size.MaxWidth = Length.Percent(84f);

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
                float currentY = Mathf.Lerp(0, 1, t);
                element.Transform.Translate = new Translate(currentX, 0, 0);
                element.Transform.Scale = new StyleScale(new Vector3(1, currentY, 1));
                yield return Timing.WaitForSeconds(0.05f);
            }
            element.Transform.Translate = new Translate(0, 0, 0);
        }
        private static IEnumerator<float> SlideOut(Msg msg, Player player, float duration = 0.75f, float endX = 220)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                float currentX = Mathf.Lerp(0, endX, t);
                float currentY = Mathf.Lerp(1, 0, t);
                msg.MsgOutSide.Transform.Translate = new Translate(currentX, 0, 0);
                msg.MsgOutSide.Transform.Scale = new StyleScale(new Vector3(1, currentY, 1));
                yield return Timing.WaitForSeconds(0.05f);
            }
            msg.MsgOutSide.Transform.Translate = new Translate(endX, 0, 0);
            if (msgs.TryGetValue(player, out var Pmsgs))
            {
                msg.MsgOutSide.Remove();
                _ = Pmsgs.Remove(msg);
            }
        }
        public bool AddMsg(Player player, MsgArgs arg)
        {
            if (CanAddMsg(player))
            {
                if (!msgQueues.TryGetValue(player, out var pq))
                {
                    pq = new PriorityQueue<MsgArgs, int>();
                    msgQueues.Add(player, pq);
                }
                pq.Enqueue(arg, arg.Priority);
            }
            return true;
        }
        public bool CanAddMsg(Player player)
        {
            if (!msgs.TryGetValue(player, out _))
            {
                List<Msg> msg = new();
                msgs.Add(player, msg);
                return true;
            }
            return true;
        }
        public void RemoveMsg(Player player, string id, bool noAnim = false)
        {
            if (msgs.TryGetValue(player, out var msg))
            {
                foreach (var item in msg.Where(x => x.id == id).ToArray())
                {
                    if (item.animCH.IsRunning)
                    {
                        _ = Timing.KillCoroutines(item.animCH);
                    }
                    var o = msg[msg.IndexOf(item)];
                    if (o.Removing || item.Removing)
                    {
                        return;
                    }

                    msg[msg.IndexOf(item)] = o;
                    o.Removing = true;
                    item.Removing = true;
                    if (noAnim)
                    {
                        item.MsgOutSide.Remove();
                        _ = msg.Remove(item);
                    }
                    else
                    {
                        o.animCH = Timing.RunCoroutine(SlideOut(item, player));
                    }
                }
            }
        }
        public void RemoveMsg(Player player, Msg msg, bool noAnim = false)
        {
            if (msg.Removing)
            {
                return;
            }

            msg.Removing = true;
            if (msgs.TryGetValue(player, out var Pmsgs))
            {
                if (msg.animCH.IsRunning)
                {
                    _ = Timing.KillCoroutines(msg.animCH);
                }
                var o = Pmsgs[Pmsgs.IndexOf(msg)];
                o.Removing = true;
                msg.Removing = true;
                Pmsgs[Pmsgs.IndexOf(msg)] = o;
                if (noAnim)
                {
                    msg.MsgOutSide.Remove();
                    _ = Pmsgs.Remove(msg);
                }
                else
                {
                    o.animCH = Timing.RunCoroutine(SlideOut(msg, player));
                }
            }
        }
        public static Dictionary<Player, List<Msg>> msgs = new();
        private void internalAddMsg(DisplayElement container, Player player, MsgArgs args)
        {
            Msg m = new()
            {
                id = args.id,
                Updater = args.Updater
            };
            if (msgs.TryGetValue(player, out var existing))
            {
                var old = existing.FirstOrDefault(m => m.id == args.id);
                if (old != null)
                {
                    RemoveMsg(player, old);
                }
            }
            m.MsgOutSide = CreateMsgNode(container);
            var re = m.Updater?.Invoke(player);
            foreach (var item in m.MsgOutSide.Children)
            {
                if (Enum.TryParse<PlayerMessageArea>(item.BaseElement.name, true, out var result))
                {
                    if (item is DisplayText t)
                    {
                        switch (result)
                        {
                            case PlayerMessageArea.MsgText:
                                m.MsgText = t;
                                t.Content = $"[{m.startTime + m.lifetime - Time.time:F0}]" + re.Content;
                                break;
                            case PlayerMessageArea.title:
                                m.MsgTitle = t;
                                t.Content = re.Title;
                                break;
                        }
                    }
                    else if (item is DisplayElement e)
                    {
                        switch (result)
                        {
                            case PlayerMessageArea.color:
                                m.MsgNoticeCircle = e;
                                e.Background.Color = re.NoticeCircleColor;
                                break;
                        }
                    }
                    break;
                }
            }
            m.startTime = Time.time;
            m.lifetime = args.lifetime;
            m.animCH = Timing.RunCoroutine(SlideIn(m.MsgOutSide));
            msgs[player].Add(m);
        }
        public static Dictionary<Player, PriorityQueue<MsgArgs, int>> msgQueues = new();

        public override void Update(Player player, DisplayCanvas canvas)
        {
            try
            {
                foreach (var child in canvas.Children)
                {
                    if (child.BaseElement.name == "Container" && child is DisplayElement container)
                    {
                        if (msgs.TryGetValue(player, out var msg))
                        {
                            while (msg.Count >= 6)
                            {
                                RemoveMsg(player, msg[0]);
                            }
                            if (msgQueues.TryGetValue(player, out var re))
                            {
                                while (msg.Count < 6 && re.TryDequeue(out var arg, out _))
                                {
                                    internalAddMsg(container, player, arg);
                                }
                            }
                            string res = "";
                            for (int i = msg.Count - 1; i >= 0; i--)
                            {
                                var item = msg[i];
                                res = "";
                                try
                                {
                                    var msgUpdateResult = item.Updater?.Invoke(player);
                                    res = $"[{item.startTime + item.lifetime - Time.time:F0}]" + msgUpdateResult.Content;
                                    if (item.MsgText != null)
                                    {
                                        if (res != item.MsgText.Content)
                                        {
                                            item.MsgText.Content = res;
                                        }
                                    }
                                    res = msgUpdateResult.Title;
                                    if (item.MsgText != null)
                                    {
                                        if (res != item.MsgTitle.Content)
                                        {
                                            item.MsgTitle.Content = res;
                                        }
                                    }
                                    var resC = msgUpdateResult.NoticeCircleColor;
                                    if (item.MsgNoticeCircle != null)
                                    {
                                        if (resC != item.MsgNoticeCircle.Background.Color)
                                        {
                                            item.MsgNoticeCircle.Background.Color = resC;
                                        }
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
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> heap = new();

        public int Count => heap.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            heap.Add((element, priority));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].Priority.CompareTo(heap[i].Priority) <= 0)
                {
                    break;
                }

                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (heap.Count == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            (element, priority) = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = (2 * i) + 1;
                int right = (2 * i) + 2;
                int smallest = i;

                if (left < heap.Count && heap[left].Priority.CompareTo(heap[smallest].Priority) < 0)
                {
                    smallest = left;
                }

                if (right < heap.Count && heap[right].Priority.CompareTo(heap[smallest].Priority) < 0)
                {
                    smallest = right;
                }

                if (smallest == i)
                {
                    break;
                }

                (heap[i], heap[smallest]) = (heap[smallest], heap[i]);
                i = smallest;
            }

            return true;
        }
    }
    public static class PlayerMessageHelper
    {
        private static void AddHint(Player player, string id, float time, Func<Player, MsgUpdateResult> updater, int priority = 0)
        {
            if (player == null || PlayerMessageLayer.Instance == null)
            {
                return;
            }

            MsgArgs args = new()
            {
                Updater = updater,
                lifetime = time,
                id = id,
                Priority = priority
            };

            _ = PlayerMessageLayer.Instance.AddMsg(player, args);
        }

        public static void AddHint(this Player player, string id, float time, string str, PriorityLevel Priority = 0)
        {
            AddHint(player, id, time, x => new MsgUpdateResult { Content = str }, (int)Priority);
        }

        public static void AddHint(this Player player, string id, float time, Func<Player, string> getter, PriorityLevel Priority = 0)
        {
            AddHint(player, id, time, x => new MsgUpdateResult { Content = getter(x) }, (int)Priority);
        }

        public static void AddHint(this Player player, string id, float time, Func<Player, MsgUpdateResult> updater, PriorityLevel Priority = 0)
        {
            AddHint(player, id, time, updater, (int)Priority);
        }

        public static void RemoveHint(this Player player, string id)
        {
            if (player == null || PlayerMessageLayer.Instance == null)
            {
                return;
            }

            PlayerMessageLayer.Instance.RemoveMsg(player, id);
        }
    }
    public class MsgUpdateResult
    {
        public Color NoticeCircleColor = Color.green;
        public string Title = "";
        public string Content = "";
    }
    public class Msg
    {
        public string id;
        public Func<Player, MsgUpdateResult> Updater;
        public float startTime;
        public float lifetime;
        public DisplayElement MsgOutSide;
        public DisplayText MsgText;
        public DisplayText MsgTitle;
        public DisplayElement MsgNoticeCircle;
        public CoroutineHandle animCH;
        public override bool Equals(object obj)
        {
            return obj is Msg other && id == other.id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
        public bool Removing;
    }
    public struct MsgArgs
    {
        public string id;
        public Func<Player, MsgUpdateResult> Updater;
        public float lifetime;
        public int Priority;
        public override bool Equals(object obj)
        {
            return obj is MsgArgs other && id == other.id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }
    public enum PriorityLevel : int
    {
        Lowest = 0,
        Low = 20,
        Medium = 30,
        High = 45,
        Highest = 100,
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
            if (player == null) { response = "pass_player == null"; return false; }
            if (PlayerMessageLayer.Instance == null) { response = "PlayerMessageLayer.Instance == null"; return false; }
            if (arguments.Count < 3) { response = $"用法: {Description}"; return false; }
            var list = RAUtils.ProcessPlayerIdOrNamesList(arguments, 0, out string[] na);
            if (list == null || list.Count == 0) { response = "目标无效"; return false; }
            var time = float.Parse(na.First());
            string message = string.Join(" ", na.Skip(1).Take(na.Length - 1));
            var er = 0;
            var su = 0;
            foreach (var target in list)
            {
                try
                {
                    Player.Get(target).AddHint($"SendPrivateTip_{player.Id}_{target.PlayerId}_{Time.time:F1}", time, x =>
                    {
                        return new MsgUpdateResult() { Content = $"{player.Nickname}发送消息:{message}", NoticeCircleColor = Color.green, Title = "AdminMessage" };
                    }, PriorityLevel.Highest);
                    su++;
                }
                catch (Exception e)
                {
                    er++;
                    Log.Error(e);
                }
            }
            response = $"已发送 成功:{su} 失败:{er}"; return true;
        }
    }
}