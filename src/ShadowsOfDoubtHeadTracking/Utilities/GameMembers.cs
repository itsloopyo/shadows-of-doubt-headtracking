// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Reflection;

namespace ShadowsOfDoubtHeadTracking.Utilities;

/// <summary>
/// Reflection over Shadows of Doubt's own types, which the mod never references
/// at compile time so the build stays game-independent.
///
/// A member that reads as a plain field in the game's source becomes a property
/// wrapping the native field offset in the IL2CPP interop assembly, so every
/// lookup here tries the property first and falls back to the field.
/// </summary>
internal static class GameMembers
{
    private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.Static;

    /// <summary>The getter for a game singleton's static <c>Instance</c> property.</summary>
    internal static MethodInfo? FindSingletonGetter(Type type)
    {
        return type.GetProperty("Instance", StaticMembers)?.GetGetMethod();
    }

    /// <summary>
    /// The live singleton for a game type, or null when the type is unknown, has
    /// no <c>Instance</c> accessor, or has not spawned yet.
    /// </summary>
    internal static object? ReadSingleton(Type? type)
    {
        if (type == null) return null;
        return FindSingletonGetter(type)?.Invoke(null, null);
    }

    internal static MethodInfo? FindGetter(Type type, string name)
    {
        return type.GetProperty(name, InstanceMembers)?.GetGetMethod();
    }

    internal static FieldInfo? FindField(Type type, string name)
    {
        return type.GetField(name, InstanceMembers);
    }

    /// <summary>
    /// Reads a public instance member by name. Resolves on every call, so this is
    /// for one-shot dumps and startup searches; anything on a per-frame path
    /// should cache the accessor from <see cref="FindGetter"/> instead.
    /// </summary>
    internal static object? ReadMember(Type type, object instance, string name)
    {
        var getter = FindGetter(type, name);
        if (getter != null) return getter.Invoke(instance, null);
        return FindField(type, name)?.GetValue(instance);
    }
}
