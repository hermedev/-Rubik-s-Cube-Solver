using UnityEngine;
using DG.Tweening;

/// <summary>
/// Controls whole-cube view rotation — orbit and 90° swipe steps.
///
/// Input flow (via CubeInputReader events):
///   OnSecondaryDelta   → orbit cube directly (raw feel, no DOTween)
///   OnSecondaryReleased:
///     swipeDir != zero → rotate target 90°, DOTween visual to match
///     swipeDir == zero → DOTween visual back to target
///
/// Bug fixes vs previous version:
///   • No double-tween on release (single event path, not two parallel checks)
///   • No Quaternion != infinite restart loop (replaced with DOTween.IsTweening check)
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

    private const float SnapDuration  = 0.40f;  // drag-release snap
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

    // ── Orbit (raw, direct) ───────────────────────────────────────────────────

    /// <summary>
    /// Directly applies the already-scaled delta from CubeInputReader.
    /// Kills any running snap tween so drag feels immediate.
    /// </summary>
    private void HandleOrbit(Vector2 scaledDelta)
    {
        snapTween?.Kill();
        swipeTween?.Kill();

        // Apply orbit: y-axis for horizontal delta, x-axis for vertical
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
            // Just a drag-release — snap visual back to current target
            TweenSnapToTarget();
        }
    }

    /// <summary>Rotates target 90° based on swipe direction, then tweens visual to match.</summary>
    private void ApplySwipeStep(Vector2 dir)
    {
        Vector3 axis  = Vector3.zero;
        float   angle = 90f;

        if      (IsLeft(dir))      { axis = Vector3.up;      angle =  90f; }
        else if (IsRight(dir))     { axis = Vector3.up;      angle = -90f; }
        else if (IsUpLeft(dir))    { axis = Vector3.right;   angle =  90f; }
        else if (IsUpRight(dir))   { axis = Vector3.forward; angle = -90f; }
        else if (IsDownLeft(dir))  { axis = Vector3.forward; angle =  90f; }
        else if (IsDownRight(dir)) { axis = Vector3.right;   angle = -90f; }

        if (axis == Vector3.zero) { TweenSnapToTarget(); return; }

        // Step the logical target instantly
        target.transform.Rotate(axis, angle, Space.World);

        // Animate visual with a satisfying slight overshoot (OutBack)
        snapTween?.Kill();
        swipeTween?.Kill();
        swipeTween = transform
            .DORotateQuaternion(target.transform.rotation, SwipeDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => swipeTween = null);
    }

    /// <summary>
    /// Smoothly rotates the visual cube to match target.transform.rotation.
    /// Bug fix: only starts if not already tweening (was: Quaternion != infinite loop).
    /// </summary>
    private void TweenSnapToTarget()
    {
        // Guard: don't restart if already tweening toward target (fixes infinite loop bug)
        if (DOTween.IsTweening(transform)) return;
        if (Quaternion.Angle(transform.rotation, target.transform.rotation) < 0.5f) return;

        snapTween?.Kill();
        snapTween = transform
            .DORotateQuaternion(target.transform.rotation, SnapDuration)
            .SetEase(Ease.OutQuint)
            .OnComplete(() => snapTween = null);
    }

    // ── Swipe Direction Helpers ───────────────────────────────────────────────

    private bool IsLeft(Vector2 s)      => s.x < 0  && s.y > -0.5f && s.y < 0.5f;
    private bool IsRight(Vector2 s)     => s.x > 0  && s.y > -0.5f && s.y < 0.5f;
    private bool IsUpLeft(Vector2 s)    => s.y > 0  && s.x <= 0f;
    private bool IsUpRight(Vector2 s)   => s.y > 0  && s.x > 0f;
    private bool IsDownLeft(Vector2 s)  => s.y < 0  && s.x <= 0f;
    private bool IsDownRight(Vector2 s) => s.y < 0  && s.x > 0f;
}
