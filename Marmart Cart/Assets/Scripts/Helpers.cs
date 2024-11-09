using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Helpers
{
    private static Matrix4x4 _isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));

    /// <summary>
    /// Converts a vector to isometric coordinates.
    /// </summary>
    /// <param name="input">The input vector.</param>
    /// <returns>The vector in isometric coordinates.</returns>
    public static Vector3 ToIso(this Vector3 input) => _isoMatrix.MultiplyPoint3x4(input);
}
