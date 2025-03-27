using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using ModLoader;
using SFS.Input;
using SFS.IO;
using SFS.UI;
using UITools;
using UnityEngine;

namespace SFSUnityExplorer
{
    [UsedImplicitly]
    public class Main : Mod, IUpdatable
    {
        public override string ModNameID => "SFSUnityExplorer";
        public override string DisplayName => "Unity Explorer";
        public override string Author => "sinai-dev, NeptuneSky";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v1.0";

        public override string Description =>
            "Implementation of sinai-dev's UnityExplorer into SFS. See https://github.com/sinai-dev/UnityExplorer";

        public override Dictionary<string, string> Dependencies => new() { { "UITools", "1.0" } };

        public Dictionary<string, FilePath> UpdatableFiles => new()
        {
            {
                "",
                new FolderPath(ModFolder).ExtendToFile("UnityExplorer.dll")
            },
            {
                "https://github.com/sinai-dev/UnityExplorer/releases/latest/download/UnityExplorer.Editor.zip",
                new FolderPath(ModFolder).ExtendToFile("RequiredLibs.zip")
            }
        };

        public static string modFolder;
        
        public override void Early_Load()
        {
            modFolder = ModFolder;
            
            LibrariesHandler.ExtractAndLoadLibraries();
        }

        public override void Load()
        {
            if (!LibrariesHandler.librariesLoadedSuccessfully) return;

            try
            {
                LibrariesHandler.CallMethodFromAssembly(
                    LibrariesHandler.loadedAssemblies,
                    "UnityExplorer.STANDALONE.Mono",
                    "UnityExplorer.ExplorerStandalone",
                    "CreateInstance");
            }
            catch (Exception)
            {
                LibrariesHandler.RemoveLibraries();
            }
            
        }
    }
}