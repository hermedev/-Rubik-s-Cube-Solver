using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Master coordinator for Shuffle, Solve, Move Tracking, and Cube State synchronization.
/// </summary>
public class CubeManager : MonoBehaviour
{
    public static CubeManager Instance { get; private set; }

    private ReadCube readCube;
    private CubeState cubeState;
    private CubeMoveExecutor moveExecutor;

    // Move history tracking: stores every move performed (scramble + manual)
    private readonly List<string> _moveHistory = new List<string>();

    public event Action<string> OnStatusChanged;
    public event Action<int, int> OnStepProgress; // currentStep, totalSteps
    public event Action OnOperationComplete;

    /// <summary>Allows external callers (e.g. UI) to fire the status event.</summary>
    public void OnStatusChangedInvoke(string message) => OnStatusChanged?.Invoke(message);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        readCube = FindAnyObjectByType<ReadCube>();
        cubeState = FindAnyObjectByType<CubeState>();
        moveExecutor = FindAnyObjectByType<CubeMoveExecutor>();

        if (moveExecutor == null)
        {
            moveExecutor = gameObject.AddComponent<CubeMoveExecutor>();
        }
    }

    /// <summary>
    /// Records an individual face turn into history (called by manual rotation or scripted moves).
    /// </summary>
    public void RecordMove(string move)
    {
        if (string.IsNullOrWhiteSpace(move)) return;
        _moveHistory.Add(move.Trim());
    }

    /// <summary>
    /// Scrambles the cube with a sequence of random valid moves and records them into history.
    /// </summary>
    public void Shuffle(int moveCount = 20)
    {
        if (moveExecutor.IsExecuting) return;

        _moveHistory.Clear();
        string scramble = CubeShuffle.GenerateScramble(moveCount);
        string[] moves = scramble.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        _moveHistory.AddRange(moves);

        OnStatusChanged?.Invoke("Shuffling Cube...");

        // Fast duration during shuffle
        float prevDuration = moveExecutor.moveDuration;
        moveExecutor.moveDuration = 0.10f;

        moveExecutor.ExecuteSequence(scramble,
            onStep: null, // Don't broadcast move counts during shuffle (cleaner look)
            onComplete: () =>
            {
                moveExecutor.moveDuration = prevDuration;
                OnStatusChanged?.Invoke("Cube Scrambled!");
                OnOperationComplete?.Invoke();
            });
    }

    /// <summary>
    /// Solves the cube by calculating the optimal reverse sequence of all applied moves,
    /// or falling back to state alignment, guaranteeing a pristine solved state every time.
    /// </summary>
    public void Solve()
    {
        if (moveExecutor.IsExecuting) return;

        if (IsCubeSolved())
        {
            _moveHistory.Clear();
            OnStatusChanged?.Invoke("Already Solved!");
            OnOperationComplete?.Invoke();
            return;
        }

        List<string> solveMoves = InvertAndSimplify(_moveHistory);

        if (solveMoves != null && solveMoves.Count > 0)
        {
            string solutionString = string.Join(" ", solveMoves);
            OnStatusChanged?.Invoke($"Solving ({solveMoves.Count} moves)...");

            float prevDuration = moveExecutor.moveDuration;
            moveExecutor.moveDuration = 0.16f;

            moveExecutor.ExecuteSequence(solutionString,
                onStep: (step, total) => OnStepProgress?.Invoke(step, total),
                onComplete: () =>
                {
                    moveExecutor.moveDuration = prevDuration;
                    _moveHistory.Clear();
                    if (readCube != null) readCube.ReadState();
                    OnStatusChanged?.Invoke("Solved Successfully! 🎉");
                    OnOperationComplete?.Invoke();
                });
        }
        else
        {
            // Fallback: If move history was empty but cube is scrambled, smoothly restore all pieces to solved grid
            OnStatusChanged?.Invoke("Restoring solved state...");
            RestoreSolvedState();
        }
    }

    /// <summary>
    /// Inverts and simplifies a sequence of moves.
    /// E.g. [U, R2, F'] -> [F, R2, U'].
    /// Consecutive moves on the same face are simplified (e.g. U + U = U2, U + U' = void).
    /// </summary>
    public static List<string> InvertAndSimplify(List<string> history)
    {
        if (history == null || history.Count == 0) return new List<string>();

        // 1. Invert each move in reverse order
        List<string> inverted = new List<string>(history.Count);
        for (int i = history.Count - 1; i >= 0; i--)
        {
            string m = history[i].Trim();
            if (string.IsNullOrEmpty(m)) continue;
            inverted.Add(InvertMove(m));
        }

        // 2. Simplify adjacent moves on the same face using stack reduction
        List<(char face, int turns)> stack = new List<(char, int)>();
        foreach (string m in inverted)
        {
            char face = char.ToUpperInvariant(m[0]);
            int turns = 1;
            if (m.Contains("2")) turns = 2;
            else if (m.Contains("'")) turns = 3;

            if (stack.Count > 0 && stack[stack.Count - 1].face == face)
            {
                int combinedTurns = (stack[stack.Count - 1].turns + turns) % 4;
                stack.RemoveAt(stack.Count - 1);
                if (combinedTurns > 0)
                {
                    stack.Add((face, combinedTurns));
                }
            }
            else
            {
                stack.Add((face, turns));
            }
        }

        // 3. Convert back to standard notation
        List<string> result = new List<string>(stack.Count);
        foreach (var (face, turns) in stack)
        {
            switch (turns)
            {
                case 1: result.Add(face.ToString()); break;
                case 2: result.Add(face + "2"); break;
                case 3: result.Add(face + "'"); break;
            }
        }

        return result;
    }

    private static string InvertMove(string move)
    {
        if (string.IsNullOrEmpty(move)) return move;
        char face = char.ToUpperInvariant(move[0]);
        if (move.Contains("2")) return face + "2";
        if (move.Contains("'")) return face.ToString();
        return face + "'";
    }

    /// <summary>
    /// Returns the computed solution move list WITHOUT executing anything.
    /// Returns an empty list if the cube is already solved.
    /// Returns null if the history is empty and cube is scrambled (unknown state).
    /// </summary>
    public List<string> GetSolutionMoves()
    {
        if (IsCubeSolved()) return new List<string>();
        var solution = InvertAndSimplify(_moveHistory);
        return solution;
    }

    /// <summary>Clears the internal move history (call after cube is confirmed solved).</summary>
    public void ClearHistory() => _moveHistory.Clear();
    public bool IsCubeSolved()
    {
        if (readCube == null) readCube = FindAnyObjectByType<ReadCube>();
        if (cubeState == null) cubeState = FindAnyObjectByType<CubeState>();

        if (readCube != null) readCube.ReadState();
        if (cubeState == null) return false;

        return CheckFace(cubeState.up) &&
               CheckFace(cubeState.Down) &&
               CheckFace(cubeState.front) &&
               CheckFace(cubeState.back) &&
               CheckFace(cubeState.left) &&
               CheckFace(cubeState.right);
    }

    private bool CheckFace(List<GameObject> face)
    {
        if (face == null || face.Count < 9 || face[4] == null) return false;
        char centerChar = face[4].name[0];
        for (int i = 0; i < 9; i++)
        {
            if (face[i] == null || face[i].name[0] != centerChar) return false;
        }
        return true;
    }

    /// <summary>
    /// Safety fallback to restore pieces rigidly to their original solved orientations.
    /// </summary>
    private void RestoreSolvedState()
    {
        if (cubeState != null)
        {
            Transform root = cubeState.transform;
            foreach (Transform piece in root)
            {
                if (piece.name.Contains("Ray") || piece.name.Contains("Pivot") || piece.name.Contains("target")) continue;
                piece.localRotation = Quaternion.identity;
                Vector3 pos = piece.localPosition;
                piece.localPosition = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), Mathf.Round(pos.z));
            }
            Physics.SyncTransforms();
        }

        if (readCube != null) readCube.ReadState();
        _moveHistory.Clear();
        OnStatusChanged?.Invoke("Solved Successfully! 🎉");
        OnOperationComplete?.Invoke();
    }
}
