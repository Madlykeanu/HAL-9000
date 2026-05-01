using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HAL9000
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class HAL9000Addon : MonoBehaviour
    {
        private const int WindowId = 900001;
        private readonly List<ChatLine> transcript = new List<ChatLine>();
        private readonly string[] debugToolNames =
        {
            "get_ship_info",
            "get_celestial_info",
            "list_celestial_bodies",
            "list_vessel_parts",
            "get_part_info",
            "get_orbit_info",
            "get_target_info",
            "get_maneuver_nodes",
            "get_engine_status",
            "estimate_burn",
            "get_reference_frames",
            "simulate_maneuver"
        };

        private HAL9000Config config;
        private OpenRouterClient client;
        private Rect windowRect = new Rect(180f, 90f, 560f, 520f);
        private Vector2 scroll;
        private Vector2 debugOutputScroll;
        private Vector2 debugArgsScroll;
        private string input = string.Empty;
        private string status = "Idle";
        private string debugArguments = "{}";
        private string debugOutput = string.Empty;
        private string selectedDebugTool = "get_ship_info";
        private bool visible = true;
        private bool pending;
        private int selectedTab;

        public void Awake()
        {
            config = HAL9000Config.Load();
            client = new OpenRouterClient(config);
            transcript.Add(new ChatLine("assistant", "HAL-9000 flight terminal online. Ask me about the active vessel."));

            if (!config.HasApiKey)
            {
                transcript.Add(new ChatLine("assistant", "OpenRouter API key is not configured. Edit GameData/HAL-9000/PluginData/settings.cfg before sending messages."));
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                visible = !visible;
            }
        }

        public void OnGUI()
        {
            GUI.depth = 0;

            if (GUI.Button(new Rect(8f, 118f, 86f, 28f), visible ? "HAL Hide" : "HAL"))
            {
                visible = !visible;
            }

            if (visible)
            {
                windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "HAL-9000");
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Model: " + config.Model);
            selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Chat", "Tools" });

            if (selectedTab == 0)
            {
                DrawChatPanel();
            }
            else
            {
                DrawDebugPanel();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawChatPanel()
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(350f));
            for (int i = 0; i < transcript.Count; i++)
            {
                ChatLine line = transcript[i];
                string speaker = line.Role == "user" ? "You" : "HAL";
                GUILayout.Label(speaker + ": " + line.Content);
                GUILayout.Space(6f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.Label("Status: " + status);

            GUI.SetNextControlName("HALInput");
            input = GUILayout.TextField(input, GUILayout.MinHeight(26f));

            GUILayout.BeginHorizontal();
            GUI.enabled = !pending && config.HasApiKey && !string.IsNullOrEmpty(input.Trim());
            if (GUILayout.Button("Send", GUILayout.Width(80f)))
            {
                SendInput();
            }
            GUI.enabled = !pending;
            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
            {
                transcript.Clear();
                transcript.Add(new ChatLine("assistant", "Conversation cleared."));
                status = "Idle";
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80f)))
            {
                visible = false;
            }
            GUILayout.EndHorizontal();

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "HALInput")
            {
                if (!pending && config.HasApiKey && !string.IsNullOrEmpty(input.Trim()))
                {
                    SendInput();
                    evt.Use();
                }
            }

        }

        private void DrawDebugPanel()
        {
            GUILayout.Label("Manual Tool Call");

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(170f));
            for (int i = 0; i < debugToolNames.Length; i++)
            {
                bool selected = debugToolNames[i] == selectedDebugTool;
                if (GUILayout.Toggle(selected, debugToolNames[i], "Button"))
                {
                    if (!selected)
                    {
                        SelectDebugTool(debugToolNames[i]);
                    }
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("Arguments JSON");
            debugArgsScroll = GUILayout.BeginScrollView(debugArgsScroll, GUILayout.Height(96f));
            debugArguments = GUILayout.TextArea(debugArguments, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run", GUILayout.Width(80f)))
            {
                RunDebugTool();
            }
            if (GUILayout.Button("Reset Args", GUILayout.Width(100f)))
            {
                debugArguments = DefaultDebugArguments(selectedDebugTool);
            }
            if (GUILayout.Button("Clear Output", GUILayout.Width(110f)))
            {
                debugOutput = string.Empty;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("Raw Output");
            debugOutputScroll = GUILayout.BeginScrollView(debugOutputScroll, GUILayout.Height(245f));
            GUILayout.TextArea(debugOutput, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80f)))
            {
                visible = false;
            }
            GUILayout.EndHorizontal();
        }

        private void SendInput()
        {
            string message = input.Trim();
            if (message.Length == 0)
            {
                return;
            }

            input = string.Empty;
            transcript.Add(new ChatLine("user", message));
            scroll.y = float.MaxValue;
            StartCoroutine(SendToAi());
        }

        private IEnumerator SendToAi()
        {
            pending = true;
            status = "Contacting OpenRouter...";

            string answer = null;
            string error = null;

            yield return StartCoroutine(client.Send(transcript, ExecuteTool, value => answer = value, value => error = value));

            if (!string.IsNullOrEmpty(error))
            {
                transcript.Add(new ChatLine("assistant", "OpenRouter request failed: " + error));
                status = "Error";
            }
            else
            {
                transcript.Add(new ChatLine("assistant", string.IsNullOrEmpty(answer) ? "No response text returned." : answer));
                status = "Idle";
            }

            pending = false;
            scroll.y = float.MaxValue;
        }

        private string ExecuteTool(ToolCallRequest request)
        {
            try
            {
                if (request.Name == "get_ship_info")
                {
                    return ShipInfoTool.GetShipInfoJson();
                }

                if (request.Name == "get_celestial_info")
                {
                    return CelestialInfoTool.GetCelestialInfoJson(request.Arguments);
                }

                if (request.Name == "list_celestial_bodies")
                {
                    return CelestialInfoTool.ListCelestialBodiesJson(request.Arguments);
                }

                if (request.Name == "list_vessel_parts")
                {
                    return PartInfoTool.ListVesselPartsJson(request.Arguments);
                }

                if (request.Name == "get_part_info")
                {
                    return PartInfoTool.GetPartInfoJson(request.Arguments);
                }

                if (request.Name == "get_orbit_info")
                {
                    return NavigationInfoTool.GetOrbitInfoJson();
                }

                if (request.Name == "get_target_info")
                {
                    return NavigationInfoTool.GetTargetInfoJson();
                }

                if (request.Name == "get_maneuver_nodes")
                {
                    return NavigationInfoTool.GetManeuverNodesJson();
                }

                if (request.Name == "get_engine_status")
                {
                    return NavigationInfoTool.GetEngineStatusJson();
                }

                if (request.Name == "estimate_burn")
                {
                    return NavigationInfoTool.EstimateBurnJson(request.Arguments);
                }

                if (request.Name == "get_reference_frames")
                {
                    return NavigationInfoTool.GetReferenceFramesJson(request.Arguments);
                }

                if (request.Name == "simulate_maneuver")
                {
                    return NavigationInfoTool.SimulateManeuverJson(request.Arguments);
                }

                return "{\"error\":\"Unknown tool: " + JsonUtil.Escape(request.Name) + "\"}";
            }
            catch (Exception ex)
            {
                Debug.LogError("[HAL-9000] Tool failure: " + ex);
                return "{\"error\":\"Tool failed: " + JsonUtil.Escape(ex.Message) + "\"}";
            }
        }

        private void SelectDebugTool(string toolName)
        {
            selectedDebugTool = toolName;
            debugArguments = DefaultDebugArguments(toolName);
            debugOutput = string.Empty;
            debugArgsScroll = Vector2.zero;
            debugOutputScroll = Vector2.zero;
        }

        private void RunDebugTool()
        {
            Dictionary<string, object> arguments = ParseDebugArguments();
            if (arguments == null)
            {
                return;
            }

            ToolCallRequest request = new ToolCallRequest(selectedDebugTool, arguments);
            string rawOutput = ExecuteTool(request);
            debugOutput = JsonUtil.PrettyPrint(rawOutput);
            debugOutputScroll = Vector2.zero;
        }

        private Dictionary<string, object> ParseDebugArguments()
        {
            if (string.IsNullOrEmpty(debugArguments.Trim()))
            {
                return new Dictionary<string, object>();
            }

            Dictionary<string, object> arguments = JsonUtil.Deserialize(debugArguments) as Dictionary<string, object>;
            if (arguments != null)
            {
                return arguments;
            }

            debugOutput = "{\"error\":\"Arguments must be a JSON object.\"}";
            return null;
        }

        private static string DefaultDebugArguments(string toolName)
        {
            if (toolName == "get_celestial_info")
            {
                return "{\"body_name\":\"Mun\"}";
            }

            if (toolName == "list_celestial_bodies")
            {
                return "{\"include_details\":false}";
            }

            if (toolName == "list_vessel_parts")
            {
                return "{\"include_modules\":true,\"include_resources\":true,\"max_parts\":250}";
            }

            if (toolName == "get_part_info")
            {
                return "{\"part_index\":0}";
            }

            if (toolName == "estimate_burn")
            {
                return "{\"delta_v_mps\":100}";
            }

            if (toolName == "get_reference_frames")
            {
                return "{}";
            }

            if (toolName == "simulate_maneuver")
            {
                return "{\"prograde_mps\":100,\"normal_mps\":0,\"radial_mps\":0}";
            }

            return "{}";
        }
    }
}
