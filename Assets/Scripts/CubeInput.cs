/// <summary>
/// Shared input lock used to prevent new rotations from starting
/// while a DOTween face or view animation is in progress.
/// </summary>
public static class CubeInput
{
    /// <summary>
    /// True while any rotation tween is running.
    /// SelectFace and RotateCube both check this before accepting input.
    /// </summary>
    public static bool IsLocked { get; set; }
}

