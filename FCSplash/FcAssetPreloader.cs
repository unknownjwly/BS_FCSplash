using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using IPA.Utilities;
using Zenject;

namespace FCSplash;

public class FcAssetPreloader : IInitializable
{
    private static readonly Dictionary<string, Sprite> ImageCache = new();
    private static readonly Dictionary<string, AudioClip> AudioCache = new();
    
    public void Initialize()
    {
        SharedCoroutineStarter.instance.StartCoroutine(PreloadEverythingRoutine());
    }

    private IEnumerator PreloadEverythingRoutine()
    {
        string modFolder = Path.Combine(UnityGame.UserDataPath, "FCSplash");
        string imagesFolder = Path.Combine(modFolder, "Images & Gifs");
        string soundsFolder = Path.Combine(modFolder, "Sounds");

        ImageCache.Clear();
        AudioCache.Clear();

        // Preload Images
        if (Directory.Exists(imagesFolder))
        {
            string[] imageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
            foreach (string file in Directory.GetFiles(imagesFolder))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (System.Array.Exists(imageExtensions, e => e == ext))
                {
                    try
                    {
                        byte[] fileData = File.ReadAllBytes(file);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(fileData))
                        {
                            tex.filterMode = FilterMode.Bilinear;
                            tex.wrapMode = TextureWrapMode.Clamp;
                            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                            ImageCache[file] = sprite;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log.Error($"[FcAssetPreloader] Failed to cache image {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
        }

        // Preload Audio
        if (Directory.Exists(soundsFolder))
        {
            string[] audioExtensions = [".ogg", ".wav", ".mp3"];
            foreach (string file in Directory.GetFiles(soundsFolder))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (System.Array.Exists(audioExtensions, e => e == ext))
                {
                    AudioType audioType = ext switch
                    {
                        ".ogg" => AudioType.OGGVORBIS,
                        ".mp3" => AudioType.MPEG,
                        _ => AudioType.WAV
                    };

                    string url = "file://" + file;
                    using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                        clip.name = Path.GetFileNameWithoutExtension(file);
                        AudioCache[file] = clip;
                    }
                    else
                    {
                        Plugin.Log.Error($"[FcAssetPreloader] Failed to cache audio {Path.GetFileName(file)}: {www.error}");
                    }
                }
            }
        }

        Plugin.Log.Info($"[FCSplash] Preloaded {ImageCache.Count} image(s) and {AudioCache.Count} audio clip(s) on game startup.");
        
        yield break;
    }

    public static Sprite? GetCachedImage(string filePath) => ImageCache.TryGetValue(filePath, out var sprite) ? sprite : null;
    public static AudioClip? GetCachedAudio(string filePath) => AudioCache.TryGetValue(filePath, out var clip) ? clip : null;
}