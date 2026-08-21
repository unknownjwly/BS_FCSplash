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

    private static bool _isAnimating;
    private static float _animTimer;
    private const float AnimDuration = 0.4f;

    private void Update()
    {
        if (_isAnimating && _contentTransform != null)
        {
            _animTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_animTimer / AnimDuration);
            float scale = EaseOutBack(progress);
            
            _contentTransform.localScale = new Vector3(0.010f * scale, 0.010f * scale, 0.010f * scale);

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
            Destroy(_activeCanvas);
        }

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
                string[] supportedExtensions = [ ".png", ".jpg", ".jpeg", ".webp" ];
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
                    
                    if (texture is { width: > 2, height: > 2 })
                    {
                        loadSuccess = true;
                    }
                }
            }

            if (!loadSuccess)
            {
                texture.Reinitialize(256, 256);
                Color[] greenPixels = new Color[256 * 256];
                for (int i = 0; i < greenPixels.Length; i++)
                {
                    greenPixels[i] = Color.green;
                }
                texture.SetPixels(greenPixels);
                texture.Apply();
            }

            // Procedurally round the corners by modifying texture pixels directly
            try
            {
                int w = texture.width;
                int h = texture.height;
                float radius = Mathf.Min(w, h) * 0.12f;
                Color32[] pixels = texture.GetPixels32();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        bool isTopLeft = x < radius && y > h - radius;
                        bool isTopRight = x > w - radius && y > h - radius;
                        bool isBottomLeft = x < radius && y < radius;
                        bool isBottomRight = x > w - radius && y < radius;

                        if (isTopLeft || isTopRight || isBottomLeft || isBottomRight)
                        {
                            float dx = x < radius ? radius - x : x - (w - radius);
                            float dy = y < radius ? radius - y : y - (h - radius);
                            if (dx * dx + dy * dy > radius * radius)
                            {
                                int index = y * w + x;
                                pixels[index] = new Color32(0, 0, 0, 0);
                            }
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Failed to apply procedural rounded corners: {ex.Message}");
            }

            _activeCanvas = new GameObject("FcSplashCanvas");
            _activeCanvas.AddComponent<FcSpawner>();
            
            Canvas canvas = _activeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            
            CanvasScaler canvasScaler = _activeCanvas.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            
            _activeCanvas.AddComponent<GraphicRaycaster>();
            
            _activeCanvas.transform.position = new Vector3(0f, 1.45f, 5.5f);
            _activeCanvas.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            GameObject containerObj = new GameObject("SplashContainer");
            containerObj.transform.SetParent(_activeCanvas.transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            _contentTransform = containerObj.transform;
            _contentTransform.localScale = Vector3.zero;

            _animTimer = 0f;
            _isAnimating = true;

            GameObject imageObj = new GameObject("SplashImage");
            imageObj.transform.SetParent(containerObj.transform, false);
            imageObj.transform.localRotation = Quaternion.identity;

            Image imageView = imageObj.AddComponent<Image>();
            imageView.color = Color.white;

            // Load asset bundle material to prevent bloom bug
            AssetBundle? bundle = null;
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("sprite.assetbundle", StringComparison.OrdinalIgnoreCase))
                    {
                        using Stream? stream = assembly.GetManifestResourceStream(name);
                        if (stream != null)
                        {
                            using MemoryStream ms = new MemoryStream();
                            stream.CopyTo(ms);
                            bundle = AssetBundle.LoadFromMemory(ms.ToArray());
                        }
                        break;
                    }
                }

                if (bundle != null)
                {
                    GameObject spriteObj = bundle.LoadAsset<GameObject>("_Sprite");
                    if (spriteObj != null)
                    {
                        Renderer renderer = spriteObj.GetComponent<Renderer>();
                        if (renderer != null && renderer.sharedMaterial != null)
                        {
                            imageView.material = new Material(renderer.sharedMaterial);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error loading asset bundle material: {ex.Message}");
            }
            finally
            {
                bundle?.Unload(false);
            }
            
            imageView.preserveAspect = true;
            
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            imageView.sprite = sprite;

            RectTransform imageRect = imageView.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.sizeDelta = new Vector2(280, 280);
            imageRect.anchoredPosition = new Vector2(0, 50);
            imageRect.localRotation = Quaternion.identity;
            imageRect.localScale = Vector3.one;

            GameObject textObj = new GameObject("SplashText");
            textObj.transform.SetParent(containerObj.transform, false);
            textObj.transform.localRotation = Quaternion.identity;

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
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(700, 100);
            textRect.anchoredPosition = new Vector2(0, -90);
            
            return _activeCanvas;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to spawn splash display: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}
