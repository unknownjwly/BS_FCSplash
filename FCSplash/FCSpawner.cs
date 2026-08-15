using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IPA.Utilities;

namespace FCSplash;

public class FcSpawner : MonoBehaviour
{
    private static GameObject? _activeCanvas;
    private static Transform? _contentTransform;
    
    private static bool _isAnimating = false;
    private static float _animTimer = 0f;
    private const float AnimDuration = 0.4f;

    private void Update()
    {
        if (_isAnimating && _contentTransform != null)
        {
            _animTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_animTimer / AnimDuration);
            float scale = EaseOutBack(progress);

            // Increased base scale multiplier from 0.006f to 0.012f to make the whole splash bigger
            _contentTransform.localScale = new Vector3(0.012f * scale, 0.012f * scale, 0.012f * scale);

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 2.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    public static GameObject? SpawnDisplay()
    {
        if (_activeCanvas != null)
        {
            Plugin.Log.Info("Destroying previous active canvas instance.");
            Destroy(_activeCanvas);
        }

        Plugin.Log.Info("Full Combo'd! Spawning splash...");
        
        try
        {
            string folderPath = Path.Combine(UnityGame.UserDataPath, "FCSplash");
            string? imagePath = null;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                string[] supportedExtensions = { ".png", ".jpg", ".jpeg", ".webp" };
                string[] files = Directory.GetFiles(folderPath);

                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(supportedExtensions, e => e == ext))
                    {
                        imagePath = file;
                        break;
                    }
                }
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool loadSuccess = false;

            if (imagePath != null && File.Exists(imagePath))
            {
                byte[] fileData = File.ReadAllBytes(imagePath);
                if (texture.LoadImage(fileData))
                {
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.Apply(false, false);
                    
                    if (texture.width > 2 && texture.height > 2)
                    {
                        loadSuccess = true;
                    }
                }
            }

            if (!loadSuccess)
            {
                texture.Reinitialize(2, 2);
                texture.SetPixels(new[] { Color.green, Color.green, Color.green, Color.green });
                texture.Apply();
            }

            _activeCanvas = new GameObject("FcSplashCanvas");
            _activeCanvas.AddComponent<FcSpawner>();
            
            Canvas canvas = _activeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            _activeCanvas.transform.position = new Vector3(0f, 1.3f, 5.5f);

            GameObject containerObj = new GameObject("SplashContainer");
            containerObj.transform.SetParent(_activeCanvas.transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            _contentTransform = containerObj.transform;
            _contentTransform.localScale = Vector3.zero;

            _animTimer = 0f;
            _isAnimating = true;

            GameObject imageObj = new GameObject("SplashImage");
            imageObj.transform.SetParent(containerObj.transform, false);

            Image image = imageObj.AddComponent<Image>();
            image.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            
            // Forces an unlit shader to completely stop post-processing bloom from washing out the texture into a white box
            image.material = new Material(Shader.Find("Unlit/Texture"));
            image.preserveAspect = true;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            image.sprite = sprite;

            RectTransform imageRect = image.GetComponent<RectTransform>();
            imageRect.sizeDelta = new Vector2(280, 280);
            imageRect.anchoredPosition = new Vector2(0, 100);

            GameObject textObj = new GameObject("SplashText");
            textObj.transform.SetParent(containerObj.transform, false);

            TextMeshProUGUI textMesh = textObj.AddComponent<TextMeshProUGUI>();
            textMesh.text = "FULL COMBO";
            textMesh.fontSize = 72;
            textMesh.fontStyle = FontStyles.Bold | FontStyles.Italic;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = new Color(0.1f, 1f, 0.3f);
            textMesh.outlineWidth = 0.25f;
            textMesh.outlineColor = Color.black;
            textMesh.enableWordWrapping = false;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(700, 100);
            textRect.anchoredPosition = new Vector2(0, -90);
            
            return _activeCanvas;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to spawn splash display: {ex}");
            return null;
        }
    }
}