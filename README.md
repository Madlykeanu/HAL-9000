# HAL-9000 for Kerbal Space Program

HAL-9000 is a v0.1 in-flight AI terminal for KSP. It provides a text chat window, sends messages to OpenRouter, and exposes one read-only tool, `get_ship_info`, so the model can answer using live vessel data.

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
OPENROUTER_TTS_MODEL = openai/gpt-4o-mini-tts-2025-12-15
OPENROUTER_TTS_VOICE = nova
OPENROUTER_TTS_FORMAT = mp3
OPENROUTER_TTS_SPEED = 1
```

Set `VOICE_TTS_MODE = openrouter` or `advanced` to start in advanced TTS mode. Advanced TTS may sound better, but it adds one more network request per answer and is billed by input characters.

If no microphone is available to Unity, the chat UI will show voice as unavailable.

The chat window includes a `Voice debug` panel. Useful messages include:

- `Recording started from ...` means Unity started capturing microphone audio.
- `Recording stopped: ... WAV bytes` means audio was captured and encoded.
- `Sending ... WAV to OpenRouter STT model ...` means transcription has started.
- `STT text: ...` shows the text returned by OpenRouter.
- `STT failed: ...` shows the OpenRouter error if transcription failed.

Unity records from its default microphone. To use a Quest 3 microphone, set the Quest 3 mic as the default Windows input device before launching KSP:

1. Open Windows `Settings > System > Sound`.
2. Under `Input`, select the Quest 3 microphone.
3. Confirm the input meter moves when speaking through the Quest 3.
4. Restart KSP after changing the default input device.
