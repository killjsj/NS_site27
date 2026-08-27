using AudioManagerAPI.Defaults;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NeteaseMusicAPI;
using Newtonsoft.Json.Linq;
using NS_site27_api.Core;
using NS_site27_api.Core.UI.DisplayKit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using Round = Exiled.API.Features.Round;

namespace NS_site27_api.Modules.LobbyMusic
{
        public enum SongSource
    {
        NeteaseCloud = 0,
        MonsterSiren = 1
    }

        public struct SongReq
    {
        public string id;
        public SongSource source;
        public Player player;

        public SongReq(string id, Player player, SongSource ss = SongSource.NeteaseCloud)
        {
            this.id = id;
            this.player = player;
            source = ss;
        }

        public override bool Equals(object obj)
        {
            return obj is SongReq other && other.id == id && other.player == player;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

        public class LobbyMusicConfig : ModuleConfigBase
    {
    }

        [CommandHandler(typeof(ClientCommandHandler))]
    public class OrderSongCommand : ICommand
    {
        public string Command => "orderSong";
        public string[] Aliases => new[] { "os" };
        public string Description => "大厅点歌 (仅网易云ID)，用法: os <歌曲ID> / os <歌曲url>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = "需要歌曲ID!"; return false; }
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            if (LobbyMusicManager.Instance.AdminOverride && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "管理员已禁止点歌!"; return false; }
            if (Round.InProgress && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "回合已开始，禁止点歌!"; return false; }

            LobbyMusicManager.Instance.WaitForProcess.Enqueue(new SongReq(arguments.At(0), Player.Get(sender)));
            response = "点歌请求已提交!";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class OrderSongMSRCommand : ICommand
    {
        public string Command => "orderSongMSR";
        public string[] Aliases => new[] { "osMSR" };
        public string Description => "大厅点歌 (仅MSR ID，如 https://monster-siren.hypergryph.com/music/...), 用法: osMSR <歌曲ID>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = "需要歌曲ID!"; return false; }
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            if (LobbyMusicManager.Instance.AdminOverride && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "管理员已禁止点歌!"; return false; }
            if (Round.InProgress && !LobbyMusicManager.Instance.AdminOverrideEnable) { response = "回合已开始，禁止点歌!"; return false; }

            LobbyMusicManager.Instance.WaitForProcess.Enqueue(new SongReq(arguments.At(0), Player.Get(sender), SongSource.MonsterSiren));
            response = "点歌请求已提交!";
            return true;
        }
    }

        [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class DisOrderSongCommand : ICommand
    {
        public string Command => "DisOrderSong";
        public string[] Aliases => new[] { "Dos" };
        public string Description => "切换全局点歌禁止/允许状态";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            while (LobbyMusicManager.Instance.WaitForProcess.TryDequeue(out _))
            {
                ;
            }

            LobbyMusicManager.Instance.AdminOverride = !LobbyMusicManager.Instance.AdminOverride;
            response = $"点歌功能已{(LobbyMusicManager.Instance.AdminOverride ? "禁止" : "允许")}";
            LobbyMusicManager.Instance.DestroySpeaker();
            return true;
        }
    }

        [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class EnOrderSongCommand : ICommand
    {
        public string Command => "EnOrderSong";
        public string[] Aliases => new[] { "Eos" };
        public string Description => "切换强制允许回合中点歌状态（即使管理员禁止也可用）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (LobbyMusicManager.Instance == null) { response = "点歌系统未初始化"; return false; }
            LobbyMusicManager.Instance.AdminOverrideEnable = !LobbyMusicManager.Instance.AdminOverrideEnable;
            response = $"强制回合内点歌已{(LobbyMusicManager.Instance.AdminOverrideEnable ? "允许" : "禁止")}";
            return true;
        }
    }

        public class LobbyMusicManager
    {
        public static LobbyMusicManager Instance { get; set; }

        public bool AdminOverride { get; set; }
        public bool AdminOverrideEnable { get; set; }
        public readonly ConcurrentQueue<SongReq> WaitForProcess = new();

        public SongReq _processing;
        public CoroutineHandle _processor;
        public CancellationTokenSource _cts;
        public bool _readyToNext = true;
        public int sessionId = 0;

        public List<LrcLine> _lrcLines;
        public float _songStartTime;
        public float current;
        public CoroutineHandle monitor;

        public string CurrentSongName { get; set; }
        public double TotalTime { get; set; }

        public readonly List<string> _tempFiles = new();
        public readonly NeteaseAPI _api = new();

        public struct LrcLine
        {
            public float Time;
            public string Text;
        }

                public void Init()
        {
            Instance = this;
            Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound += OnRestartingRound;
            _cts = new CancellationTokenSource();
        }

                public void Cleanup()
        {
            if (_processor.IsRunning)
            {
                _ = Timing.KillCoroutines(_processor);
            }

            Instance = null;
            Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
            Exiled.Events.Handlers.Server.RestartingRound -= OnRestartingRound;
            _cts?.Cancel();
            _cts?.Dispose();
            DestroySpeaker();
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            _tempFiles.Clear();
        }

                public void OnRoundStarted()
        {
            DestroySpeaker();
        }

                public void OnRestartingRound()
        {
            _readyToNext = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            DestroySpeaker();
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            _tempFiles.Clear();
        }

                public void OnWaitingForPlayers()
        {
            OnRestartingRound();
            if (_processor.IsRunning)
            {
                _ = Timing.KillCoroutines(_processor);
            }

            _processor = Timing.RunCoroutine(ProcessQueue());
            AdminOverride = false;
            AdminOverrideEnable = false;
        }


                public IEnumerator<float> MonitorPlaybackEnd()
        {
            int currentSid = sessionId;
            while (sessionId == currentSid && TotalTime > 0)
            {
                yield return Timing.WaitForSeconds(0.5f);
                var state = DefaultAudioManager.Instance.GetSessionState(currentSid);
                if (state?.PhysicalSpeaker is DefaultSpeakerToyAdapter adapter)
                {
                    float pos = adapter.GetPlaybackPosition();
                    current = pos;
                    if (Time.time - _songStartTime >= (float)TotalTime + 0.5f)
                    {
                        foreach (var file in _tempFiles)
                        {
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                            }
                        }
                        _tempFiles.Clear();
                        DestroySpeaker();
                        yield break;
                    }
                }
                else
                {
                    DestroySpeaker();
                    yield break;
                }
                foreach (var player in Player.Enumerable)
                {
                    if (player.Role.Type == PlayerRoles.RoleTypeId.None)
                    {
                        player.RoleManager.ServerSetRole(PlayerRoles.RoleTypeId.Tutorial, PlayerRoles.RoleChangeReason.None, PlayerRoles.RoleSpawnFlags.UseSpawnpoint);
                        _ = Timing.CallDelayed(0.2f, () =>
                        {
                            player.RoleManager.ServerSetRole(PlayerRoles.RoleTypeId.Spectator, PlayerRoles.RoleChangeReason.None);

                        });
                    }
                }

            }
        }

                public void DestroySpeaker()
        {
            StopLyrics();
            if (sessionId != 0)
            {
                DefaultAudioManager.Instance.StopAudio(sessionId);
                sessionId = 0;

            }
            if (monitor.IsRunning)
            {
                _ = Timing.KillCoroutines(monitor);
            }

            TotalTime = 0;
            _readyToNext = true;
        }

                public bool SongPlayable => Round.IsLobby || AdminOverrideEnable;

                public IEnumerator<float> ProcessQueue()
        {
            Log.Info("start!");
            while (true)
            {
                yield return Timing.WaitForSeconds(0.4f);
                if (SongPlayable && _readyToNext && WaitForProcess.TryDequeue(out _processing))
                {
                    if (!long.TryParse(_processing.id, out long songId))
                    {
                        _processing.player?.SendConsoleMessage($"歌曲ID无效: {_processing.id}", "yellow");
                        continue;
                    }
                    _readyToNext = false;
                    _ = ProcessSongAsync(songId);
                }
            }
        }

                public async Awaitable ProcessSongAsync(long songId)
        {
            int retries = 2;
            CancellationToken ct = _cts.Token;
            await Awaitable.BackgroundThreadAsync();

            while (retries >= 0)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    Log.Info($"正在加载歌曲 {songId}");
                    _processing.player?.SendConsoleMessage($"歌曲加载中 - 解析... ({songId})", "yellow");

                    string downloadUrl = null;
                    string songName = null;
                    string lrcContent = null;

                    if (_processing.source == SongSource.NeteaseCloud)
                    {
                        var urlTask = _api.GetSongUrl(songId, NeteaseMusicAPI.QualityLevel.STANDARD, new Dictionary<string, string>());
                        var detailTask = _api.GetSongDetail(songId);
                        await Task.WhenAll(urlTask, detailTask);

                        var urlResult = await urlTask;
                        var detailResult = await detailTask;

                        if (urlResult.exception != null)
                        {
                            Log.Info($"获取歌曲URL失败: {urlResult.exception}");
                            _processing.player?.SendConsoleMessage("获取歌曲链接失败", "red");
                            retries--;
                            continue;
                        }

                        if (urlResult.result.data.Count > 0)
                        {
                            downloadUrl = urlResult.result.data[0].url;
                            songName = detailResult.result.songs[0].name;

                            var lyricResult = await _api.GetLyric(songId, new());
                            if (lyricResult?.code == 200 && !string.IsNullOrEmpty(lyricResult.lrc?.lyric))
                            {
                                lrcContent = lyricResult.lrc.lyric;
                            }
                        }
                    }
                    else                     {
                        string apiUrl = $"https://monster-siren.hypergryph.com/api/song/{songId}";
                        using var http = new HttpClient();
                        var response = await http.GetStringAsync(apiUrl);
                        var json = JObject.Parse(response);
                        var data = json["data"];
                        if (data == null)
                        {
                            Log.Error($"MS-R 歌曲数据为空: {songId}");
                            _processing.player?.SendConsoleMessage("歌曲数据获取失败", "red");
                            _readyToNext = true;
                            return;
                        }

                        songName = data["name"]?.ToString() ?? "未知曲目";
                        downloadUrl = data["sourceUrl"]?.ToString();
                        var lyricUrl = data["lyricUrl"]?.ToString();

                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            _processing.player?.SendConsoleMessage("该歌曲无播放链接", "red");
                            _readyToNext = true;
                            return;
                        }

                        if (!string.IsNullOrEmpty(lyricUrl))
                        {
                            try { lrcContent = await http.GetStringAsync(lyricUrl); }
                            catch { }
                        }
                    }

                    _processing.player?.SendConsoleMessage("歌曲解析完成，准备下载", "green");
                    await StartPlayback(downloadUrl, songName, lrcContent, ct);
                    return;                 }
                catch (OperationCanceledException)
                {
                    Log.Info($"歌曲 {songId} 被取消");
                    _processing.player?.SendConsoleMessage("点歌已被系统取消", "yellow");
                    _readyToNext = true;
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error($"处理歌曲 {songId} 时出错: {ex}");
                    if (retries == 0)
                    {
                        _processing.player?.SendConsoleMessage($"歌曲加载失败: {ex.Message}", "red");
                        _readyToNext = true;
                        return;
                    }
                    retries--;
                    await Awaitable.WaitForSecondsAsync(1f);
                }
            }
        }
                public async Awaitable StartPlayback(string url, string name, string lrc, CancellationToken ct)
        {
            await Awaitable.BackgroundThreadAsync();
            if (string.IsNullOrEmpty(url))
            {
                _processing.player?.SendConsoleMessage("歌曲链接无效", "red");
                _readyToNext = true;
                return;
            }


            string tempFile = Path.GetTempFileName();
            var guid = Guid.NewGuid();
            string tempOggFile = Path.Combine(Path.GetTempPath(), $"{guid}.ogg");
            try
            {
                Log.Info($"开始下载: {name}");
                _processing.player?.SendConsoleMessage("歌曲下载中...", "green");

                using (var httpClient = new HttpClient())
                {
                    using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    _ = response.EnsureSuccessStatusCode();
                    using var fileStream = File.OpenWrite(tempFile);
                    await response.Content.CopyToAsync(fileStream);
                }

                ct.ThrowIfCancellationRequested();

                Log.Info($"Loading {_processing.id} - decoding");
                _processing.player?.SendConsoleMessage("歌曲加载 - 解码中", "green");

                ct.ThrowIfCancellationRequested();
                using var reader = new AudioFileReader(tempFile);

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
                TotalTime = reader.TotalTime.TotalSeconds;
                WaveFileWriter.CreateWaveFile16(
    tempOggFile,
    provider);
                _tempFiles.Add(tempOggFile);
                _tempFiles.Add(tempFile);

                await Awaitable.MainThreadAsync();
                ct.ThrowIfCancellationRequested();

                if (!SongPlayable)
                {
                    _processing.player?.SendConsoleMessage("播放被阻止（当前禁止点歌）", "red");
                    _readyToNext = true;
                    return;
                }
                DefaultAudioManager.Instance.RegisterAudio(guid.ToString(), () => File.OpenRead(tempOggFile));
                sessionId = DefaultAudioManager.Instance.PlayGlobalAudio<int>(guid.ToString(), queue: false, fadeInDuration: 0, volume: 0.8f,state:1,validPlayersFilter:(p,_) => p.IsReady);
                CurrentSongName = name;
                _songStartTime = Time.time;
                StartLyrics(lrc);
                _ = Timing.CallDelayed(0.4f, () =>
                {
                    if (ct.IsCancellationRequested || !SongPlayable)
                    {
                        DestroySpeaker();
                        _readyToNext = true;
                        return;
                    }
                    if (sessionId != 0)
                    {
                        monitor = Timing.RunCoroutine(MonitorPlaybackEnd(), Segment.LateUpdate);
                    }

                    Log.Info($"歌曲播放开始: {name}");
                    _processing.player?.SendConsoleMessage("歌曲播放成功!", "green");

                });
            }
            catch (OperationCanceledException)
            {
                Log.Info($"歌曲 {name} 加载被取消");
                _readyToNext = true;
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"播放准备失败: {ex}");
                _processing.player?.SendConsoleMessage($"播放失败: {ex.Message}", "red");
                _readyToNext = true;
                throw;
            }
        }

                public List<LrcLine> ParseLrc(string lrcContent)
        {
            var lines = new List<LrcLine>();
            if (!string.IsNullOrEmpty(lrcContent))
            {
                var regex = new System.Text.RegularExpressions.Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
                foreach (var line in lrcContent.Split('\n'))
                {
                    var match = regex.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    int min = int.Parse(match.Groups[1].Value);
                    int sec = int.Parse(match.Groups[2].Value);
                    int ms = int.Parse(match.Groups[3].Value.PadRight(3, '0'));
                    float time = (min * 60) + sec + (ms / 1000f);
                    string text = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        lines.Add(new LrcLine { Time = time, Text = text });
                    }
                }
                lines.Sort((a, b) => a.Time.CompareTo(b.Time));
            }
            else
            {
                lines.Add(new LrcLine { Text = "无字幕", Time = 0 });
            }
            return lines;
        }

        public void StartLyrics(string lrcContent)
        {
            try
            {
                StopLyrics();
                _lrcLines = ParseLrc(lrcContent);
                foreach (var item in Player.Enumerable)
                {
                    item.AddLayer("lyDisplay");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void StopLyrics()
        {
            _lrcLines = null;
            foreach (var item in Player.Enumerable)
            {
                item.RemoveLayer("lyDisplay");
            }
        }

        public string GetSourceName()
        {
            return _processing.source switch
            {
                SongSource.NeteaseCloud => "网易云",
                SongSource.MonsterSiren => "塞壬唱片",
                _ => "未知"
            };
        }

        public static string FormatTime(float seconds)
        {
            int min = (int)(seconds / 60);
            int sec = (int)(seconds % 60);
            return $"{min:D2}:{sec:D2}";
        }

            }

        public class LobbyMusicModule : ModuleBase<LobbyMusicConfig>
    {
        public override string ModuleName => "LobbyMusic";
        public LobbyMusicManager _manager;

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