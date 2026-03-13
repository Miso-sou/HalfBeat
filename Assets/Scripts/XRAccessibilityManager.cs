// XRAccessibilityManager.cs
using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Accessibility controller system for a Beat Saber–style rhythm game.
///
/// DOMINANT HAND (Inspector)
///   Right — right hand is active; left is driven by mirroring/flick (default).
///   Left  — left hand is active; right is driven by mirroring/flick.
///   Both  — no accessibility overrides; both TrackedPoseDrivers run normally.
///
/// CONTROL MODES
///   PositionMirror— (Unfrozen) non-dominant TrackedPoseDriver is DISABLED; dominant controller
///                   drives non-dominant via static point reflection (X and Y inverted).
///   FlickFixed    — (Freeze Mode) Calculates the positional offset between the hands at the 
///                   moment of freezing. The non-dominant controller rigidly maintains this 
///                   offset from the dominant hand.
///   Swap          — sabers re-parented to opposite controllers. The non-dominant controller 
///                   is completely DEACTIVATED. Only the dominant hand is active.
///
/// FLICK MODES
///   NoInversion      — standard mirrored rotation. Sabers swing perfectly parallel.
///   BothAxisInversion— rotation is flipped 180 degrees.
/// </summary>
public class XRAccessibilityManager : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────
    //  Inspector fields
    // ──────────────────────────────────────────────────────────────

    [Header("Dominant Hand")]
    public DominantHand dominantHand = DominantHand.Right;

    [Header("Scene References (required)")]
    public Transform xrOrigin;
    public UnityEngine.InputSystem.XR.TrackedPoseDriver leftTrackedDriver;
    public UnityEngine.InputSystem.XR.TrackedPoseDriver rightTrackedDriver;

    [Header("Saber References (required for Swap mode)")]
    public Transform leftSaber;
    public Transform rightSaber;

    [Header("Right Hand Input Actions")]
    public InputActionProperty gripTriggerAction;
    public InputActionProperty primaryButton;
    public InputActionProperty secondaryButton;
    public InputActionProperty joystickAction;

    [Header("Left Hand Input Actions")]
    public InputActionProperty leftGripTriggerAction;
    public InputActionProperty leftPrimaryButton;
    public InputActionProperty leftSecondaryButton;
    public InputActionProperty leftJoystickAction;

    // ──────────────────────────────────────────────────────────────
    //  Enums
    // ──────────────────────────────────────────────────────────────

    public enum DominantHand { Right, Left, Both }
    public enum ControlMode { Default, PositionMirror, FlickFixed, Swap }
    public enum FlickMode { NoInversion, BothAxisInversion }

    // ──────────────────────────────────────────────────────────────
    //  Inspector-visible state
    // ──────────────────────────────────────────────────────────────

    [Header("State (read-only in play)")]
    public ControlMode currentMode = ControlMode.PositionMirror;
    public FlickMode   flickMode   = FlickMode.BothAxisInversion;

    [Header("Position Mirror Settings")]
    [Tooltip("Fixed height for Y-axis mirroring (meters relative to XR Origin floor).")]
    public float mirrorCenterHeight = 1.2f;

    [Header("Joystick Settings")]
    public float joystickSpeed = 0.5f;

    // ──────────────────────────────────────────────────────────────
    //  Private state
    // ──────────────────────────────────────────────────────────────

    UnityEngine.InputSystem.XR.TrackedPoseDriver dominantDriver;
    UnityEngine.InputSystem.XR.TrackedPoseDriver nonDominantDriver;
    Transform dominantSaber;
    Transform nonDominantSaber;
    InputActionProperty domGrip;
    InputActionProperty domPrimary;
    InputActionProperty domSecondary;
    InputActionProperty domJoystick;

    Vector3 joystickOffset;

    ControlMode modeBeforeSwap = ControlMode.PositionMirror;
    FlickMode flickModeBeforeSwap = FlickMode.BothAxisInversion;

    Vector3    nonDomSaberOrigLocalPos;
    Quaternion nonDomSaberOrigLocalRot;
    Vector3    domSaberOrigLocalPos;
    Quaternion domSaberOrigLocalRot;
    bool       saberOffsetsStored;
    bool       saberSwapped;

    // Freeze state variable: Stores the rigid distance/offset between the two sabers
    Vector3 frozenOffsetFromDominant;
    
    Vector3 saberGestureBaseLocalPos;
    bool wasCrossed;
    bool actionsEnabled;
    bool debugLogs = true;

    // ──────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        if (leftTrackedDriver == null || rightTrackedDriver == null || xrOrigin == null)
            Debug.LogError("[XRAccessibility] Missing required scene references — assign in Inspector.");

        if (dominantHand == DominantHand.Right)
        {
            dominantDriver    = rightTrackedDriver;
            nonDominantDriver = leftTrackedDriver;
            dominantSaber     = rightSaber;
            nonDominantSaber  = leftSaber;
            domGrip           = gripTriggerAction;
            domPrimary        = primaryButton;
            domSecondary      = secondaryButton;
            domJoystick       = joystickAction;
        }
        else if (dominantHand == DominantHand.Left)
        {
            dominantDriver    = leftTrackedDriver;
            nonDominantDriver = rightTrackedDriver;
            dominantSaber     = leftSaber;
            nonDominantSaber  = rightSaber;
            domGrip           = leftGripTriggerAction;
            domPrimary        = leftPrimaryButton;
            domSecondary      = leftSecondaryButton;
            domJoystick       = leftJoystickAction;
        }

        if (nonDominantSaber != null)
            saberGestureBaseLocalPos = nonDominantSaber.localPosition;

        // Automatically activate mirroring on start if an accessibility mode is chosen
        if (dominantHand != DominantHand.Both)
        {
            EnterPositionMirror();
        }
    }

    void OnEnable()
    {
        EnableActions();
        Application.onBeforeRender += OnBeforeRenderApply;
    }

    void OnDisable()
    {
        // Safety net: ensure the non-dominant hand is fully restored if the script is disabled
        if (nonDominantDriver != null)
        {
            nonDominantDriver.enabled = true;
            nonDominantDriver.gameObject.SetActive(true);
        }
        DisableActions();
        Application.onBeforeRender -= OnBeforeRenderApply;
    }

    void EnableActions()
    {
        try
        {
            TryEnable(domGrip); TryEnable(domPrimary); TryEnable(domSecondary); TryEnable(domJoystick);
            actionsEnabled = true;
        }
        catch (Exception e) { Debug.LogWarning("[XRAccessibility] Enable error: " + e.Message); }
    }

    void DisableActions()
    {
        try
        {
            TryDisable(domGrip); TryDisable(domPrimary); TryDisable(domSecondary); TryDisable(domJoystick);
            actionsEnabled = false;
        }
        catch (Exception e) { Debug.LogWarning("[XRAccessibility] Disable error: " + e.Message); }
    }

    void Update()
    {
        if (dominantHand == DominantHand.Both) return;
        if (!ReferencesOk() || !actionsEnabled) return;

        HandleGrip();
        HandlePrimary();
        HandleSecondary();
        HandleJoystick();
        CheckCrossover();
    }

    void LateUpdate()
    {
        if (dominantHand != DominantHand.Both) ApplyMode();
    }

    void OnBeforeRenderApply()
    {
        if (dominantHand != DominantHand.Both) ApplyMode();
    }

    // ──────────────────────────────────────────────────────────────
    //  Input Handling
    // ──────────────────────────────────────────────────────────────

    void HandleGrip()
    {
        if (!Triggered(domGrip)) return;

        if (currentMode == ControlMode.Swap)
        {
            ExitSwap();
            return;
        }

        // Direct Toggle: Freeze <--> Unfreeze
        if (currentMode == ControlMode.FlickFixed)
        {
            EnterPositionMirror(); // Unfreeze
        }
        else
        {
            EnterFlickFixed(); // Freeze
        }
    }

    void HandlePrimary()
    {
        if (!Triggered(domPrimary)) return;

        if (currentMode == ControlMode.Swap) ExitSwap();
        else
        {
            modeBeforeSwap = currentMode;
            flickModeBeforeSwap = flickMode;
            flickMode = FlickMode.NoInversion;
            EnterSwap();
        }
    }

    void HandleSecondary()
    {
        if (!Triggered(domSecondary)) return;

        if (currentMode == ControlMode.Swap) ExitSwap();

        // Simply toggle the flick mode independently of the control mode
        if (flickMode == FlickMode.NoInversion)
        {
            flickMode = FlickMode.BothAxisInversion;
            if (debugLogs) Debug.Log("[XRAccessibility] Secondary Pressed: BothAxisInversion ON");
        }
        else
        {
            flickMode = FlickMode.NoInversion;
            if (debugLogs) Debug.Log("[XRAccessibility] Secondary Pressed: NoInversion ON");
        }
    }

    void HandleJoystick()
    {
        if (currentMode != ControlMode.PositionMirror && currentMode != ControlMode.FlickFixed) return;
        if (domJoystick.action == null || !domJoystick.action.enabled) return;

        Vector2 joy = domJoystick.action.ReadValue<Vector2>();
        if (joy.sqrMagnitude < 0.01f) return;

        float dt = Time.deltaTime;
        joystickOffset.x += joy.x * joystickSpeed * dt;
        joystickOffset.y += joy.y * joystickSpeed * dt;
    }

    // ──────────────────────────────────────────────────────────────
    //  Crossover detection
    // ──────────────────────────────────────────────────────────────

    void CheckCrossover()
    {
        if (currentMode != ControlMode.PositionMirror && currentMode != ControlMode.FlickFixed) return;

        Transform domParent = dominantDriver.transform.parent;
        Vector3 domWorld = domParent.TransformPoint(dominantDriver.transform.localPosition);
        float domLocalX = xrOrigin.InverseTransformPoint(domWorld).x;

        bool isCrossed = (dominantHand == DominantHand.Right) ? (domLocalX < 0f) : (domLocalX > 0f);

        if (isCrossed == wasCrossed) return;
        wasCrossed = isCrossed;
    }

    // ──────────────────────────────────────────────────────────────
    //  Mode transitions
    // ──────────────────────────────────────────────────────────────

    void EnterPositionMirror()
    {
        if (nonDominantSaber != null) nonDominantSaber.localPosition = saberGestureBaseLocalPos;
        nonDominantDriver.enabled = false;
        currentMode = ControlMode.PositionMirror;
        joystickOffset = Vector3.zero;
        wasCrossed = false;
        if (debugLogs) Debug.Log("[XRAccessibility] MIRROR ON (Unfrozen).");
    }

    void EnterFlickFixed()
    {
        // Snapshot the exact distance/offset between the two controllers at the moment of freezing
        frozenOffsetFromDominant = nonDominantDriver.transform.localPosition - dominantDriver.transform.localPosition;
        
        nonDominantDriver.enabled = false;
        currentMode = ControlMode.FlickFixed;
        joystickOffset = Vector3.zero;
        wasCrossed = false;

        if (nonDominantSaber != null)
            saberGestureBaseLocalPos = nonDominantSaber.localPosition;

        if (debugLogs) Debug.Log($"[XRAccessibility] FREEZE ON — Offset locked at {frozenOffsetFromDominant}");
    }

    void EnterSwap()
    {
        if (nonDominantSaber != null) nonDominantSaber.localPosition = saberGestureBaseLocalPos;
        joystickOffset = Vector3.zero;
        nonDominantDriver.enabled = false;
        
        SwapSabers();
        currentMode = ControlMode.Swap;
        
        if (debugLogs) Debug.Log("[XRAccessibility] SWAP ON — Only dominant hand active.");
    }

    void ExitSwap()
    {
        RestoreSabers();
        flickMode = flickModeBeforeSwap;
        
        // Smoothly return to the active mode we were in before swapping
        if (modeBeforeSwap == ControlMode.PositionMirror)
            EnterPositionMirror();
        else if (modeBeforeSwap == ControlMode.FlickFixed)
            EnterFlickFixed();
    }

    // ──────────────────────────────────────────────────────────────
    //  Saber swap
    // ──────────────────────────────────────────────────────────────

    void StoreSaberOffsets()
    {
        if (saberOffsetsStored || nonDominantSaber == null || dominantSaber == null) return;
        nonDomSaberOrigLocalPos = nonDominantSaber.localPosition;
        nonDomSaberOrigLocalRot = nonDominantSaber.localRotation;
        domSaberOrigLocalPos    = dominantSaber.localPosition;
        domSaberOrigLocalRot    = dominantSaber.localRotation;
        saberOffsetsStored      = true;
    }

    void SwapSabers()
    {
        if (nonDominantSaber == null || dominantSaber == null) return;
        StoreSaberOffsets();

        // 1. Move non-dominant saber to dominant hand
        nonDominantSaber.SetParent(dominantDriver.transform, false);
        nonDominantSaber.localPosition = domSaberOrigLocalPos;
        nonDominantSaber.localRotation = domSaberOrigLocalRot;
        nonDominantSaber.gameObject.SetActive(true);

        // 2. Move dominant saber to non-dominant hand and hide it
        dominantSaber.SetParent(nonDominantDriver.transform, false);
        dominantSaber.localPosition = nonDomSaberOrigLocalPos;
        dominantSaber.localRotation = nonDomSaberOrigLocalRot;
        dominantSaber.gameObject.SetActive(false);

        // 3. Completely deactivate the non-dominant controller object
        nonDominantDriver.gameObject.SetActive(false);

        saberSwapped = true;
    }

    void RestoreSabers()
    {
        if (!saberSwapped || nonDominantSaber == null || dominantSaber == null) return;

        // 1. Reactivate the non-dominant controller object
        nonDominantDriver.gameObject.SetActive(true);

        // 2. Restore non-dominant saber
        nonDominantSaber.SetParent(nonDominantDriver.transform, false);
        nonDominantSaber.localPosition = nonDomSaberOrigLocalPos;
        nonDominantSaber.localRotation = nonDomSaberOrigLocalRot;

        // 3. Restore dominant saber and show it
        dominantSaber.SetParent(dominantDriver.transform, false);
        dominantSaber.localPosition = domSaberOrigLocalPos;
        dominantSaber.localRotation = domSaberOrigLocalRot;
        dominantSaber.gameObject.SetActive(true);

        saberSwapped = false;
    }

    // ──────────────────────────────────────────────────────────────
    //  Apply mode (LateUpdate + onBeforeRender)
    // ──────────────────────────────────────────────────────────────

    void ApplyMode()
    {
        if (!ReferencesOk()) return;

        switch (currentMode)
        {
            case ControlMode.PositionMirror: ApplyPositionMirror(); break;
            case ControlMode.FlickFixed: ApplyFlickFixed(); break;
        }
    }

    void ApplyPositionMirror()
    {
        Vector3 mirroredPos = MirrorPositionByAxes(dominantDriver.transform.localPosition);
        
        if (joystickOffset.sqrMagnitude > 0f)
        {
            Transform nonDomParent = nonDominantDriver.transform.parent;
            mirroredPos += nonDomParent.InverseTransformVector(xrOrigin.TransformVector(joystickOffset));
        }

        nonDominantDriver.transform.localPosition = mirroredPos;
        
        // Check the flickMode to determine the correct rotation, just like ApplyFlickFixed does
        nonDominantDriver.transform.localRotation = flickMode == FlickMode.BothAxisInversion
            ? InvertMirrorRotation(dominantDriver.transform.localRotation)
            : MirrorRotation(dominantDriver.transform.localRotation);
    }

    void ApplyFlickFixed()
    {
        // 1. Maintain the exact rigid distance from the dominant hand
        Vector3 targetPos = dominantDriver.transform.localPosition + frozenOffsetFromDominant;

        // Add joystick fine-tuning if needed
        if (joystickOffset.sqrMagnitude > 0f)
        {
            Transform nonDomParent = nonDominantDriver.transform.parent;
            targetPos += nonDomParent.InverseTransformVector(xrOrigin.TransformVector(joystickOffset));
        }

        nonDominantDriver.transform.localPosition = targetPos;

        // 2. Rotation matches dominant hand (NoInversion) to allow parallel cutting
        nonDominantDriver.transform.localRotation = flickMode == FlickMode.BothAxisInversion
            ? InvertMirrorRotation(dominantDriver.transform.localRotation)
            : MirrorRotation(dominantDriver.transform.localRotation);
    }

    // ──────────────────────────────────────────────────────────────
    //  Spatial math
    // ──────────────────────────────────────────────────────────────

    Vector3 MirrorPositionByAxes(Vector3 domLocalPos)
    {
        Transform domParent    = dominantDriver.transform.parent;
        Transform nonDomParent = nonDominantDriver.transform.parent;

        Vector3 worldPos    = domParent.TransformPoint(domLocalPos);
        Vector3 originLocal = xrOrigin.InverseTransformPoint(worldPos);

        float mx = -originLocal.x; 
        float my = 2f * mirrorCenterHeight - originLocal.y; 
        float mz = originLocal.z;

        Vector3 mirroredWorld = xrOrigin.TransformPoint(new Vector3(mx, my, mz));
        return nonDomParent.InverseTransformPoint(mirroredWorld);
    }

    Quaternion MirrorRotation(Quaternion domLocalRot)
    {
        Transform domParent = dominantDriver.transform.parent;
        Transform nonDomParent = nonDominantDriver.transform.parent;
        Quaternion domWorldRot = domParent.rotation * domLocalRot;
        return Quaternion.Inverse(nonDomParent.rotation) * domWorldRot;
    }

    Quaternion InvertMirrorRotation(Quaternion domLocalRot)
    {
        Transform domParent = dominantDriver.transform.parent;
        Transform nonDomParent = nonDominantDriver.transform.parent;
        Quaternion domWorldRot = domParent.rotation * domLocalRot;

        Quaternion originLocal = Quaternion.Inverse(xrOrigin.rotation) * domWorldRot;
        Quaternion flipped = Quaternion.AngleAxis(180f, Vector3.forward) * originLocal;
        Quaternion flippedWorld = xrOrigin.rotation * flipped;

        return Quaternion.Inverse(nonDomParent.rotation) * flippedWorld;
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

    bool ReferencesOk() => leftTrackedDriver != null && rightTrackedDriver != null && xrOrigin != null;
    static void TryEnable(InputActionProperty p) { if (p.action != null && !p.action.enabled) p.action.Enable(); }
    static void TryDisable(InputActionProperty p) { if (p.action != null && p.action.enabled) p.action.Disable(); }
    static bool Triggered(InputActionProperty p) => p.action != null && p.action.enabled && p.action.triggered;
    static bool IsPressed(InputActionProperty p) => p.action != null && p.action.enabled && p.action.IsPressed();

    public void CycleMode()
    {
        if (dominantHand == DominantHand.Both) return;

        switch (currentMode)
        {
            case ControlMode.PositionMirror: EnterFlickFixed(); break;
            case ControlMode.FlickFixed: EnterPositionMirror(); break;
            case ControlMode.Swap: ExitSwap(); break;
        }
    }
}