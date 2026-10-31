using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects which cube face the player taps/clicks and initiates a face-drag.
///
/// Input: subscribes to CubeInputReader.OnTapBegan — never calls Input.* directly.
/// Works identically on PC (left-click), mobile, and tablet (tap).
/// </summary>
public class SelectFace : MonoBehaviour
{
    // ── Fields ────────────────────────────────────────────────────────────────
    private CubeState cubeState;
    private ReadCube  readCube;

    /// <summary>Layer 8 — only cube face colliders live here.</summary>
    private readonly int layerMask = 1 << 8;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        readCube  = FindAnyObjectByType<ReadCube>();
        cubeState = FindAnyObjectByType<CubeState>();

        if (CubeInputReader.Instance != null)
            CubeInputReader.Instance.OnTapBegan += HandleTap;
    }

    void OnDestroy()
    {
        if (CubeInputReader.Instance != null)
            CubeInputReader.Instance.OnTapBegan -= HandleTap;
    }

    // ── Input Handler ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fires once per tap/click-down with the screen-space position.
    /// Ignored while any rotation animation is in progress.
    /// </summary>
    private void HandleTap(Vector2 screenPos)
    {
        if (CubeInput.IsLocked) return;

        readCube.ReadState();

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask)) return;

        GameObject face = hit.collider.gameObject;

        List<List<GameObject>> cubeSides = new List<List<GameObject>>()
        {
            cubeState.up,
            cubeState.Down,
            cubeState.front,
            cubeState.back,
            cubeState.right,
            cubeState.left
        };

        foreach (List<GameObject> cubeSide in cubeSides)
        {
            if (cubeSide.Contains(face))
            {
                cubeState.PickUp(cubeSide);
                break; // a face belongs to exactly one side
            }
        }
    }
}
