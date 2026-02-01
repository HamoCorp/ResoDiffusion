using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.Runtime.InteropServices;
using Elements.Assets;
using System.Runtime.Serialization;

namespace ResoDiffusion {

    public class ResoDiffusion : ResoniteMod {

        public override string Name => "ResoDiffusion";
        public override string Author => "HamoCorp";
        public override string Version => "0.0.0";

        public override string Link => "https://github.com/HamoCorp/";

        private static ModConfiguration Config;

        public override void OnEngineInit() {

            Config = GetConfiguration();
            Config.Save(true);
            Harmony harmony = new Harmony("com.HamoCorp.ResoDiffusion");
            harmony.PatchAll();

        }

        public static string nameGenerator(int length) {

            string name = " ";
            for (int i = 0; i < length; i++) {
                name += " ";
            }
            return name;
        }

        public static Diffusion addInterfaceToMenuUi(DynamicVariableSpace dynvarSpace) {


            Slot TSlot = dynvarSpace.Slot;
            //conmenu.OnValueChange
            Slot MenuRoot = TSlot.AddSlot("ModData", false);

            Slot buttonSlot = MenuRoot.AddSlot("SendButton", false);
            LegacyButton button = buttonSlot.AttachComponent<LegacyButton>();
            button.LabelText = "Run Script";
            Slot buttonSlot2 = MenuRoot.AddSlot("OpenFolderButtonOutput", false);
            buttonSlot2.LocalPosition = new float3(0f, -0.05f, 0f);
            LegacyButton buttonOpenFolder = buttonSlot2.AttachComponent<LegacyButton>();
            buttonOpenFolder.LabelText = "Open Output Folder On DesktopInput";

            Slot buttonSlot4 = MenuRoot.AddSlot("OpenFolderButtonInput", false);
            buttonSlot4.LocalPosition = new float3(0f, -0.1f, 0f);
            LegacyButton buttonOpenFolderI = buttonSlot4.AttachComponent<LegacyButton>();
            buttonOpenFolderI.LabelText = "Open Input Folder On Desktop";

            Slot buttonSlot3 = MenuRoot.AddSlot("ImportButton", false);
            buttonSlot3.LocalPosition = new float3(0f, -0.15f, 0f);
            LegacyButton buttonImport = buttonSlot3.AttachComponent<LegacyButton>();
            buttonImport.LabelText = "Import Content";

            Slot buttonSlot5 = MenuRoot.AddSlot("ExportButton", false);
            buttonSlot5.LocalPosition = new float3(0f, -0.25f, 0f);
            LegacyButton buttonExport = buttonSlot5.AttachComponent<LegacyButton>();
            buttonExport.LabelText = "Save Content";

            Slot logSlot = MenuRoot.AddSlot("Log", false);

            Slot injectedSlot = MenuRoot.AddSlot("Injected", false);
            DynamicValueVariable<string> jsonVar = CreateDynamicVar<string>(injectedSlot, "ResoDiffusion Mod/JsonIn");
            DynamicValueVariable<bool> isRuning = CreateDynamicVar<bool>(injectedSlot, "ResoDiffusion Mod/isRuning");
            isRuning.Value.Value = false;
            //DynamicReferenceVariable<IAssetProvider<ITexture2D>> imagevar = CreateDynamicRef(injectedSlot, "ResoDiffusion Mod/Export");

            //setup log slot and stable diff
            Diffusion stableDiffusion = new Diffusion((str) =>
            {
                MenuRoot.World.RunSynchronously(() =>
                {
                    logSlot.AddSlot(str);
                });

                Msg(str);

            }, (running) =>
            {
                MenuRoot.World.RunSynchronously(() =>
                {
                    isRuning.Value.Value = running;
                });
            }, (outputLoc) =>
            {
                MenuRoot.World.RunSynchronously(() =>
                {
                    if (outputLoc != null || outputLoc != "") {
                        World targetWorld = logSlot.World;
                        float3 globalPosition = logSlot.GlobalPosition;
                        float3 backward = logSlot.Backward;
                        float3 @float = backward * 0.1f;
                        float3 point = WorldManager.TransferPoint(globalPosition + @float, logSlot.World, targetWorld);
                        floatQ orientation = WorldManager.TransferRotation(logSlot.GlobalRotation, logSlot.World, targetWorld);
                        UniversalImporter.Import(outputLoc, logSlot.World, point, orientation, true, false);
                    }
                });
            });

            //            _diffusionList.Add(stableDiffusion);
            //            logSlot.AddSlot("LOG: Dif List Add Success: " + _diffusionList.Count, false);

            button.LocalPressed += async (IButton b, ButtonEventData d) =>
            {
                if (logSlot == null || logSlot.IsDestroyed) return;

                try {


                    logSlot.AddSlot("LOG: json data sent");
                    stableDiffusion.imageGen(jsonVar.Value.Value);

                }
                catch (Exception ex) {
                    logSlot.AddSlot("ERROR: generate button fail" + ex.Message, false);
                    Msg("generate button fail");
                }

            };


            buttonOpenFolder.LocalPressed += async (IButton b, ButtonEventData d) =>
            {
                if (logSlot == null || logSlot.IsDestroyed) return;
                SubFolder("Open Output Folder", logSlot, buttonOpenFolder, stableDiffusion.OutPutLocation);

            };

            buttonOpenFolderI.LocalPressed += async (IButton b, ButtonEventData d) =>
            {
                if (logSlot == null || logSlot.IsDestroyed) return;
                OpenFolder(Diffusion.GetInputLocation(jsonVar.Value.Value), logSlot);

            };

            buttonImport.LocalPressed += (IButton b, ButtonEventData d) =>
            {
                if (logSlot == null || logSlot.IsDestroyed) return;

                World targetWorld = logSlot.World;
                float3 globalPosition = logSlot.GlobalPosition;
                float3 backward = logSlot.Backward;
                float3 @float = backward * 0.1f;
                float3 point = WorldManager.TransferPoint(globalPosition + @float, logSlot.World, targetWorld);
                floatQ orientation = WorldManager.TransferRotation(logSlot.GlobalRotation, logSlot.World, targetWorld);
                UniversalImporter.Import(stableDiffusion.OutPutLocation, logSlot.World, point, orientation, true, false);

            };

            //test export button
            buttonExport.LocalPressed += (IButton b, ButtonEventData d) =>
            {
                if (logSlot == null || logSlot.IsDestroyed)
                    return;

                World world = logSlot.World;

                // Get grabber for the pressed button source
                var grabber = world.GetLocalUserGrabberWithItems(d.source.Slot);
                if (grabber == null || grabber.HolderSlot.ChildrenCount != 1) {
                    logSlot.AddSlot("Nothing held to export.");
                    return;
                }

                Slot itemSlot = grabber.HolderSlot[0];

                // Resolve ReferenceProxy (inventory items, tools, etc.)
                var refProxy = itemSlot.GetComponentInChildren<ReferenceProxy>(
                    r => r.Reference.Target is Slot, false, false);

                if (refProxy != null)
                    itemSlot = (Slot)refProxy.Reference.Target;

                // Permission checks (REQUIRED)
                if (!world.CanSaveItems() ||
                    !itemSlot.ForeachComponentInChildren<IItemPermissions>(
                        p => p.CanSave, false, false)) {
                    logSlot.AddSlot("Export not permitted.");
                    return;
                }

                // Get the folder to clear existing images
                string exportFolder = Diffusion.GetInputLocation(jsonVar.Value.Value);
                if (string.IsNullOrEmpty(exportFolder)) return;

                try {
                    DirectoryInfo dir = new DirectoryInfo(exportFolder);

                    // Delete all files
                    foreach (FileInfo file in dir.GetFiles()) {
                        file.Delete();
                    }

                    // Delete all subdirectories recursively
                    foreach (DirectoryInfo subDir in dir.GetDirectories()) {
                        subDir.Delete(true);
                    }

                    logSlot.AddSlot("Cleared folder: " + exportFolder);
                }
                catch (Exception ex) {
                    logSlot.AddSlot("Failed to clear folder: " + ex.Message, false);
                }

                // Position dialog in front of user
                float3 pos = logSlot.GlobalPosition + logSlot.Backward * 0.1f;
                floatQ rot = logSlot.GlobalRotation;

                Slot dialogSlot = world.AddSlot("Export Dialog", true);
                dialogSlot.GlobalPosition = pos;
                dialogSlot.GlobalRotation = rot;

                var exportDialog = dialogSlot.AttachComponent<ExportDialog>(true, null);

                // Collect exportables
                List<IExportable> exportables =
                    itemSlot.GetComponentsInChildren<IExportable>(null, false, false, null);

                // If none exist, create a ModelExportable
                if (exportables.Count == 0) {
                    var model = dialogSlot.AttachComponent<ModelExportable>(true, null);
                    model.Root.Target = itemSlot;
                    exportables.Add(model);
                }
                else {
                    // Duplicate exportables onto dialog slot
                    for (int i = 0; i < exportables.Count; i++) {
                        exportables[i] = (IExportable)dialogSlot
                            .DuplicateComponent((Component)exportables[i], false);
                    }
                }

                // Ensure PackageExportable exists
                if (!exportables.Any(e => e is PackageExportable)) {
                    var pkg = dialogSlot.AttachComponent<PackageExportable>(true, null);
                    pkg.Root.Target = itemSlot;
                    exportables.Insert(0, pkg);
                }

                // Open export UI
                exportDialog.Setup(
                    Diffusion.GetInputLocation(jsonVar.Value.Value),
                    exportables.ToArray()
                );
            };

            return stableDiffusion;

        }

        private static string getSubFolder(string buttonName, Slot log, LegacyButton button, string parentFolder) {

            if (!Directory.Exists(parentFolder)) {
                button.LabelText = buttonName + " - Folder does not exist!";
                log.AddSlot("Error: opening folder: " + parentFolder);
                return "";
            }

            // Get all subfolders
            var subfolders = new DirectoryInfo(parentFolder).GetDirectories();

            if (subfolders.Length == 0) {
                button.LabelText = buttonName + " - No subfolders found.";
                log.AddSlot("Error: finding sub folder: " + parentFolder);
                return "";
            }

            // Find the newest folder by LastWriteTime
            var newestFolder = subfolders
                .OrderByDescending(f => f.LastWriteTime)
                .First();

            button.LabelText = buttonName;

            return newestFolder.FullName;
        }

        private static void SubFolder(string buttonName, Slot log, LegacyButton button, string parentFolder) {

            string sub = getSubFolder(buttonName, log, button, parentFolder);
            if (sub == "") return;


            // Open it in File Explorer
            OpenFolder(sub, log);
        }

        private static void OpenFolder(string folderPath, Slot log) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                // Try common Linux file managers
                Process.Start("xdg-open", folderPath); // Most Linux distros
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process.Start("open", folderPath); // macOS
            }
            else {
                log.AddSlot("Unsupported OS.", false);
                Msg("Unsupported OS");
            }
        }

        private static DynamicValueVariable<T> CreateDynamicVar<T>(Slot slot, string variableName) {
            var dyn = slot.AddSlot(variableName, false).AttachComponent<DynamicValueVariable<T>>();
            dyn.VariableName.Value = variableName;
            return dyn;
        }

        //        private static DynamicReferenceVariable<IAssetProvider<ITexture2D>> CreateDynamicRef(Slot slot, string variableName) {
        //            var dyn = slot.AddSlot(variableName, false).AttachComponent<DynamicReferenceVariable<IAssetProvider<ITexture2D>>>();
        //            dyn.VariableName.Value = variableName;
        //            return dyn;
        //        }

        [HarmonyPatch]
        class DiffusionPatch {

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DynamicVariableSpace), "OnStart")]
            public static void GetDiffusionUi(DynamicVariableSpace __instance) {

                //if (__instance.Text.Value != "ResoDiffusion") return;
                if(!Config.GetValue(_enabled)) return;
                if (__instance.Slot.Tag != "ResoDiffusion Mod " + __instance.World.LocalUser.UserID) return;

                addInterfaceToMenuUi(__instance);


            }

        }


        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> _enabled = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d29 = new ModConfigurationKey<dummy>(nameGenerator(1), "");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d28 = new ModConfigurationKey<dummy>(nameGenerator(2), "");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d27 = new ModConfigurationKey<dummy>(nameGenerator(3), "█▀█ █▀▀ █▀ █▀█ █▀▄ █ █▀▀ █▀▀ █░█ █▀ █ █▀█ █▄░█");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d26 = new ModConfigurationKey<dummy>(nameGenerator(4), "█▀▄ ██▄ ▄█ █▄█ █▄▀ █ █▀░ █▀░ █▄█ ▄█ █ █▄█ █░▀█");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d25 = new ModConfigurationKey<dummy>(nameGenerator(5), "");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d24 = new ModConfigurationKey<dummy>(nameGenerator(6), "");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> _d23 = new ModConfigurationKey<dummy>(nameGenerator(7), "");

        //use defult location

        //new location

        // cpu vs gpu

        /*
                                               
 █▀█ █▀▀ █▀ █▀█ █▀▄ █ █▀▀ █▀▀ █░█ █▀ █ █▀█ █▄░█
 █▀▄ ██▄ ▄█ █▄█ █▄▀ █ █▀░ █▀░ █▄█ ▄█ █ █▄█ █░▀█
                                                                                      
         */


    }
}
