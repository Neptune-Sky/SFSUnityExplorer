using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using SFS.Input;
using SFS.UI;
using UnityEngine;

namespace SFSUnityExplorer
{
    public abstract class LibrariesHandler
    {
        private static string directoryWithSeparator;
        private static string libDir;
        private static string runtimeDir;

        public static bool librariesLoadedSuccessfully = true;
        public static List<Assembly> loadedAssemblies = new();
        
        public static void ExtractAndLoadLibraries()
        {
            directoryWithSeparator = Main.modFolder + Path.DirectorySeparatorChar;
            libDir = directoryWithSeparator + "Libraries";
            runtimeDir = libDir + Path.DirectorySeparatorChar + "Runtime";
            
            /*
             On error, only files are deleted and subdirectories usually remain on first delete pass which prevents the
             Libraries folder from fully being removed. Check Runtime folder for files, do a second pass if empty.
             */
            if (Directory.Exists(runtimeDir) && !Directory.EnumerateFileSystemEntries(runtimeDir).Any())
            {
                Directory.Delete(libDir, true);
            }
            
            if (!Directory.Exists(libDir) || !Directory.EnumerateFileSystemEntries(runtimeDir).Any())
            {
                Debug.Log("Unity Explorer: Libraries directory missing files or doesn't exist, trying to extract existing libraries file...");
                if (!File.Exists(directoryWithSeparator + "RequiredLibs.zip"))
                {
                    Debug.LogError($"RequiredLibs.zip Not Found: {Main.modFolder}");
                    librariesLoadedSuccessfully = false;
                    return;
                }

                ZipFile.ExtractToDirectory(directoryWithSeparator + "RequiredLibs.zip", libDir, true);
            }

            try
            {
                loadedAssemblies = LoadUniqueAssemblies(runtimeDir);
            }
            catch (Exception)
            {
                librariesLoadedSuccessfully = false;
                RemoveLibraries(true);
            }
        }

        public static void RemoveLibraries(bool force = false)
        {
            void Delete()
            {
                Application.Unload();
                Directory.Delete(libDir, true);
                File.Delete(directoryWithSeparator + "RequiredLibs.zip");
                Application.Quit();
            }

            if (force)
            {
                Delete();
                return;
            }
            MenuGenerator.ShowChoices(() => "Something went wrong when trying to start Unity Explorer. Please restart the game and press the update button again.",
                ButtonBuilder.CreateButton(null, () => "Close Game", Delete, CloseMode.Stack));
            
        }
        
        
        private static List<Assembly> LoadUniqueAssemblies(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            var dllPaths = Directory.EnumerateFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly);
            loadedAssemblies = new List<Assembly>();

            foreach (var dllPath in dllPaths)
                try
                {
                    // Get assembly name (includes name, version, culture, public key, etc.).
                    var candidateAssemblyName = AssemblyName.GetAssemblyName(dllPath);

                    // Check if a matching assembly (by name/version/token) is already loaded.
                    var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(asm =>
                    {
                        AssemblyName existingName = asm.GetName();
                        return string.Equals(existingName.Name, candidateAssemblyName.Name,
                                   StringComparison.OrdinalIgnoreCase)
                               && existingName.Version == candidateAssemblyName.Version
                               && PublicKeyTokensMatch(existingName, candidateAssemblyName);
                    });

                    if (!alreadyLoaded)
                    {
                        Assembly loaded = Assembly.LoadFrom(dllPath);
                        loadedAssemblies.Add(loaded);
                    }
                }
                catch (Exception ex)
                {
                    // Could be invalid .NET assembly, missing dependencies, etc.
                    Console.WriteLine($"Could not load assembly from '{dllPath}': {ex.Message}");
                }

            return loadedAssemblies;
        }

        /// <summary>
        ///     Calls a method in the specified assembly from a list of already-loaded assemblies.
        /// </summary>
        /// <param name="assemblies">List of assemblies (e.g., from LoadUniqueAssemblies).</param>
        /// <param name="targetAssemblyName">Simple name of the assembly (without ".dll"). Example: "MyPluginAssembly".</param>
        /// <param name="typeName">Fully qualified type name. Example: "MyPluginNamespace.MyClass".</param>
        /// <param name="methodName">The method to call on that type.</param>
        /// <param name="methodParams">Parameters for the method (or null if none).</param>
        /// <param name="isStatic">True if the target method is static; otherwise, an instance will be created.</param>
        /// <returns>The result of the method call (null if void).</returns>
        public static object CallMethodFromAssembly(
            List<Assembly> assemblies,
            string targetAssemblyName,
            string typeName,
            string methodName,
            object[] methodParams = null,
            bool isStatic = true)
        {
            // Find the target assembly by simple name (case-insensitive).
            Assembly targetAssembly = assemblies.FirstOrDefault(asm =>
                asm.GetName().Name.Equals(targetAssemblyName, StringComparison.OrdinalIgnoreCase));

            if (targetAssembly == null)
                throw new InvalidOperationException($"Assembly '{targetAssemblyName}' not found in the provided list.");

            // Get the type.
            Type targetType = targetAssembly.GetType(typeName, false);
            if (targetType == null)
                throw new TypeLoadException($"Type '{typeName}' not found in assembly '{targetAssemblyName}'.");

            // Get the method info (public static).
            MethodInfo method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
                throw new MissingMethodException($"Method '{methodName}' not found in type '{typeName}'.");

            // If it's not static, create an instance.
            object instance = null;
            if (!isStatic) instance = Activator.CreateInstance(targetType);

            // Invoke the method, return any result.
            return method.Invoke(instance, methodParams);
        }

        // Helper method to compare public key tokens (or handle null).
        private static bool PublicKeyTokensMatch(AssemblyName a, AssemblyName b)
        {
            var tokenA = a.GetPublicKeyToken() ?? Array.Empty<byte>();
            var tokenB = b.GetPublicKeyToken() ?? Array.Empty<byte>();
            return tokenA.SequenceEqual(tokenB);
        }
    }
}