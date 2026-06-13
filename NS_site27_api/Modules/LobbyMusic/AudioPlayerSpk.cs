using Exiled.API.Features;
using LabApi.Features.Audio;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VoiceChat;
using VoiceChat.Codec;
using VoiceChat.Codec.Enums;
using VoiceChat.Networking;
using Random = UnityEngine.Random;

namespace NS_site27_api.Modules.LobbyMusic
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
    public class VoicePlayerBase
    {
        /// <summary>
        /// AudioPlayers列表
        /// </summary>
        public static Dictionary<SpeakerToy, VoicePlayerBase> AudioPlayers { get; set; } = new();
        public int HeadSamples { get; set; } = 1920;
        public CoroutineHandle PlaybackCoroutine;
        public float allowedSamples;
        public int samplesPerSecond;
        public ISampleProvider SampleProvider { get; set; }
        public float[] ReadBuffer { get; set; }
        private long _playedSamples;
        public List<string> AudioToPlay { get; set; } = new();
        public string CurrentPlay { get; set; }
        public bool LogDebug { get; set; } = false;
        public bool LogInfo { get; set; } = true;
        public bool IsFinished { get; set; } = false;
        public List<ReferenceHub> BroadcastTo { get; set; } = new();
        public double seconds => _playedSamples / 48000.0;
        public Func<ReferenceHub, bool> BroadcastFunc { get; set; }
        public VoiceChatChannel BroadcastChannel { get; set; } = VoiceChatChannel.Proximity;
        public static event Action<TrackFinishedEventArgs> OnFinishedTrack;
        SpeakerToy speaker;

        public static VoicePlayerBase Get(SpeakerToy hub)
        {
            if (AudioPlayers.TryGetValue(hub, out VoicePlayerBase player))
            {
                return player;
            }

            player = new();
            player.speaker = hub;

            AudioPlayers.Add(hub, player);
            return player;
        }
        public virtual void Play(ISampleProvider prv)
        {
            if (PlaybackCoroutine.IsRunning)
                Timing.KillCoroutines(PlaybackCoroutine);
            PlaybackCoroutine = Timing.RunCoroutine(Playback(prv), Segment.FixedUpdate);
        }
        public virtual void Stoptrack(bool clear)
        {
            if (clear)
                AudioToPlay.Clear();
        }
        public virtual void Enqueue(string audio, int pos)
        {
            if (pos == -1)
                AudioToPlay.Add(audio);
            else
                AudioToPlay.Insert(pos, audio);
        }
        bool IsPlaybackComplete(AudioTransmitter transmitter)
        {
            // 协程没有运行（非暂停）且队列为空，并且当前没有正在播放的片段
            return !transmitter.IsPlaying && !transmitter.IsPaused
                //&& transmitter.AudioClipSamples.Count == 0
                && transmitter.CurrentSampleCount == 0;
        }
        public virtual IEnumerator<float> Playback(ISampleProvider provider)
        {
            Log.Info("Playback starting...");
            IsFinished = false;
            _playedSamples = 0;

            // 1. 读取所有样本
            var sampleList = new List<float>();
            float[] buffer = new float[VoiceChatSettings.SampleRate / 4]; // 每次读取 1/4 秒的样本
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                sampleList.AddRange(buffer.Take(read));
            }

            if (sampleList.Count == 0)
            {
                Log.Warn("Audio provider returned 0 samples.");
                IsFinished = true;
                OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, false));
                yield break;
            }

            float[] allSamples = sampleList.ToArray();
            _playedSamples = allSamples.Length;

            // 2. 开始播放（非排队，立即替换）
            this.speaker.Play(allSamples, queue: false, loop: false);

            // 3. 等待播放完成
            while (speaker.Transmitter.IsPlaying || speaker.Transmitter.IsPaused)
                yield return Timing.WaitForSeconds(0.2f);

            // 4. 播放自然结束（未被 Stop 打断）
            if (!speaker.Transmitter.IsPaused)
            {
                Log.Info("Playback finished naturally.");
                IsFinished = true;
                OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, true));
            }
            else
            {
                Log.Warn("Playback ended while paused (likely stopped externally).");
                IsFinished = true;
                OnFinishedTrack?.Invoke(new TrackFinishedEventArgs(this, CurrentPlay, false));
            }
        }
    }
}