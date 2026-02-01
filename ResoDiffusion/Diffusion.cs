using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;                       // Console, general
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;                    // Directory, Path
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;       // Task, async/await

namespace ResoDiffusion
{
    public class Diffusion {

        public Diffusion(Action<string> log, Action<bool> running, Action<string> outPutRecived) {

            OnLog = log;
            isRunning = running;
            onOutputRecived = outPutRecived;

            //           Models = GetModels();
            writeToLog(Directory.GetCurrentDirectory().ToString());
            writeToLog("Current Directory: " + Directory.GetCurrentDirectory().ToString());
        }

        public void imageGen(string jsonInput) {


            writeToLog("LOG: Integration starting");

            //                string jsonInput = @"
            //                {
            //                    ""integration"": ""stableDiffusion"",
            //                    ""model_id"": ""dreamlike-art/dreamlike-diffusion-1.0"",
            //                    ""use_image"": true,
            //                    ""prompt"": ""anime red hair, in city, flying cars"",
            //                    ""negative_prompt"": ""blurry, lowres"",
            //                    ""batch_size"": 12,
            //                    ""strength"": 0.4,
            //                    ""height"": 512,
            //                    ""width"": 512,
            //                    ""steps"": 55,
            //                    ""guidance"": 15
            //                }";

            // Parse JSON
            isRunning(true);

            JsonDocument doc = JsonDocument.Parse(jsonInput);
            string integrationName = doc.RootElement
                .GetProperty("integration")
                .GetString(); 

            string pythonScriptPath = Path.Combine(
                exeFolder,
                "ResoDiffusionIntegrations",
                integrationName,
                "Integration.py"
            );

            OutPutLocation = Path.Combine(
                exeFolder,
                "ResoDiffusionIntegrations",
                integrationName,
                "output");

            if (!File.Exists(pythonScriptPath)) {
                writeToLog("Error: Integration file not found: " + pythonScriptPath);
                return;
            }

            writeToLog("LOG: Integration File exists");

            string pythonExe = "python";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) pythonExe = "python3";

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-u \"{pythonScriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = Process.Start(psi);
                if (process == null) return;

            writeToLog("LOG: Integration is running");

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;

                writeToLog(e.Data);

                // Check if the line contains the result type
                if (e.Data.Contains("\"type\": \"result\"")) {
                    try {
                        using (JsonDocument doc = JsonDocument.Parse(e.Data)) {
                            var root = doc.RootElement;

                            if (root.TryGetProperty("output_folder", out JsonElement folderElem)) {
                                string folder = folderElem.GetString();
                                if (!string.IsNullOrEmpty(folder)) {
                                    OutPutLocation = folder;
                                    onOutputRecived(OutPutLocation);
                                    writeToLog($"[Output folder set] {OutPutLocation}");
                                    isRunning(false);
                                }
                            }
                        }
                    }
                    catch (Exception ex) {
                        writeToLog("[JSON parse error] " + ex.Message);
                    }
                }
            };


            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                writeToLog(e.Data);
            };

//            process.Exited += (s, e) =>
//            {
//                writeToLog("LOG: Pocess Ended " + process.ExitCode.ToString());
//                isRunning(false);
//            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.StandardInput.Write(jsonInput);
            process.StandardInput.Close();

            writeToLog("LOG: Its still alive and you havent crashed!!!");

        }


        private string exeFolder = AppContext.BaseDirectory;
        public static string GetInputLocation(string json) {
            JsonDocument doc = JsonDocument.Parse(json);
            string integrationName = doc.RootElement
                .GetProperty("integration")
                .GetString();

            return Path.Combine(
                AppContext.BaseDirectory,
                "ResoDiffusionIntegrations",
                integrationName,
                "Input");
        }
        public string OutPutLocation = "";
        void writeToLog(string str) {

            OnLog(str);
            ResoniteMod.Msg(str);
        }

        public event Action<string> OnLog;
        public event Action<bool> isRunning;
        public event Action<string> onOutputRecived;

    }
}
