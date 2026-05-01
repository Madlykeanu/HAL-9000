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
        private const int MaxToolRounds = 2;
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

        private static List<object> BuildMessages(List<ChatLine> transcript)
        {
            List<object> messages = new List<object>();
            messages.Add(Message("system", "You are HAL-9000, a concise onboard spacecraft computer in Kerbal Space Program. Use get_ship_info for basic active-vessel state. Use get_orbit_info for detailed orbit, surface, patch, and reference-frame data. Use get_target_info for target distance, relative velocity, target orbit, closest approach, and plane relationship. Use get_maneuver_nodes for detailed existing node data. Use get_engine_status and estimate_burn for thrust, engine, burn-duration, and delta-v feasibility questions. Use list_vessel_parts when the player asks what kind of craft they are flying, what the vessel is made of, or whether it has engines, tanks, wings, command modules, science parts, docking ports, landing gear, or other capabilities. Use get_part_info to inspect a specific part from list_vessel_parts in detail. Use list_celestial_bodies when the player asks what planets, moons, stars, or systems exist, or when a named body may come from a planet pack and you need to discover loaded bodies. Use get_celestial_info whenever the player asks about a specific celestial body, distance to a body, or facts about a body such as atmosphere, gravity, orbit, or sphere of influence. Use simulate_maneuver only as a read-only rough two-body estimate, not as a final planner. When a question needs multiple kinds of live data, call all needed tools before answering. Do not invent live game data. You are read-only in version 0.1 and must not claim to create nodes, control throttle, steer, warp, dock, land, or execute burns."));

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
