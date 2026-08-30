using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FCSplash.Features.Spawning;

public class GifAnimator : MonoBehaviour
{
    private List<Sprite> _frames = new List<Sprite>();
    private List<float> _delays = new List<float>();
    private Image? _targetImage;
    private int _currentIndex;
    private Coroutine? _animationRoutine;

    public void Initialize(List<Sprite> frames, List<float> delays, Image targetImage)
    {
        _frames = frames;
        _delays = delays;
        _targetImage = targetImage;

        if (_frames.Count > 0 && _targetImage != null)
        {
            _targetImage.sprite = _frames[0];
        }

        if (_frames.Count > 1 && _animationRoutine == null && enabled)
        {
            _animationRoutine = StartCoroutine(PlayAnimation());
        }
    }

    private void OnEnable()
    {
        _currentIndex = 0;
        if (_frames.Count > 0 && _targetImage != null)
        {
            _targetImage.sprite = _frames[0];
        }

        if (_frames.Count > 1 && _animationRoutine == null)
        {
            _animationRoutine = StartCoroutine(PlayAnimation());
        }
    }

    private void OnDisable()
    {
        if (_animationRoutine != null)
        {
            StopCoroutine(_animationRoutine);
            _animationRoutine = null;
        }
    }

    private IEnumerator PlayAnimation()
    {
        while (_frames.Count > 0)
        {
            float delay = _delays[_currentIndex];
            if (delay <= 0f) delay = 0.1f;

            yield return new WaitForSeconds(delay);

            _currentIndex = (_currentIndex + 1) % _frames.Count;
            if (_targetImage != null)
            {
                _targetImage.sprite = _frames[_currentIndex];
            }
        }
    }
}