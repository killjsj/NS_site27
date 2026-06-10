using AudioApi;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using NeteaseMusicAPI;
using NS_site27_api.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.LobbyMusic
{
    public class LobbyMusicConfig : ModuleConfigBase
    {
    }

    public struct SongReq
    {
        public string id;
        public Player player;
        public SongReq(string id, Player player) { this.id = id; this.player = player; }

        public override bool Equals(object obj) => obj is SongReq other && other.id == id && other.player == player;
        public override int GetHashCode() => id.GetHashCode();
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class OrderSongCommand : ICommand
    {
        public string Command => "orderSong";
        public string[] Aliases => new[] { "os" };
        public string Description => "大厅点歌(仅网易云id),用法:os <歌曲id>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = "需要歌曲id!"; return false; }
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            if (LobbyMusicManager.Instance.AdminOverride && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "管理禁止点歌!"; return false; }
            if (Round.InProgress && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "回合已开始,禁止点歌!"; return false; }

            LobbyMusicManager.Instance.WaitForProcess.Enqueue(new SongReq(arguments.At(0), Player.Get(sender)));
            response = "Done!";
            return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DisOrderSongCommand : ICommand
    {
        public string Command => "DisOrderSOng";
        public string[] Aliases => new[] { "Dos" };
        public string Description => "禁止/允许点歌(toggle)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (LobbyMusicManager.Instance == null) { response = "未初始化"; return false; }
            LobbyMusicManager.Instance.AdminOverride = !LobbyMusicManager.Instance.AdminOverride;
            response = "Done! " + (LobbyMusicManager.Instance.AdminOverride ? "已禁止" : "已允许");
            return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class EnOrderSongCommand : ICommand
    {
        public string Command => "EnOrderSOng";
        public string[] Aliases => new[] { "Eos" };
        public string Description => "强制允许回合中点歌(toggle)";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (LobbyMusicManager.Instance == null) { response = "未初始化"; return false; }
            LobbyMusicManager.Instance.AdminOverrideEnable = !LobbyMusicManager.Instance.AdminOverrideEnable;
            response = "Done! " + (LobbyMusicManager.Instance.AdminOverrideEnable ? "已允许" : "已禁止");
            return true;
        }
    }

    public class LobbyMusicManager
    {
        public static LobbyMusicManager Instance { get; private set; }
        public bool _AdminOverride = false;
        public bool AdminOverrideEnable = false;
        public readonly ConcurrentQueue<SongReq> WaitForProcess = new ConcurrentQueue<SongReq>();
        private SongReq _processing;
        private CoroutineHandle _processor;
        public void Init()
        {
            Instance = this;
            Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
            VoicePlayerBase.OnFinishedTrack += VoicePlayerBase_OnFinishedTrack;
            Exiled.Events.Handlers.Server.RestartingRound += restart;
        }

        public void Cleanup()
        {
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            Instance = null;
            //Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
            Exiled.Events.Handlers.Server.RestartingRound -= restart;
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            if (DummyHub != null) DummyHub.Destroy();
            VoicePlayerBase.OnFinishedTrack -= VoicePlayerBase_OnFinishedTrack;
        }

        public void RoundStarted()
        {
            if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }

            readytonext = true;
        }
        public void restart()
        {
            if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
            readytonext = true;

        }
        public bool AdminOverride
        {
            get => _AdminOverride; set
            {
                if (_AdminOverride != value)
                {
                    if (value)
                    {
                        if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
                        readytonext = true;
                    }
                }
                _AdminOverride = value;
            }
        }
        bool SongAble => Round.IsLobby || AdminOverrideEnable;
        SongReq Processing;
        readonly NeteaseAPI api = new();
        public Npc DummyHub;
        VoicePlayerBase vpb;
        bool readytonext = true;

        void WaitingForPlayers()
        {
            restart();
            //createDummy();
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            _processor = Timing.RunCoroutine(Processer());
            _AdminOverride = false;
            AdminOverrideEnable = false;
        }
        void createDummy()
        {
            if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
            DummyHub = Npc.Spawn("音乐播放器");
            //Plugin.plugin.eventhandle.SPD.Add(DummyHub.ReferenceHub);
            DummyHub.ReferenceHub.serverRoles.NetworkHideFromPlayerList = true;
            //Intercom.TrySetOverride(DummyHub, true);
            vpb = VoicePlayerBase.Get(DummyHub.ReferenceHub);
            vpb.BroadcastChannel = VoiceChat.VoiceChatChannel.Intercom;
        }

        private void VoicePlayerBase_OnFinishedTrack(TrackFinishedEventArgs obj)
        {
            if (obj == null) return;
            if (obj.VoicePlayerBase == vpb)
            {
                readytonext = true;
                if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }

                File.Delete(obj.Track);
            }
        }

        IEnumerator<float> Processer()
        {
            Log.Info("start!");
            while (true)
            {
                yield return Timing.WaitForSeconds(0.4f);
                if (SongAble && readytonext && WaitForProcess.TryDequeue(out Processing))
                {
                    _processing = Processing;
                    if (!long.TryParse(Processing.id, out long songId)) { Processing.player?.SendConsoleMessage($"歌曲加载 - {songId} 无效id!", "yellow"); continue; }
                    readytonext = false;
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                    ProcessSongAsync(songId);
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                }
            }
            yield break;
        }

        private async Awaitable ProcessSongAsync(long songId)
        {
            int retries = 3;
            await Awaitable.BackgroundThreadAsync();
            while (retries >= 0)
            {
                try
                {
                    Log.Info($"Loading {songId}");
                    Processing.player?.SendConsoleMessage($"歌曲加载 - 开始处理{songId} 解析中", "yellow");
                    var urlTask = api.GetSongUrl(songId, NeteaseMusicAPI.QualityLevel.STANDARD, new Dictionary<string, string>());
                    var detailTask = api.GetSongDetail(songId);
                    await Task.WhenAll(urlTask, detailTask);

                    var url = await urlTask;
                    var del = await detailTask;
                    Log.Info($"Loading {songId} - ana");
                    if (url.exception != null)
                    {
                        Log.Info($"Loading {songId} ana- exception {url.exception}");
                        Processing.player?.SendConsoleMessage($"歌曲加载错误 ana- {url.exception}", "red");
                        if (retries == 0)
                        {
                            readytonext = true;
                            if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
                            break;
                        }
                        continue;
                    }
                    Processing.player?.SendConsoleMessage($"歌曲加载 - 解析完成", "green");
                    if (url.result.data.Count > 0)
                    {
                        var target = url.result.data[0];
                        var name = del.result.songs[0].name;
                        var author = del.result.songs[0].ar;

                        Log.Info($"Loading {songId} - downlaoding");
                        Processing.player?.SendConsoleMessage("歌曲加载 - 下载中", "green");
                        var p = Path.GetTempFileName();
                        var pn = Path.GetTempFileName() + ".ogg";

                        try
                        {
                            using (var c = new HttpClient())
                            {
                                var response = await c.GetAsync(target.url);
                                using (var f = File.OpenWrite(p))
                                {
                                    await response.Content.CopyToAsync(f);
                                }
                            }
                            Log.Info($"Loading {songId} - decoding");
                            Processing.player?.SendConsoleMessage("歌曲加载 - 解码中", "green");
                            NeteaseAPI.ConvertToOggMono48kHz(p, pn);
                            await Awaitable.MainThreadAsync();
                            if (!_AdminOverride && SongAble)
                            {
                                createDummy();
                                Timing.CallDelayed(0.5f, () => // wait for dummy init
                                {
                                    vpb.BroadcastChannel = VoiceChat.VoiceChatChannel.Intercom;
                                    if (!_AdminOverride && SongAble)
                                    {
                                        DummyHub.ReferenceHub.nicknameSync.MyNick = $"正在播放 - {name}";
                                        DummyHub.ReferenceHub.nicknameSync.Network_myNickSync = $"正在播放 - {name}";
                                        vpb.Enqueue(pn, -1);
                                        vpb.Play(0);
                                        Log.Info($"Loading {songId} done!");
                                        Processing.player?.SendConsoleMessage("歌曲加载成功!", "green");
                                    }
                                    else
                                    {
                                        readytonext = true;
                                        if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
                                        Processing.player?.SendConsoleMessage($"歌曲加载 - 处理失败 禁止点歌", "red");

                                    }
                                });

                            }
                            else
                            {
                                Processing.player?.SendConsoleMessage($"歌曲加载 - 处理失败 禁止点歌", "red");

                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"歌曲 {Processing.id} 处理失败 HttpClient: {ex}");
                            Processing.player?.SendConsoleMessage($"歌曲加载 - 处理失败 {ex}", "red");
                            if (retries == 0)
                            {
                                readytonext = true;
                                if (DummyHub != null) { DummyHub.Destroy(); DummyHub = null; }
                                break;
                            }
                        }
                        finally
                        {
                            File.Delete(p);

                        }

                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"歌曲 {Processing.id} 处理失败: {ex}");
                    if (retries == 0)
                    {
                        Processing.player?.SendConsoleMessage($"歌曲加载失败 {ex}", "red");
                        readytonext = true;
                    }
                    else
                    {
                        await Awaitable.WaitForSecondsAsync(1000);
                    }
                }
                retries--;
            }
        }
    }

    public class LobbyMusicModule : ModuleBase<LobbyMusicConfig>
    {
        public override string ModuleName => "LobbyMusic";

        private LobbyMusicManager _manager;


        public override void OnEnable()
        {
            

            _manager = new LobbyMusicManager();
            _manager.Init();
        }

        public override void OnDisable()
        {
            _manager?.Cleanup();
        }
    }
}
