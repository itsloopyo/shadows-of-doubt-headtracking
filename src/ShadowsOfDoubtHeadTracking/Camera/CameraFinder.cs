// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using ShadowsOfDoubtHeadTracking.Core;
using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Camera;

/// <summary>
/// Locates the player camera and caches it until it is destroyed or the scene
/// changes. Shadows of Doubt tags its first-person camera "MainCamera", so
/// <c>Camera.main</c> resolves it - and it is the one Unity API that behaves
/// consistently under IL2CPP.
/// </summary>
public sealed class CameraFinder : IDisposable
{
    // Camera.main walks the tag index, so re-running it every frame while no
    // camera exists (during a load) is wasteful. One retry every 30 frames is
    // half a second at worst, which nobody can see against a loading screen.
    private const int SearchRetryFrames = 30;

    private Transform? _cached;
    private int _framesUntilRetry;
    private bool _disposed;

    /// <summary>
    /// Fired when the camera reference changes: a new camera, or null when lost.
    /// </summary>
    public event Action<Transform?>? OnCameraChanged;

    /// <summary>Whether a live camera is currently cached.</summary>
    public bool HasCamera => _cached != null;

    /// <summary>
    /// Drop the cached camera so the next <see cref="GetCamera"/> re-searches.
    /// </summary>
    public void InvalidateCache()
    {
        if (_cached == null) return;

        _cached = null;
        _framesUntilRetry = 0;
        OnCameraChanged?.Invoke(null);
    }

    /// <summary>
    /// Returns the player camera transform, searching if the cache is empty.
    /// </summary>
    public Transform? GetCamera()
    {
        if (_disposed) return null;

        if (_cached != null)
        {
            return _cached;
        }

        if (--_framesUntilRetry > 0)
        {
            return null;
        }
        _framesUntilRetry = SearchRetryFrames;

        var mainCamera = UnityEngine.Camera.main;
        if (mainCamera == null || !mainCamera.enabled)
        {
            return null;
        }

        _cached = mainCamera.transform;
        OnCameraChanged?.Invoke(_cached);
        return _cached;
    }

    public void Dispose()
    {
        _disposed = true;
        _cached = null;
    }
}
