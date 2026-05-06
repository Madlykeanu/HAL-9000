using System;
using System.Collections.Generic;
using System.IO;

namespace HAL9000
{
    internal sealed class HAL9000Config
    {
        public string ApiKey = string.Empty;
        public string Model = "openai/gpt-4o-mini";
        public string BaseUrl = "https://openrouter.ai/api/v1/chat/completions";
        public string SpeechToTextModel = "google/chirp-3";
        public string SpeechToTextBaseUrl = "https://openrouter.ai/api/v1/audio/transcriptions";
        public string TextToSpeechMode = "windows";
        public string TextToSpeechModel = "google/gemini-3.1-flash-tts-preview";
        public string TextToSpeechBaseUrl = "https://openrouter.ai/api/v1/audio/speech";
        public string TextToSpeechVoice = "Iapetus";
        public string TextToSpeechResponseFormat = "pcm";
        public float TextToSpeechSpeed = 1f;
        public float TextToSpeechVolume = 1.8f;
        public int TextToSpeechPcmSampleRate = 24000;
        public string HttpReferer = "https://github.com/local-ksp-hal-9000";
        public string AppTitle = "KSP HAL-9000";
        public int TimeoutSeconds = 45;
        public int VoiceSampleRate = 16000;
        public int VoiceMaxSeconds = 20;
        public int HumorPercent = 15;
        public int HonestyPercent = 90;

        public bool HasApiKey
        {
            get { return !string.IsNullOrEmpty(ApiKey); }
        }

        public static HAL9000Config Load()
        {
            HAL9000Config config = new HAL9000Config();
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "HAL-9000", "PluginData", "settings.cfg");
            if (!File.Exists(path))
            {
                return config;
            }

            Dictionary<string, string> values = ReadValues(path);
            config.ApiKey = Get(values, "OPENROUTER_API_KEY", config.ApiKey);
            config.Model = Get(values, "OPENROUTER_MODEL", config.Model);
            config.BaseUrl = Get(values, "OPENROUTER_BASE_URL", config.BaseUrl);
            config.SpeechToTextModel = Get(values, "OPENROUTER_STT_MODEL", config.SpeechToTextModel);
            config.SpeechToTextBaseUrl = Get(values, "OPENROUTER_STT_BASE_URL", config.SpeechToTextBaseUrl);
            config.TextToSpeechMode = Get(values, "VOICE_TTS_MODE", config.TextToSpeechMode);
            config.TextToSpeechModel = Get(values, "OPENROUTER_TTS_MODEL", config.TextToSpeechModel);
            config.TextToSpeechBaseUrl = Get(values, "OPENROUTER_TTS_BASE_URL", config.TextToSpeechBaseUrl);
            config.TextToSpeechVoice = Get(values, "OPENROUTER_TTS_VOICE", config.TextToSpeechVoice);
            config.TextToSpeechResponseFormat = Get(values, "OPENROUTER_TTS_FORMAT", config.TextToSpeechResponseFormat);
            config.HttpReferer = Get(values, "OPENROUTER_HTTP_REFERER", config.HttpReferer);
            config.AppTitle = Get(values, "OPENROUTER_APP_TITLE", config.AppTitle);

            int humorPercent;
            if (int.TryParse(Get(values, "HAL_HUMOR_PERCENT", config.HumorPercent.ToString()), out humorPercent))
            {
                config.HumorPercent = ClampPercent(humorPercent);
            }

            int honestyPercent;
            if (int.TryParse(Get(values, "HAL_HONESTY_PERCENT", config.HonestyPercent.ToString()), out honestyPercent))
            {
                config.HonestyPercent = ClampPercent(honestyPercent);
            }

            int timeout;
            if (int.TryParse(Get(values, "REQUEST_TIMEOUT_SECONDS", config.TimeoutSeconds.ToString()), out timeout))
            {
                config.TimeoutSeconds = Math.Max(5, timeout);
            }

            int voiceSampleRate;
            if (int.TryParse(Get(values, "VOICE_SAMPLE_RATE", config.VoiceSampleRate.ToString()), out voiceSampleRate))
            {
                config.VoiceSampleRate = Math.Max(8000, voiceSampleRate);
            }

            int voiceMaxSeconds;
            if (int.TryParse(Get(values, "VOICE_MAX_SECONDS", config.VoiceMaxSeconds.ToString()), out voiceMaxSeconds))
            {
                config.VoiceMaxSeconds = Math.Max(2, voiceMaxSeconds);
            }

            int ttsPcmSampleRate;
            if (int.TryParse(Get(values, "OPENROUTER_TTS_PCM_SAMPLE_RATE", config.TextToSpeechPcmSampleRate.ToString()), out ttsPcmSampleRate))
            {
                config.TextToSpeechPcmSampleRate = Math.Max(8000, ttsPcmSampleRate);
            }

            float ttsSpeed;
            if (float.TryParse(Get(values, "OPENROUTER_TTS_SPEED", config.TextToSpeechSpeed.ToString()), out ttsSpeed))
            {
                config.TextToSpeechSpeed = Math.Max(0.25f, Math.Min(4f, ttsSpeed));
            }

            float ttsVolume;
            if (float.TryParse(Get(values, "OPENROUTER_TTS_VOLUME", config.TextToSpeechVolume.ToString()), out ttsVolume))
            {
                config.TextToSpeechVolume = Math.Max(0f, Math.Min(3f, ttsVolume));
            }

            return config;
        }

        private static Dictionary<string, string> ReadValues(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//") || line == "{" || line == "}")
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals < 0)
                {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                values[key] = value;
            }

            return values;
        }

        private static string Get(Dictionary<string, string> values, string key, string fallback)
        {
            string value;
            if (!values.TryGetValue(key, out value) || string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            return value;
        }

        public static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
