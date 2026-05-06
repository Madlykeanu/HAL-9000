# HAL-9000 for Kerbal Space Program

HAL-9000 is a v0.1 in-flight AI terminal for KSP. It provides text and push-to-talk chat, sends messages to OpenRouter, and exposes read-only tools so the model can answer using live vessel, orbit, target, part, and celestial-body data.

The project uses an SDK-style `.csproj` with `KSPBuildTools`, similar to newer KSP mod repos. `dotnet build` resolves the installed KSP assemblies and stages the plugin into the repo's `GameData` folder.

## Build

From this folder:

```powershell
dotnet build -c Release
```

The build outputs:

```text
GameData\HAL-9000\Plugins\HAL9000.dll
GameData\HAL-9000\Voice\HAL9000VoiceHelper.exe
```

`build.ps1` is just a thin wrapper around the same `dotnet build` command.

## Configure

Edit:

```text
GameData\HAL-9000\PluginData\settings.cfg
```

Set `OPENROUTER_API_KEY` before launching KSP. `OPENROUTER_MODEL` can be changed without rebuilding.

Optional personality defaults:

```text
HAL_HUMOR_PERCENT = 15
HAL_HONESTY_PERCENT = 90
```

HAL also exposes TARS-style runtime personality controls. Ask HAL to change its humor or honesty setting, or use the manual tool tab with `set_hal_personality`. Humor changes how much dry wit HAL uses. Honesty changes how directly HAL states uncertainty, limitations, and corrections; it never allows false answers.

## Test Install

Copy the `GameData\HAL-9000` folder into KSP's `GameData` folder. In flight, press `F8` or use the visible HAL button to open the terminal.

## Voice

Hold `Right Alt` in flight to talk to HAL. Release the key to send the recorded phrase to OpenRouter speech-to-text, then through the same chat path as typed input. HAL reads successful LLM responses aloud using the default Windows text-to-speech voice.

Voice input uses Unity microphone recording plus OpenRouter STT. By default it transcribes with:

```text
google/chirp-3
```

You can override it in `GameData\HAL-9000\PluginData\settings.cfg`:

```text
OPENROUTER_STT_MODEL = google/chirp-3
VOICE_SAMPLE_RATE = 16000
VOICE_MAX_SECONDS = 20
```

Voice output is implemented by the helper executable staged at:

```text
GameData\HAL-9000\Voice\HAL9000VoiceHelper.exe
```

By default, HAL uses free local Windows TTS for responses. Use the in-game `Use Advanced TTS` button to switch to OpenRouter TTS at runtime, or set the default in `settings.cfg`:

```text
VOICE_TTS_MODE = windows
OPENROUTER_TTS_MODEL = google/gemini-3.1-flash-tts-preview
OPENROUTER_TTS_VOICE = Iapetus
OPENROUTER_TTS_FORMAT = pcm
OPENROUTER_TTS_PCM_SAMPLE_RATE = 24000
OPENROUTER_TTS_SPEED = 1
OPENROUTER_TTS_VOLUME = 1.8
```

Set `VOICE_TTS_MODE = openrouter` or `advanced` to start in advanced TTS mode. Advanced TTS may sound better, but it adds one more network request per answer and is billed by input characters. When advanced mode is active, the chat window shows volume and speed sliders. Some TTS providers ignore `OPENROUTER_TTS_SPEED`; OpenRouter accepts the field, but provider support varies.

If no microphone is available to Unity, the chat UI will show voice as unavailable. Voice and TTS diagnostics are written to the KSP log with the `[HAL-9000] Voice:` prefix.

Unity records from its default microphone. To use a Quest 3 microphone, set the Quest 3 mic as the default Windows input device before launching KSP:

1. Open Windows `Settings > System > Sound`.
2. Under `Input`, select the Quest 3 microphone.
3. Confirm the input meter moves when speaking through the Quest 3.
4. Restart KSP after changing the default input device.
