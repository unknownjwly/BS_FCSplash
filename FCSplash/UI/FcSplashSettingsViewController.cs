using System.IO;
using System.Collections.Generic;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using FCSplash.Features;
using FCSplash.Features.Spawning;
using UnityEngine;
using Zenject;

namespace FCSplash.UI;

[HotReload(RelativePathToLayout = @"..\UI\FcSplashSettingsViewController.bsml")]
[ViewDefinition("FCSplash.UI.FcSplashSettingsViewController.bsml")]

public class FcSplashSettingsViewController : BSMLAutomaticViewController

{
    private GameObject? _previewSplashObj;
    
    private readonly ParticleEffectManager _particleManager = new();

    [UIValue("mod-enabled")]
    public bool ModEnabled
    {
        get => Config.Instance.General.EnableMod;
        set
        {
            Config.Instance.General.EnableMod = value;
            Config.Save();
        }
    }

    [UIValue("global-scale")]
    public float GlobalScaleMultiplier
    {
        get => Config.Instance.General.GlobalScaleMultiplier;
        set
        {
            Config.Instance.General.GlobalScaleMultiplier = value;
            Config.Save();
        }
    }

    [UIValue("z-position")]
    public float ZPositionDistance
    {
        get => Config.Instance.General.ZPositionDistance;
        set
        {
            Config.Instance.General.ZPositionDistance = value;
            Config.Save();
        }
    }

    [UIValue("post-song-wait")]
    public float LevelFinishDelay
    {
        get => Config.Instance.General.LevelFinishDelay;
        set
        {
            Config.Instance.General.LevelFinishDelay = value;
            Config.Save();
        }
    }

    [UIValue("round-corners")]
    public bool EnableRoundCorners
    {
        get => Config.Instance.General.EnableRoundCorners;
        set
        {
            Config.Instance.General.EnableRoundCorners = value;
            Config.Save();
        }
    }

    [UIValue("random-image")]
    public bool EnableRandomImage
    {
        get => Config.Instance.General.EnableRandomImage;
        set
        {
            Config.Instance.General.EnableRandomImage = value;
            Config.Save();
        }
    }


    [UIValue("enable-text")]
    public bool EnableText
    {
        get => Config.Instance.Text.EnableText;
        set
        {
            Config.Instance.Text.EnableText = value;
            Config.Save();
        }
    }

    [UIValue("enable-3d-text")]
    public bool Enable3DText
    {
        get => Config.Instance.Text.Enable3DText;
        set
        {
            Config.Instance.Text.Enable3DText = value;
            Config.Save();
        }
    }

    [UIValue("text-content")]
    public string TextContent
    {
        get => Config.Instance.Text.TextContent;
        set
        {
            Config.Instance.Text.TextContent = value;
            Config.Save();
        }
    }

    [UIValue("enable-bold")]
    public bool EnableBold
    {
        get => Config.Instance.Text.EnableBold;
        set
        {
            Config.Instance.Text.EnableBold = value;
            Config.Save();
        }
    }

    [UIValue("enable-italic")]
    public bool EnableItalic
    {
        get => Config.Instance.Text.EnableItalic;
        set
        {
            Config.Instance.Text.EnableItalic = value;
            Config.Save();
        }
    }

    [UIValue("text-color")]
    public Color TextColor
    {
        get => new Color32(
            (byte)Config.Instance.Text.TextColorR,
            (byte)Config.Instance.Text.TextColorG,
            (byte)Config.Instance.Text.TextColorB,
            255
        );
        set
        {
            Config.Instance.Text.TextColorR = (int)(value.r * 255f);
            Config.Instance.Text.TextColorG = (int)(value.g * 255f);
            Config.Instance.Text.TextColorB = (int)(value.b * 255f);
            Config.Save();
        }
    }

    [UIValue("font-size")]
    public float FontSize
    {
        get => Config.Instance.Text.FontSize;
        set
        {
            Config.Instance.Text.FontSize = (int)value;
            Config.Save();
        }
    }

    [UIValue("depth-layers")]
    public int DepthLayers
    {
        get => Config.Instance.Text.DepthLayers;
        set
        {
            Config.Instance.Text.DepthLayers = value;
            Config.Save();
        }
    }


    [UIValue("enable-effects")]
    public bool EnableSparkles
    {
        get => Config.Instance.Particles.EnableSparkles;
        set
        {
            Config.Instance.Particles.EnableSparkles = value;
            Config.Save();
        }
    }

    [UIValue("effect-color")]
    public Color EffectColor
    {
        get => new Color32(
            (byte)Config.Instance.Particles.SparkleColorR,
            (byte)Config.Instance.Particles.SparkleColorG,
            (byte)Config.Instance.Particles.SparkleColorB,
            255
        );
        set
        {
            Config.Instance.Particles.SparkleColorR = (int)(value.r * 255f);
            Config.Instance.Particles.SparkleColorG = (int)(value.g * 255f);
            Config.Instance.Particles.SparkleColorB = (int)(value.b * 255f);
            Config.Save();
        }
    }


    [UIValue("animation-duration")]
    public float AnimationDuration
    {
        get => Config.Instance.Animation.AnimationDuration;
        set
        {
            Config.Instance.Animation.AnimationDuration = value;
            Config.Save();
        }
    }

    [UIValue("AnimationTypeChoices")]
    public List<object> AnimationTypeChoices => new List<object>
    {
        "Linear",
        "EaseInQuad",
        "EaseOutQuad",
        "EaseInOutQuad",
        "EaseOutBack",
        "Elastic",
        "Bounce"
    };

    [UIValue("SelectedAnimationType")]
    public string SelectedAnimationType
    {
        get => Config.Instance.Animation.AnimationType;
        set
        {
            Config.Instance.Animation.AnimationType = value;
            Config.Save();
        }
    }


    [UIValue("enable-audio")]
    public bool EnableAudio
    {
        get => Config.Instance.Audio.EnableAudio;
        set
        {
            Config.Instance.Audio.EnableAudio = value;
            Config.Save();
        }
    }

    [UIValue("audio-volume")]
    public float AudioVolume
    {
        get => Config.Instance.Audio.AudioVolume;
        set
        {
            Config.Instance.Audio.AudioVolume = value;
            Config.Save();
        }
    }

    [UIValue("random-sound")]
    public bool EnableRandomSound
    {
        get => Config.Instance.Audio.RandomAudio;
        set
        {
            Config.Instance.Audio.RandomAudio = value;
            Config.Save();
        }
    }
    
    [UIAction("RespawnButtonClicked")]
    protected void OnRespawnButtonClicked()
    {
        Plugin.Log.Debug("Respawn button clicked!");
        SpawnPreviewWithEffects();
    }
    
    private FcSplashFlowCoordinator? _flowCoordinator;

    [Inject]
    public void Construct(FcSplashFlowCoordinator flowCoordinator)
    {
        _flowCoordinator = flowCoordinator;
    }

    [UIAction("BackButtonPressed")]
    private void BackButtonPressed()
    {
        Plugin.Log.Debug("BackButtonPressed"); 
        _flowCoordinator?.DismissSelf();
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
        StartCoroutine(SpawnPreviewWithDelay(0.5f));
    }

    protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
    {
        base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);

        if (_previewSplashObj != null)
        {
            Destroy(_previewSplashObj);
            _previewSplashObj = null;
        }
    }

    private Coroutine? _previewDespawnRoutine;

    private System.Collections.IEnumerator SpawnPreviewWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (gameObject.activeInHierarchy)
        {
            SpawnPreviewWithEffects();
        }
    }

    private void SpawnPreviewWithEffects()
    {
        if (_previewSplashObj != null)
        {
            Destroy(_previewSplashObj);
        }
        
        if (_previewDespawnRoutine != null)
        {
            StopCoroutine(_previewDespawnRoutine);
        }

        _previewSplashObj = FcSpawner.SpawnDisplay();

        if (Config.Instance.Particles.EnableSparkles && _previewSplashObj != null)
        {
            _particleManager.TriggerSparkleEffect(_previewSplashObj.transform.position);
        }

        if (Config.Instance.Audio.EnableAudio)
        {
            Plugin.AudioManager.PlaySound();
        }
        
        _previewDespawnRoutine = StartCoroutine(DespawnPreviewAfterDelay(5f));
    }

    private System.Collections.IEnumerator DespawnPreviewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_previewSplashObj != null)
        {
            Destroy(_previewSplashObj);
            _previewSplashObj = null;
        }

        _previewDespawnRoutine = null;
    }
}