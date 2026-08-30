using System;
using System.IO;
using System.Reflection;
using BeatSaberMarkupLanguage;
using IPA.Utilities;
using UnityEngine;

namespace FCSplash.UI;

public static class BSMLHelper
{
    public static void ExtractAndEnsureCache(string fileName)
    {
        try
        {
            string cacheFolder = Path.Combine(UnityGame.PluginsPath, ".cache");
            Directory.CreateDirectory(cacheFolder);
            string destinationPath = Path.Combine(cacheFolder, fileName);

            Assembly assembly = Assembly.GetExecutingAssembly();
            
            // Dynamically find the resource that ends with the filename
            string? matchingResource = null;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingResource = name;
                    break;
                }
            }

            if (matchingResource == null)
            {
                Plugin.Log?.Error($"[BSMLHelper] Could not find any embedded resource ending with: '{fileName}'");
                return;
            }

            using Stream stream = assembly.GetManifestResourceStream(matchingResource);
            if (stream != null)
            {
                using FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fileStream);
                Plugin.Log?.Debug($"[BSMLHelper] Successfully extracted {matchingResource} to cache: {destinationPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Error($"[BSMLHelper] Failed to extract resource to cache: {ex.Message}");
        }
    }

    public static void ParseFromCache(GameObject targetGameObject, object host, string fileName)
    {
        try
        {
            foreach (Transform child in targetGameObject.transform)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            string cachePath = Path.Combine(UnityGame.PluginsPath, ".cache", fileName);
            if (File.Exists(cachePath))
            {
                string content = File.ReadAllText(cachePath);
                BSMLParser.Instance.Parse(content, targetGameObject, host);
            }
            else
            {
                Plugin.Log?.Error($"[BSMLHelper] Cache file not found: {cachePath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Error($"[BSMLHelper] Error parsing cache BSML: {ex.Message}");
        }
    }
}