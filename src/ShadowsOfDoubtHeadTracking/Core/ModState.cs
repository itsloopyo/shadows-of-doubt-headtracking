// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;

namespace ShadowsOfDoubtHeadTracking.Core;

/// <summary>
/// Global enabled/disabled flag for head tracking.
/// Volatile so the Harmony patch hot path (CameraProjectionPatches) gets
/// lock-free reads.
/// </summary>
public sealed class ModState
{
    private static readonly Lazy<ModState> LazyInstance = new(() => new ModState());
    public static ModState Instance => LazyInstance.Value;

    private volatile bool _isEnabled = true;

    private ModState()
    {
    }

    /// <summary>
    /// Whether head tracking is currently enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Toggle enabled state.
    /// </summary>
    public void Toggle()
    {
        IsEnabled = !IsEnabled;
    }
}
