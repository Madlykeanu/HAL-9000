using System;
using System.Speech.Synthesis;
using System.Text;

namespace HAL9000VoiceHelper
{
    internal static class Program
    {
        private static readonly object OutputSync = new object();
        private static SpeechSynthesizer synthesizer;

        private static int Main()
        {
            try
            {
                synthesizer = new SpeechSynthesizer();
                synthesizer.SetOutputToDefaultAudioDevice();
                WriteMessage("LOG", "Windows TTS helper ready.");
                RunCommandLoop();
                return 0;
            }
            catch (Exception ex)
            {
                WriteMessage("ERROR", "TTS helper failed: " + ex.Message);
                return 1;
            }
            finally
            {
                DisposeSpeech();
            }
        }

        private static void RunCommandLoop()
        {
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (line.StartsWith("SPEAK\t", StringComparison.Ordinal))
                {
                    Speak(Decode(line.Substring(6)));
                }
                else if (line == "EXIT")
                {
                    return;
                }
            }
        }

        private static void Speak(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                WriteMessage("ERROR", "Voice speak failed: " + ex.Message);
            }
        }

        private static void DisposeSpeech()
        {
            if (synthesizer == null)
            {
                return;
            }

            try
            {
                synthesizer.SpeakAsyncCancelAll();
                synthesizer.Dispose();
            }
            catch
            {
            }
        }

        private static void WriteMessage(string command, string payload)
        {
            lock (OutputSync)
            {
                Console.WriteLine(command + "\t" + Encode(payload ?? string.Empty));
                Console.Out.Flush();
            }
        }

        private static string Encode(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        }

        private static string Decode(string text)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(text));
            }
            catch
            {
                return text;
            }
        }
    }
}
