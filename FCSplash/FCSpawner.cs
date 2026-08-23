using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using IPA.Utilities;

namespace FCSplash;

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
    }

    private void OnEnable()
    {
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

public class FcSpawner : MonoBehaviour
{
    private static GameObject? _activeCanvas;
    private static Transform? _contentTransform;

    private static bool _isAnimating;
    private static float _animTimer;

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

        const float c1 = 2.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(progress - 1f, 3f) + c1 * Mathf.Pow(progress - 1f, 2f);
    }
    
    public static GameObject? SpawnDisplay()
    {
        if (_activeCanvas != null)
        {
            Destroy(_activeCanvas);
        }

        try
        {
            string folderPath = Path.Combine(UnityGame.UserDataPath, "FCSplash", "Images & Gifs");
            string? imagePath = null;

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
                List<string> validImageFiles = new List<string>();

                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(supportedExtensions, e => e == ext))
                    {
                        validImageFiles.Add(file);
                    }
                }

                if (validImageFiles.Count > 0)
                {
                    if (Config.Instance.General.EnableRandomImage)
                    {
                        var randomizedFiles = validImageFiles.OrderBy(_ => Guid.NewGuid()).ToList();
                        imagePath = randomizedFiles[0];
                    }
                    else
                    {
                        imagePath = validImageFiles[0];
                    }
                }
            }

            List<Texture2D> texturesToProcess = new List<Texture2D>();
            List<float> frameDelays = new List<float>();

            if (imagePath != null && File.Exists(imagePath))
            {
                byte[] fileData = File.ReadAllBytes(imagePath);
                string ext = Path.GetExtension(imagePath).ToLowerInvariant();

                if (ext == ".gif")
                {
                    try
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
                                frameDelays.Add(frameData.Delay);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"[FcSpawner] Failed to parse GIF file: {ex.Message}");
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
                        frameDelays.Add(1f);
                    }
                }
            }

            if (texturesToProcess.Count == 0)
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

                            Texture2D tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (tempTex.LoadImage(fileData))
                            {
                                Texture2D fallbackTex = new Texture2D(tempTex.width, tempTex.height, TextureFormat.RGBA32, false);
                                fallbackTex.SetPixels32(tempTex.GetPixels32());
                                fallbackTex.Apply(false, false);
                                UnityEngine.Object.Destroy(tempTex);

                                fallbackTex.filterMode = FilterMode.Bilinear;
                                fallbackTex.wrapMode = TextureWrapMode.Clamp;

                                texturesToProcess.Add(fallbackTex);
                                frameDelays.Add(1f);
                            }
                        }
                        else
                        {
                            Plugin.Log.Error($"[FcSpawner] Could not find embedded resource: {resourceName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"[FcSpawner] Failed to load embedded fallback image: {ex.Message}");
                }
            }

            List<Sprite> generatedSprites = new List<Sprite>();

            foreach (var tex in texturesToProcess)
            {
                try
                {
                    int width = tex.width;
                    int height = tex.height;
                    Color32[] pixels = tex.GetPixels32();

                    if (Config.Instance.General.EnableRoundCorners)
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
                    
                    generatedSprites.Add(Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f));
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"[FcSpawner] Failed to apply rounding logic to frame: {ex.Message}");
                }
            }

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
            
            _animTimer = 0f;
            _isAnimating = true;
            
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
                Plugin.Log.Error($"[FcSpawner] Error loading asset bundle material: {ex.Message}");
            }
            finally
            {
                bundle?.Unload(false);
            }
            
            imageView.preserveAspect = true;
            
            if (generatedSprites.Count > 0)
            {
                imageView.sprite = generatedSprites[0];
            }

            if (generatedSprites.Count > 1)
            {
                GifAnimator animator = imageObj.AddComponent<GifAnimator>();
                animator.Initialize(generatedSprites, frameDelays, imageView);
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

public class GifFrameData
{
    public int Width;
    public int Height;
    public Color32[] Pixels = Array.Empty<Color32>();
    public float Delay;
}

public static class SimpleGifDecoder
{
    public static List<GifFrameData> Decode(BinaryReader br)
    {
        var frames = new List<GifFrameData>();
        string sig = new string(br.ReadChars(6));
        if (!sig.StartsWith("GIF")) return frames;

        ushort logicalWidth = br.ReadUInt16();
        ushort logicalHeight = br.ReadUInt16();
        byte packedFields = br.ReadByte();
        byte bgColorIndex = br.ReadByte();
        byte pixelAspectRatio = br.ReadByte();

        bool hasGlobalCT = (packedFields & 0x80) != 0;
        int globalCTSize = 2 << (packedFields & 0x07);

        Color32[] globalCT = Array.Empty<Color32>();
        if (hasGlobalCT)
        {
            globalCT = ReadColorTable(br, globalCTSize);
        }

        Color32[] currentCT = globalCT;
        int frameDelay = 10;
        int transparentIndex = -1;
        bool hasTransparent = false;
        int disposalMethod = 0;

        Color32[]? prevScreenPixels = null;
        Color32[] screenPixels = new Color32[logicalWidth * logicalHeight];
        for (int i = 0; i < screenPixels.Length; i++) screenPixels[i] = new Color32(0, 0, 0, 0);

        bool done = false;
        while (!done && br.BaseStream.Position < br.BaseStream.Length)
        {
            byte introducer = br.ReadByte();
            if (introducer == 0x3B)
            {
                done = true;
                break;
            }
            else if (introducer == 0x21)
            {
                byte label = br.ReadByte();
                if (label == 0xF9)
                {
                    int blockSize = br.ReadByte();
                    byte gceFlags = br.ReadByte();
                    disposalMethod = (gceFlags >> 2) & 0x07;
                    hasTransparent = (gceFlags & 1) != 0;
                    ushort delayTime = br.ReadUInt16();
                    frameDelay = delayTime == 0 ? 10 : delayTime;
                    transparentIndex = br.ReadByte();
                    br.ReadByte();
                }
                else
                {
                    SkipBlocks(br);
                }
            }
            else if (introducer == 0x2C)
            {
                ushort xPos = br.ReadUInt16();
                ushort yPos = br.ReadUInt16();
                ushort width = br.ReadUInt16();
                ushort height = br.ReadUInt16();
                byte imgPacked = br.ReadByte();

                bool hasLocalCT = (imgPacked & 0x80) != 0;
                bool interlace = (imgPacked & 0x40) != 0;
                int localCTSize = 2 << (imgPacked & 0x07);

                if (hasLocalCT)
                {
                    currentCT = ReadColorTable(br, localCTSize);
                }

                byte lzwMinCodeSize = br.ReadByte();
                byte[] lzwData = ReadDataBlocks(br);

                if (disposalMethod == 3 && prevScreenPixels == null)
                {
                    prevScreenPixels = (Color32[])screenPixels.Clone();
                }

                Color32[] indexedPixels = DecodeLzw(lzwData, lzwMinCodeSize, width * height);

                int rix = 0;
                int pass = 1;
                int inc = 8;
                int line = 0;

                for (int y = 0; y < height; y++)
                {
                    int drawY = y;
                    if (interlace)
                    {
                        if (line >= height)
                        {
                            pass++;
                            if (pass == 2) { line = 4; inc = 8; }
                            else if (pass == 3) { line = 2; inc = 4; }
                            else if (pass == 4) { line = 1; inc = 2; }
                        }
                        drawY = line;
                        line += inc;
                    }

                    if (drawY + yPos < logicalHeight)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (x + xPos < logicalWidth)
                            {
                                int colorIdx = indexedPixels[rix].r;
                                bool isTrans = hasTransparent && (colorIdx == transparentIndex);

                                if (!isTrans)
                                {
                                    int screenIdx = (drawY + yPos) * logicalWidth + (x + xPos);
                                    if (colorIdx < currentCT.Length)
                                    {
                                        screenPixels[screenIdx] = currentCT[colorIdx];
                                    }
                                }
                            }
                            rix++;
                        }
                    }
                }

                Color32[] flippedPixels = new Color32[logicalWidth * logicalHeight];
                for (int r = 0; r < logicalHeight; r++)
                {
                    int sourceRow = logicalHeight - 1 - r;
                    Array.Copy(screenPixels, sourceRow * logicalWidth, flippedPixels, r * logicalWidth, logicalWidth);
                }

                frames.Add(new GifFrameData
                {
                    Width = logicalWidth,
                    Height = logicalHeight,
                    Pixels = flippedPixels,
                    Delay = frameDelay * 0.01f
                });

                if (disposalMethod == 2)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int screenIdx = (y + yPos) * logicalWidth + (x + xPos);
                            screenPixels[screenIdx] = new Color32(0, 0, 0, 0);
                        }
                    }
                }
                else if (disposalMethod == 3 && prevScreenPixels != null)
                {
                    screenPixels = prevScreenPixels;
                    prevScreenPixels = null;
                }

                currentCT = globalCT;
            }
            else
            {
                break;
            }
        }

        return frames;
    }

    private static Color32[] ReadColorTable(BinaryReader br, int size)
    {
        Color32[] table = new Color32[size];
        for (int i = 0; i < size; i++)
        {
            byte r = br.ReadByte();
            byte g = br.ReadByte();
            byte b = br.ReadByte();
            table[i] = new Color32(r, g, b, 255);
        }
        return table;
    }

    private static byte[] ReadDataBlocks(BinaryReader br)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            while (true)
            {
                byte blockSize = br.ReadByte();
                if (blockSize == 0) break;
                byte[] block = br.ReadBytes(blockSize);
                ms.Write(block, 0, block.Length);
            }
            return ms.ToArray();
        }
    }

    private static void SkipBlocks(BinaryReader br)
    {
        while (true)
        {
            byte blockSize = br.ReadByte();
            if (blockSize == 0) break;
            br.ReadBytes(blockSize);
        }
    }

    private static Color32[] DecodeLzw(byte[] compressed, int minCodeSize, int capacity)
    {
        int clearCode = 1 << minCodeSize;
        int eoiCode = clearCode + 1;
        int codeSize = minCodeSize + 1;
        int available = clearCode + 2;
        int oldCode = -1;

        int[] prefix = new int[4096];
        byte[] suffix = new byte[4096];
        byte[] pixelStack = new byte[4096];
        Color32[] pixels = new Color32[capacity];

        for (int i = 0; i < clearCode; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
        }

        int top = 0;
        int bi = 0;
        int pi = 0;

        int bits = 0;
        int datum = 0;

        for (int i = 0; i < capacity;)
        {
            if (top == 0)
            {
                if (bits < codeSize)
                {
                    if (bi >= compressed.Length) break;
                    datum += compressed[bi++] << bits;
                    bits += 8;
                    continue;
                }

                int code = datum & ((1 << codeSize) - 1);
                datum >>= codeSize;
                bits -= codeSize;

                if (code > available || code == eoiCode) break;

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    available = clearCode + 2;
                    oldCode = -1;
                    continue;
                }

                if (oldCode == -1)
                {
                    pixelStack[top++] = suffix[code];
                    oldCode = code;
                    continue;
                }

                int inCode = code;
                if (code == available)
                {
                    pixelStack[top++] = (byte)suffix[oldCode];
                    code = oldCode;
                }

                while (code > clearCode)
                {
                    pixelStack[top++] = suffix[code];
                    code = prefix[code];
                }

                byte first = suffix[code];
                pixelStack[top++] = first;

                if (available < 4096)
                {
                    prefix[available] = oldCode;
                    suffix[available] = first;
                    available++;
                    if ((available & ((1 << codeSize) - 1)) == 0 && available < 4096)
                    {
                        codeSize++;
                    }
                }

                oldCode = inCode;
            }

            top--;
            pixels[pi++] = new Color32(pixelStack[top], 0, 0, 255);
            i++;
        }

        return pixels;
    }
}
