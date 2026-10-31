using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Handles rotating a single cube face pivot.
///
/// Input flow (via CubeInputReader events):
///   Rotate(side) called          -> subscribe to OnPrimaryDelta + OnPrimaryReleased
///   OnPrimaryDelta fires         -> SpinSide() (raw feel, dominant delta)
///   OnPrimaryReleased fires      -> unsubscribe, SnapToNearestAngle() (DOTween)
///   DOTween OnComplete           -> PutDown, snap integer coordinates, record move, ReadState, unlock input
/// </summary>
public class PivotRotation : MonoBehaviour
{
    // ── Fields ───────────────────────────────────────────────────────────────
    private List<GameObject> activeSide;
    private bool dragging = false;
    private Vector3 startEuler;

    private ReadCube readCube;
    private CubeState cubeState;

    private float sensitivity = 0.4f;
    private Vector3 rotation;

    private Tween snapTween;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        readCube = FindAnyObjectByType<ReadCube>();
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
        startEuler = transform.localEulerAngles;
        dragging = true;
        SubscribeToInput();
    }

    // ── Input Subscriptions ───────────────────────────────────────────────────

    private void SubscribeToInput()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnPrimaryDelta += HandleDelta;
        CubeInputReader.Instance.OnPrimaryReleased += HandleReleased;
    }

    private void UnsubscribeFromInput()
    {
        if (CubeInputReader.Instance == null) return;
        CubeInputReader.Instance.OnPrimaryDelta -= HandleDelta;
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

    // ── Spin (raw, direct — dominant drag) ────────────────────────────────────

    /// <summary>
    /// Maps dominant input delta directly to pivot rotation while dragging.
    /// Dominant delta prevents diagonal mouse cancellation.
    /// </summary>
    private void SpinSide(List<GameObject> side, Vector2 delta)
    {
        rotation = Vector3.zero;

        // Use dominant direction to prevent cancelling when dragging diagonally
        float dominant = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? delta.x : delta.y;
        float mag = dominant * sensitivity;

        if (side == cubeState.up) rotation.y = mag * 1f;
        else if (side == cubeState.Down) rotation.y = mag * -1f;
        else if (side == cubeState.left) rotation.z = mag * 1f;
        else if (side == cubeState.right) rotation.z = mag * -1f;
        else if (side == cubeState.front) rotation.x = mag * -1f;
        else if (side == cubeState.back) rotation.x = mag * 1f;

        transform.Rotate(rotation, Space.Self);
    }

    // ── Snap (DOTween) ────────────────────────────────────────────────────────

    /// <summary>
    /// Tweens the pivot to the nearest 90° after the player releases.
    /// Snaps only the single active face axis to completely eliminate gimbal twisting.
    /// </summary>
    public void SnapToNearestAngle()
    {
        Vector3 snapped = transform.localEulerAngles;

        if (cubeState != null)
        {
            if (activeSide == cubeState.up || activeSide == cubeState.Down)
            {
                snapped.y = Mathf.Round(snapped.y / 90f) * 90f;
            }
            else if (activeSide == cubeState.left || activeSide == cubeState.right)
            {
                snapped.z = Mathf.Round(snapped.z / 90f) * 90f;
            }
            else if (activeSide == cubeState.front || activeSide == cubeState.back)
            {
                snapped.x = Mathf.Round(snapped.x / 90f) * 90f;
            }
            else
            {
                snapped.x = Mathf.Round(snapped.x / 90f) * 90f;
                snapped.y = Mathf.Round(snapped.y / 90f) * 90f;
                snapped.z = Mathf.Round(snapped.z / 90f) * 90f;
            }
        }
        else
        {
            snapped.x = Mathf.Round(snapped.x / 90f) * 90f;
            snapped.y = Mathf.Round(snapped.y / 90f) * 90f;
            snapped.z = Mathf.Round(snapped.z / 90f) * 90f;
        }

        CubeInput.IsLocked = true;

        snapTween?.Kill();
        snapTween = transform
            .DOLocalRotate(snapped, 0.25f, RotateMode.FastBeyond360)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                cubeState.PutDown(activeSide, transform.parent);

                // Snap all layer pieces rigidly to integer coordinates and 90-degree rotations
                if (activeSide != null)
                {
                    foreach (GameObject go in activeSide)
                    {
                        if (go == null) continue;
                        Transform p = go.transform.parent;
                        if (p != null)
                        {
                            Vector3 pos = p.localPosition;
                            p.localPosition = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), Mathf.Round(pos.z));
                            Vector3 rot = p.localEulerAngles;
                            p.localEulerAngles = new Vector3(
                                Mathf.Round(rot.x / 90f) * 90f,
                                Mathf.Round(rot.y / 90f) * 90f,
                                Mathf.Round(rot.z / 90f) * 90f
                            );
                        }
                    }
                }
                Physics.SyncTransforms();

                // Track move in CubeManager
                if (CubeManager.Instance != null && activeSide != null)
                {
                    string move = DetectMove(activeSide, startEuler, snapped);
                    if (!string.IsNullOrEmpty(move))
                    {
                        CubeManager.Instance.RecordMove(move);
                    }
                }

                if (readCube != null) readCube.ReadState();
                CubeInput.IsLocked = false;
            });
    }

    private string DetectMove(List<GameObject> side, Vector3 start, Vector3 end)
    {
        if (cubeState == null) return null;

        char face = ' ';
        float delta = 0f;

        if (side == cubeState.up)
        {
            face = 'U';
            delta = Mathf.DeltaAngle(start.y, end.y);
        }
        else if (side == cubeState.Down)
        {
            face = 'D';
            delta = -Mathf.DeltaAngle(start.y, end.y);
        }
        else if (side == cubeState.front)
        {
            face = 'F';
            delta = -Mathf.DeltaAngle(start.x, end.x);
        }
        else if (side == cubeState.back)
        {
            face = 'B';
            delta = Mathf.DeltaAngle(start.x, end.x);
        }
        else if (side == cubeState.left)
        {
            face = 'L';
            delta = Mathf.DeltaAngle(start.z, end.z);
        }
        else if (side == cubeState.right)
        {
            face = 'R';
            delta = -Mathf.DeltaAngle(start.z, end.z);
        }

        int turns = Mathf.RoundToInt(delta / 90f);
        turns = ((turns % 4) + 4) % 4;

        switch (turns)
        {
            case 1: return face.ToString();
            case 2: return face.ToString() + "2";
            case 3: return face.ToString() + "'";
            default: return null;
        }
    }
}
