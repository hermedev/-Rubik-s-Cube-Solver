using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controls whole-cube view rotation — orbit and 90° steps along all 3 axes (X, Y, Z).
///
/// In angled/isometric camera view, 6 swipe directions correspond to 3 rotation axes:
///   1. Left / Right         -> Rotate around Y axis (+90° / -90°)
///   2. Up-Left / Down-Right  -> Rotate around X axis (+90° / -90°)
///   3. Down-Left / Up-Right  -> Rotate around Z axis (+90° / -90°)
/// </summary>
public class RotateCube : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Logical target — swipes rotate this by 90°.
    /// Visual cube tweens toward this on release.
    /// </summary>
    public GameObject target;

    // ── Tween state ───────────────────────────────────────────────────────────
    private Tween snapTween;
    private Tween swipeTween;

    private const float SnapDuration  = 0.35f;  // drag-release snap
    private const float SwipeDuration = 0.35f;  // 90° swipe step

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnSecondaryDelta    += HandleOrbit;
        CubeInputReader.Instance.OnSecondaryReleased += HandleSecondaryReleased;
    }

    void OnDestroy()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnSecondaryDelta    -= HandleOrbit;
        CubeInputReader.Instance.OnSecondaryReleased -= HandleSecondaryReleased;
        snapTween?.Kill();
        swipeTween?.Kill();
    }

    // ── Orbit (raw, direct drag) ──────────────────────────────────────────────

    /// <summary>
    /// Directly applies the delta from CubeInputReader while dragging right-mouse.
    /// Kills any running snap tween so drag feels immediate.
    /// </summary>
    private void HandleOrbit(Vector2 scaledDelta)
    {
        snapTween?.Kill();
        swipeTween?.Kill();

        Quaternion rot = Quaternion.Euler(scaledDelta.y, -scaledDelta.x, 0);
        transform.rotation = rot * transform.rotation;
    }

    // ── Release / Swipe ───────────────────────────────────────────────────────

    /// <summary>
    /// Called once per secondary contact release.
    /// swipeDir is the normalised gesture direction, or Vector2.zero for a short drag.
    /// </summary>
    private void HandleSecondaryReleased(Vector2 swipeDir)
    {
        if (swipeDir != Vector2.zero)
        {
            ApplySwipeStep(swipeDir);
        }
        else
        {
            TweenSnapToTarget();
        }
    }

    /// <summary>
    /// Rotates target 90° on all 3 main axes (and their opposites) based on swipe angle:
    ///   - Left / Right        -> Y-axis
    ///   - Up-Left / Down-Right -> X-axis
    ///   - Down-Left / Up-Right -> Z-axis
    /// </summary>
    private void ApplySwipeStep(Vector2 swipe)
    {
        Vector3 axis = Vector3.zero;
        float angle = 90f;

        // 1. Horizontal swipes -> Y-Axis rotation
        if (IsLeft(swipe))
        {
            axis = Vector3.up;
            angle = 90f;
        }
        else if (IsRight(swipe))
        {
            axis = Vector3.up;
            angle = -90f;
        }
        // 2. Diagonal 1 (Up-Left / Down-Right) -> X-Axis rotation
        else if (IsUpLeft(swipe))
        {
            axis = Vector3.right;
            angle = 90f;
        }
        else if (IsDownRight(swipe))
        {
            axis = Vector3.right;
            angle = -90f;
        }
        // 3. Diagonal 2 (Up-Right / Down-Left) -> Z-Axis rotation
        else if (IsUpRight(swipe))
        {
            axis = Vector3.forward;
            angle = -90f;
        }
        else if (IsDownLeft(swipe))
        {
            axis = Vector3.forward;
            angle = 90f;
        }

        if (axis == Vector3.zero)
        {
            TweenSnapToTarget();
            return;
        }

        // Step the logical target instantly
        target.transform.Rotate(axis, angle, Space.World);

        // Animate visual cube with DOTween
        snapTween?.Kill();
        swipeTween?.Kill();
        swipeTween = transform
            .DORotateQuaternion(target.transform.rotation, SwipeDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => swipeTween = null);
    }

    /// <summary>
    /// Smoothly rotates the visual cube to match target.transform.rotation.
    /// </summary>
    private void TweenSnapToTarget()
    {
        if (DOTween.IsTweening(transform)) return;
        if (Quaternion.Angle(transform.rotation, target.transform.rotation) < 0.5f) return;

        snapTween?.Kill();
        snapTween = transform
            .DORotateQuaternion(target.transform.rotation, SnapDuration)
            .SetEase(Ease.OutQuint)
            .OnComplete(() => snapTween = null);
    }

    // ── Direction predicates for the 6 isometric swipe angles ────────────────
    private bool IsLeft(Vector2 s)      => s.x < 0 && s.y > -0.5f && s.y < 0.5f;
    private bool IsRight(Vector2 s)     => s.x > 0 && s.y > -0.5f && s.y < 0.5f;
    private bool IsUpLeft(Vector2 s)    => s.y > 0 && s.x < 0f;
    private bool IsUpRight(Vector2 s)   => s.y > 0 && s.x >= 0f;
    private bool IsDownLeft(Vector2 s)  => s.y < 0 && s.x < 0f;
    private bool IsDownRight(Vector2 s) => s.y < 0 && s.x >= 0f;
}
