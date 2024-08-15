using Stride.Graphics;
using System;
using System.Collections.Generic;

namespace StrideCommunity.ImGuiDebug;
public static class TextureExtensions
{

    // Dictionary to hold textures
    private static readonly Dictionary<IntPtr, Texture> _textureRegistry = [];
    private static readonly Dictionary<Texture, IntPtr> _pointerRegistry = [];
    private static nint _count;

    /// <summary>
    /// Gets a pointer to the Texture and adds it to the <see cref="_textureRegistry"/> if it was not previously added.
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    public static IntPtr GetPointer(this Texture texture)
    {
        if(_pointerRegistry.TryGetValue(texture, out var pointer)) return pointer;
        _count++;
        _textureRegistry.Add(_count, texture);
        _pointerRegistry.Add(texture, _count);

        return _count;
    }

    /// <summary>
    /// Attempts to convert a pointer to a texture if its in the <see cref="_textureRegistry"/>
    /// </summary>
    /// <param name="pointer"></param>
    /// <param name="texture"></param>
    /// <returns></returns>
    public static bool TryGetTexture(this IntPtr pointer, out Texture texture)
    {
        if(_textureRegistry.TryGetValue(pointer, out texture))
        {
            return true;
        }
        return false;
    }
}
