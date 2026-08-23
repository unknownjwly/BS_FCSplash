using System.IO;
using IPA.Utilities;
using Newtonsoft.Json;

namespace FCSplash;

public class Config
{
    public static Config Instance { get; set; } = new();

    [JsonProperty("General")]
    public GeneralSettings General { get; set; } = new();

    [JsonProperty("Text Settings")]
    public TextSettings Text { get; set; } = new();

    [JsonProperty("Particle Settings")]
    public ParticleSettings Particles { get; set; } = new();

    [JsonProperty("Animation Settings")]
    public AnimationSettings Animation { get; set; } = new();

    [JsonProperty("Audio Settings")]
    public AudioSettings Audio { get; set; } = new();

    public class GeneralSettings
    {
        public float GlobalScaleMultiplier { get; set; } = 0.7f;
        public float ZPositionDistance { get; set; } = 3.5f;
        public float LevelFinishDelay { get; set; } = 2.0f;
        public bool EnableRoundCorners { get; set; } = true;
        public bool EnableRandomImage { get; set; } = false;
    }

    public class TextSettings
    {
        public bool EnableText { get; set; } = true;
        public bool Enable3DText { get; set; } = true;
        public string TextContent { get; set; } = "FULL COMBO";
        public int FontSize { get; set; } = 72;
        public bool EnableBold { get; set; } = true;
        public bool EnableItalic { get; set; } = true;
        public int TextColorR { get; set; } = 0;   
        public int TextColorG { get; set; } = 255;
        public int TextColorB { get; set; } = 0;   
        public int DepthLayers { get; set; } = 40;
        public float DepthLayerDistance { get; set; } = 1.2f;
    }

    public class ParticleSettings
    {
        public bool EnableSparkles { get; set; } = true;
        public int SparkleColorR { get; set; } = 255;
        public int SparkleColorG { get; set; } = 230;
        public int SparkleColorB { get; set; } = 100;
    }

    public class AnimationSettings
    {
        public float AnimationDuration { get; set; } = 0.4f;
        public string AnimationType { get; set; } = "EaseOutBack";
    }

    public class AudioSettings
    {
        public bool EnableAudio { get; set; } = true;
        public float AudioVolume { get; set; } = 1.0f;
    }

    private static readonly string ConfigPath = Path.Combine(UnityGame.UserDataPath, "FCSplash.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                Instance = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                
                Instance.Text.FontSize = UnityEngine.Mathf.Clamp(Instance.Text.FontSize, 1, 200);
                Instance.Text.TextColorR = UnityEngine.Mathf.Clamp(Instance.Text.TextColorR, 0, 255);
                Instance.Text.TextColorG = UnityEngine.Mathf.Clamp(Instance.Text.TextColorG, 0, 255);
                Instance.Text.TextColorB = UnityEngine.Mathf.Clamp(Instance.Text.TextColorB, 0, 255);
                Instance.Particles.SparkleColorR = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorR, 0, 255);
                Instance.Particles.SparkleColorG = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorG, 0, 255);
                Instance.Particles.SparkleColorB = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorB, 0, 255);
                Instance.Animation.AnimationDuration = UnityEngine.Mathf.Clamp(Instance.Animation.AnimationDuration, 0.1f, 5.0f);
                Instance.Audio.AudioVolume = UnityEngine.Mathf.Clamp(Instance.Audio.AudioVolume, 0.1f, 1.0f);
            }
            else
            {
                Save();
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.Error($"[Config] Failed to load config: {ex.Message}");
            Instance = new Config();
        }
    }

    public static void Save()
    {
        try
        {
            Instance.Text.FontSize = UnityEngine.Mathf.Clamp(Instance.Text.FontSize, 1, 200);
            Instance.Text.TextColorR = UnityEngine.Mathf.Clamp(Instance.Text.TextColorR, 0, 255);
            Instance.Text.TextColorG = UnityEngine.Mathf.Clamp(Instance.Text.TextColorG, 0, 255);
            Instance.Text.TextColorB = UnityEngine.Mathf.Clamp(Instance.Text.TextColorB, 0, 255);
            Instance.Particles.SparkleColorR = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorR, 0, 255);
            Instance.Particles.SparkleColorG = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorG, 0, 255);
            Instance.Particles.SparkleColorB = UnityEngine.Mathf.Clamp(Instance.Particles.SparkleColorB, 0, 255);
            Instance.Animation.AnimationDuration = UnityEngine.Mathf.Clamp(Instance.Animation.AnimationDuration, 0.1f, 5.0f);
            Instance.Audio.AudioVolume = UnityEngine.Mathf.Clamp(Instance.Audio.AudioVolume, 0.1f, 1.0f);

            string json = JsonConvert.SerializeObject(Instance, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.Error($"[Config] Failed to save config: {ex.Message}");
        }
    }
}