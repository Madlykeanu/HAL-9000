using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace HAL9000
{
    internal sealed class OpenRouterTextToSpeechClient
    {
        private readonly HAL9000Config config;

        public OpenRouterTextToSpeechClient(HAL9000Config config)
        {
            this.config = config;
        }

        public IEnumerator CreateSpeech(string text, Action<byte[]> onComplete, Action<string> onError)
        {
            if (string.IsNullOrEmpty(text))
            {
                onError("No text to synthesize.");
                yield break;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = config.TextToSpeechModel;
            payload["input"] = text;
            payload["voice"] = config.TextToSpeechVoice;
            payload["response_format"] = config.TextToSpeechResponseFormat;
            payload["speed"] = config.TextToSpeechSpeed;

            byte[] body = Encoding.UTF8.GetBytes(JsonUtil.Serialize(payload));
            UnityWebRequest request = new UnityWebRequest(config.TextToSpeechBaseUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = config.TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
            request.SetRequestHeader("HTTP-Referer", config.HttpReferer);
            request.SetRequestHeader("X-Title", config.AppTitle);

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                string responseText = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
                onError(request.error + " " + responseText);
                yield break;
            }

            byte[] audioBytes = request.downloadHandler == null ? null : request.downloadHandler.data;
            if (audioBytes == null || audioBytes.Length == 0)
            {
                onError("OpenRouter TTS returned empty audio.");
                yield break;
            }

            onComplete(audioBytes);
        }
    }
}
