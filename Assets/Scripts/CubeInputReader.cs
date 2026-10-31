using System;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

/// <summary>
/// Singleton that owns all Unity New Input System actions and fires clean,
/// device-agnostic C# events. Game scripts subscribe to these events and
/// never call Input.* directly.
///
/// Device support:
///   PC     — left-click drag (face), right-click drag/swipe (view)
///   Mobile — single-finger drag (face), two-finger drag/swipe (view)
///   Tablet — same as mobile
/// </summary>
public class CubeInputReader : MonoBehaviour
{
    // ── Singleton with Auto-Instantiation ──────────────────────────────────────
    private static CubeInputReader _instance;
    public static CubeInputReader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<CubeInputReader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CubeInputReader");
                    _instance = go.AddComponent<CubeInputReader>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        _ = Instance;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>Screen-space position where primary contact began (tap/click-down).</summary>
    public event Action<Vector2> OnTapBegan;

    /// <summary>
    /// Per-frame delta while primary contact is held and secondary is NOT held.
    /// Used by PivotRotation to spin a cube face.
    /// </summary>
    public event Action<Vector2> OnPrimaryDelta;

    /// <summary>Primary contact ended (mouse-up / finger lifted).</summary>
    public event Action OnPrimaryReleased;

    /// <summary>
    /// Per-frame delta (already scaled) while secondary contact is held.
    /// Used by RotateCube to orbit the whole cube view.
    /// </summary>
    public event Action<Vector2> OnSecondaryDelta;

    /// <summary>
    /// Secondary contact ended.  
    /// Carries the normalised swipe direction if the gesture was a swipe,
    /// or Vector2.zero if it was just a short drag/release.
    /// </summary>
    public event Action<Vector2> OnSecondaryReleased;

    // ── Input Actions (built programmatically — no .inputactions asset needed) ──
    private InputAction _primaryPosition;   // screen pos of primary pointer
    private InputAction _primaryContact;    // left-mouse / touch0 press
    private InputAction _primaryDelta;      // frame delta of primary pointer
    private InputAction _secondaryContact;  // right-mouse / touch1 press
    private InputAction _secondaryDelta;    // frame delta of secondary pointer

    // ── Swipe state ──────────────────────────────────────────────────────────
    private Vector2 _secondaryStartPos;

    /// <summary>Minimum screen drag distance (px) to register as a swipe rather than a static click.</summary>
    private const float SwipeThreshold = 20f;

    /// <summary>Scales secondary delta before broadcasting — matches the old 0.1f feel.</summary>
    private const float OrbitSensitivity = 0.1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialise DOTween once here so no other script needs to
        DOTween.Init(recycleAllByDefault: true, useSafeMode: true)
               .SetCapacity(50, 10);

        BuildAndEnableActions();
    }

    void OnDestroy()
    {
        DisableAndDisposeActions();
        if (_instance == this) _instance = null;
    }

    // ── Action Setup ──────────────────────────────────────────────────────────

    private void BuildAndEnableActions()
    {
        // ── Primary position (used to report tap position) ──────────────────
        _primaryPosition = new InputAction("PrimaryPosition",
            InputActionType.Value, expectedControlType: "Vector2");
        _primaryPosition.AddBinding("<Mouse>/position");
        _primaryPosition.AddBinding("<Touchscreen>/primaryTouch/position");

        // ── Primary contact ─────────────────────────────────────────────────
        _primaryContact = new InputAction("PrimaryContact", InputActionType.Button);
        _primaryContact.AddBinding("<Mouse>/leftButton");
        _primaryContact.AddBinding("<Touchscreen>/primaryTouch/press");

        _primaryContact.started  += _ => OnTapBegan?.Invoke(_primaryPosition.ReadValue<Vector2>());
        _primaryContact.canceled += _ => OnPrimaryReleased?.Invoke();

        // ── Primary delta (face spin) ────────────────────────────────────────
        _primaryDelta = new InputAction("PrimaryDelta",
            InputActionType.Value, expectedControlType: "Vector2");
        _primaryDelta.AddBinding("<Mouse>/delta");
        _primaryDelta.AddBinding("<Touchscreen>/primaryTouch/delta");

        // ── Secondary contact ────────────────────────────────────────────────
        _secondaryContact = new InputAction("SecondaryContact", InputActionType.Button);
        _secondaryContact.AddBinding("<Mouse>/rightButton");
        _secondaryContact.AddBinding("<Touchscreen>/touch1/press");

        _secondaryContact.started += _ =>
        {
            _secondaryStartPos = _primaryPosition.ReadValue<Vector2>();
        };

        _secondaryContact.canceled += _ =>
        {
            Vector2 endPos = _primaryPosition.ReadValue<Vector2>();
            Vector2 diff = endPos - _secondaryStartPos;

            Vector2 swipeDir = diff.magnitude >= SwipeThreshold
                ? diff.normalized
                : Vector2.zero;

            OnSecondaryReleased?.Invoke(swipeDir);
        };

        // ── Secondary delta (view orbit) ─────────────────────────────────────
        _secondaryDelta = new InputAction("SecondaryDelta",
            InputActionType.Value, expectedControlType: "Vector2");
        _secondaryDelta.AddBinding("<Mouse>/delta");       // right-click drag on PC
        _secondaryDelta.AddBinding("<Touchscreen>/touch1/delta");  // 2nd finger on mobile

        // Enable all
        _primaryPosition.Enable();
        _primaryContact.Enable();
        _primaryDelta.Enable();
        _secondaryContact.Enable();
        _secondaryDelta.Enable();
    }

    private void DisableAndDisposeActions()
    {
        _primaryPosition?.Disable();   _primaryPosition?.Dispose();
        _primaryContact?.Disable();    _primaryContact?.Dispose();
        _primaryDelta?.Disable();      _primaryDelta?.Dispose();
        _secondaryContact?.Disable();  _secondaryContact?.Dispose();
        _secondaryDelta?.Disable();    _secondaryDelta?.Dispose();
    }

    // ── Per-Frame Delta Broadcasting ──────────────────────────────────────────

    void Update()
    {
        bool primaryHeld   = _primaryContact.IsPressed();
        bool secondaryHeld = _secondaryContact.IsPressed();

        // Face spin: primary held, secondary NOT held (avoids conflict on mobile)
        if (primaryHeld && !secondaryHeld)
        {
            Vector2 d = _primaryDelta.ReadValue<Vector2>();
            if (d.sqrMagnitude > 0.01f)
                OnPrimaryDelta?.Invoke(d);
        }

        // View orbit: secondary held
        if (secondaryHeld)
        {
            Vector2 d = _secondaryDelta.ReadValue<Vector2>();
            if (d.sqrMagnitude > 0.01f)
            {
                OnSecondaryDelta?.Invoke(d * OrbitSensitivity);
            }
        }
    }
}

