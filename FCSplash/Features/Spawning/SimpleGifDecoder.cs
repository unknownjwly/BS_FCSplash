using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace FCSplash.Features.Spawning;

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