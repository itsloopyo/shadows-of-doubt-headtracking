// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using UnityEngine;

namespace ShadowsOfDoubtHeadTracking.Utilities;

/// <summary>
/// Utility for building hierarchy paths from Unity transforms.
/// </summary>
public static class TransformPath
{
    /// <summary>
    /// Get the full hierarchy path of a transform (e.g. "Root/Parent/Child").
    /// </summary>
    public static string GetFullPath(Transform transform)
    {
        if (transform == null) return "(null)";

        var path = transform.name;
        var parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
