using Exiled.API.Features;
using MEC;
using NS_site27_api.Core;
using System;
using System.Collections.Generic;

namespace NS_site27_api.Modules.EventHandle.Handlers
{
    public static class BroadcastHandler
    {
        private static CoroutineHandle _handle;
        private static bool _stop;

        public static void Start()
        {
            _stop = false;
            _handle = CorePlugin.RunCoroutine(Broadcaster());
        }

        public static void Stop()
        {
            _stop = true;
        }

        private static IEnumerator<float> Broadcaster()
        {
            var module = ItemCleanerModule.Ins;
            if (module == null) yield break;

            int counter = 0;
            int index = 0;
            var cfg = module.Config;

            while (!_stop)
            {
                counter++;
                if (counter <= cfg.BroadcastWaitTime)
                {
                    yield return Timing.WaitForSeconds(1);
                }
                else
                {
                    counter = 0;
                    if (cfg.BroadcastContext.Count > index)
                    {
                        foreach (var player in Player.Enumerable)
                        {
                            player.Broadcast(new Exiled.API.Features.Broadcast(
                                $"<size={cfg.BroadcastSize}><color={cfg.BroadcastColor}>{cfg.BroadcastContext[index]}</color></size>",
                                (ushort)cfg.BroadcastShowTime));
                        }
                    }
                    index = (index + 1) % Math.Max(1, cfg.BroadcastContext.Count);
                }
            }
        }
    }
}
