using System;
using UnityEngine;
using Zenject;

namespace FCSplash;

public class FcSpawnManager : IInitializable, IDisposable, ITickable
{
    [Inject] private readonly IComboController _comboController = null!;
    [Inject] private readonly AudioTimeSyncController _audioTimeSyncController = null!;
    [Inject] private readonly BeatmapObjectManager _beatmapObjectManager = null!;
    [Inject] private readonly GameplayCoreSceneSetupData _sceneSetupData = null!;

    private bool _isFullCombo = true;
    private bool _hasTriggeredFc = false;
    private float _songDuration = 0f;
    private GameObject? _splashCanvasObj;

    public void Initialize()
    {
        _isFullCombo = true;
        _hasTriggeredFc = false;
        _songDuration = _sceneSetupData.beatmapLevel.songDuration;
        _splashCanvasObj = null;

        _comboController.comboBreakingEventHappenedEvent += OnComboBreak;
        _beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent += OnNoteWasMissed;
        
        Plugin.Log.Info($"FcSpawnManager Initialized. Song duration: {_songDuration}s");
    }

    public void Dispose()
    {
        if (_comboController != null)
        {
            _comboController.comboBreakingEventHappenedEvent -= OnComboBreak;
        }
            
        if (_beatmapObjectManager != null)
        {
            _beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
            _beatmapObjectManager.noteWasMissedEvent -= OnNoteWasMissed;
        }

        if (_splashCanvasObj != null)
        {
            UnityEngine.Object.Destroy(_splashCanvasObj);
        }
    }

    public void Tick()
    {
        if (_hasTriggeredFc) return;

        // Check if song has ended (with small buffer for audio fade)
        float currentTime = _audioTimeSyncController.songTime;
        if (currentTime >= _songDuration - 0.5f)
        {
            _hasTriggeredFc = true;
            if (_isFullCombo)
            {
                _splashCanvasObj = FcSpawner.SpawnDisplay();
                Plugin.Log.Info("Full Combo achieved! Triggering splash.");
            }
            else
            {
                Plugin.Log.Info("Song ended but Full Combo was lost.");
            }
        }
    }

    private void OnComboBreak()
    {
        if (_isFullCombo)
        {
            _isFullCombo = false;
            Plugin.Log.Info("FcSpawnManager: Full Combo lost!");
        }
    }

    private void OnNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
    {
        // Bad cuts on colored notes break combo
        if (!noteCutInfo.allIsOK && noteController.noteData.colorType != ColorType.None)
        {
            _isFullCombo = false;
        }
    }

    private void OnNoteWasMissed(NoteController noteController)
    {
        // Missing any colored note breaks combo
        if (noteController.noteData.colorType != ColorType.None)
        {
            _isFullCombo = false;
        }
    }
}
