using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace FCSplash.Features;

public class SplashAudioManager
{
    private static Dictionary<string, AudioClip> _cachedAudioLibrary = new Dictionary<string, AudioClip>();
    private static List<string> _availableAudioPaths = new List<string>();
    private static string? _lastChosenAudioPath;
    private static AudioSource? _activeAudioSource;

    public static bool IsAudioLoaded => _cachedAudioLibrary.Count > 0;

    public IEnumerator LoadAudioClipRoutine()
    {
        string soundsFolder = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Sounds");

        _cachedAudioLibrary.Clear();
        _availableAudioPaths.Clear();
        _lastChosenAudioPath = null;

        if (!Directory.Exists(soundsFolder))
        {
            Directory.CreateDirectory(soundsFolder);
        }
        else
        {
            string[] supportedExtensions = [ ".wav", ".ogg", ".mp3" ];
            string[] files = Directory.GetFiles(soundsFolder);

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.Exists(supportedExtensions, e => e == ext))
                {
                    _availableAudioPaths.Add(file);
                }
            }
        }

        foreach (string audioPath in _availableAudioPaths)
        {
            string url = "file://" + audioPath;
            AudioType audioType = AudioType.WAV;
            string ext = Path.GetExtension(audioPath).ToLowerInvariant();
            if (ext == ".ogg") audioType = AudioType.OGGVORBIS;
            else if (ext == ".mp3") audioType = AudioType.MPEG;

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    clip.name = Path.GetFileNameWithoutExtension(audioPath);
                    _cachedAudioLibrary[audioPath] = clip;
                }
                else
                {
                    Plugin.Log.Error($"SplashAudioManager: Failed to load custom audio file {Path.GetFileName(audioPath)}: {www.error}");
                }
            }
        }

        if (_cachedAudioLibrary.Count == 0)
        {
            Plugin.Log.Info("SplashAudioManager: No custom audio found or failed to load. Using embedded fallback.");
            AudioClip? fallbackClip = LoadEmbeddedAudioClip("FCSplash.Resources.tick.wav");
            if (fallbackClip != null)
            {
                string fallbackKey = "__FALLBACK__";
                _availableAudioPaths.Add(fallbackKey);
                _cachedAudioLibrary[fallbackKey] = fallbackClip;
            }
        }

        Plugin.Log.Info($"[SplashAudioManager] Successfully preloaded and cached {_cachedAudioLibrary.Count} sound(s) into memory on game boot.");
    }

    public void PlaySound()
    {
        if (!Config.Instance.Audio.EnableAudio || _cachedAudioLibrary.Count == 0)
        {
            return;
        }
        
        StopActiveAudio();

        string? chosenPath = null;

        if (Config.Instance.Audio.RandomAudio && _availableAudioPaths.Count > 1)
        {
            var pool = _availableAudioPaths.Where(p => p != _lastChosenAudioPath).ToList();
            int randomIndex = UnityEngine.Random.Range(0, pool.Count);
            chosenPath = pool[randomIndex];
        }
        else
        {
            string selectedName = Config.Instance.Audio.SelectedAudio;
            if (!string.IsNullOrEmpty(selectedName))
            {
                chosenPath = _availableAudioPaths.FirstOrDefault(p => 
                    p.Equals(selectedName, StringComparison.OrdinalIgnoreCase) || 
                    Path.GetFileName(p).Equals(selectedName, StringComparison.OrdinalIgnoreCase));
            }

            if (chosenPath == null && _availableAudioPaths.Count > 0)
            {
                chosenPath = _availableAudioPaths[0];
            }
        }

        _lastChosenAudioPath = chosenPath;

        if (chosenPath != null && _cachedAudioLibrary.TryGetValue(chosenPath, out AudioClip? clipToPlay) && clipToPlay != null)
        {
            GameObject tempAudioObj = new GameObject("FCSplash_TempAudio");
            tempAudioObj.transform.position = Vector3.zero;
            AudioSource source = tempAudioObj.AddComponent<AudioSource>();
            source.clip = clipToPlay;
            source.volume = Config.Instance.Audio.AudioVolume;
            source.Play();

            _activeAudioSource = source;
            
            UnityEngine.Object.Destroy(tempAudioObj, clipToPlay.length);
        }
    }

    public void StopActiveAudio()
    {
        if (_activeAudioSource != null)
        {
            if (_activeAudioSource.gameObject != null)
            {
                UnityEngine.Object.Destroy(_activeAudioSource.gameObject);
            }
            _activeAudioSource = null;
        }
    }

    public void Cleanup()
    {
        StopActiveAudio();

        foreach (var kvp in _cachedAudioLibrary)
        {
            if (kvp.Value != null)
            {
                UnityEngine.Object.Destroy(kvp.Value);
            }
        }
        _cachedAudioLibrary.Clear();
        _availableAudioPaths.Clear();
        _lastChosenAudioPath = null;
    }

    private static AudioClip? LoadEmbeddedAudioClip(string resourcePath)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null) return null;

                using (BinaryReader reader = new BinaryReader(stream))
                {
                    reader.ReadChars(4);
                    reader.ReadInt32();
                    reader.ReadChars(4);
                    reader.ReadChars(4);
                    reader.ReadInt32();
                    reader.ReadInt16();
                    short numChannels = reader.ReadInt16();
                    int sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    short bitsPerSample = reader.ReadInt16();

                    string subChunk2Id = new string(reader.ReadChars(4));
                    while (subChunk2Id != "data")
                    {
                        int subChunk2Size = reader.ReadInt32();
                        reader.ReadBytes(subChunk2Size);
                        subChunk2Id = new string(reader.ReadChars(4));
                    }

                    int dataSize = reader.ReadInt32();
                    byte[] audioData = reader.ReadBytes(dataSize);

                    float[] floatData = ConvertBytesToFloat(audioData, bitsPerSample);

                    AudioClip clip = AudioClip.Create("TickSoundFallback", floatData.Length / numChannels, numChannels, sampleRate, false);
                    clip.SetData(floatData, 0);
                    return clip;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"SplashAudioManager: Failed to load embedded audio clip: {ex.Message}");
            return null;
        }
    }

    private static float[] ConvertBytesToFloat(byte[] input, int bitsPerSample)
    {
        int sampleCount = input.Length / (bitsPerSample / 8);
        float[] output = new float[sampleCount];

        if (bitsPerSample == 16)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                short val = BitConverter.ToInt16(input, i * 2);
                output[i] = val / 32768f;
            }
        }
        else if (bitsPerSample == 8)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                output[i] = (input[i] - 128f) / 128f;
            }
        }

        return output;
    }
}