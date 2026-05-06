using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HAL9000
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class HAL9000Addon : MonoBehaviour
    {
        private const int WindowId = 900001;
        private static readonly string[] GeminiVoices =
        {
            "Achernar", "Achird", "Algenib", "Algieba", "Alnilam", "Aoede", "Autonoe", "Callirrhoe", "Charon", "Despina",
            "Enceladus", "Erinome", "Fenrir", "Gacrux", "Iapetus", "Kore", "Laomedeia", "Leda", "Orus", "Puck",
            "Pulcherrima", "Rasalgethi", "Sadachbia", "Sadaltager", "Schedar", "Sulafat", "Umbriel", "Vindemiatrix", "Zephyr", "Zubenelgenubi"
        };
        private static readonly string[] OpenAiTtsVoices =
        {
            "alloy", "ash", "ballad", "coral", "echo", "fable", "onyx", "nova", "sage", "shimmer", "verse", "marin", "cedar"
        };
        private static readonly TtsModelOption[] TtsModelOptions =
        {
            new TtsModelOption("Gemini Flash TTS", "google/gemini-3.1-flash-tts-preview", GeminiVoices, "Iapetus", "pcm", 24000),
            new TtsModelOption("GPT-4o Mini TTS", "openai/gpt-4o-mini-tts-2025-12-15", OpenAiTtsVoices, "cedar", "pcm", 24000)
        };
        private readonly List<ChatLine> transcript = new List<ChatLine>();
        private readonly string[] debugToolNames =
        {
            "get_ship_info",
            "get_hal_personality",
            "set_hal_personality",
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
        private OpenRouterSpeechToTextClient speechToText;
        private OpenRouterTextToSpeechClient textToSpeech;
        private MicrophoneVoiceRecorder recorder;
        private VoiceHelperClient tts;
        private AudioSource advancedTtsAudioSource;
        private Rect windowRect = new Rect(180f, 90f, 560f, 520f);
        private Vector2 scroll;
        private Vector2 settingsScroll;
        private Vector2 debugOutputScroll;
        private Vector2 debugArgsScroll;
        private string input = string.Empty;
        private string status = "Idle";
        private string debugArguments = "{}";
        private string debugOutput = string.Empty;
        private string selectedDebugTool = "get_ship_info";
        private bool visible = true;
        private bool pending;
        private bool advancedTtsEnabled;
        private int selectedTab;

        public void Awake()
        {
            config = HAL9000Config.Load();
            client = new OpenRouterClient(config);
            speechToText = new OpenRouterSpeechToTextClient(config);
            textToSpeech = new OpenRouterTextToSpeechClient(config);
            recorder = new MicrophoneVoiceRecorder(config.VoiceSampleRate, config.VoiceMaxSeconds);
            tts = new VoiceHelperClient();
            advancedTtsEnabled = IsAdvancedTtsMode(config.TextToSpeechMode);
            EnsureTtsSelectionValid();
            advancedTtsAudioSource = gameObject.AddComponent<AudioSource>();
            advancedTtsAudioSource.spatialBlend = 0f;
            advancedTtsAudioSource.playOnAwake = false;
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

            UpdateVoiceInput();
        }

        public void OnDestroy()
        {
            if (tts != null)
            {
                tts.Dispose();
                tts = null;
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
            selectedTab = GUILayout.Toolbar(selectedTab, new[] { "Chat", "Tools", "Settings" });

            if (selectedTab == 0)
            {
                DrawChatPanel();
            }
            else if (selectedTab == 1)
            {
                DrawDebugPanel();
            }
            else
            {
                DrawSettingsPanel();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawChatPanel()
        {
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(275f));
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
            GUILayout.Label("Voice: " + VoiceStatusText());
            GUILayout.Label("Personality: Humor " + config.HumorPercent + "%, Honesty " + config.HonestyPercent + "%");

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

        private void DrawSettingsPanel()
        {
            settingsScroll = GUILayout.BeginScrollView(settingsScroll, GUILayout.Height(420f));

            GUILayout.Label("Voice Output");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode: " + (advancedTtsEnabled ? "Advanced OpenRouter" : "Windows"), GUILayout.Width(210f));
            string buttonText = advancedTtsEnabled ? "Use Windows TTS" : "Use Advanced TTS";
            if (GUILayout.Button(buttonText, GUILayout.Width(150f)))
            {
                advancedTtsEnabled = !advancedTtsEnabled;
                AddVoiceDebugLine("TTS mode switched to " + (advancedTtsEnabled ? "Advanced OpenRouter." : "Windows."));
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Advanced TTS Model");
            int selectedModel = SelectedTtsModelIndex();
            string[] modelLabels = TtsModelLabels();
            int nextModel = GUILayout.SelectionGrid(selectedModel, modelLabels, 1);
            if (nextModel != selectedModel && nextModel >= 0 && nextModel < TtsModelOptions.Length)
            {
                ApplyTtsModelOption(TtsModelOptions[nextModel]);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Voice: " + config.TextToSpeechVoice);
            TtsModelOption current = CurrentTtsModelOption();
            int selectedVoice = IndexOf(current.Voices, config.TextToSpeechVoice);
            int nextVoice = GUILayout.SelectionGrid(Math.Max(0, selectedVoice), current.Voices, 3);
            if (nextVoice >= 0 && nextVoice < current.Voices.Length && current.Voices[nextVoice] != config.TextToSpeechVoice)
            {
                config.TextToSpeechVoice = current.Voices[nextVoice];
                AddVoiceDebugLine("Advanced TTS voice switched to " + config.TextToSpeechVoice + ".");
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume: " + Math.Round(config.TextToSpeechVolume * 100f) + "%", GUILayout.Width(210f));
            config.TextToSpeechVolume = GUILayout.HorizontalSlider(config.TextToSpeechVolume, 0f, 3f, GUILayout.Width(220f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed: " + config.TextToSpeechSpeed.ToString("0.00") + "x", GUILayout.Width(210f));
            config.TextToSpeechSpeed = GUILayout.HorizontalSlider(config.TextToSpeechSpeed, 0.25f, 4f, GUILayout.Width(220f));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Request: " + config.TextToSpeechModel);
            GUILayout.Label("Format: " + config.TextToSpeechResponseFormat + ", PCM sample rate: " + config.TextToSpeechPcmSampleRate + " Hz");
            GUILayout.Label("These settings apply immediately for this KSP session.");

            GUILayout.EndScrollView();

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
            SendText(message);
        }

        private void SendText(string message)
        {
            message = message == null ? string.Empty : message.Trim();
            if (message.Length == 0 || pending)
            {
                return;
            }

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
                string response = string.IsNullOrEmpty(answer) ? "No response text returned." : answer;
                transcript.Add(new ChatLine("assistant", response));
                SpeakResponse(response);

                status = "Idle";
            }

            pending = false;
            scroll.y = float.MaxValue;
        }

        private void UpdateVoiceInput()
        {
            string voiceError;
            while (tts != null && tts.TryGetError(out voiceError))
            {
                status = voiceError;
                AddVoiceDebugLine("Error: " + voiceError);
            }

            string voiceLog;
            while (tts != null && tts.TryGetLog(out voiceLog))
            {
                AddVoiceDebugLine(voiceLog);
            }

            if (!pending && !transcribingVoice && config.HasApiKey)
            {
                if (Input.GetKeyDown(KeyCode.RightAlt))
                {
                    BeginVoiceRecording();
                }

                if (Input.GetKeyUp(KeyCode.RightAlt))
                {
                    EndVoiceRecording();
                }
            }

            if (recorder != null && recorder.IsRecording && recorder.CurrentDurationSeconds() >= config.VoiceMaxSeconds - 0.1f)
            {
                AddVoiceDebugLine("Maximum voice recording length reached.");
                EndVoiceRecording();
            }
        }

        private string VoiceStatusText()
        {
            if (recorder == null || !recorder.HasMicrophone)
            {
                return "No microphone available";
            }

            if (recorder.IsRecording)
            {
                return "Recording while Right Alt is held";
            }

            if (transcribingVoice)
            {
                return "Transcribing with " + config.SpeechToTextModel;
            }

            return "Hold Right Alt to talk";
        }

        private bool transcribingVoice;

        private void BeginVoiceRecording()
        {
            if (recorder == null)
            {
                status = "Voice recorder unavailable.";
                AddVoiceDebugLine(status);
                return;
            }

            string error;
            if (!recorder.Begin(out error))
            {
                status = error;
                AddVoiceDebugLine("Recording failed: " + error);
                return;
            }

            status = "Recording voice...";
            AddVoiceDebugLine("Recording started from " + recorder.InputDescription + ".");
        }

        private void EndVoiceRecording()
        {
            if (recorder == null || !recorder.IsRecording)
            {
                return;
            }

            byte[] wavBytes;
            float durationSeconds;
            string error;
            if (!recorder.End(out wavBytes, out durationSeconds, out error))
            {
                status = error;
                AddVoiceDebugLine("Recording failed: " + error);
                return;
            }

            AddVoiceDebugLine("Recording stopped: " + durationSeconds.ToString("0.00") + "s, " + wavBytes.Length + " WAV bytes.");
            StartCoroutine(TranscribeVoice(wavBytes, durationSeconds));
        }

        private IEnumerator TranscribeVoice(byte[] wavBytes, float durationSeconds)
        {
            transcribingVoice = true;
            status = "Transcribing voice...";
            AddVoiceDebugLine("Sending " + durationSeconds.ToString("0.00") + "s WAV to OpenRouter STT model " + config.SpeechToTextModel + ".");

            string transcriptText = null;
            string error = null;
            yield return StartCoroutine(speechToText.Transcribe(wavBytes, value => transcriptText = value, value => error = value));

            transcribingVoice = false;

            if (!string.IsNullOrEmpty(error))
            {
                status = "Voice transcription failed.";
                AddVoiceDebugLine("STT failed: " + error);
                yield break;
            }

            if (string.IsNullOrEmpty(transcriptText))
            {
                status = "No speech transcribed.";
                AddVoiceDebugLine(status);
                yield break;
            }

            status = "Heard: " + transcriptText;
            AddVoiceDebugLine("STT text: " + transcriptText);
            SendText(transcriptText);
        }

        private void AddVoiceDebugLine(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Debug.Log("[HAL-9000] Voice: " + message);
        }

        private void SpeakResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return;
            }

            if (advancedTtsEnabled)
            {
                StartCoroutine(SpeakWithOpenRouter(response));
                return;
            }

            if (tts != null)
            {
                tts.Speak(response);
            }
        }

        private IEnumerator SpeakWithOpenRouter(string response)
        {
            if (!config.HasApiKey)
            {
                AddVoiceDebugLine("Advanced TTS skipped: OpenRouter API key is missing.");
                yield break;
            }

            if (config.TextToSpeechResponseFormat != "mp3" && config.TextToSpeechResponseFormat != "pcm")
            {
                AddVoiceDebugLine("Advanced TTS playback currently requires mp3 or pcm response format.");
                yield break;
            }

            AddVoiceDebugLine("Requesting OpenRouter TTS model " + config.TextToSpeechModel + " voice " + config.TextToSpeechVoice + ".");

            byte[] audioBytes = null;
            string error = null;
            yield return StartCoroutine(textToSpeech.CreateSpeech(response, value => audioBytes = value, value => error = value));

            if (!string.IsNullOrEmpty(error))
            {
                AddVoiceDebugLine("Advanced TTS failed: " + error);
                if (tts != null)
                {
                    AddVoiceDebugLine("Falling back to Windows TTS.");
                    tts.Speak(response);
                }

                yield break;
            }

            AudioClip clip = null;
            if (config.TextToSpeechResponseFormat == "pcm")
            {
                clip = CreateClipFromPcm16(audioBytes, config.TextToSpeechPcmSampleRate);
            }
            else
            {
                yield return StartCoroutine(LoadMp3Clip(audioBytes, value => clip = value));
            }

            if (clip == null)
            {
                AddVoiceDebugLine("Advanced TTS audio conversion returned no clip.");
                yield break;
            }

            ApplyAdvancedTtsGain(clip);
            advancedTtsAudioSource.Stop();
            advancedTtsAudioSource.volume = Math.Min(1f, Math.Max(0f, config.TextToSpeechVolume));
            advancedTtsAudioSource.clip = clip;
            advancedTtsAudioSource.Play();
            AddVoiceDebugLine("Advanced TTS playing " + audioBytes.Length + " " + config.TextToSpeechResponseFormat + " bytes.");
        }

        private IEnumerator LoadMp3Clip(byte[] audioBytes, Action<AudioClip> onComplete)
        {
            string path = Path.Combine(Application.temporaryCachePath, "HAL9000-tts.mp3");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, audioBytes);
            }
            catch (Exception ex)
            {
                AddVoiceDebugLine("Advanced TTS file write failed: " + ex.Message);
                onComplete(null);
                yield break;
            }

            string uri = "file:///" + path.Replace("\\", "/");
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG);
            yield return request.SendWebRequest();

            if (request.isNetworkError || request.isHttpError)
            {
                AddVoiceDebugLine("Advanced TTS audio load failed: " + request.error);
                onComplete(null);
                yield break;
            }

            onComplete(DownloadHandlerAudioClip.GetContent(request));
        }

        private AudioClip CreateClipFromPcm16(byte[] audioBytes, int sampleRate)
        {
            if (audioBytes == null || audioBytes.Length < 2)
            {
                return null;
            }

            int sampleCount = audioBytes.Length / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                int byteIndex = i * 2;
                short value = (short)(audioBytes[byteIndex] | (audioBytes[byteIndex + 1] << 8));
                samples[i] = Mathf.Clamp(value / 32768f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("HAL9000-tts-pcm", sampleCount, 1, sampleRate, false);
            if (clip == null)
            {
                return null;
            }

            return clip.SetData(samples, 0) ? clip : null;
        }

        private void ApplyAdvancedTtsGain(AudioClip clip)
        {
            if (clip == null || config.TextToSpeechVolume <= 1f)
            {
                return;
            }

            try
            {
                float[] samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0))
                {
                    return;
                }

                float gain = config.TextToSpeechVolume;
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
                }

                clip.SetData(samples, 0);
            }
            catch (Exception ex)
            {
                AddVoiceDebugLine("Advanced TTS volume gain failed: " + ex.Message);
            }
        }

        private static bool IsAdvancedTtsMode(string mode)
        {
            return string.Equals(mode, "advanced", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "openrouter", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureTtsSelectionValid()
        {
            TtsModelOption option = CurrentTtsModelOption();
            config.TextToSpeechModel = option.ModelId;
            config.TextToSpeechResponseFormat = option.ResponseFormat;
            config.TextToSpeechPcmSampleRate = option.PcmSampleRate;
            if (IndexOf(option.Voices, config.TextToSpeechVoice) < 0)
            {
                config.TextToSpeechVoice = option.DefaultVoice;
            }
        }

        private TtsModelOption CurrentTtsModelOption()
        {
            int index = SelectedTtsModelIndex();
            return index >= 0 ? TtsModelOptions[index] : TtsModelOptions[0];
        }

        private int SelectedTtsModelIndex()
        {
            for (int i = 0; i < TtsModelOptions.Length; i++)
            {
                if (string.Equals(TtsModelOptions[i].ModelId, config.TextToSpeechModel, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private static string[] TtsModelLabels()
        {
            string[] labels = new string[TtsModelOptions.Length];
            for (int i = 0; i < TtsModelOptions.Length; i++)
            {
                labels[i] = TtsModelOptions[i].DisplayName;
            }

            return labels;
        }

        private void ApplyTtsModelOption(TtsModelOption option)
        {
            config.TextToSpeechModel = option.ModelId;
            config.TextToSpeechVoice = option.DefaultVoice;
            config.TextToSpeechResponseFormat = option.ResponseFormat;
            config.TextToSpeechPcmSampleRate = option.PcmSampleRate;
            AddVoiceDebugLine("Advanced TTS model switched to " + option.DisplayName + " voice " + option.DefaultVoice + ".");
        }

        private static int IndexOf(string[] values, string value)
        {
            if (values == null || value == null)
            {
                return -1;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private string ExecuteTool(ToolCallRequest request)
        {
            try
            {
                if (request.Name == "get_ship_info")
                {
                    return ShipInfoTool.GetShipInfoJson();
                }

                if (request.Name == "get_hal_personality")
                {
                    return GetHalPersonalityJson();
                }

                if (request.Name == "set_hal_personality")
                {
                    return SetHalPersonalityJson(request.Arguments);
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

        private string GetHalPersonalityJson()
        {
            return JsonUtil.Serialize(new Dictionary<string, object>
            {
                { "humor_percent", config.HumorPercent },
                { "honesty_percent", config.HonestyPercent },
                { "note", "Runtime setting for HAL responses. Honesty changes candor, not factual accuracy." }
            });
        }

        private string SetHalPersonalityJson(Dictionary<string, object> arguments)
        {
            bool changed = false;
            int humor;
            if (TryGetArgumentInt(arguments, "humor_percent", out humor))
            {
                config.HumorPercent = HAL9000Config.ClampPercent(humor);
                changed = true;
            }

            int honesty;
            if (TryGetArgumentInt(arguments, "honesty_percent", out honesty))
            {
                config.HonestyPercent = HAL9000Config.ClampPercent(honesty);
                changed = true;
            }

            Dictionary<string, object> result = new Dictionary<string, object>
            {
                { "changed", changed },
                { "humor_percent", config.HumorPercent },
                { "honesty_percent", config.HonestyPercent },
                { "note", "Applied for this KSP session. Set HAL_HUMOR_PERCENT and HAL_HONESTY_PERCENT in settings.cfg to make defaults permanent." }
            };

            return JsonUtil.Serialize(result);
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
            if (toolName == "set_hal_personality")
            {
                return "{\"humor_percent\":15,\"honesty_percent\":90}";
            }

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

        private static bool TryGetArgumentInt(Dictionary<string, object> arguments, string key, out int result)
        {
            result = 0;
            if (arguments == null)
            {
                return false;
            }

            object value;
            if (!arguments.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is int)
            {
                result = (int)value;
                return true;
            }

            if (value is long)
            {
                result = (int)(long)value;
                return true;
            }

            if (value is double)
            {
                result = (int)Math.Round((double)value);
                return true;
            }

            if (value is float)
            {
                result = (int)Math.Round((float)value);
                return true;
            }

            return int.TryParse(value.ToString(), out result);
        }

        private sealed class TtsModelOption
        {
            public readonly string DisplayName;
            public readonly string ModelId;
            public readonly string[] Voices;
            public readonly string DefaultVoice;
            public readonly string ResponseFormat;
            public readonly int PcmSampleRate;

            public TtsModelOption(string displayName, string modelId, string[] voices, string defaultVoice, string responseFormat, int pcmSampleRate)
            {
                DisplayName = displayName;
                ModelId = modelId;
                Voices = voices;
                DefaultVoice = defaultVoice;
                ResponseFormat = responseFormat;
                PcmSampleRate = pcmSampleRate;
            }
        }
    }
}
