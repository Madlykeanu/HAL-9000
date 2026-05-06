using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace HAL9000
{
    internal sealed class OpenRouterSpeechToTextClient
    {
        private readonly HAL9000Config config;

        public OpenRouterSpeechToTextClient(HAL9000Config config)
        {
            this.config = config;
        }

        public IEnumerator Transcribe(byte[] wavBytes, Action<string> onComplete, Action<string> onError)
        {
            if (wavBytes == null || wavBytes.Length == 0)
            {
                onError("No audio bytes to transcribe.");
                yield break;
            }

            Dictionary<string, object> inputAudio = new Dictionary<string, object>();
            inputAudio["data"] = Convert.ToBase64String(wavBytes);
            inputAudio["format"] = "wav";

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = config.SpeechToTextModel;
            payload["input_audio"] = inputAudio;
            payload["language"] = "en";
            payload["temperature"] = 0;

            byte[] body = Encoding.UTF8.GetBytes(JsonUtil.Serialize(payload));
            UnityWebRequest request = new UnityWebRequest(config.SpeechToTextBaseUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = config.TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
            request.SetRequestHeader("HTTP-Referer", config.HttpReferer);
            request.SetRequestHeader("X-Title", config.AppTitle);

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
            if (request.isNetworkError || request.isHttpError)
            {
                onError(request.error + " " + responseText);
                yield break;
            }

            Dictionary<string, object> response = JsonUtil.Deserialize(responseText) as Dictionary<string, object>;
            string text = GetString(response, "text");
            if (string.IsNullOrEmpty(text))
            {
                onError("OpenRouter STT returned no text. Raw response: " + responseText);
                yield break;
            }

            onComplete(text.Trim());
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }
    }
}
