using DisplayKit.Elements;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NS_site27_api.Core.UI.DisplayKit
{
    public struct PlayerDisplayer
    {
        public Player player;
        public DisplayCanvas canvas;
        public DisplayLayer displayLayer;
        public override bool Equals(object obj)
        {
            if (obj is not PlayerDisplayer other)
                return false;

            return player == other.player &&
                   displayLayer == other.displayLayer;
        }


        public override int GetHashCode()
        {
            return HashCode.Combine(
                player,
                displayLayer
            );
        }
    }
    public class DisplayKitRunner
    {
        public static DisplayKitRunner Instance;
        private HashSet<DisplayLayer> Layers = new();
        private Dictionary<Player, Dictionary<PlayerDisplayer, CoroutineHandle>> Players = new();

        public void RegisterLayer(DisplayLayer layer)
        {
            if (layer == null) return;
            Layers.Add(layer);
        }
        public IEnumerator<DisplayLayer> GetLayers()
        {
            return Layers.GetEnumerator();
        }
        public DisplayLayer? GetLayer(string id)
        {
            return Layers.Where(x => x.Id == id).FirstOrDefault();
        }
        public DisplayLayer? UnregisterLayer(string id)
        {
            var l = GetLayer(id);
            if (l != null)
            {
                Layers.Remove(l);
            }
            return l;
        }
        public DisplayLayer? UnregisterLayer(DisplayLayer l)
        {
            if (l != null)
            {
                Layers.Remove(l);
            }
            return l;
        }

        public void AddLayer(Player player, DisplayLayer layer)
        {
            if (!Layers.Contains(layer))
            {
                RegisterLayer(layer);
            }
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
            }
            if (l.Keys.Any(x => x.displayLayer == layer))
            {
                return;
            }
            var c = DisplayCanvas.Create();
            layer.InitNodes(player, c);
            var er = new PlayerDisplayer() { canvas = c, displayLayer = layer, player = player };
            var ch = Timing.RunCoroutine(Updater(er));
            l.Add(er, ch);
            c.Show(player.ReferenceHub);
        }
        public void AddLayer(Player player, string id)
        {
            var layer = GetLayer(id);
            if (layer == null)
            {
                return;
            }
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
            }
            if (l.Keys.Any(x => x.displayLayer == layer))
            {
                return;
            }
            var c = DisplayCanvas.Create();
            layer.InitNodes(player, c);
            var er = new PlayerDisplayer() { canvas = c, displayLayer = layer, player = player };
            var ch = Timing.RunCoroutine(Updater(er));
            l.Add(er, ch);
            c.Show(player.ReferenceHub);
        }

        public IEnumerator<float> Updater(PlayerDisplayer Displayer)
        {
            while (true)
            {
                if (Displayer.displayLayer == null ||
                        Displayer.canvas == null)
                    yield break;
                try
                {
                    Displayer.displayLayer.Update(Displayer.player, Displayer.canvas);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to update ui at player:{Displayer.player} in id: {Displayer.displayLayer.Id} due:{ex}");
                }
                yield return Timing.WaitForSeconds((float)Displayer.displayLayer.updateTime.TotalSeconds);
            }
        }
        public void SetVisible(bool vis, Player player, DisplayLayer layer)
        {
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
                return;
            }
            var r = l.FirstOrDefault(x => x.Key.displayLayer == layer);
            if (r.Key.displayLayer == null)
            {
                return;
            }
            r.Key.displayLayer.SetVisible(vis, player, r.Key.canvas);
        }
        public void SetVisible(bool vis, Player player, string id)
        {
            var layer = GetLayer(id);
            if (layer == null)
            {
                return;
            }
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
                return;
            }
            var r = l.FirstOrDefault(x => x.Key.displayLayer == layer);
            if (r.Key.displayLayer == null)
            {
                return;
            }
            r.Key.displayLayer.SetVisible(vis, player, r.Key.canvas);
        }
        public void RemoveLayer(Player player, DisplayLayer layer)
        {
            if (!Layers.Contains(layer))
            {
                RegisterLayer(layer);
            }
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
                return;
            }
            var re = l.FirstOrDefault(x => x.Key.displayLayer == layer);
            if (re.Key.displayLayer == null)
            {
                return;
            }
            if (re.Value.IsRunning)
                Timing.KillCoroutines(re.Value);
            re.Key.displayLayer.DestroyNodes(player, re.Key.canvas);
            if (re.Key.canvas != null)
            {
                re.Key.canvas.Destroy();
            }
            l.Remove(re.Key);
        }
        public void RemoveLayer(Player player, string id)
        {
            var layer = GetLayer(id);
            if (layer == null)
            {
                return;
            }
            if (!Players.TryGetValue(player, out var l))
            {
                l = new();
                Players[player] = l;
                return;
            }
            var re = l.FirstOrDefault(x => x.Key.displayLayer == layer);
            if (re.Key.displayLayer == null)
            {
                return;
            }
            if (re.Value.IsRunning)
                Timing.KillCoroutines(re.Value);
            re.Key.displayLayer.DestroyNodes(player, re.Key.canvas);
            if (re.Key.canvas != null)
            {
                re.Key.canvas.Destroy();
            }
            l.Remove(re.Key);
        }
        public void Disable()
        {
            Instance = null;
            Exiled.Events.Handlers.Player.Left -= Left;
            Exiled.Events.Handlers.Server.RestartingRound -= restart;
        }
        public void Left(LeftEventArgs ev)
        {
            if (Players.TryGetValue(ev.Player, out var v))
            {
                if (v != null)
                {
                    foreach (var item in v)
                    {
                        try
                        {
                            if (item.Value.IsRunning)
                                Timing.KillCoroutines(item.Value);
                            item.Key.displayLayer.DestroyNodes(ev.Player, item.Key.canvas);
                            if (item.Key.canvas != null)
                            {
                                item.Key.canvas.Destroy();
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to destroy! {e}");
                        }
                    }
                }
            }
            Players.Remove(ev.Player);
        }
        public void restart()
        {
            foreach (var player in Players)
            {
                foreach (var item in player.Value)
                {
                    Timing.KillCoroutines(item.Value);

                    try
                    {
                        item.Key.displayLayer.DestroyNodes(
                            player.Key,
                            item.Key.canvas
                        );

                        item.Key.canvas.Destroy();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            Players.Clear();
        }
        public void Enable()
        {
            Instance = this;
            Exiled.Events.Handlers.Player.Left += Left;
            Exiled.Events.Handlers.Server.RestartingRound += restart;
        }
    }
    public static class DisplayKitExt
    {
        public static void AddLayer(this Player player, DisplayLayer layer)
        {
            DisplayKitRunner.Instance.AddLayer(player, layer);
        }
        public static void AddLayer(this Player player, string id)
        {
            DisplayKitRunner.Instance.AddLayer(player, id);
        }
        public static void SetVisible(this Player player, bool vis, DisplayLayer layer)
        {
            DisplayKitRunner.Instance.SetVisible(vis, player, layer);
        }
        public static void SetVisible(this Player player, bool vis, string id)
        {
            DisplayKitRunner.Instance.SetVisible(vis, player, id);
        }
        public static void RemoveLayer(this Player player, DisplayLayer layer)
        {
            DisplayKitRunner.Instance.RemoveLayer(player, layer);
        }
        public static void RemoveLayer(this Player player, string id)
        {
            DisplayKitRunner.Instance.RemoveLayer(player, id);
        }
    }
}
