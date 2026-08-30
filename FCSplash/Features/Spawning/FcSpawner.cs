using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IPA.Utilities;

namespace FCSplash.Features.Spawning;

public class FcSpawner : MonoBehaviour
{
    private static GameObject? _activeCanvas;
    private static Transform? _contentTransform;

    private bool _isAnimating;
    private float _animTimer;
    
    private static Dictionary<string, List<Sprite>> _cachedImageLibrary = new Dictionary<string, List<Sprite>>();
    private static Dictionary<string, List<float>> _cachedDelayLibrary = new Dictionary<string, List<float>>();
    private static List<string> _availableImagePaths = new List<string>();
    private static Material? _cachedMaterial;
    
    private static bool? _lastRoundCornersState;
    private static string? _lastChosenImagePath;

    public static void InitializeOnStartup()
    {
        PreloadAssets();
    }

    public static void PreloadAssets()
    {
        bool currentRoundCorners = Config.Instance.General.EnableRoundCorners;
        
        if (_cachedImageLibrary.Count > 0 && _lastRoundCornersState == currentRoundCorners)
        {
            return;
        }

        _lastRoundCornersState = currentRoundCorners;
        _cachedImageLibrary.Clear();
        _cachedDelayLibrary.Clear();
        _availableImagePaths.Clear();
        _lastChosenImagePath = null;

        try
        {
            string folderPath = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Images & Gifs");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                string[] supportedExtensions = [ 
                    ".gif", ".jfi", ".jfif", ".jif", ".jpe", ".jpeg", ".jpg", ".png", ".webp" 
                ];
                
                string[] files = Directory.GetFiles(folderPath);

                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(supportedExtensions, e => e == ext))
                    {
                        _availableImagePaths.Add(file);
                    }
                }
            }
            
            if (_availableImagePaths.Count == 0)
            {
                LoadFallbackIntoLibrary(currentRoundCorners);
            }
            else
            {
                foreach (var imagePath in _availableImagePaths)
                {
                    ProcessAndCacheImage(imagePath, currentRoundCorners);
                }
            }
            
            Plugin.Log.Info($"[FcSpawner] Successfully preloaded and cached {_cachedImageLibrary.Count} image(s)/GIF(s) into memory on game boot.");
            
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
                            _cachedMaterial = new Material(renderer.sharedMaterial);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[FcSpawner] Error loading asset bundle material: {ex.Message}");
            }
            finally
            {
                bundle?.Unload(false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[FcSpawner] Failed to preload assets: {ex.Message}");
        }
    }

    private static void ProcessAndCacheImage(string imagePath, bool currentRoundCorners)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(imagePath);
            string ext = Path.GetExtension(imagePath).ToLowerInvariant();

            List<Sprite> sprites = new List<Sprite>();
            List<float> delays = new List<float>();
            List<Texture2D> texturesToProcess = new List<Texture2D>();

            if (ext == ".gif")
            {
                using (MemoryStream ms = new MemoryStream(fileData))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    var gifFrames = SimpleGifDecoder.Decode(br);
                    foreach (var frameData in gifFrames)
                    {
                        Texture2D tex = new Texture2D(frameData.Width, frameData.Height, TextureFormat.RGBA32, false);
                        tex.SetPixels32(frameData.Pixels);
                        tex.Apply(false, false);
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        
                        texturesToProcess.Add(tex);
                        delays.Add(frameData.Delay);
                    }
                }
            }
            else
            {
                Texture2D tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tempTex.LoadImage(fileData))
                {
                    Texture2D singleTex = new Texture2D(tempTex.width, tempTex.height, TextureFormat.RGBA32, false);
                    singleTex.SetPixels32(tempTex.GetPixels32());
                    singleTex.Apply(false, false);
                    UnityEngine.Object.Destroy(tempTex);

                    singleTex.filterMode = FilterMode.Bilinear;
                    singleTex.wrapMode = TextureWrapMode.Clamp;
                    
                    texturesToProcess.Add(singleTex);
                    delays.Add(1f);
                }
            }

            foreach (var tex in texturesToProcess)
            {
                int width = tex.width;
                int height = tex.height;
                Color32[] pixels = tex.GetPixels32();

                if (currentRoundCorners)
                {
                    float radius = Mathf.Min(width, height) * 0.12f;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            float cx = x < radius ? radius : (x > width - radius ? width - radius : -1f);
                            float cy = y < radius ? radius : (y > height - radius ? height - radius : -1f);

                            if (cx != -1f && cy != -1f)
                            {
                                float dx = x - cx;
                                float dy = y - cy;
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                                if (distance > radius)
                                {
                                    int index = y * width + x;
                                    Color32 original = pixels[index];
                                    float alphaFactor = Mathf.Clamp01((radius + 1f - distance) / 1f);
                                    original.a = (byte)(original.a * alphaFactor);
                                    pixels[index] = original;
                                }
                            }
                        }
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                
                sprites.Add(Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f));
            }

            _cachedImageLibrary[imagePath] = sprites;
            _cachedDelayLibrary[imagePath] = delays;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[FcSpawner] Failed to process image {Path.GetFileName(imagePath)}: {ex.Message}");
        }
    }

    private static void LoadFallbackIntoLibrary(bool currentRoundCorners)
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string resourceName = "FCSplash.Resources.NoImageFound.png";

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    byte[] fileData = new byte[stream.Length];
                    stream.Read(fileData, 0, fileData.Length);

                    string fallbackKey = "__FALLBACK__";
                    _availableImagePaths.Add(fallbackKey);

                    Texture2D tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tempTex.LoadImage(fileData))
                    {
                        Texture2D fallbackTex = new Texture2D(tempTex.width, tempTex.height, TextureFormat.RGBA32, false);
                        fallbackTex.SetPixels32(tempTex.GetPixels32());
                        fallbackTex.Apply(false, false);
                        UnityEngine.Object.Destroy(tempTex);

                        fallbackTex.filterMode = FilterMode.Bilinear;
                        fallbackTex.wrapMode = TextureWrapMode.Clamp;

                        List<Sprite> sprites = new List<Sprite> { Sprite.Create(fallbackTex, new Rect(0, 0, fallbackTex.width, fallbackTex.height), new Vector2(0.5f, 0.5f), 100f) };
                        List<float> delays = new List<float> { 1f };

                        _cachedImageLibrary[fallbackKey] = sprites;
                        _cachedDelayLibrary[fallbackKey] = delays;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[FcSpawner] Failed to load embedded fallback image: {ex.Message}");
        }
    }

    public static void ClearCache()
    {
        _cachedImageLibrary.Clear();
        _cachedDelayLibrary.Clear();
        _availableImagePaths.Clear();
        _cachedMaterial = null;
        _lastRoundCornersState = null;
        _lastChosenImagePath = null;
    }

    private void Start()
    {
        _animTimer = 0f;
        _isAnimating = true;
    }

    private void Update()
    {
        if (_isAnimating && _contentTransform != null)
        {
            _animTimer += Time.deltaTime;
            float duration = Mathf.Max(0.01f, Config.Instance.Animation.AnimationDuration);
            float progress = Mathf.Clamp01(_animTimer / duration);
            
            float scale = GetAnimationScale(progress, Config.Instance.Animation.AnimationType);
            
            float finalScale = 0.010f * scale * Config.Instance.General.GlobalScaleMultiplier;
            _contentTransform.localScale = new Vector3(finalScale, finalScale, finalScale);

            if (progress >= 1f)
            {
                _isAnimating = false;
            }
        }
    }

    private float GetAnimationScale(float progress, string animType)
    {
        if (animType.Equals("Linear", StringComparison.OrdinalIgnoreCase))
        {
            return progress;
        }
        
        if (animType.Equals("EaseInQuad", StringComparison.OrdinalIgnoreCase))
        {
            return progress * progress;
        }

        if (animType.Equals("EaseOutQuad", StringComparison.OrdinalIgnoreCase))
        {
            return progress * (2f - progress);
        }

        if (animType.Equals("EaseInOutQuad", StringComparison.OrdinalIgnoreCase))
        {
            return progress < 0.5f ? 2f * progress * progress : -1f + (4f - 2f * progress) * progress;
        }

        if (animType.Equals("EaseOutBack", StringComparison.OrdinalIgnoreCase))
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(progress - 1f, 3f) + c1 * Mathf.Pow(progress - 1f, 2f);
        }

        if (animType.Equals("Elastic", StringComparison.OrdinalIgnoreCase))
        {
            if (progress == 0f) return 0f;
            if (progress == 1f) return 1f;
            return Mathf.Pow(2f, -10f * progress) * Mathf.Sin((progress - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
        }

        if (animType.Equals("Bounce", StringComparison.OrdinalIgnoreCase))
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (progress < 1f / d1)
            {
                return n1 * progress * progress;
            }
            else if (progress < 2f / d1)
            {
                return n1 * (progress -= 1.5f / d1) * progress + 0.75f;
            }
            else if (progress < 2.5f / d1)
            {
                return n1 * (progress -= 2.25f / d1) * progress + 0.9375f;
            }
            else
            {
                return n1 * (progress -= 2.625f / d1) * progress + 0.984375f;
            }
        }

        return progress;
    }
    
    public static GameObject? SpawnDisplay()
    {
        if (_activeCanvas != null)
        {
            Destroy(_activeCanvas);
        }

        try
        {
            _activeCanvas = new GameObject("FcSplashCanvas");
            _activeCanvas.AddComponent<FcSpawner>();
            
            Canvas canvas = _activeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            
            CanvasScaler canvasScaler = _activeCanvas.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            
            _activeCanvas.AddComponent<GraphicRaycaster>();
            
            _activeCanvas.transform.position = new Vector3(0f, 1.45f, Config.Instance.General.ZPositionDistance);
            _activeCanvas.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            GameObject containerObj = new GameObject("SplashContainer");
            containerObj.transform.SetParent(_activeCanvas.transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            containerObj.transform.localRotation = Quaternion.identity;
            _contentTransform = containerObj.transform;
            _contentTransform.localScale = Vector3.zero;
            
            RectTransform textParentRect = null!;
            if (Config.Instance.Text.EnableText)
            {
                GameObject textStackObj = new GameObject("SplashText3D_Stack");
                textStackObj.transform.SetParent(containerObj.transform, false);
                textStackObj.transform.localRotation = Quaternion.identity;

                textParentRect = textStackObj.AddComponent<RectTransform>();
                textParentRect.anchorMin = new Vector2(0.5f, 0.5f);
                textParentRect.anchorMax = new Vector2(0.5f, 0.5f);
                textParentRect.pivot = new Vector2(0.5f, 0.5f);
                textParentRect.sizeDelta = new Vector2(700, 100);
                textParentRect.anchoredPosition = new Vector2(0, -90);

                int layerCount = Config.Instance.Text.Enable3DText ? Mathf.Max(1, Config.Instance.Text.DepthLayers) : 1;
                float depthStep = Config.Instance.Text.DepthLayerDistance;

                FontStyles style = FontStyles.Normal;
                if (Config.Instance.Text.EnableBold) style |= FontStyles.Bold;
                if (Config.Instance.Text.EnableItalic) style |= FontStyles.Italic;

                Color mainColor = new Color32(
                    (byte)Config.Instance.Text.TextColorR,
                    (byte)Config.Instance.Text.TextColorG,
                    (byte)Config.Instance.Text.TextColorB,
                    255
                );

                for (int i = layerCount - 1; i >= 0; i--)
                {
                    GameObject layerObj = new GameObject($"Layer_{i}");
                    layerObj.transform.SetParent(textStackObj.transform, false);

                    TextMeshProUGUI layerText = layerObj.AddComponent<TextMeshProUGUI>();
                    layerText.text = Config.Instance.Text.TextContent;
                    layerText.fontSize = Config.Instance.Text.FontSize;
                    layerText.fontStyle = style;
                    layerText.alignment = TextAlignmentOptions.Center;
                    layerText.enableWordWrapping = false;
                    layerText.raycastTarget = false;

                    float shade = Mathf.Lerp(0.35f, 0.9f, (float)i / layerCount);
                    if (i == 0)
                    {
                        layerText.color = mainColor;
                    }
                    else
                    {
                        layerText.color = new Color(mainColor.r * 0.2f, shade * mainColor.g * 0.8f, mainColor.b * 0.2f);
                    }

                    layerText.outlineWidth = i == 0 ? 0.35f : 0f;

                    RectTransform layerRect = layerObj.GetComponent<RectTransform>();
                    layerRect.anchorMin = new Vector2(0.5f, 0.5f);
                    layerRect.anchorMax = new Vector2(0.5f, 0.5f);
                    layerRect.pivot = new Vector2(0.5f, 0.5f);
                    layerRect.sizeDelta = new Vector2(700, 100);
                    layerRect.anchoredPosition = new Vector2(0, i * 0.4f);
                    layerObj.transform.localPosition = new Vector3(0f, 0f, i * depthStep);
                }
            }
            
            GameObject imageObj = new GameObject("SplashImage");
            imageObj.transform.SetParent(containerObj.transform, false);
            imageObj.transform.localRotation = Quaternion.identity;

            Image imageView = imageObj.AddComponent<Image>();
            imageView.color = Color.white;
            
            if (_cachedMaterial != null)
            {
                imageView.material = new Material(_cachedMaterial);
            }
            
            imageView.preserveAspect = true;
            
            string? chosenPath = null;
            if (_availableImagePaths == null || _availableImagePaths.Count == 0)
            {
                chosenPath = null;
            }
            else if (Config.Instance.General.EnableRandomImage)
            {
                if (_availableImagePaths.Count > 1)
                {
                    var pool = _availableImagePaths.Where(p => p != _lastChosenImagePath).ToList();
                    if (pool.Count == 0) pool = _availableImagePaths;
        
                    int randomIndex = UnityEngine.Random.Range(0, pool.Count);
                    chosenPath = pool[randomIndex];
                }
                else
                {
                    chosenPath = _availableImagePaths[0];
                }
            }
            else
            {
                string manualPath = Config.Instance.General.SelectedImage;
                if (!string.IsNullOrEmpty(manualPath) && _availableImagePaths.Contains(manualPath))
                {
                    chosenPath = manualPath;
                }
                else
                {
                    chosenPath = _availableImagePaths[0];
                }
            }
            _lastChosenImagePath = chosenPath;

            List<Sprite>? activeSprites = (chosenPath != null && _cachedImageLibrary.ContainsKey(chosenPath)) 
                ? _cachedImageLibrary[chosenPath] 
                : null;
            
            List<float>? activeDelays = (chosenPath != null && _cachedDelayLibrary.ContainsKey(chosenPath)) 
                ? _cachedDelayLibrary[chosenPath] 
                : null;
            
            if (activeSprites != null && activeSprites.Count > 0)
            {
                imageView.sprite = activeSprites[0];

                if (activeSprites.Count > 1 && activeDelays != null)
                {
                    GifAnimator animator = imageObj.AddComponent<GifAnimator>();
                    animator.Initialize(activeSprites, activeDelays, imageView);
                }
            }
            
            RectTransform imageRect = imageView.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0f);
            imageRect.sizeDelta = new Vector2(280, 280);
            imageRect.localRotation = Quaternion.identity;
            imageRect.localScale = Vector3.one;

            if (Config.Instance.Text.EnableText && textParentRect != null)
            {
                float fixedGap = 20f;
                float textTopY = textParentRect.anchoredPosition.y + (textParentRect.sizeDelta.y * 0.5f);
                imageRect.anchoredPosition = new Vector2(0, textTopY + fixedGap);
            }
            else
            {
                imageRect.anchoredPosition = new Vector2(0, -140);
            }
            
            return _activeCanvas;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[FcSpawner] Failed to spawn splash display: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}