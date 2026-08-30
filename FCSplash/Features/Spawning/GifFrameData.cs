using System;
using UnityEngine;

namespace FCSplash.Features.Spawning;

public class GifFrameData
{
    public int Width;
    public int Height;
    public Color32[] Pixels = Array.Empty<Color32>();
    public float Delay;
}