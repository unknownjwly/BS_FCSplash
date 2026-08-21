using System;
using System.Linq;
using UnityEngine;
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
    
    public bool HasTriggeredFc => _hasTriggeredFc;

    public void Initialize()
    {
        _isFullCombo = true;
        _processedNotes = 0;
        _hasTriggeredFc = false;
        _splashCanvasObj = null;

        _totalValidNotes = _beatmapData.GetBeatmapDataItems<NoteData>(0)
            .Where(noteData => noteData.gameplayType != NoteData.GameplayType.Bomb)
            .Count();

        Plugin.Log.Info($"FcSpawnManager Initialized. Total valid notes: {_totalValidNotes}");
        
        CoroutineHost.Start(PreloadSplashRoutine());

        _beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent += OnNoteWasMissed;
    }

    public void Dispose()
    {
        Plugin.Log.Info($"FcSpawnManager Disposed. Combo: {_processedNotes}/{_totalValidNotes}");
        _beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent -= OnNoteWasMissed;

        if (_splashCanvasObj != null)
        {
            UnityEngine.Object.Destroy(_splashCanvasObj);
        }
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
                Plugin.Log.Info("FcSpawnManager: Full Combo achieved! Activating splash display.");
                
                if (_splashCanvasObj != null)
                {
                    _splashCanvasObj.SetActive(true);
                }
                else
                {
                    _splashCanvasObj = FcSpawner.SpawnDisplay();
                }
            }
        }
    }
}
