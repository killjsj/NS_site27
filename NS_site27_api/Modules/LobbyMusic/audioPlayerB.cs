using MEC;
using Mirror;
using NVorbis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoiceChat;
using VoiceChat.Codec;
using VoiceChat.Networking;
using Random = UnityEngine.Random;
using Log = LabApi.Features.Console.Logger;
using System;
using VoiceChat.Codec.Enums;

namespace AudioApi
{
    public class TrackFinishedEventArgs
    {
        public VoicePlayerBase VoicePlayerBase { get; }
        public string Track { get; }
        public bool DirectPlay { get; }
        public TrackFinishedEventArgs(VoicePlayerBase playerBase, string track, bool directPlay)
        {
            VoicePlayerBase = playerBase;
            Track = track;
            DirectPlay = directPlay;
        }
    }
#pragma warning disable CS8618
    public class VoicePlayerBase : MonoBehaviour
    {
        /// <summary>
        /// AudioPlayers列表
        /// </summary>
        public static Dictionary<ReferenceHub, VoicePlayerBase> AudioPlayers { get; set; } = new();
        public OpusEncoder Encoder { get; } = new(OpusApplicationType.Voip);
        public PlaybackBuffer PlaybackBuffer { get; } = new PlaybackBuffer();
        public int HeadSamples { get; set; } = 1920;
        public byte[] EncodedBuffer { get; } = new byte[512];

        /// <summary>
        /// 玩家Hub
        /// </summary>
        public ReferenceHub Owner { get; set; }

        public bool stopTrack = false;
        public bool ready = false;
        public CoroutineHandle PlaybackCoroutine;
        public float allowedSamples;
        public int samplesPerSecond;
        public Queue<float> StreamBuffer { get; } = new();
        public VorbisReader VorbisReader { get; set; }
        public float[] SendBuffer { get; set; }
        public float[] ReadBuffer { get; set; }
        public float Volume { get; set; } = 100f;
        public List<string> AudioToPlay { get; set; } = new();
        public string CurrentPlay { get; set; }
        public MemoryStream CurrentPlayStream { get; set; }
        public bool Loop { get; set; } = false;
        public bool Shuffle { get; set; } = false;
        public bool Continue { get; set; } = true;
        public bool ShouldPlay { get; set; } = true;
        public bool LogDebug { get; set; } = false;
        public bool LogInfo { get; set; } = true;
        public bool IsFinished { get; set; } = false;
        public bool ClearOnFinish { get; set; } = false;
        public List<ReferenceHub> BroadcastTo { get; set; } = new();
        public Func<ReferenceHub, bool> BroadcastFunc { get; set; }
        public VoiceChatChannel BroadcastChannel { get; set; } = VoiceChatChannel.Proximity;
        public static event Action<TrackFinishedEventArgs> OnFinishedTrack;

        /// <summary>
        /// 创建VoicePlayerBase
        /// </summary>
        /// <param name="hub">游戏对象</param>
        /// <returns><see cref="VoicePlayerBase"/></returns>
        public static VoicePlayerBase Create()
        {
            var player = new GameObject().AddComponent<VoicePlayerBase>();
            AudioPlayers.Add(player.Owner, player);
            return player;
        }
        /// <summary>
        /// 通过Hub获取VoicePlayerBase
        /// </summary>
        /// <param name="hub">玩家Hub</param>
        /// <returns><see cref="VoicePlayerBase"/></returns>
        public static VoicePlayerBase Get(ReferenceHub hub)
        {
            if (AudioPlayers.TryGetValue(hub, out VoicePlayerBase player))
            {
                return player;
            }

            player = hub.gameObject.AddComponent<VoicePlayerBase>();
            player.Owner = hub;

            AudioPlayers.Add(hub, player);
            return player;
        }
        public virtual void Play(int queuePos)
        {
            if (PlaybackCoroutine.IsRunning)
                Timing.KillCoroutines(PlaybackCoroutine);
            PlaybackCoroutine = Timing.RunCoroutine(Playback(queuePos), Segment.FixedUpdate);
        }
        public virtual void Stoptrack(bool clear)
        {
            if (clear)
                AudioToPlay.Clear();
            stopTrack = true;
        }
        public virtual void Enqueue(string audio, int pos)
        {
            if (pos == -1)
                AudioToPlay.Add(audio);
            else
                AudioToPlay.Insert(pos, audio);
        }
        public virtual IEnumerator<float> Playback(int position)
        {
            stopTrack = false;
            IsFinished = false;
            int index = position;
            if (index != -1)
            {
                if (Shuffle)
                    AudioToPlay = AudioToPlay.OrderBy(i => Random.value).ToList();
                CurrentPlay = AudioToPlay[index];
                AudioToPlay.RemoveAt(index);
                if (Loop)
                {
                    AudioToPlay.Add(CurrentPlay);
                }
            }
            if (LogInfo)
                Log.Info($"加载音频中...");
            if (File.Exists(CurrentPlay))
            {
                if (!CurrentPlay.EndsWith(".ogg"))
                {
                    Log.Error($"音频 {CurrentPlay} 必须为.ogg格式");
                    yield return Timing.WaitForSeconds(1);
                    if (AudioToPlay.Count >= 1)
                        Timing.RunCoroutine(Playback(0));
                    yield break;
                }
                CurrentPlayStream = new MemoryStream(File.ReadAllBytes(CurrentPlay));
            }
            else
            {
                Log.Error($"音频 {CurrentPlay} 不存在，已经跳过");
                yield return Timing.WaitForSeconds(1);
                if (AudioToPlay.Count >= 1)
                    Timing.RunCoroutine(Playback(0));
                yield break;
            }
            CurrentPlayStream.Seek(0, SeekOrigin.Begin);
            VorbisReader = new VorbisReader(CurrentPlayStream);

            if (VorbisReader.Channels >= 2)
            {
                Log.Error($"音频 {CurrentPlay} 必须为单轨道");
                yield return Timing.WaitForSeconds(1);
                if (AudioToPlay.Count >= 1)
                    Timing.RunCoroutine(Playback(0));
                VorbisReader.Dispose();
                CurrentPlayStream.Dispose();
                yield break;
            }

            if (VorbisReader.SampleRate != 48000)
            {
                Log.Error($"音频 {CurrentPlay} 采样率必须为48000");
                yield return Timing.WaitForSeconds(1);
                if (AudioToPlay.Count >= 1)
                    Timing.RunCoroutine(Playback(0));
                VorbisReader.Dispose();
                CurrentPlayStream.Dispose();
                yield break;
            }
            if (LogInfo)
                Log.Info($"播放 {CurrentPlay}");
            if (LogInfo)
                Log.Info($"音频总样本: {VorbisReader.TotalSamples}，时长: {VorbisReader.TotalTime.TotalSeconds} 秒");
            samplesPerSecond = VoiceChatSettings.SampleRate * VoiceChatSettings.Channels;
            SendBuffer = new float[samplesPerSecond / 5 + HeadSamples];
            ReadBuffer = new float[samplesPerSecond / 5 + HeadSamples];
            int cnt;
            while ((cnt = VorbisReader.ReadSamples(ReadBuffer, 0, ReadBuffer.Length)) > 0)
            {
                if (stopTrack)
                {
                    VorbisReader.SeekTo(VorbisReader.TotalSamples - 1);
                    stopTrack = false;
                }
                while (!ShouldPlay)
                {
                    yield return Timing.WaitForOneFrame;
                }
                while (StreamBuffer.Count >= ReadBuffer.Length)
                {
                    ready = true;
                    yield return Timing.WaitForOneFrame;
                }
                for (int i = 0; i < cnt; i++)
                {
                    StreamBuffer.Enqueue(ReadBuffer[i]);
                }
            }

            if (LogInfo)
                Log.Info($"播放完成");

            int nextQueuepos = 0;
            if (Continue && Loop && index == -1)
            {
                nextQueuepos = -1;
                Timing.RunCoroutine(Playback(nextQueuepos));
                OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, index == -1));
                yield break;
            }

            if (Continue && AudioToPlay.Count >= 1)
            {
                IsFinished = true;
                Timing.RunCoroutine(Playback(nextQueuepos));
                OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, index == -1));
                yield break;
            }

            IsFinished = true;
            OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, index == -1));

            if (ClearOnFinish)
                Destroy(this);
        }
        public virtual void OnDestroy()
        {
            if (PlaybackCoroutine.IsRunning)
                Timing.KillCoroutines(PlaybackCoroutine);

            AudioPlayers.Remove(Owner);

            if (ClearOnFinish)
                NetworkServer.RemovePlayerForConnection(Owner.connectionToClient, true);
        }
        public virtual void Update()
        {
            try
            {
                if (Owner == null || !ready || StreamBuffer.Count == 0 || !ShouldPlay) return;

                allowedSamples += Time.deltaTime * samplesPerSecond;
                int toCopy = Mathf.Min(Mathf.FloorToInt(allowedSamples), StreamBuffer.Count);
                if (LogDebug)
                    Log.Debug($"1 {toCopy} {allowedSamples} {samplesPerSecond} {StreamBuffer.Count} {PlaybackBuffer.Length} {PlaybackBuffer.WriteHead}");
                if (toCopy > 0)
                {
                    for (int i = 0; i < toCopy; i++)
                    {
                        PlaybackBuffer.Write(StreamBuffer.Dequeue() * (Volume / 100f));
                    }
                }

                if (LogDebug)
                    Log.Debug($"2 {toCopy} {allowedSamples} {samplesPerSecond} {StreamBuffer.Count} {PlaybackBuffer.Length} {PlaybackBuffer.WriteHead}");

                allowedSamples -= toCopy;
                //Log.Info($"allowedSamples{allowedSamples}");

                while (PlaybackBuffer.Length >= 480)
                {
                    PlaybackBuffer.ReadTo(SendBuffer, 480, 0L);
                    int dataLen = Encoder.Encode(SendBuffer, EncodedBuffer, 480);

                    foreach (var ply in ReferenceHub.AllHubs)
                    {
                        var conn = ply.connectionToClient;
                        if (conn == null || !conn.isReady || BroadcastTo.Count >= 1 && !BroadcastTo.Contains(ply)) continue;
                        //conn.Send(new VoiceMessage(Owner, VoiceChatChannel.Intercom, EncodedBuffer, dataLen, false));
                        //conn.Send(new VoiceMessage(Owner, VoiceChatChannel.PreGameLobby, EncodedBuffer, dataLen, false));
                        conn.Send(new VoiceMessage(Owner, VoiceChatChannel.RoundSummary, EncodedBuffer, dataLen, false));
                        //conn.Send(new VoiceMessage(Owner, VoiceChatChannel.Spectator, EncodedBuffer, dataLen, false));

                    }
                }
            }catch ( Exception ex)
            {
                Log.Info(ex);
            }
        }
        public virtual void Start()
        {
        }
        public virtual void Awake()
        {
        }
    }
}