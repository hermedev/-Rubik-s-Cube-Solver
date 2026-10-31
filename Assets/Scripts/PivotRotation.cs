using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Handles rotating a single cube face pivot.
///
/// Input flow (via CubeInputReader events):
///   Rotate(side) called          → subscribe to OnPrimaryDelta + OnPrimaryReleased
///   OnPrimaryDelta fires         → SpinSide() (raw feel, no DOTween)
///   OnPrimaryReleased fires      → unsubscribe, SnapToNearestAngle() (DOTween)
///   DOTween OnComplete           → PutDown, ReadState, unlock input
/// </summary>
public class PivotRotation : MonoBehaviour
{
    // ── Fields ───────────────────────────────────────────────────────────────
    private List<GameObject> activeSide;
    private bool             dragging = false;

    private ReadCube  readCube;
    private CubeState cubeState;

    private float   sensitivity = 0.4f;
    private Vector3 rotation;

    private Tween snapTween;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        readCube  = FindAnyObjectByType<ReadCube>();
        cubeState = FindAnyObjectByType<CubeState>();
    }

    void OnDestroy()
    {
        UnsubscribeFromInput();
        snapTween?.Kill();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by CubeState.PickUp() — begins face-drag session.</summary>
    public void Rotate(List<GameObject> side)
    {
        if (CubeInput.IsLocked) return;

        activeSide = side;
        dragging   = true;
        SubscribeToInput();
    }

    // ── Input Subscriptions ───────────────────────────────────────────────────

    private void SubscribeToInput()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnPrimaryDelta    += HandleDelta;
        CubeInputReader.Instance.OnPrimaryReleased += HandleReleased;
    }

    private void UnsubscribeFromInput()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnPrimaryDelta    -= HandleDelta;
        CubeInputReader.Instance.OnPrimaryReleased -= HandleReleased;
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void HandleDelta(Vector2 delta)
    {
        if (!dragging) return;
        SpinSide(activeSide, delta);
    }

    private void HandleReleased()
    {
        if (!dragging) return;
        dragging = false;
        UnsubscribeFromInput();
        SnapToNearestAngle();
    }

    // ── Spin (raw, direct — no DOTween) ──────────────────────────────────────

    /// <summary>
    /// Maps input delta directly to pivot rotation while the player drags.
    /// DOTween is only used for the release snap — drag stays raw and responsive.
    /// </summary>
    private void SpinSide(List<GameObject> side, Vector2 delta)
    {
        rotation = Vector3.zero;

        if      (side == cubeState.up)    rotation.y = (delta.x + delta.y) * sensitivity *  1;
        else if (side == cubeState.Down)  rotation.y = (delta.x + delta.y) * sensitivity * -1;
        else if (side == cubeState.left)  rotation.z = (delta.x + delta.y) * sensitivity *  1;
        else if (side == cubeState.right) rotation.z = (delta.x + delta.y) * sensitivity * -1;
        else if (side == cubeState.front) rotation.x = (delta.x + delta.y) * sensitivity * -1;
        else if (side == cubeState.back)  rotation.x = (delta.x + delta.y) * sensitivity *  1;

        transform.Rotate(rotation, Space.Self);
    }

    // ── Snap (DOTween) ────────────────────────────────────────────────────────

    /// <summary>
    /// Tweens the pivot to the nearest 90° after the player releases.
    /// Uses Ease.OutCubic for a fast-start / smooth-land feel.
    /// </summary>
    public void SnapToNearestAngle()
    {
        Vector3 snapped = transform.localEulerAngles;
        snapped.x = Mathf.Round(snapped.x / 90f) * 90f;
        snapped.y = Mathf.Round(snapped.y / 90f) * 90f;
        snapped.z = Mathf.Round(snapped.z / 90f) * 90f;

        CubeInput.IsLocked = true;

        snapTween?.Kill();
        snapTween = transform
            .DOLocalRotate(snapped, 0.25f, RotateMode.Fast)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                cubeState.PutDown(activeSide, transform.parent);
                readCube.ReadState();
                CubeInput.IsLocked = false;
            });
    }
}
