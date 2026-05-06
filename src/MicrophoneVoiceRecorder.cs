using System;
using System.IO;
using UnityEngine;

namespace HAL9000
{
    internal sealed class MicrophoneVoiceRecorder
    {
        private readonly int sampleRate;
        private readonly int maxSeconds;
        private AudioClip clip;
        private string deviceName;
        private float startedAt;

        public bool IsRecording
        {
            get { return clip != null && Microphone.IsRecording(deviceName); }
        }

        public MicrophoneVoiceRecorder(int sampleRate, int maxSeconds)
        {
            this.sampleRate = sampleRate;
            this.maxSeconds = maxSeconds;
        }

        public bool HasMicrophone
        {
            get { return Microphone.devices != null && Microphone.devices.Length > 0; }
        }

        public string InputDescription
        {
            get
            {
                if (!HasMicrophone)
                {
                    return "No Unity microphone devices found";
                }

                return string.IsNullOrEmpty(deviceName) ? "Unity default microphone" : deviceName;
            }
        }

        public bool Begin(out string error)
        {
            error = null;
            if (!HasMicrophone)
            {
                error = "No microphone devices found.";
                return false;
            }

            try
            {
                deviceName = null;
                clip = Microphone.Start(deviceName, false, maxSeconds, sampleRate);
                startedAt = Time.realtimeSinceStartup;
                if (clip == null)
                {
                    error = "Unity microphone recording did not start.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Microphone start failed: " + ex.Message;
                clip = null;
                return false;
            }
        }

        public bool End(out byte[] wavBytes, out float durationSeconds, out string error)
        {
            wavBytes = null;
            durationSeconds = 0f;
            error = null;

            if (clip == null)
            {
                error = "No active microphone recording.";
                return false;
            }

            int frames = Microphone.GetPosition(deviceName);
            try
            {
                if (Microphone.IsRecording(deviceName))
                {
                    Microphone.End(deviceName);
                }
            }
            catch (Exception ex)
            {
                error = "Microphone stop failed: " + ex.Message;
                clip = null;
                return false;
            }

            AudioClip recordedClip = clip;
            clip = null;

            if (frames <= 0)
            {
                error = "No microphone samples captured.";
                return false;
            }

            frames = Math.Min(frames, recordedClip.samples);
            int channels = Math.Max(1, recordedClip.channels);
            float[] samples = new float[frames * channels];
            if (!recordedClip.GetData(samples, 0))
            {
                error = "Could not read microphone samples.";
                return false;
            }

            durationSeconds = frames / (float)recordedClip.frequency;
            wavBytes = WavEncode(samples, channels, recordedClip.frequency);
            return wavBytes != null && wavBytes.Length > 44;
        }

        public float CurrentDurationSeconds()
        {
            if (!IsRecording)
            {
                return 0f;
            }

            return Time.realtimeSinceStartup - startedAt;
        }

        private static byte[] WavEncode(float[] samples, int channels, int frequency)
        {
            const short bitsPerSample = 16;
            const short bytesPerSample = bitsPerSample / 8;
            int dataLength = samples.Length * bytesPerSample;

            using (MemoryStream stream = new MemoryStream(44 + dataLength))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * bytesPerSample);
                writer.Write((short)(channels * bytesPerSample));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);

                for (int i = 0; i < samples.Length; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    writer.Write((short)(sample * short.MaxValue));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
