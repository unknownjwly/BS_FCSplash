using System;
using System.Linq;
using Zenject;
using UnityEngine;
using FCSplash.Features.Spawning;

namespace FCSplash.Features.Tracking;

public class FcTrackerManager : IInitializable, IDisposable
{
    [Inject] private readonly BeatmapObjectManager _beatmapObjectManager = null!;
    [Inject] private readonly IReadonlyBeatmapData _beatmapData = null!;

    private readonly ParticleEffectManager _particleManager = new();

    private bool _isFullCombo = true;
    private int _totalValidNotes = 0;
    private int _processedNotes = 0;
    private bool _hasTriggeredFc = false;
    private GameObject? _splashCanvasObj;
    
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

        Plugin.Log.Info($"FcTrackerManager Initialized. Total notes: {_totalValidNotes}");
        
        _beatmapObjectManager.noteWasCutEvent += OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent += OnNoteWasMissed;
    }

    public void Dispose()
    {
        Plugin.Log.Info($"FcTrackerManager deinitialized. Combo: {_processedNotes}/{_totalValidNotes}");
        _beatmapObjectManager.noteWasCutEvent -= OnNoteWasCut;
        _beatmapObjectManager.noteWasMissedEvent -= OnNoteWasMissed;

        if (_splashCanvasObj != null)
        {
            UnityEngine.Object.Destroy(_splashCanvasObj);
        }
        
    }

    private void OnNoteWasCut(NoteController noteController, in NoteCutInfo noteCutInfo)
    {
        if (_hasTriggeredFc) return;

        NoteData noteData = noteController.noteData;

        if (noteData.gameplayType == NoteData.GameplayType.Bomb)
        {
            _isFullCombo = false;
            return;
        }

        _processedNotes++;
        if (!noteCutInfo.allIsOK)
        {
            _isFullCombo = false;
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
        }

        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (!Config.Instance.General.EnableMod) return;
        if (_processedNotes >= _totalValidNotes)
        {
            if (_isFullCombo && !_hasTriggeredFc)
            {
                _hasTriggeredFc = true;
                Plugin.Log.Info("FcTrackerManager: Full Combo'd!");
                
                _splashCanvasObj = FcSpawner.SpawnDisplay();

                if (Config.Instance.Particles.EnableSparkles && _splashCanvasObj != null)
                {
                    _particleManager.TriggerSparkleEffect(_splashCanvasObj.transform.position);
                }
                
                Plugin.AudioManager.PlaySound();
            }
        }
    }
}