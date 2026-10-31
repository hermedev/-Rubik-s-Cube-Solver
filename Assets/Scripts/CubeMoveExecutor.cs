using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Robust, drift-free move executor for the 3x3 Rubik's cube.
/// Selects face pieces strictly by their local grid position (-1, 0, 1) under CubeRoot,
/// and rotates them via a dedicated local pivot. Pieces CANNOT drift, skew, or break apart.
/// </summary>
public class CubeMoveExecutor : MonoBehaviour
{
    public static CubeMoveExecutor Instance { get; private set; }

    private ReadCube readCube;
    private CubeState cubeState;
    private Transform cubeRoot;
    private Transform movePivot;

    /// <summary>Duration in seconds for a single 90° face turn.</summary>
    public float moveDuration = 0.16f;

    /// <summary>True while an automated sequence of moves is playing.</summary>
    public bool IsExecuting { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        cubeState = FindAnyObjectByType<CubeState>();
        readCube = FindAnyObjectByType<ReadCube>();

        if (cubeState != null)
        {
            cubeRoot = cubeState.transform;
            CreatePivot();
        }
    }

    private void CreatePivot()
    {
        if (movePivot != null) return;

        // Check if an existing pivot exists
        Transform existing = cubeRoot.Find("CubeMovePivot");
        if (existing != null)
        {
            movePivot = existing;
            return;
        }

        GameObject pGo = new GameObject("CubeMovePivot");
        pGo.transform.SetParent(cubeRoot, false);
        pGo.transform.localPosition = Vector3.zero;
        pGo.transform.localRotation = Quaternion.identity;
        movePivot = pGo.transform;
    }

    public void ExecuteMove(string move, Action onComplete = null)
    {
        StartCoroutine(ExecuteMoveRoutine(move, onComplete));
    }

    public void ExecuteSequence(string sequence, Action<int, int> onStep = null, Action onComplete = null)
    {
        if (string.IsNullOrWhiteSpace(sequence))
        {
            onComplete?.Invoke();
            return;
        }

        string[] moves = sequence.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        StartCoroutine(ExecuteSequenceRoutine(moves, onStep, onComplete));
    }

    private IEnumerator ExecuteSequenceRoutine(string[] moves, Action<int, int> onStep, Action onComplete)
    {
        IsExecuting = true;
        CubeInput.IsLocked = true;

        for (int i = 0; i < moves.Length; i++)
        {
            onStep?.Invoke(i + 1, moves.Length);
            bool stepFinished = false;
            StartCoroutine(ExecuteMoveRoutine(moves[i], () => stepFinished = true));
            yield return new WaitUntil(() => stepFinished);
        }

        IsExecuting = false;
        CubeInput.IsLocked = false;
        onComplete?.Invoke();
    }

    private IEnumerator ExecuteMoveRoutine(string move, Action onComplete)
    {
        CubeInput.IsLocked = true;

        if (readCube == null) readCube = FindAnyObjectByType<ReadCube>();
        if (cubeState == null) cubeState = FindAnyObjectByType<CubeState>();
        if (cubeRoot == null && cubeState != null) cubeRoot = cubeState.transform;
        if (movePivot == null && cubeRoot != null) CreatePivot();

        if (!ParseMoveParameters(move, out char face, out Vector3 localAxis, out float angle))
        {
            Debug.LogWarning($"[CubeMoveExecutor] Unknown move: '{move}'");
            CubeInput.IsLocked = IsExecuting;
            onComplete?.Invoke();
            yield break;
        }

        // Get exactly the 9 pieces on this face layer by geometric coordinate
        List<Transform> pieces = GetPiecesForFace(face);
        if (pieces.Count != 9)
        {
            Debug.LogWarning($"[CubeMoveExecutor] Expected 9 pieces for face '{face}', found {pieces.Count}. Falling back to clean coordinates.");
        }

        // Reset pivot to origin and parent all layer pieces to it
        movePivot.localPosition = Vector3.zero;
        movePivot.localRotation = Quaternion.identity;

        foreach (Transform p in pieces)
        {
            p.SetParent(movePivot, true);
        }

        // Smoothly rotate the pivot in local space
        bool tweenDone = false;
        movePivot.DOLocalRotate(localAxis * angle, moveDuration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => tweenDone = true);

        yield return new WaitUntil(() => tweenDone);

        // Put pieces back under cubeRoot and rigidly snap to integer coordinates (-1, 0, 1)
        foreach (Transform p in pieces)
        {
            p.SetParent(cubeRoot, true);

            Vector3 pos = p.localPosition;
            p.localPosition = new Vector3(
                Mathf.Round(pos.x),
                Mathf.Round(pos.y),
                Mathf.Round(pos.z)
            );

            Vector3 rot = p.localEulerAngles;
            p.localEulerAngles = new Vector3(
                Mathf.Round(rot.x / 90f) * 90f,
                Mathf.Round(rot.y / 90f) * 90f,
                Mathf.Round(rot.z / 90f) * 90f
            );
        }

        movePivot.localRotation = Quaternion.identity;

        // Force PhysX update so ReadCube gets fresh facelet hits
        Physics.SyncTransforms();
        if (readCube != null)
        {
            readCube.ReadState();
        }

        CubeInput.IsLocked = IsExecuting;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Selects all 9 LittleCube pieces physically located on the requested face layer.
    /// Based on the scene hierarchy:
    ///   U: y = +1  (Up)
    ///   D: y = -1  (Down)
    ///   F: x = -1  (Front)
    ///   B: x = +1  (Back)
    ///   L: z = +1  (Left)
    ///   R: z = -1  (Right)
    /// </summary>
    private List<Transform> GetPiecesForFace(char face)
    {
        List<Transform> list = new List<Transform>();
        if (cubeRoot == null) return list;

        foreach (Transform child in cubeRoot)
        {
            // Skip pivot and non-piece objects
            if (child == movePivot || child.name.Contains("Ray") || child.name.Contains("target")) continue;

            Vector3 pos = child.localPosition;

            switch (face)
            {
                case 'U':
                    if (pos.y > 0.5f) list.Add(child);
                    break;
                case 'D':
                    if (pos.y < -0.5f) list.Add(child);
                    break;
                case 'F':
                    if (pos.x < -0.5f) list.Add(child);
                    break;
                case 'B':
                    if (pos.x > 0.5f) list.Add(child);
                    break;
                case 'L':
                    if (pos.z > 0.5f) list.Add(child);
                    break;
                case 'R':
                    if (pos.z < -0.5f) list.Add(child);
                    break;
            }
        }

        return list;
    }

    // ── Physical face outward normals in LOCAL cube space ─────────────────────
    // These match the coordinate mapping in GetPiecesForFace:
    //   U -> local +Y   D -> local -Y
    //   F -> local -X   B -> local +X
    //   L -> local +Z   R -> local -Z
    private static readonly (char face, Vector3 localNormal)[] FaceLocalNormals =
    {
        ('U', Vector3.up),
        ('D', Vector3.down),
        ('F', Vector3.left),    // F layer at x < -0.5 → outward normal is local -X = Vector3.left
        ('B', Vector3.right),   // B layer at x > +0.5 → outward normal is local +X
        ('L', Vector3.forward), // L layer at z > +0.5 → outward normal is local +Z
        ('R', Vector3.back),    // R layer at z < -0.5 → outward normal is local -Z
    };

    /// <summary>
    /// Remaps a logical face letter (U/D/F/B/L/R) as the player understands it
    /// (relative to their current camera view) into the physical face letter that
    /// actually occupies that position in the cube's current world rotation.
    ///
    /// Example: after spinning the cube so the orange face is now in front,
    ///          logical 'F' resolves to physical 'F' (orange) even though the cube has rotated.
    ///
    /// Called only for manual UI button presses — automated sequences use absolute notation.
    /// </summary>
    public char ResolveFaceByCamera(char logicalFace)
    {
        if (cubeRoot == null) return logicalFace;

        Camera cam = Camera.main;
        if (cam == null) return logicalFace;

        // ── Compute the world-space "reference direction" for each logical face ──
        // U/D are always world Y (gravity is always up/down for the player).
        // F/B are toward/away from the camera.
        // R/L are the camera's own right/left (horizontal plane).

        Vector3 camTowardCube = (cubeRoot.position - cam.transform.position).normalized;
        Vector3 camRight      = cam.transform.right;
        // Re-orthogonalise right so it stays horizontal even if camera tilts
        camRight = Vector3.ProjectOnPlane(camRight, Vector3.up).normalized;
        if (camRight == Vector3.zero) camRight = cam.transform.right;

        Vector3 logicalDir;
        switch (char.ToUpperInvariant(logicalFace))
        {
            case 'U': logicalDir = Vector3.up;          break;
            case 'D': logicalDir = Vector3.down;        break;
            case 'F': logicalDir = camTowardCube;       break;  // toward camera = player sees this as front
            case 'B': logicalDir = -camTowardCube;      break;
            case 'R': logicalDir = camRight;            break;
            case 'L': logicalDir = -camRight;           break;
            default:  return logicalFace;
        }

        // ── Find the physical face whose world-space outward normal is closest ──
        char best     = logicalFace;
        float bestDot = float.MinValue;

        foreach (var (face, localNormal) in FaceLocalNormals)
        {
            // Transform local normal to world space using current cube rotation
            Vector3 worldNormal = cubeRoot.TransformDirection(localNormal);
            float   dot         = Vector3.Dot(worldNormal, logicalDir);

            if (dot > bestDot)
            {
                bestDot = dot;
                best    = face;
            }
        }

        return best;
    }

    /// <summary>
    /// Maps standard notation to face character, local rotation axis, and signed angle.
    /// Clockwise looking directly at each face:
    ///   U: +Y, angle = +90
    ///   D: +Y, angle = -90
    ///   F: +X, angle = -90
    ///   B: +X, angle = +90
    ///   L: +Z, angle = +90
    ///   R: +Z, angle = -90
    /// </summary>
    private bool ParseMoveParameters(string move, out char face, out Vector3 localAxis, out float angle)
    {
        face = ' ';
        localAxis = Vector3.zero;
        angle = 0f;

        if (string.IsNullOrEmpty(move)) return false;

        face = char.ToUpperInvariant(move[0]);
        bool isPrime = move.Contains("'");
        bool isDouble = move.Contains("2");

        float multiplier = isDouble ? 2f : 1f;
        if (isPrime) multiplier = -multiplier;

        switch (face)
        {
            case 'U':
                localAxis = Vector3.up;
                angle = 90f * multiplier;
                break;
            case 'D':
                localAxis = Vector3.up;
                angle = -90f * multiplier;
                break;
            case 'F':
                localAxis = Vector3.right;
                angle = -90f * multiplier;
                break;
            case 'B':
                localAxis = Vector3.right;
                angle = 90f * multiplier;
                break;
            case 'L':
                localAxis = Vector3.forward;
                angle = 90f * multiplier;
                break;
            case 'R':
                localAxis = Vector3.forward;
                angle = -90f * multiplier;
                break;
            default:
                return false;
        }

        return true;
    }
}
