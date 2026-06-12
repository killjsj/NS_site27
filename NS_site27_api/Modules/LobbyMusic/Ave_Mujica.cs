
using AudioManagerAPI.Defaults;
using AudioManagerAPI.Features.Management;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NeteaseMusicAPI;
using NS_site27_api.Core;
using NS_site27_api.Core.UI;
using NS_site27_api.Modules.EventHandle.Handlers;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace NS_site27_api.Modules.LobbyMusic
{
    public class LobbyMusicConfig : ModuleConfigBase
    {
    }
    public enum SongSource
    {
        NeteaseCloud = 0,
        MonsterSiren = 1,//鹰角网络API
    }
    public struct SongReq
    {
        public string id;
        public SongSource source;
        public Player player;
        public SongReq(string id, Player player, SongSource ss = SongSource.NeteaseCloud) { this.id = id; this.player = player; source = ss; }

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
    [CommandHandler(typeof(ClientCommandHandler))]
    public class OrderSong_Monster_Siren_Command : ICommand
    {
        public string Command => "orderSongMSR";
        public string[] Aliases => new[] { "osMSR" };
        public string Description => "大厅点歌(仅MSR-id(https://monster-siren.hypergryph.com/music/....)),用法:osMSR <歌曲id>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = "需要歌曲id!"; return false; }
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            if (LobbyMusicManager.Instance.AdminOverride && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "管理禁止点歌!"; return false; }
            if (Round.InProgress && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "回合已开始,禁止点歌!"; return false; }

            LobbyMusicManager.Instance.WaitForProcess.Enqueue(new SongReq(arguments.At(0), Player.Get(sender), SongSource.MonsterSiren));
            response = "Done!";
            return true;
        }
    }
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DisOrderSongCommand : ICommand
    {
        public string Command => "DisOrderSong";
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
        public string Command => "EnOrderSong";
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
        private CancellationTokenSource _cts;
        public void Init()
        {
            Instance = this;
            Exiled.Events.Handlers.Server.WaitingForPlayers += WaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified += Verified;
            Exiled.Events.Handlers.Server.RoundStarted += RoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound += restart;
            _cts = new CancellationTokenSource();
        }

        public void Cleanup()
        {
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            Instance = null;
            Exiled.Events.Handlers.Server.RoundStarted -= RoundStarted;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= WaitingForPlayers;
            Exiled.Events.Handlers.Player.Verified -= Verified;
            Exiled.Events.Handlers.Server.RestartingRound -= restart;
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            CleanupSpeaker();
            _cts?.Cancel();
            _cts?.Dispose();
            foreach (var item in FilePaths)
            {
                if (File.Exists(item))
                {
                    File.Delete(item);
                }
            }
            FilePaths.Clear();
        }

        public void RoundStarted()
        {
            CleanupSpeaker();
        }
        public void restart()
        {
            CleanupSpeaker();

        }
        public bool AdminOverride
        {
            get => _AdminOverride; set
            {
                if (_AdminOverride != value)
                {
                    if (value)
                    {
                        CleanupSpeaker();
                        _cts?.Cancel();

                    }
                }
                _AdminOverride = value;
            }
        }
        //private CoroutineHandle _lyricsCoroutine;
        private List<LrcLine> _lrcLines = null;
        private float _songStartTime;
        public string CurrentSongName = "";
        private struct LrcLine
        {
            public float Time;
            public string Text;
        }

        private List<LrcLine> ParseLrc(string lrcContent)
        {
            var lines = new List<LrcLine>();
            var regex = new System.Text.RegularExpressions.Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
            foreach (var line in lrcContent.Split('\n'))
            {
                var match = regex.Match(line);
                if (!match.Success) continue;

                int min = int.Parse(match.Groups[1].Value);
                int sec = int.Parse(match.Groups[2].Value);
                int ms = int.Parse(match.Groups[3].Value.PadRight(3, '0'));
                float time = min * 60 + sec + ms / 1000f;
                string text = match.Groups[4].Value.Trim();
                if (!string.IsNullOrEmpty(text))
                    lines.Add(new LrcLine { Time = time, Text = text });
            }
            lines.Sort((a, b) => a.Time.CompareTo(b.Time));
            return lines;
        }
        public string GetSoucre()
        {
            var re = "";
            switch (this.Processing.source)
            {
                case SongSource.NeteaseCloud:
                    re = "网易云";
                    break;
                case SongSource.MonsterSiren:
                    re = "塞壬唱片";
                    break;
            }
            return re;
        }

        private string[] GetLyricsDisplay(Player player)
        {
            if (CurrentSongName != "" && sessionId != -1)
            {
                    var CurrentTime = DefaultAudioManager.Instance.GetSessionState(sessionId).PlaybackPosition;
                var lrcstr = "";
                for (int i = _lrcLines.Count - 1; i >= 0; i--)
                {
                    if (_lrcLines == null || _lrcLines.Count == 0) break;

                    if (_lrcLines[i].Time <= (float)(CurrentTime))
                    {
                        lrcstr = _lrcLines[i].Text;
                        break;
                    }
                }
                float total = (float)(SongTotalTime);
                string timeStr;
                if (total > 0)
                    timeStr = $"{FormatTime((float)(CurrentTime))}/{FormatTime(total)}";
                else
                    timeStr = FormatTime((float)(CurrentTime));
                return new[] { $"<align=right><size=14><line-height=45%><color=#00FFFF50>[{timeStr}]:{CurrentSongName}{{{GetSoucre()}}}\n{lrcstr}</color></line-height></size></align>" };
            }
            return new[] { "" };
        }

        private void StartSongInfoShower(string lrcContent)
        {
            StopLyrics(); // 确保之前的歌词协程停止
            _lrcLines = ParseLrc(lrcContent);
            foreach (var player in Player.List)
            {
                if (player != null && !player.HasMessage("LobbyMusicLyrics"))
                {
                    player.AddMessage("LobbyMusicLyrics", GetLyricsDisplay, -1f, new UIPosition(0, 990));
                }
            }
        }
        private void Verified(VerifiedEventArgs ev)
        {
                ev.Player.AddMessage("LobbyMusicLyrics", GetLyricsDisplay, -1f, new UIPosition(0, 990));
            
        }
        private void StopLyrics()
        {
            //if (_lyricsCoroutine.IsRunning)
            //    Timing.KillCoroutines(_lyricsCoroutine);
            foreach (var player in Player.List)
            {
                if (player != null && player.HasMessage("LobbyMusicLyrics"))
                {
                    player.RemoveMessage("LobbyMusicLyrics");
                }
            }
            _lrcLines = null;
        }

        //private IEnumerator<float> LyricsUpdateCoroutine()
        //{
        //    while (_lrcLines != null && DummyHub != null)
        //    {
        //        // 只需触发一次UI刷新即可，AddMessage内部已自动处理更新
        //        yield return Timing.WaitForSeconds(0.2f);
        //    }
        //}
        bool SongAble => Round.IsLobby || AdminOverrideEnable;

        public double SongTotalTime { get; private set; }

        SongReq Processing;
        readonly NeteaseAPI api = new();
        public int sessionId = -1;
        bool readytonext = true;

        void WaitingForPlayers()
        {
            restart();
            //createSpeaker();
            if (_processor.IsRunning) Timing.KillCoroutines(_processor);
            _processor = Timing.RunCoroutine(Processer());
            _AdminOverride = false;
            AdminOverrideEnable = false;
        }
        void createSpeaker(string key)
        {
            CleanupSpeaker();
            sessionId = DefaultAudioManager.Instance.PlayGlobalAudio(key, queue: false, fadeInDuration: 0);
        }
        private static string FormatTime(float seconds)
        {
            int min = (int)(seconds / 60);
            int sec = (int)(seconds % 60);
            return $"{min:D2}:{sec:D2}";
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
        }
        private async Awaitable ProcessSongAsync(long songId)
        {
            int retries = 2;
            CancellationToken ct = _cts.Token;

            while (retries >= 0)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    Log.Info($"Loading {songId}");
                    Processing.player?.SendConsoleMessage($"歌曲加载 - 开始处理 {songId} 解析中", "yellow");

                    string songDownloadPath = "";
                    string name = "";
                    string lrcContent = null;

                    if (Processing.source == SongSource.NeteaseCloud)
                    {
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
                            retries--;
                            if (retries == 0)
                            {
                                CleanupSpeaker();
                                break;
                            }
                            continue;
                        }
                        if (url.result.data.Count > 0)
                        {
                            songDownloadPath = url.result.data[0].url;
                            name = del.result.songs[0].name;
                            var Lyc = await api.GetLyric(songId, new());
                            if(Lyc != null && Lyc.code == 200 && Lyc.lrc != null && !string.IsNullOrEmpty(Lyc.lrc.lyric))
                            {
                                lrcContent = Lyc.lrc.lyric;
                            }
                        }
                    }
                    else if (Processing.source == SongSource.MonsterSiren)
                    {
                        // MSR API 请求
                        string apiUrl = $"https://monster-siren.hypergryph.com/api/song/{songId}";
                        using (var httpClient = new HttpClient())
                        {
                            var response = await httpClient.GetStringAsync(apiUrl);
                            var json = Newtonsoft.Json.Linq.JObject.Parse(response);
                            if (json == null)
                            {
                                Log.Error($"MSR json歌曲 {songId} 数据为空 rsp:{response}");
                                Processing.player?.SendConsoleMessage("歌曲数据获取失败", "red");
                                readytonext = true;
                                break;
                            }
                            var data = json["data"];
                            if (data == null)
                            {
                                Log.Error($"MSR 歌曲 {songId} 数据为空");
                                Processing.player?.SendConsoleMessage("歌曲数据获取失败", "red");
                                readytonext = true;
                                break;
                            }
                            name = data["name"]?.ToString() ?? "未知曲目";
                            songDownloadPath = data["sourceUrl"]?.ToString();
                            var lyricUrl = data["lyricUrl"]?.ToString();

                            if (string.IsNullOrEmpty(songDownloadPath))
                            {
                                Processing.player?.SendConsoleMessage("该歌曲无播放链接", "red");
                                readytonext = true;
                                break;
                            }

                            // 下载歌词（如果有）
                            if (!string.IsNullOrEmpty(lyricUrl))
                            {
                                try
                                {
                                    lrcContent = await httpClient.GetStringAsync(lyricUrl);
                                }
                                catch (Exception ex)
                                {
                                    Processing.player?.SendConsoleMessage($"歌词下载失败: {ex.Message}，继续播放纯音乐", "yellow");
                                }
                            }
                        }
                    }

                    Processing.player?.SendConsoleMessage($"歌曲加载 - 解析完成", "green");
                    await StartPlay(songDownloadPath, name, Processing, ct, lrcContent);
                    break;
                }
                catch (OperationCanceledException)
                {
                    Log.Info($"歌曲 {songId} 点歌被取消");
                    Processing.player?.SendConsoleMessage("点歌被系统取消", "yellow");
                    readytonext = true;
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"歌曲 {Processing.id} 处理失败: {ex}");
                    if (retries == 0)
                    {
                        Processing.player?.SendConsoleMessage($"歌曲加载失败 {ex.Message}", "red");
                        readytonext = true;
                        break;
                    }
                    else
                    {
                        retries--;
                        await Awaitable.WaitForSecondsAsync(1f);
                    }
                }
            }
        }
        private List<string> FilePaths = new();
        private async Awaitable StartPlay(string songDownloadPath, string name, SongReq req, CancellationToken ct, string lrcContent = null)
        {
            if (string.IsNullOrEmpty(songDownloadPath))
            {
                Processing.player?.SendConsoleMessage("歌曲链接无效", "red");
                readytonext = true;
                return;
            }

            string tempDownloadFile = Path.GetTempFileName();
            var guid = Guid.NewGuid();
            string tempOggFile = Path.Combine(Path.GetTempPath(), $"{guid}.ogg");
            try
            {
                Log.Info($"Loading {req.id} - downloading");
                Processing.player?.SendConsoleMessage("歌曲加载 - 下载中", "green");

                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(songDownloadPath, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var fileStream = File.OpenWrite(tempDownloadFile))
                        {
                            await response.Content.CopyToAsync(fileStream);
                        }
                    }
                }

                ct.ThrowIfCancellationRequested();

                Log.Info($"Loading {req.id} - decoding");
                Processing.player?.SendConsoleMessage("歌曲加载 - 解码中", "green");

                ct.ThrowIfCancellationRequested();
                using var reader = new AudioFileReader(tempDownloadFile);

                ISampleProvider provider = reader;

                if (reader.WaveFormat.Channels == 2)
                {
                    provider = new StereoToMonoSampleProvider(provider)
                    {
                        LeftVolume = 0.5f,
                        RightVolume = 0.5f
                    };
                }
                else if (reader.WaveFormat.Channels > 2)
                {
                    throw new NotSupportedException(
                        $"Unsupported channel count {reader.WaveFormat.Channels}");
                }

                if (reader.WaveFormat.SampleRate != 48000)
                {
                    provider = new WdlResamplingSampleProvider(
                        provider,
                        48000);
                }
                SongTotalTime = reader.TotalTime.TotalSeconds;
                WaveFileWriter.CreateWaveFile16(
                    tempOggFile,
                    provider);
                FilePaths.Add(tempOggFile);
                FilePaths.Add(tempDownloadFile);
                DefaultAudioManager.Instance.RegisterAudio(guid.ToString(), () => File.OpenRead(tempOggFile));
                await Awaitable.MainThreadAsync();
                ct.ThrowIfCancellationRequested();

                if (_AdminOverride || !SongAble)
                {
                    Processing.player?.SendConsoleMessage("歌曲加载 - 处理失败 禁止点歌", "red");
                    readytonext = true;
                    return;
                }

                Timing.CallDelayed(0.4f, () =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        CleanupSpeaker();
                        return;
                    }
                    if (!_AdminOverride && SongAble)
                    {
                        Log.Info($"Loading {req.id} done!");
                        Processing.player?.SendConsoleMessage("歌曲加载成功!", "green");
                        CurrentSongName = name;
                        _songStartTime = Time.time;
                        createSpeaker(guid.ToString());
                        StartSongInfoShower(lrcContent);
                    }
                    else
                    {
                        CleanupSpeaker();
                        Processing.player?.SendConsoleMessage("歌曲加载 - 处理失败 禁止点歌", "red");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Log.Info($"歌曲 {req.id} 加载被取消");
                CleanupSpeaker();
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"StartPlay 异常: {ex}");
                Processing.player?.SendConsoleMessage($"歌曲加载 - 处理失败 {ex.Message}", "red");
                CleanupSpeaker();
                throw;
            }
            finally
            {
            }
        }
        // 辅助方法
        private void CleanupSpeaker()
        {
            StopLyrics();
            if (sessionId != -1)
            {
                DefaultAudioManager.Instance.StopAudio(sessionId);
                sessionId = -1;

            }
            SongTotalTime = 0;
                readytonext = true;
        }
    }

    public class Ave_Mujica : ModuleBase<LobbyMusicConfig>
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
