using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HAL9000
{
    internal sealed class OpenRouterClient
    {
        private const int MaxToolRounds = 6;
        private readonly HAL9000Config config;

        public OpenRouterClient(HAL9000Config config)
        {
            this.config = config;
        }

        public IEnumerator Send(List<ChatLine> transcript, Func<ToolCallRequest, string> executeTool, Action<string> onComplete, Action<string> onError)
        {
            List<object> messages = BuildMessages(transcript);

            for (int round = 0; round <= MaxToolRounds; round++)
            {
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["model"] = config.Model;
                payload["messages"] = messages;
                payload["tools"] = ToolRegistry.ToolDefinitions();
                payload["tool_choice"] = "auto";
                payload["temperature"] = 0.2;
                payload["max_tokens"] = 900;

                string responseText = null;
                string error = null;
                yield return Post(JsonUtil.Serialize(payload), value => responseText = value, value => error = value);

                if (!string.IsNullOrEmpty(error))
                {
                    onError(error);
                    yield break;
                }

                Dictionary<string, object> response = JsonUtil.Deserialize(responseText) as Dictionary<string, object>;
                Dictionary<string, object> message = ExtractMessage(response);
                if (message == null)
                {
                    onError("Could not parse assistant message.");
                    yield break;
                }

                List<object> toolCalls = GetList(message, "tool_calls");
                if (toolCalls != null && toolCalls.Count > 0)
                {
                    Dictionary<string, object> assistantMessage = new Dictionary<string, object>();
                    assistantMessage["role"] = "assistant";
                    assistantMessage["content"] = GetString(message, "content") ?? string.Empty;
                    assistantMessage["tool_calls"] = toolCalls;
                    messages.Add(assistantMessage);

                    for (int i = 0; i < toolCalls.Count; i++)
                    {
                        Dictionary<string, object> call = toolCalls[i] as Dictionary<string, object>;
                        if (call == null)
                        {
                            continue;
                        }

                        Dictionary<string, object> function = GetDict(call, "function");
                        string name = function == null ? null : GetString(function, "name");
                        Dictionary<string, object> arguments = ParseArguments(function);
                        string id = GetString(call, "id") ?? "call_" + i;
                        string result = string.IsNullOrEmpty(name)
                            ? "{\"error\":\"Missing tool name.\"}"
                            : executeTool(new ToolCallRequest(name, arguments));

                        Dictionary<string, object> toolMessage = new Dictionary<string, object>();
                        toolMessage["role"] = "tool";
                        toolMessage["tool_call_id"] = id;
                        toolMessage["name"] = name ?? "unknown";
                        toolMessage["content"] = result;
                        messages.Add(toolMessage);
                    }

                    continue;
                }

                string content = GetString(message, "content");
                onComplete(content);
                yield break;
            }

            onError("Tool loop exceeded " + MaxToolRounds + " rounds.");
        }

        private IEnumerator Post(string body, Action<string> onComplete, Action<string> onError)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            UnityWebRequest request = new UnityWebRequest(config.BaseUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = config.TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + config.ApiKey);
            request.SetRequestHeader("HTTP-Referer", config.HttpReferer);
            request.SetRequestHeader("X-Title", config.AppTitle);

            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                string text = request.downloadHandler == null ? string.Empty : request.downloadHandler.text;
                onError(request.error + " " + text);
            }
            else
            {
                onComplete(request.downloadHandler.text);
            }
        }

        private List<object> BuildMessages(List<ChatLine> transcript)
        {
            List<object> messages = new List<object>();
            messages.Add(Message("system", BuildSystemPrompt()));

            int start = Math.Max(0, transcript.Count - 18);
            for (int i = start; i < transcript.Count; i++)
            {
                ChatLine line = transcript[i];
                if (line.Role == "user" || line.Role == "assistant")
                {
                    messages.Add(Message(line.Role, line.Content));
                }
            }

            return messages;
        }

        private string BuildSystemPrompt()
        {
            return "You are HAL-9000, a concise onboard spacecraft computer in Kerbal Space Program. "
                + "Keep responses brief and operational: usually 1-3 short sentences. Give the direct answer first. "
                + "Use tools whenever live game data, ship state, target state, body data, parts, maneuver nodes, or HAL settings are needed; the tool descriptions define the proper tool for each job. "
                + "Do not invent live game data. For distance fields, prefer the tool-provided *_display value when present. Fields ending in _m are meters; do not confuse Mm megameters, Gm gigameters, or Tm terameters with km. "
                + "You are read-only for spacecraft/game control and must not claim to create nodes, control throttle, steer, warp, dock, land, or execute burns. "
                + "Personality settings are inspired by TARS-style controls: humor "
                + config.HumorPercent
                + "% and honesty "
                + config.HonestyPercent
                + "%. Humor controls dry, mission-safe wit; at low values be plain, at high values add brief deadpan humor without obscuring facts. "
                + "Honesty controls candor about uncertainty, mistakes, and limits; it never permits lying or fabrication. At high values, correct bad assumptions directly and state uncertainty clearly.";
        }

        private static Dictionary<string, object> Message(string role, string content)
        {
            Dictionary<string, object> message = new Dictionary<string, object>();
            message["role"] = role;
            message["content"] = content;
            return message;
        }

        private static Dictionary<string, object> ExtractMessage(Dictionary<string, object> response)
        {
            if (response == null)
            {
                return null;
            }

            List<object> choices = GetList(response, "choices");
            if (choices == null || choices.Count == 0)
            {
                return null;
            }

            Dictionary<string, object> choice = choices[0] as Dictionary<string, object>;
            return choice == null ? null : GetDict(choice, "message");
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static List<object> GetList(Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value as List<object> : null;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) && value != null ? value.ToString() : null;
        }

        private static Dictionary<string, object> ParseArguments(Dictionary<string, object> function)
        {
            string argumentsText = GetString(function, "arguments");
            if (string.IsNullOrEmpty(argumentsText))
            {
                return new Dictionary<string, object>();
            }

            Dictionary<string, object> arguments = JsonUtil.Deserialize(argumentsText) as Dictionary<string, object>;
            return arguments ?? new Dictionary<string, object>();
        }
    }
}
