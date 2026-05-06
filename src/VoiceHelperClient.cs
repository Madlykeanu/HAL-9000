using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace HAL9000
{
    internal sealed class VoiceHelperClient : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<string> recognizedTexts = new Queue<string>();
        private readonly Queue<string> errors = new Queue<string>();
        private readonly Queue<string> logs = new Queue<string>();
        private Process process;
        private bool available;
        private bool listening;

        public bool Available
        {
            get { return available; }
        }

        public bool IsListening
        {
            get { return listening; }
        }

        public VoiceHelperClient()
        {
            available = File.Exists(HelperPath());
            if (!available)
            {
                EnqueueError("Voice helper is missing.");
            }
            else
            {
                EnqueueLog("Voice helper found at " + HelperPath());
            }
        }

        public void BeginListening()
        {
            if (!EnsureStarted())
            {
                return;
            }

            listening = true;
            EnqueueLog("Right Alt pressed: sending LISTEN to helper.");
            SendCommand("LISTEN");
        }

        public void EndListening()
        {
            if (!available || process == null || process.HasExited)
            {
                listening = false;
                return;
            }

            EnqueueLog("Right Alt released: sending STOP to helper.");
            SendCommand("STOP");
        }

        public bool TryGetRecognizedText(out string text)
        {
            lock (sync)
            {
                if (recognizedTexts.Count > 0)
                {
                    text = recognizedTexts.Dequeue();
                    return true;
                }
            }

            text = null;
            return false;
        }

        public bool TryGetError(out string error)
        {
            lock (sync)
            {
                if (errors.Count > 0)
                {
                    error = errors.Dequeue();
                    return true;
                }
            }

            error = null;
            return false;
        }

        public bool TryGetLog(out string log)
        {
            lock (sync)
            {
                if (logs.Count > 0)
                {
                    log = logs.Dequeue();
                    return true;
                }
            }

            log = null;
            return false;
        }

        public void Speak(string text)
        {
            if (string.IsNullOrEmpty(text) || !EnsureStarted())
            {
                return;
            }

            EnqueueLog("Sending response text to Windows TTS.");
            SendCommand("SPEAK\t" + Encode(text));
        }

        public void Dispose()
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    SendCommand("EXIT");
                    process.WaitForExit(500);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[HAL-9000] Voice helper shutdown failed: " + ex);
            }

            try
            {
                if (process != null)
                {
                    process.OutputDataReceived -= OnOutputDataReceived;
                    process.ErrorDataReceived -= OnErrorDataReceived;
                    process.Dispose();
                    process = null;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[HAL-9000] Voice helper dispose failed: " + ex);
            }

            listening = false;
        }

        private bool EnsureStarted()
        {
            if (!available)
            {
                return false;
            }

            if (process != null && !process.HasExited)
            {
                return true;
            }

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = HelperPath();
                info.WorkingDirectory = Path.GetDirectoryName(info.FileName);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;

                process = new Process();
                process.StartInfo = info;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += OnOutputDataReceived;
                process.ErrorDataReceived += OnErrorDataReceived;
                process.Exited += OnExited;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                EnqueueLog("Voice helper process started.");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[HAL-9000] Voice helper start failed: " + ex);
                EnqueueError("Voice helper start failed: " + ex.Message);
                available = false;
                listening = false;
                return false;
            }
        }

        private void SendCommand(string command)
        {
            try
            {
                process.StandardInput.WriteLine(command);
                process.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[HAL-9000] Voice helper command failed: " + ex);
                EnqueueError("Voice helper command failed: " + ex.Message);
                listening = false;
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            string[] parts = e.Data.Split(new[] { '\t' }, 2);
            if (parts.Length == 0)
            {
                return;
            }

            string command = parts[0];
            string payload = parts.Length > 1 ? Decode(parts[1]) : string.Empty;
            if (command == "TEXT")
            {
                lock (sync)
                {
                    recognizedTexts.Enqueue(payload);
                }

                EnqueueLog("Recognized text: " + payload);
            }
            else if (command == "ERROR")
            {
                EnqueueError(payload);
            }
            else if (command == "LOG")
            {
                EnqueueLog(payload);
            }
            else if (command == "STATE")
            {
                EnqueueLog("Helper state: " + payload);
                if (payload == "IDLE")
                {
                    listening = false;
                }
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                EnqueueError(e.Data);
            }
        }

        private void OnExited(object sender, EventArgs e)
        {
            listening = false;
            EnqueueLog("Voice helper process exited.");
        }

        private void EnqueueError(string error)
        {
            lock (sync)
            {
                errors.Enqueue(error);
            }
        }

        private void EnqueueLog(string log)
        {
            lock (sync)
            {
                logs.Enqueue(log);
            }
        }

        private static string HelperPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "HAL-9000", "Voice", "HAL9000VoiceHelper.exe");
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
