using System;
using System.Collections.Generic;
using System.IO;
using LabApi.Features.Audio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer;

namespace NS_site27_heavy.heavy.Module.audio
{
    /// <summary>
    /// Loads an audio file from disk into the PCM format LabApi's speaker wants:
    /// mono, non-interleaved, <see cref="AudioTransmitter.SampleRate"/> Hz, samples in [-1, 1].
    ///
    /// <para>
    /// Everything here is pure managed, so it works on a Linux/Mono server. That is why MP3 goes
    /// through <see cref="MpegFile"/> (NLayer) rather than NAudio: NAudio's <c>Mp3FileReader</c>
    /// and <c>AudioFileReader</c> decode via the Windows ACM subsystem, and
    /// <c>MediaFoundationReader</c> via Media Foundation — both throw on Linux.
    /// <c>WaveFileReader</c> and <c>WdlResamplingSampleProvider</c> live in NAudio.Core and are
    /// managed, so they are fine.
    /// </para>
    /// </summary>
    public static class AudioFileLoader
    {
        public const int TargetSampleRate = AudioTransmitter.SampleRate;

        private static readonly Dictionary<string, float[]> Cache
            = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Sync = new object();

        /// <summary>
        /// Decodes <paramref name="path"/> into mono PCM at <see cref="TargetSampleRate"/>.
        /// Results are cached by path; the returned array is shared and must be treated as
        /// read-only (LabApi only reads from it).
        /// </summary>
        /// <exception cref="FileNotFoundException"/>
        /// <exception cref="InvalidDataException"/>
        public static float[] LoadMono(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Audio path is empty.", nameof(path));

            lock (Sync)
            {
                if (Cache.TryGetValue(path, out float[] cached))
                    return cached;
            }

            if (!File.Exists(path))
                throw new FileNotFoundException("Audio file not found.", path);

            float[] interleaved;
            int sampleRate;
            int channels;

            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".mp3")
                interleaved = DecodeMp3(path, out sampleRate, out channels);
            else
                interleaved = DecodeWav(path, out sampleRate, out channels);

            if (interleaved == null || interleaved.Length == 0)
                throw new InvalidDataException($"Decoded no samples from '{path}'.");

            if (channels < 1)
                channels = 1;

            float[] mono = channels == 1 ? interleaved : Downmix(interleaved, channels);
            float[] result = sampleRate == TargetSampleRate ? mono : Resample(mono, sampleRate);

            lock (Sync)
                Cache[path] = result;

            return result;
        }

        /// <summary>Same as <see cref="LoadMono"/> but returns false instead of throwing.</summary>
        public static bool TryLoadMono(string path, out float[] samples, out string error)
        {
            samples = null;
            error = null;

            try
            {
                samples = LoadMono(path);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Drops every decoded clip. Call on plugin disable.</summary>
        public static void Reset()
        {
            lock (Sync)
                Cache.Clear();
        }

        // ---------------------------------------------------------------- decoding

        /// <summary>WAV via NAudio. Handles 8/16/24/32-bit PCM and IEEE float transparently.</summary>
        private static float[] DecodeWav(string path, out int sampleRate, out int channels)
        {
            using (WaveFileReader reader = new WaveFileReader(path))
            {
                ISampleProvider provider = reader.ToSampleProvider();
                sampleRate = provider.WaveFormat.SampleRate;
                channels = provider.WaveFormat.Channels;

                // Length is in bytes; 4 bytes per float sample after conversion.
                int estimate = (int)Math.Min(reader.Length / 2 + 1024, int.MaxValue);
                return ReadAll(provider, estimate);
            }
        }

        /// <summary>MP3 via NLayer. Managed decoder, no ACM / Media Foundation dependency.</summary>
        private static float[] DecodeMp3(string path, out int sampleRate, out int channels)
        {
            using (MpegFile mp3 = new MpegFile(path))
            {
                sampleRate = mp3.SampleRate;
                channels = mp3.Channels;

                // StereoMode.DownmixToMono exists, but Channels keeps reporting the file's real
                // channel count, so downmixing here would double-apply. Read interleaved instead
                // and let Downmix handle it.
                float[] buffer = new float[16384];
                float[] result = new float[Math.Max(16384, (int)Math.Min(mp3.Length, 1 << 24))];
                int total = 0;

                while (true)
                {
                    int read = mp3.ReadSamples(buffer, 0, buffer.Length);
                    if (read <= 0)
                        break;

                    EnsureCapacity(ref result, total + read);
                    Array.Copy(buffer, 0, result, total, read);
                    total += read;
                }

                Array.Resize(ref result, total);
                return result;
            }
        }

        private static float[] ReadAll(ISampleProvider provider, int initialCapacity)
        {
            float[] result = new float[Math.Max(16384, initialCapacity)];
            float[] buffer = new float[16384];
            int total = 0;

            while (true)
            {
                int read = provider.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;

                EnsureCapacity(ref result, total + read);
                Array.Copy(buffer, 0, result, total, read);
                total += read;
            }

            Array.Resize(ref result, total);
            return result;
        }

        private static void EnsureCapacity(ref float[] array, int required)
        {
            if (array.Length >= required)
                return;

            int size = array.Length;
            while (size < required)
                size *= 2;

            Array.Resize(ref array, size);
        }

        // ---------------------------------------------------------------- shaping

        private static float[] Downmix(float[] interleaved, int channels)
        {
            int frames = interleaved.Length / channels;
            float[] mono = new float[frames];
            float scale = 1f / channels;

            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                int b = f * channels;
                for (int c = 0; c < channels; c++)
                    sum += interleaved[b + c];

                mono[f] = sum * scale;
            }

            return mono;
        }

        /// <summary>
        /// Resamples with NAudio's WDL resampler — managed, and much better quality than the
        /// linear interpolation you would otherwise hand-roll.
        /// </summary>
        private static float[] Resample(float[] mono, int sourceRate)
        {
            ISampleProvider source = new FloatArraySampleProvider(mono, sourceRate, 1);
            ISampleProvider resampler = new WdlResamplingSampleProvider(source, TargetSampleRate);

            int estimate = (int)((long)mono.Length * TargetSampleRate / Math.Max(1, sourceRate)) + 1024;
            return ReadAll(resampler, estimate);
        }

        /// <summary>Feeds an existing float buffer into the NAudio pipeline.</summary>
        private sealed class FloatArraySampleProvider : ISampleProvider
        {
            private readonly float[] _data;
            private int _position;

            public FloatArraySampleProvider(float[] data, int sampleRate, int channels)
            {
                _data = data;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                int available = _data.Length - _position;
                if (available <= 0)
                    return 0;

                int n = Math.Min(count, available);
                Array.Copy(_data, _position, buffer, offset, n);
                _position += n;
                return n;
            }
        }
    }
}
