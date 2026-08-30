#pragma warning disable CS0649 // Field is never assigned (handled via BSML reflection)
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using IPA.Utilities;
using UnityEngine.Networking;
using Zenject;

namespace FCSplash.UI;

public class FcImageAudioViewController : BSMLAutomaticViewController
{
    [UIValue("image-row-list")] private List<object> _imageRowList = new();
    [UIValue("audio-list")] private List<object> _audioList = new();
    [UIValue("image-choices")] private List<object> _imageChoices = new();
    [UIValue("audio-choices")] private List<object> _audioChoices = new();

    [UIValue("has-images")] public bool HasImages => _imageRowList.Count > 0;
    [UIValue("has-no-images")] public bool HasNoImages => _imageRowList.Count == 0;

    [UIValue("has-audio")] public bool HasAudio => _audioList.Count > 0;
    [UIValue("has-no-audio")] public bool HasNoAudio => _audioList.Count == 0;

    private int _imageCount;
    [UIValue("image-count-text")]
    public string ImageCountText => $"{_imageCount} Image{(_imageCount == 1 ? "" : "s")}";

    private int _audioCount;
    [UIValue("audio-count-text")]
    public string AudioCountText => $"{_audioCount} Audio File{(_audioCount == 1 ? "" : "s")}";

    [UIValue("selected-image-display")]
    public string SelectedImageDisplay => string.IsNullOrEmpty(Config.Instance.General.SelectedImage) 
        ? "None" 
        : Path.GetFileName(Config.Instance.General.SelectedImage);

    [UIValue("selected-image-choice")]
    private string _selectedImageChoice
    {
        get => Path.GetFileName(Config.Instance.General.SelectedImage ?? string.Empty);
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                string fullPath = Path.Combine(_imagesPath, value);
                Config.Instance.General.SelectedImage = fullPath;
                Config.Save();
                Plugin.Log.Debug($"[FCSplash] Selected image updated to: {fullPath}");
                NotifyPropertyChanged(nameof(SelectedImageDisplay));
            }
        }
    }

    [UIValue("selected-audio-display")]
    public string SelectedAudioDisplay => string.IsNullOrEmpty(Config.Instance.Audio.SelectedAudio) 
        ? "None" 
        : Path.GetFileName(Config.Instance.Audio.SelectedAudio);

    [UIValue("selected-audio-choice")]
    private string _selectedAudioChoice
    {
        get => Path.GetFileName(Config.Instance.Audio.SelectedAudio ?? string.Empty);
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                string fullPath = Path.Combine(_audioPath, value);
                Config.Instance.Audio.SelectedAudio = fullPath;
                Config.Save();
                Plugin.Log.Debug($"[FCSplash] Selected audio updated to: {fullPath}");
                NotifyPropertyChanged(nameof(SelectedAudioDisplay));
            }
        }
    }

    [UIComponent("imageList")] private CustomListTableData? _imageTableData;
    [UIComponent("audioList")] private CustomListTableData? _audioTableData;

    private readonly string _imagesPath = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Images & Gifs");
    private readonly string _audioPath = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Sounds");
    private readonly string _fallbackIconPath = Path.Combine(Path.GetTempPath(), "pixel.png");
    
    private static AudioSource? _previewAudioSource;
    private FcSplashFlowCoordinator? _flowCoordinator;

    [Inject]
    public void Construct(FcSplashFlowCoordinator flowCoordinator)
    {
        _flowCoordinator = flowCoordinator;
    }

    protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
    {
        Plugin.Log.Debug($"[FCSplash] FcImageAudioViewController DidActivate");

        ExtractFallbackIconIfNeeded();
        RefreshImageAndAudioLists();
        
        BSMLHelper.ParseFromCache(gameObject, this, "FcImageAudioViewController.bsml");
    }

    private void ExtractFallbackIconIfNeeded()
    {
        if (File.Exists(_fallbackIconPath)) return;
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("FCSplash.Resources.UI.pixel.png"))
            {
                if (stream != null)
                {
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    File.WriteAllBytes(_fallbackIconPath, data);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to extract fallback icon to temp: {ex.Message}");
        }
    }

    [UIAction("reloadImages/Sounds")]
    protected void OnReloadClicked()
    {
        Plugin.Log.Debug("[FCSplash] Rescan & Restart button clicked.");
        RefreshImageAndAudioLists();

        if (_flowCoordinator != null)
        {
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(_flowCoordinator, () =>
            {
                SharedCoroutineStarter.instance.StartCoroutine(RestartAfterDelayRoutine());
            }, HMUI.ViewController.AnimationDirection.Horizontal, false);
        }
    }

    private System.Collections.IEnumerator RestartAfterDelayRoutine()
    {
        yield return new WaitForSecondsRealtime(0.01f);
        if (_flowCoordinator != null)
        {
            BeatSaberUI.MainFlowCoordinator.PresentFlowCoordinator(_flowCoordinator);
        }
    }
    
    [UIComponent("githubModal")] private HMUI.ModalView? githubModal;
    [UIComponent("deleteModal")] private HMUI.ModalView? _deleteModal;
    private string? _pendingDeletePath;

    public FcImageAudioViewController() {}

    public FcImageAudioViewController(ModalView? githubModal)
    {
        this.githubModal = githubModal;
    }

    [UIAction("open-github-modal")]
    protected void OnOpenGitHubModalClicked()
    {
        this.githubModal?.Show(true, true);
    }

    [UIAction("close-github-modal")]
    protected void OnCloseGitHubModalClicked()
    {
        this.githubModal?.Hide(true, null);
    }

    [UIAction("confirm-github")]
    protected void OnConfirmGitHubClicked()
    {
        this.githubModal?.Hide(true, null);
        string repoUrl = "https://github.com/unknownjwly/BS_FCSplash";
        Plugin.Log.Debug($"[FCSplash] Opening GitHub repository: {repoUrl}");
        Application.OpenURL(repoUrl);
    }

    private void PromptDelete(string filePath)
    {
        _pendingDeletePath = filePath;
        _deleteModal?.Show(true, true);
    }

    [UIAction("confirm-delete")]
    protected void OnConfirmDeleteClicked()
    {
        _deleteModal?.Hide(true, null);

        if (!string.IsNullOrEmpty(_pendingDeletePath) && File.Exists(_pendingDeletePath))
        {
            try
            {
                bool success = RecycleBinHelper.MoveToRecycleBin(_pendingDeletePath!);
                if (success)
                {
                    Plugin.Log.Debug($"[FCSplash] Moved file to Recycle Bin: {_pendingDeletePath}");
                }
                else
                {
                    Plugin.Log.Error($"[FCSplash] Windows API failed to move file to Recycle Bin.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[FCSplash] Failed to send file to Recycle Bin: {ex.Message}");
            }
        }

        _pendingDeletePath = null;
        RefreshImageAndAudioLists();

        if (_flowCoordinator != null)
        {
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(_flowCoordinator, () =>
            {
                SharedCoroutineStarter.instance.StartCoroutine(RestartAfterDelayRoutine());
            }, HMUI.ViewController.AnimationDirection.Horizontal, false);
        }
    }

    [UIAction("cancel-delete")]
    protected void OnCancelDeleteClicked()
    {
        _pendingDeletePath = null;
        _deleteModal?.Hide(true, null);
    }

    private void SelectImage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        Config.Instance.General.SelectedImage = filePath;
        Config.Save();
        NotifyPropertyChanged(nameof(SelectedImageDisplay));

        foreach (var item in _imageRowList.OfType<ImageRowItemData>())
        {
            item.RefreshSelectionState();
        }

        Plugin.Log.Debug($"[FCSplash] Selected image updated to: {filePath}");
    }

    private void SelectAudio(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        Config.Instance.Audio.SelectedAudio = filePath;
        Config.Save();
        NotifyPropertyChanged(nameof(SelectedAudioDisplay));

        foreach (var item in _audioList.OfType<AudioRowItemData>())
        {
            item.RefreshSelectionState();
        }

        Plugin.Log.Debug($"[FCSplash] Selected audio updated to: {filePath}");
    }

    private void RefreshImageAndAudioLists()
    {
        Directory.CreateDirectory(_imagesPath);
        Directory.CreateDirectory(_audioPath);

        _imageRowList.Clear();
        _audioList.Clear();
        _imageChoices.Clear();
        _audioChoices.Clear();

        var imageFiles = Directory.GetFiles(_imagesPath, "*.*")
            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".gif") || f.EndsWith(".jpeg") || f.EndsWith(".webp"))
            .ToArray();

        _imageCount = imageFiles.Length;
        NotifyPropertyChanged(nameof(ImageCountText));

        foreach (var file in imageFiles)
        {
            _imageChoices.Add(Path.GetFileName(file));
        }

        for (int i = 0; i < imageFiles.Length; i += 2)
        {
            string leftFile = imageFiles[i];
            string? rightFile = (i + 1 < imageFiles.Length) ? imageFiles[i + 1] : null;

            _imageRowList.Add(new ImageRowItemData(leftFile, rightFile, _fallbackIconPath, PromptDelete, SelectImage, () => Config.Instance.General.SelectedImage));
        }

        var audioFiles = Directory.GetFiles(_audioPath, "*.*")
            .Where(f => f.EndsWith(".ogg") || f.EndsWith(".wav") || f.EndsWith(".mp3"))
            .ToArray();

        _audioCount = audioFiles.Length;
        NotifyPropertyChanged(nameof(AudioCountText));

        foreach (var file in audioFiles)
        {
            _audioChoices.Add(Path.GetFileName(file));
        }

        for (int i = 0; i < audioFiles.Length; i += 2)
        {
            string leftFile = audioFiles[i];
            string? rightFile = (i + 1 < audioFiles.Length) ? audioFiles[i + 1] : null;

            _audioList.Add(new AudioRowItemData(leftFile, rightFile, PlayAudioPreview, PromptDelete, SelectAudio, () => Config.Instance.Audio.SelectedAudio));
        }

        NotifyPropertyChanged(nameof(HasImages));
        NotifyPropertyChanged(nameof(HasNoImages));
        NotifyPropertyChanged(nameof(HasAudio));
        NotifyPropertyChanged(nameof(HasNoAudio));

        if (_imageTableData == null || _imageTableData.TableView == null || _audioTableData == null || _audioTableData.TableView == null)
        {
            var allTables = GetComponentsInChildren<CustomListTableData>(true);
            if (allTables.Length >= 2)
            {
                _imageTableData ??= allTables[0];
                _audioTableData ??= allTables[1];
            }
        }
        
        if (_imageTableData?.TableView != null)
        {
            _imageTableData.TableView.SetField("_isInitialized", false);
            _imageTableData.TableView.ReloadData();
        }

        if (_audioTableData?.TableView != null)
        {
            _audioTableData.TableView.SetField("_isInitialized", false);
            _audioTableData.TableView.ReloadData();
        }
        
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    private void PlayAudioPreview(string filePath)
    {
        SharedCoroutineStarter.instance.StartCoroutine(LoadAndPlayAudio(filePath));
    }

    private System.Collections.IEnumerator LoadAndPlayAudio(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "file://" || !File.Exists(path.Replace("file://", "")))
        {
            yield break;
        }

        string cleanPath = path.Replace("file://", "");
        string url = "file://" + cleanPath;
        AudioType audioType = AudioType.WAV;

        string lowerPath = cleanPath.ToLower();
        if (lowerPath.EndsWith(".ogg"))
        {
            audioType = AudioType.OGGVORBIS;
        }
        else if (lowerPath.EndsWith(".mp3"))
        {
            audioType = AudioType.MPEG;
        }
        else if (lowerPath.EndsWith(".wav"))
        {
            audioType = AudioType.WAV;
        }

        using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Plugin.Log.Error($"[FCSplash] Failed to load audio preview: {www.error}");
        }
        else
        {
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                Plugin.Log.Error("[FCSplash] Downloaded audio clip was null.");
                yield break;
            }

            if (_previewAudioSource == null)
            {
                GameObject audioObj = new GameObject("FCSplash_AudioPreview");
                _previewAudioSource = audioObj.AddComponent<AudioSource>();
                UnityEngine.Object.DontDestroyOnLoad(audioObj);
            }

            _previewAudioSource.Stop();
            _previewAudioSource.clip = clip;
            _previewAudioSource.Play();
        }
    }
}

public class ImageRowItemData : INotifyPropertyChanged
{
    private readonly string _leftFilePath = string.Empty;
    private readonly string _rightFilePath = string.Empty;
    private readonly string _fallbackPath;
    private readonly Func<string> _getSelectedPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    [UIValue("has-left")] 
    public bool HasLeft => !string.IsNullOrEmpty(_leftFilePath);

    public bool IsLeftSelected => !string.IsNullOrEmpty(_leftFilePath) && string.Equals(_leftFilePath, _getSelectedPath(), StringComparison.OrdinalIgnoreCase);

    [UIValue("left-name")] 
    public string LeftName { get; set; } = string.Empty;

    [UIValue("left-file-path")] 
    public string LeftFilePath => !string.IsNullOrEmpty(_leftFilePath) ? _leftFilePath : _fallbackPath;

    [UIValue("has-right")] 
    public bool HasRight => !string.IsNullOrEmpty(_rightFilePath);

    public bool IsRightSelected => !string.IsNullOrEmpty(_rightFilePath) && string.Equals(_rightFilePath, _getSelectedPath(), StringComparison.OrdinalIgnoreCase);

    [UIValue("right-name")] 
    public string RightName { get; set; } = string.Empty;

    [UIValue("right-file-path")] 
    public string RightFilePath => !string.IsNullOrEmpty(_rightFilePath) ? _rightFilePath : _fallbackPath;
    
    [UIValue("left-color")] 
    public string LeftColor => IsLeftSelected ? "#00FF00" : "#FFFFFF";

    [UIValue("right-color")] 
    public string RightColor => IsRightSelected ? "#00FF00" : "#FFFFFF";

    private readonly Action<string>? _onDelete;
    private readonly Action<string>? _onSelect;

    public ImageRowItemData(string leftPath, string? rightPath, string fallbackPath, Action<string>? onDelete, Action<string>? onSelect, Func<string> getSelectedPath)
    {
        _fallbackPath = fallbackPath;
        _onDelete = onDelete;
        _onSelect = onSelect;
        _getSelectedPath = getSelectedPath;

        if (!string.IsNullOrEmpty(leftPath))
        {
            _leftFilePath = leftPath;
            string fileName = Path.GetFileName(leftPath);
            LeftName = fileName.Length > 15 ? fileName.Substring(0, 13) + "..." : fileName;
        }

        if (!string.IsNullOrEmpty(rightPath))
        {
            _rightFilePath = rightPath!;
            string fileName = Path.GetFileName(rightPath);
            RightName = fileName.Length > 15 ? fileName.Substring(0, 13) + "..." : fileName;
        }
    }

    public void RefreshSelectionState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightColor)));
    }

    [UIAction("left-delete")]
    protected void OnLeftDelete() => _onDelete?.Invoke(_leftFilePath);

    [UIAction("left-select")]
    protected void OnLeftSelect() => _onSelect?.Invoke(_leftFilePath);

    [UIAction("right-delete")]
    protected void OnRightDelete() => _onDelete?.Invoke(_rightFilePath);

    [UIAction("right-select")]
    protected void OnRightSelect() => _onSelect?.Invoke(_rightFilePath);
}

public class AudioRowItemData : INotifyPropertyChanged
{
    private readonly string _leftFilePath = string.Empty;
    private readonly string _rightFilePath = string.Empty;
    private readonly Func<string> _getSelectedPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    [UIValue("has-left")] 
    public bool HasLeft => !string.IsNullOrEmpty(_leftFilePath);

    public bool IsLeftSelected => !string.IsNullOrEmpty(_leftFilePath) && string.Equals(_leftFilePath, _getSelectedPath(), StringComparison.OrdinalIgnoreCase);

    [UIValue("left-name")] 
    public string LeftName { get; set; } = string.Empty;

    [UIValue("has-right")] 
    public bool HasRight => !string.IsNullOrEmpty(_rightFilePath);

    public bool IsRightSelected => !string.IsNullOrEmpty(_rightFilePath) && string.Equals(_rightFilePath, _getSelectedPath(), StringComparison.OrdinalIgnoreCase);

    [UIValue("right-name")] 
    public string RightName { get; set; } = string.Empty;
    
    [UIValue("left-color")] 
    public string LeftColor => IsLeftSelected ? "#00FF00" : "#FFFFFF";

    [UIValue("right-color")] 
    public string RightColor => IsRightSelected ? "#00FF00" : "#FFFFFF";

    private readonly Action<string>? _onPreview;
    private readonly Action<string>? _onDelete;
    private readonly Action<string>? _onSelect;

    public AudioRowItemData(string leftPath, string? rightPath, Action<string>? onPreview, Action<string>? onDelete, Action<string>? onSelect, Func<string> getSelectedPath)
    {
        _onPreview = onPreview;
        _onDelete = onDelete;
        _onSelect = onSelect;
        _getSelectedPath = getSelectedPath;

        if (!string.IsNullOrEmpty(leftPath))
        {
            _leftFilePath = leftPath;
            string fileName = Path.GetFileName(leftPath);
            LeftName = fileName.Length > 15 ? fileName.Substring(0, 13) + "..." : fileName;
        }

        if (!string.IsNullOrEmpty(rightPath))
        {
            _rightFilePath = rightPath!;
            string fileName = Path.GetFileName(rightPath);
            RightName = fileName.Length > 15 ? fileName.Substring(0, 13) + "..." : fileName;
        }
    }

    public void RefreshSelectionState()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RightColor)));
    }

    [UIAction("left-preview")]
    protected void OnLeftPreview() => _onPreview?.Invoke(_leftFilePath);

    [UIAction("left-delete")]
    protected void OnLeftDelete() => _onDelete?.Invoke(_leftFilePath);

    [UIAction("left-select")]
    protected void OnLeftSelect() => _onSelect?.Invoke(_leftFilePath);

    [UIAction("right-preview")]
    protected void OnRightPreview() => _onPreview?.Invoke(_rightFilePath);

    [UIAction("right-delete")]
    protected void OnRightDelete() => _onDelete?.Invoke(_rightFilePath);

    [UIAction("right-select")]
    protected void OnRightSelect() => _onSelect?.Invoke(_rightFilePath);
}