// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Reflection;
using ShadowsOfDoubtHeadTracking.Core;
using ShadowsOfDoubtHeadTracking.Utilities;

namespace ShadowsOfDoubtHeadTracking.State;

/// <summary>
/// Reads one boolean member off a Shadows of Doubt singleton through reflection.
///
/// Everything is resolved once and cached: the interop type, the static
/// <c>Instance</c> getter, and the member getter. The mod has no compile-time
/// dependency on Assembly-CSharp, so the build stays game-independent.
///
/// A member that cannot be resolved is logged once, and every read after that
/// returns the caller's supplied fallback, so a game update that renames a field
/// degrades the gate it feeds rather than throwing once per frame.
/// </summary>
internal sealed class GameFlagReader
{
    private readonly string _typeName;
    private readonly string _memberName;

    private MethodInfo? _instanceGetter;
    private MethodInfo? _memberGetter;
    private FieldInfo? _memberField;
    private bool _resolved;

    internal GameFlagReader(string typeName, string memberName)
    {
        _typeName = typeName;
        _memberName = memberName;
    }

    /// <summary>
    /// Reads the flag. Returns <paramref name="fallback"/> when the member is
    /// unavailable or the singleton has not spawned yet.
    /// </summary>
    internal bool Read(bool fallback)
    {
        Resolve();
        if (_instanceGetter == null) return fallback;

        var instance = _instanceGetter.Invoke(null, null);
        if (instance == null) return fallback;
        if (instance is UnityEngine.Object unityObject && unityObject == null) return fallback;

        object? value = _memberGetter != null
            ? _memberGetter.Invoke(instance, null)
            : _memberField!.GetValue(instance);

        return value is bool b ? b : fallback;
    }

    /// <summary>Whether the singleton this flag lives on currently exists.</summary>
    internal bool InstanceExists()
    {
        Resolve();
        if (_instanceGetter == null) return false;

        var instance = _instanceGetter.Invoke(null, null);
        if (instance == null) return false;
        return !(instance is UnityEngine.Object unityObject && unityObject == null);
    }

    private void Resolve()
    {
        if (_resolved) return;
        _resolved = true;

        var type = Type.GetType(_typeName + ", Assembly-CSharp");
        if (type == null)
        {
            HeadTrackingPlugin.Logger.LogWarning(
                $"Game state: type '{_typeName}' not found - the '{_memberName}' gate is disabled");
            return;
        }

        _instanceGetter = GameMembers.FindSingletonGetter(type);
        if (_instanceGetter == null)
        {
            HeadTrackingPlugin.Logger.LogWarning(
                $"Game state: '{_typeName}.Instance' not found - the '{_memberName}' gate is disabled");
            return;
        }

        _memberGetter = GameMembers.FindGetter(type, _memberName);
        if (_memberGetter == null)
        {
            _memberField = GameMembers.FindField(type, _memberName);
        }

        if (_memberGetter == null && _memberField == null)
        {
            HeadTrackingPlugin.Logger.LogWarning(
                $"Game state: '{_typeName}.{_memberName}' not found - that gate is disabled");
        }
    }
}
