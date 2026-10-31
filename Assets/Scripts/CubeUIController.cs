using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete OnGUI controller for the Rubik's Cube Solver.
///
/// Features:
///   - Sleek, compact bottom dock (175px) with plenty of clearance below the 3D cube
///   - Automatic elevation of CubeHolder so the 3D cube is framed cleanly in the upper center
///   - Step Mode: 5-move window, color dot, notation + clear English explanation, progress bar, Prev/Finish/Next
///   - Full input locking during Step Solve to prevent accidental wrong moves
///   - Move Controls Pad: only shown in Normal mode
///   - Center overlay: Exit-warning confirmation dialog
/// </summary>
public class CubeUIController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<CubeUIController>() == null)
            new GameObject("CubeUIController").AddComponent<CubeUIController>();
    }

    // ── References ────────────────────────────────────────────────────────────
    private CubeManager      _manager;
    private CubeMoveExecutor _executor;

    // ── Normal-dock state ─────────────────────────────────────────────────────
    private string _statusMsg   = "Rubik's Cube Solver";
    private string _progressMsg = "Ready";
    private bool   _busy        = false;

    // ── Step-solve state ──────────────────────────────────────────────────────
    private enum UIMode { Normal, StepSolve, ExitWarning }
    private UIMode       _mode          = UIMode.Normal;
    private List<string> _solMoves      = new List<string>();
    private int          _stepIdx       = 0;   // index of NEXT move to execute
    private bool         _stepAnim      = false;
    private bool         _stepFinished  = false;

    // ── Face metadata ─────────────────────────────────────────────────────────
    private static readonly Dictionary<char, Color> FaceCol = new Dictionary<char, Color>
    {
        { 'U', new Color(0.95f, 0.82f, 0.05f) },
        { 'D', new Color(0.90f, 0.90f, 0.90f) },
        { 'R', new Color(0.15f, 0.40f, 0.85f) },
        { 'L', new Color(0.10f, 0.65f, 0.20f) },
        { 'F', new Color(0.90f, 0.42f, 0.04f) },
        { 'B', new Color(0.80f, 0.08f, 0.08f) },
    };

    private static readonly Dictionary<char, string> FaceName = new Dictionary<char, string>
    {
        { 'U', "Top" }, { 'D', "Bottom" }, { 'R', "Right" },
        { 'L', "Left" }, { 'F', "Front" }, { 'B', "Back" },
    };

    // ── Move-pad rows ─────────────────────────────────────────────────────────
    private static readonly (string face, Color cwCol, Color ccwCol)[] MoveRows =
    {
        ("U", new Color(0.90f, 0.75f, 0.05f), new Color(0.70f, 0.55f, 0.02f)),
        ("D", new Color(0.90f, 0.90f, 0.90f), new Color(0.65f, 0.65f, 0.65f)),
        ("R", new Color(0.15f, 0.40f, 0.85f), new Color(0.10f, 0.28f, 0.62f)),
        ("L", new Color(0.10f, 0.65f, 0.20f), new Color(0.07f, 0.45f, 0.14f)),
        ("F", new Color(0.90f, 0.42f, 0.04f), new Color(0.68f, 0.30f, 0.02f)),
        ("B", new Color(0.80f, 0.08f, 0.08f), new Color(0.55f, 0.05f, 0.05f)),
    };

    // ── Styles ────────────────────────────────────────────────────────────────
    private GUIStyle _boxS, _titleS, _subS, _progressS, _hintS, _bigMoveS, _bigMoveCenterS, _warnS, _moveLabelS;
    private GUIStyle _shuffleS, _autoSolveS, _stepSolveS, _smallS, _disabledS;
    private GUIStyle _cwS, _ccwS, _dbl2S;
    private GUIStyle _doneCellS, _curCellS, _futureCellS;
    private bool     _stylesReady = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start()
    {
        var old = GameObject.Find("SolverUIPanel");
        if (old != null) Destroy(old);

        // Elevate CubeHolder slightly so the 3D cube is framed cleanly in the upper center, well clear of the bottom dock
        GameObject ch = GameObject.Find("CubeHolder");
        if (ch != null && ch.transform.position.y < 0.2f)
        {
            ch.transform.position = new Vector3(ch.transform.position.x, 0.6f, ch.transform.position.z);
        }

        _manager = FindAnyObjectByType<CubeManager>();
        if (_manager == null) _manager = new GameObject("CubeManager").AddComponent<CubeManager>();
        _executor = FindAnyObjectByType<CubeMoveExecutor>();

        _manager.OnStatusChanged    += s  => _statusMsg   = s;
        _manager.OnStepProgress     += (c, t) => _progressMsg = $"Move {c} / {t}";
        _manager.OnOperationComplete += () => { _busy = false; _progressMsg = ""; };
    }

    void OnDestroy()
    {
        CubeInput.IsLocked = false;
    }

    // ── OnGUI ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        EnsureStyles();
        HandleKeyboard();

        var mat = GUI.matrix;
        var col = GUI.color;

        switch (_mode)
        {
            case UIMode.Normal:
                DrawMovePad();          // right panel — only visible in Normal mode
                DrawNormalDock();
                break;
            case UIMode.StepSolve:
                DrawStepPanel();
                break;
            case UIMode.ExitWarning:
                DrawStepPanel();        // step panel still behind
                DrawExitWarning();      // overlay on top
                break;
        }

        GUI.matrix = mat;
        GUI.color  = col;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PANEL 1 – Normal bottom dock (compact: pH = 115)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawNormalDock()
    {
        float sc = Scale1400();
        float pW = 540f, pH = 115f;
        SetMatrix((Screen.width - pW * sc) * .5f, Screen.height - (pH + 14f) * sc, sc);

        Box(0, 0, pW, pH);
        Lbl(10, 6, pW - 20, 26, _statusMsg, _titleS);
        Lbl(10, 34, pW - 20, 18, string.IsNullOrEmpty(_progressMsg) ? "Ready" : _progressMsg, _progressS);

        float bW = 160f, bH = 40f, bY = 62f, gap = 8f;
        float startX = (pW - (3 * bW + 2 * gap)) * .5f;

        GUI.enabled = !_busy;
        if (Btn(startX,            bY, bW, bH, "🎲  Shuffle",    _busy ? _disabledS : _shuffleS))
            { _busy = true; _progressMsg = "Please wait..."; _manager?.Shuffle(20); }
        if (Btn(startX + bW + gap, bY, bW, bH, "⚡  Auto Solve", _busy ? _disabledS : _autoSolveS))
            { _busy = true; _manager?.Solve(); }
        if (Btn(startX + 2*(bW+gap), bY, bW, bH, "📋  Step Solve", _busy ? _disabledS : _stepSolveS))
            EnterStepMode();
        GUI.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PANEL 2 – Step-by-step panel (Compact: pH = 175, plenty of clearance above)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawStepPanel()
    {
        float sc = Scale1400();
        float pW = 560f, pH = 175f;
        SetMatrix((Screen.width - pW * sc) * .5f, Screen.height - (pH + 14f) * sc, sc);

        Box(0, 0, pW, pH);

        // ── 1. Title + Exit (y = 5 to 26) ─────────────────────────────────
        Lbl(12, 6, 200, 20, "Step-by-Step Solver", _subS);
        if (Btn(pW - 68, 5, 58, 22, "✕  Exit", _smallS)) TryExit();

        // ── 2. 5-Move window (y = 28 to 56) ──────────────────────────────
        int total = _solMoves.Count;
        DrawMoveWindow(total, 28f, pW);

        // ── 3. Face dot + notation + plain-English hint in one sleek row (y = 62 to 90) ──
        if (_stepFinished)
        {
            Lbl(0, 64f, pW, 26, "🎉  Cube Solved Successfully!", _bigMoveCenterS);
        }
        else if (_stepIdx < total)
        {
            string mv  = _solMoves[_stepIdx];
            char face  = char.ToUpperInvariant(mv[0]);
            Color fcol = FaceCol.ContainsKey(face) ? FaceCol[face] : Color.white;

            string fname = FaceName.ContainsKey(face) ? FaceName[face] : face.ToString();
            string dir   = mv.Contains("2") ? "180° double turn" :
                           mv.Contains("'") ? "Counter-clockwise" : "Clockwise";
            string fullDesc = $"{fname} face  •  {dir}";

            // Center horizontal badge
            float badgeW = 340f;
            float badgeX = (pW - badgeW) * 0.5f;

            // Coloured dot
            GUI.DrawTexture(new Rect(badgeX, 67f, 18f, 18f), MakeTex(fcol));

            // Move notation text (e.g. D', U2)
            Lbl(badgeX + 26f, 62f, 52f, 28f, mv, _bigMoveS);

            // Plain English explanation
            Lbl(badgeX + 78f, 66f, 260f, 20f, fullDesc, _hintS);
        }

        // ── 4. Progress bar (y = 96 to 102) ─────────────────────────────
        float barY = 96f, barW = pW - 30f;
        float fill  = total > 0 ? (float)_stepIdx / total : 0f;
        Box(15, barY, barW, 6f);               // track
        if (fill > 0f)
        {
            GUI.color = new Color(0.15f, 0.75f, 0.35f);
            Box(15, barY, barW * fill, 6f);    // green fill
            GUI.color = Color.white;
        }

        // Step text (y = 104 to 120)
        string stepLbl = _stepFinished ? "Complete!" : $"Step {_stepIdx} / {total}";
        Lbl(0, 104f, pW, 16f, stepLbl, _progressS);

        // ── 5. Prev / Finish / Next buttons (y = 126 to 162) ─────────────
        float bY = 126f, bW = 160f, bH = 36f, bGap = 10f;
        float bStart = (pW - (3 * bW + 2 * bGap)) * .5f;
        bool canAct = !_stepAnim && !_busy;

        GUI.enabled = canAct && _stepIdx > 0;
        if (Btn(bStart, bY, bW, bH, "◀  Prev Step", canAct && _stepIdx > 0 ? _smallS : _disabledS))
            StepPrev();

        GUI.enabled = canAct && _stepIdx < total && !_stepFinished;
        if (Btn(bStart + bW + bGap, bY, bW, bH, "⚡  Finish", canAct && _stepIdx < total ? _autoSolveS : _disabledS))
            FinishRemaining();

        GUI.enabled = canAct && _stepIdx < total && !_stepFinished;
        if (Btn(bStart + 2*(bW + bGap), bY, bW, bH, "Next Step  ▶", canAct && _stepIdx < total ? _stepSolveS : _disabledS))
            StepNext();

        GUI.enabled = true;
    }

    void DrawMoveWindow(int total, float y, float panelW)
    {
        if (total == 0) return;

        int windowSize = Mathf.Min(5, total);
        int wStart = Mathf.Clamp(_stepIdx - 2, 0, Mathf.Max(0, total - windowSize));
        int wEnd   = Mathf.Min(wStart + windowSize - 1, total - 1);

        float cellW = 92f, cellH = 28f, cellGap = 4f;
        float totalCells = wEnd - wStart + 1;
        float seqW = totalCells * cellW + (totalCells - 1) * cellGap;
        float startX = (panelW - seqW) * .5f;

        for (int i = wStart; i <= wEnd; i++)
        {
            float cx = startX + (i - wStart) * (cellW + cellGap);
            GUIStyle cs;
            string lbl;

            if (i < _stepIdx)         { cs = _doneCellS;   lbl = "✓ " + _solMoves[i]; }
            else if (i == _stepIdx)   { cs = _curCellS;    lbl = _solMoves[i]; }
            else                      { cs = _futureCellS; lbl = _solMoves[i]; }

            GUI.Box(new Rect(cx, y, cellW, cellH), lbl, cs);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PANEL 3 – Right-side Move Pad (Only visible in Normal mode)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawMovePad()
    {
        float sc   = Mathf.Clamp(Screen.height / 720f, 0.70f, 1.30f);
        float padW = 230f;
        float lH   = 22f, rH = 38f, gap = 5f;
        float padH = 14f + MoveRows.Length * (lH + rH + gap) + 12f;
        SetMatrix(Screen.width - padW * sc - 12f * sc,
                  (Screen.height - padH * sc) * .5f, sc);

        Box(0, 0, padW, padH);
        Lbl(0, 6, padW, 20, "Move Controls", _subS);

        float y = 28f;
        GUI.enabled = !_busy && !_stepAnim;

        foreach (var (face, cwCol, ccwCol) in MoveRows)
        {
            Lbl(0, y, padW, lH, face + " face", _moveLabelS);
            y += lH;

            SetBtnTex(_cwS,  cwCol,  Lighten(cwCol,  0.15f));
            SetBtnTex(_ccwS, ccwCol, Lighten(ccwCol, 0.12f));
            SetBtnTex(_dbl2S, new Color(0.22f, 0.22f, 0.30f), new Color(0.32f, 0.32f, 0.42f));

            if (Btn(8,   y, 64, rH, face,       _cwS))  RunNotationMove(face);
            if (Btn(80,  y, 64, rH, face + "'", _ccwS)) RunNotationMove(face + "'");
            if (Btn(152, y, 64, rH, face + "2", _dbl2S)) RunNotationMove(face + "2");
            y += rH + gap;
        }

        GUI.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OVERLAY – Exit warning dialog
    // ─────────────────────────────────────────────────────────────────────────
    void DrawExitWarning()
    {
        GUI.matrix = Matrix4x4.identity;
        GUI.color  = new Color(0, 0, 0, 0.68f);
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _boxS);
        GUI.color = Color.white;

        float dW = 390f, dH = 148f;
        float dX = (Screen.width  - dW) * .5f;
        float dY = (Screen.height - dH) * .5f;

        GUI.Box(new Rect(dX, dY, dW, dH), GUIContent.none, _boxS);
        Lbl_Abs(dX + 10, dY + 10, dW - 20, 56,
            "The cube is partially solved.\nExit step-by-step mode?", _warnS);

        if (Btn_Abs(dX + 18,  dY + 98, 160, 38, "Stay",         _smallS))   _mode = UIMode.StepSolve;
        if (Btn_Abs(dX + 210, dY + 98, 160, 38, "Exit Anyway",  _disabledS)) ExitStepMode();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Step-mode logic (Locks cube face input, keeps view rotation active)
    // ─────────────────────────────────────────────────────────────────────────
    void EnterStepMode()
    {
        if (_manager == null) return;
        var moves = _manager.GetSolutionMoves();
        if (moves == null || moves.Count == 0)
            { _statusMsg = "Already Solved! Nothing to do."; return; }

        CubeInput.IsLocked = true; // Lock manual face clicks/drags so cube cannot be desynchronized!
        _solMoves     = moves;
        _stepIdx      = 0;
        _stepAnim     = false;
        _stepFinished = false;
        _mode         = UIMode.StepSolve;
    }

    void StepNext()
    {
        if (_stepAnim || _busy || _stepIdx >= _solMoves.Count) return;
        EnsureExecutor();
        if (_executor == null) return;

        string mv = _solMoves[_stepIdx];
        _stepAnim = true;
        float prev = _executor.moveDuration;
        _executor.moveDuration = 0.35f;

        _executor.ExecuteMove(mv, () =>
        {
            _executor.moveDuration = prev;
            _stepIdx++;
            _stepAnim = false;
            if (_stepIdx >= _solMoves.Count)
            {
                _stepFinished = true;
                _manager?.ClearHistory();
            }
        });
    }

    void StepPrev()
    {
        if (_stepAnim || _busy || _stepIdx <= 0) return;
        EnsureExecutor();
        if (_executor == null) return;

        _stepIdx--;
        _stepFinished = false;
        string inv = InvertOne(_solMoves[_stepIdx]);
        _stepAnim = true;
        float prev = _executor.moveDuration;
        _executor.moveDuration = 0.35f;

        _executor.ExecuteMove(inv, () =>
        {
            _executor.moveDuration = prev;
            _stepAnim = false;
        });
    }

    void FinishRemaining()
    {
        if (_stepAnim || _busy || _stepIdx >= _solMoves.Count) return;
        EnsureExecutor();
        if (_executor == null) return;

        var rem = _solMoves.GetRange(_stepIdx, _solMoves.Count - _stepIdx);
        string seq = string.Join(" ", rem);
        int baseIdx = _stepIdx;
        _busy = _stepAnim = true;
        float prev = _executor.moveDuration;
        _executor.moveDuration = 0.22f;

        _executor.ExecuteSequence(seq,
            onStep: (s, t) => _stepIdx = baseIdx + s,
            onComplete: () =>
            {
                _executor.moveDuration = prev;
                _stepIdx = _solMoves.Count;
                _stepFinished = true;
                _stepAnim = _busy = false;
                _manager?.ClearHistory();
            });
    }

    void TryExit()
    {
        if (_stepIdx == 0 || _stepFinished) ExitStepMode();
        else _mode = UIMode.ExitWarning;
    }

    void ExitStepMode()
    {
        CubeInput.IsLocked = false; // Restore manual face interaction
        _mode         = UIMode.Normal;
        _solMoves.Clear();
        _stepIdx      = 0;
        _stepAnim     = false;
        _stepFinished = false;
        _statusMsg    = "Rubik's Cube Solver";
        _progressMsg  = "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard
    // ─────────────────────────────────────────────────────────────────────────
    void HandleKeyboard()
    {
        if (Event.current.type != EventType.KeyDown) return;
        var kc = Event.current.keyCode;

        if (_mode == UIMode.StepSolve)
        {
            if      (kc == KeyCode.RightArrow) StepNext();
            else if (kc == KeyCode.LeftArrow)  StepPrev();
            else if (kc == KeyCode.Escape)     TryExit();
        }
        else if (_mode == UIMode.ExitWarning)
        {
            if      (kc == KeyCode.Escape) _mode = UIMode.StepSolve;
            else if (kc == KeyCode.Return) ExitStepMode();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Notation move pad execution
    // ─────────────────────────────────────────────────────────────────────────
    void RunNotationMove(string logicalMove)
    {
        EnsureExecutor();
        if (_executor == null || _executor.IsExecuting) return;

        char lFace = char.ToUpperInvariant(logicalMove[0]);
        char pFace = _executor.ResolveFaceByCamera(lFace);
        string suffix  = logicalMove.Length > 1 ? logicalMove.Substring(1) : "";
        string resolved = pFace + suffix;

        _manager?.RecordMove(resolved);
        string lbl = pFace != lFace ? $"{logicalMove} → {resolved}" : logicalMove;
        _manager?.OnStatusChangedInvoke($"Applied: {lbl}");
        _executor.ExecuteMove(resolved);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    void EnsureExecutor()
    {
        if (_executor == null) _executor = FindAnyObjectByType<CubeMoveExecutor>();
    }

    static string InvertOne(string mv)
    {
        if (string.IsNullOrEmpty(mv)) return mv;
        char f = char.ToUpperInvariant(mv[0]);
        if (mv.Contains("2")) return f + "2";
        if (mv.Contains("'")) return f.ToString();
        return f + "'";
    }

    float Scale1400() => Mathf.Clamp(Screen.width / 1400f, 0.65f, 1.15f);

    void SetMatrix(float x, float y, float sc) =>
        GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(sc, sc, 1));

    void Box(float x, float y, float w, float h) =>
        GUI.Box(new Rect(x, y, w, h), GUIContent.none, _boxS);

    void Lbl(float x, float y, float w, float h, string t, GUIStyle s) =>
        GUI.Label(new Rect(x, y, w, h), t, s);

    void Lbl_Abs(float x, float y, float w, float h, string t, GUIStyle s) =>
        GUI.Label(new Rect(x, y, w, h), t, s);

    bool Btn(float x, float y, float w, float h, string lbl, GUIStyle s) =>
        GUI.Button(new Rect(x, y, w, h), lbl, s);

    bool Btn_Abs(float x, float y, float w, float h, string lbl, GUIStyle s) =>
        GUI.Button(new Rect(x, y, w, h), lbl, s);

    static Color Lighten(Color c, float a) =>
        new Color(Mathf.Clamp01(c.r + a), Mathf.Clamp01(c.g + a), Mathf.Clamp01(c.b + a), c.a);

    static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(2, 2);
        t.SetPixels(new[] { col, col, col, col });
        t.Apply();
        return t;
    }

    static void SetBtnTex(GUIStyle s, Color normal, Color hover)
    {
        if (s.normal.background != null) Object.Destroy(s.normal.background);
        if (s.hover.background  != null) Object.Destroy(s.hover.background);
        s.normal.background = MakeTex(normal);
        s.hover.background  = MakeTex(hover);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Style factory
    // ─────────────────────────────────────────────────────────────────────────
    void EnsureStyles()
    {
        if (_stylesReady) return;

        _boxS = new GUIStyle(GUI.skin.box)
            { normal = { background = MakeTex(new Color(0.07f, 0.09f, 0.14f, 0.93f)) } };

        _titleS         = Lbl_Style(18, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        _subS           = Lbl_Style(13, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.75f, 0.85f, 1f));
        _progressS      = Lbl_Style(12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.30f, 0.85f, 1f));
        _moveLabelS     = Lbl_Style(12, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.75f, 0.80f, 0.95f));
        _bigMoveS       = Lbl_Style(22, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
        _bigMoveCenterS = Lbl_Style(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        _hintS          = Lbl_Style(13, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 0.98f));
        _warnS          = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter,
                            wordWrap = true, normal = { textColor = Color.white } };

        // Sequence-window cell styles
        _doneCellS   = BoxLbl_Style(13, FontStyle.Bold, TextAnchor.MiddleCenter,
                           new Color(0.50f, 0.50f, 0.50f), new Color(0.12f, 0.14f, 0.18f));
        _curCellS    = BoxLbl_Style(15, FontStyle.Bold, TextAnchor.MiddleCenter,
                           Color.white, new Color(0.15f, 0.52f, 0.90f));
        _futureCellS = BoxLbl_Style(12, FontStyle.Normal, TextAnchor.MiddleCenter,
                           new Color(0.62f, 0.68f, 0.78f), new Color(0.10f, 0.12f, 0.16f));

        // Action buttons
        _shuffleS   = Btn_Style(15, new Color(0.18f, 0.45f, 0.88f), new Color(0.25f, 0.55f, 0.98f));
        _autoSolveS = Btn_Style(15, new Color(0.15f, 0.70f, 0.35f), new Color(0.20f, 0.82f, 0.42f));
        _stepSolveS = Btn_Style(15, new Color(0.50f, 0.22f, 0.82f), new Color(0.62f, 0.32f, 0.94f));
        _smallS     = Btn_Style(12, new Color(0.22f, 0.28f, 0.40f), new Color(0.30f, 0.38f, 0.52f));
        _disabledS  = Btn_Style(13, new Color(0.25f, 0.28f, 0.35f, 0.7f), new Color(0.25f, 0.28f, 0.35f, 0.7f));
        _disabledS.normal.textColor = new Color(0.65f, 0.65f, 0.65f, 0.7f);

        _cwS   = Btn_Style(14, new Color(0.3f, 0.3f, 0.5f), new Color(0.4f, 0.4f, 0.6f));
        _ccwS  = Btn_Style(14, new Color(0.3f, 0.3f, 0.5f), new Color(0.4f, 0.4f, 0.6f));
        _dbl2S = Btn_Style(14, new Color(0.22f, 0.22f, 0.3f), new Color(0.32f, 0.32f, 0.42f));

        _stylesReady = true;
    }

    static GUIStyle Lbl_Style(int size, FontStyle font, TextAnchor align, Color color)
    {
        var s = new GUIStyle(GUI.skin.label)
            { fontSize = size, fontStyle = font, alignment = align };
        s.normal.textColor = color;
        return s;
    }

    static GUIStyle BoxLbl_Style(int size, FontStyle font, TextAnchor align, Color textColor, Color bgColor)
    {
        var s = new GUIStyle(GUI.skin.box)
            { fontSize = size, fontStyle = font, alignment = align };
        s.normal.textColor = textColor;
        s.normal.background = MakeTex(bgColor);
        return s;
    }

    static GUIStyle Btn_Style(int size, Color normal, Color hover)
    {
        var s = new GUIStyle(GUI.skin.button)
            { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        s.normal.textColor = s.hover.textColor = Color.white;
        s.normal.background = MakeTex(normal);
        s.hover.background  = MakeTex(hover);
        return s;
    }
}
