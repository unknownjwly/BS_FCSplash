using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;

namespace FCSplash;

public class FcSpawnManager : IInitializable, IDisposable
{
    [Inject] private readonly BeatmapObjectManager _beatmapObjectManager = null!;
    [Inject] private readonly IReadonlyBeatmapData _beatmapData = null!;

    private bool _isFullCombo = true;
    private int _totalValidNotes = 0;
    private int _processedNotes = 0;
    private bool _hasTriggeredFc = false;
    private GameObject? _splashCanvasObj;
    private AudioClip? _tickAudioClip;
    
    public bool HasTriggeredFc => _hasTriggeredFc;

    public void Initialize()
    {
        Config.Load();

        _isFullCombo = true;
        _processedNotes = 0;
        _hasTriggeredFc = false;
        _splashCanvasObj = null;

        _totalValidNotes = _beatmapData.GetBeatmapDataItems<NoteData>(0)
            .Where(noteData => noteData.gameplayType != NoteData.GameplayType.Bomb)
            .Count();

        Plugin.Log.Info($"FcSpawnManager Initialized. Total notes: {_totalValidNotes}");
        
        if (Config.Instance.Audio.EnableAudio)
        {
            CoroutineHost.Start(LoadAudioClipRoutine());
        }

        CoroutineHost.Start(PreloadSplashRoutine());

        _beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent += OnNoteWasMissed;
    }

    public void Dispose()
    {
        Plugin.Log.Info($"FcSpawnManager deinitialized. Combo: {_processedNotes}/{_totalValidNotes}");
        _beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent -= OnNoteWasMissed;

        if (_splashCanvasObj != null)
        {
            UnityEngine.Object.Destroy(_splashCanvasObj);
        }
        
        if (_tickAudioClip != null)
        {
            UnityEngine.Object.Destroy(_tickAudioClip);
        }
    }

    private IEnumerator LoadAudioClipRoutine()
    {
        string soundsFolder = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Sounds");
        AudioClip? loadedClip = null;

        if (Directory.Exists(soundsFolder))
        {
            string[] supportedExtensions = [ ".wav", ".ogg", ".mp3" ];
            string? customAudioFile = Directory.GetFiles(soundsFolder)
                .FirstOrDefault(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()));

            if (customAudioFile != null)
            {
                Plugin.Log.Info($"FcSpawnManager: Found custom audio file: {customAudioFile}");
                string url = "file://" + customAudioFile;
                
                AudioType audioType = AudioType.WAV;
                string ext = Path.GetExtension(customAudioFile).ToLowerInvariant();
                if (ext == ".ogg") audioType = AudioType.OGGVORBIS;
                else if (ext == ".mp3") audioType = AudioType.MPEG;

                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
                {
                    yield return www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        loadedClip = DownloadHandlerAudioClip.GetContent(www);
                        loadedClip.name = "CustomSplashSound";
                    }
                    else
                    {
                        Plugin.Log.Error($"FcSpawnManager: Failed to load custom audio file: {www.error}");
                    }
                }
            }
        }
        
        if (loadedClip == null)
        {
            Plugin.Log.Info("FcSpawnManager: No custom audio found or failed to load. Using embedded fallback.");
            loadedClip = LoadEmbeddedAudioClip("FCSplash.Resources.tick.wav");
        }

        _tickAudioClip = loadedClip;
    }

    private System.Collections.IEnumerator PreloadSplashRoutine()
    {
        yield return null;
        _splashCanvasObj = FcSpawner.SpawnDisplay();
        if (_splashCanvasObj != null)
        {
            _splashCanvasObj.SetActive(false);
        }
    }

    private void OnNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
    {
        if (_hasTriggeredFc) return;

        NoteData noteData = noteController.noteData;

        if (noteData.gameplayType == NoteData.GameplayType.Bomb)
        {
            _isFullCombo = false;
            Plugin.Log.Info("FcSpawnManager: Full Combo lost - Hit a bomb!");
            return;
        }

        _processedNotes++;
        if (!noteCutInfo.allIsOK)
        {
            _isFullCombo = false;
            Plugin.Log.Info("FcSpawnManager: Full Combo lost - Bad cut!");
        }

        CheckCompletion();
    }

    private void OnNoteWasMissed(NoteController noteController)
    {
        if (_hasTriggeredFc) return;

        if (noteController.noteData.gameplayType != NoteData.GameplayType.Bomb)
        {
            _processedNotes++;
            _isFullCombo = false;
            Plugin.Log.Info("FcSpawnManager: Full Combo lost - Note missed!");
        }

        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (_processedNotes >= _totalValidNotes)
        {
            if (_isFullCombo && !_hasTriggeredFc)
            {
                _hasTriggeredFc = true;
                Plugin.Log.Info("FcSpawnManager: Full Combo'd!");
                
                if (_splashCanvasObj != null)
                {
                    _splashCanvasObj.SetActive(true);
                }
                else
                {
                    _splashCanvasObj = FcSpawner.SpawnDisplay();
                }

                if (Config.Instance.Particles.EnableSparkles && _splashCanvasObj != null)
                {
                    TriggerSparkleEffect(_splashCanvasObj.transform.position);
                }
                
                if (Config.Instance.Audio.EnableAudio && _tickAudioClip != null)
                {
                    AudioSource.PlayClipAtPoint(_tickAudioClip, Vector3.zero, Config.Instance.Audio.AudioVolume);
                }
            }
        }
    }

    private void TriggerSparkleEffect(Vector3 position)
    {
        try
        {
            GameObject sparkleObj = new GameObject("FC_SparkleParticles");
            sparkleObj.transform.position = position;

            ParticleSystem ps = sparkleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2.0f;
            main.startLifetime = 3.5f;
            main.startSpeed = 5.0f;
            main.startSize = 0.06f;
            main.maxParticles = 300;
            main.loop = false;
            main.gravityModifier = 0.5f;

            var cfg = Config.Instance.Particles;
            Color32 sparkleColor = new Color32(
                (byte)cfg.SparkleColorR, 
                (byte)cfg.SparkleColorG, 
                (byte)cfg.SparkleColorB, 
                255
            );
            main.startColor = new ParticleSystem.MinMaxGradient(sparkleColor);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 200) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = sparkleObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Shader? shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                }
                else
                {
                    Plugin.Log.Error("FcSpawnManager: Could not find a valid shader for particles!");
                }
            }

            ps.Play();

            UnityEngine.Object.Destroy(sparkleObj, 5.0f);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"FcSpawnManager: Failed to create particle effect: {ex.Message}");
        }
    }

    private AudioClip? LoadEmbeddedAudioClip(string resourcePath)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null)
                {
                    Plugin.Log.Error($"FcSpawnManager: Could not find embedded resource: {resourcePath}");
                    return null;
                }

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
            Plugin.Log.Error($"FcSpawnManager: Failed to load embedded audio clip: {ex.Message}");
            return null;
        }
    }

    private float[] ConvertBytesToFloat(byte[] input, int bitsPerSample)
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
