using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using InputSystemKeyboard = UnityEngine.InputSystem.Keyboard;
using InputSystemMouse = UnityEngine.InputSystem.Mouse;
using XRInputDevice = UnityEngine.XR.InputDevice;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(CharacterController))]
public sealed class NightShiftVRRigController : MonoBehaviour
{
    [SerializeField] private NightShiftGameController gameController;
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private LineRenderer pointerLine;
    [SerializeField] private float turnSpeed = 70f;
    [SerializeField] private float deadzone = 0.18f;
    [SerializeField] private float cameraSwitchDeadzone = 0.65f;
    [SerializeField, Min(1f)] private float seatedEyeHeight = 1.62f;
    [SerializeField] private float pointerLength = 4f;
    [SerializeField] private bool desktopFallbackEnabled = true;
    [SerializeField] private float desktopMouseSensitivity = 0.08f;

    private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    private CharacterController characterController;
    private XRInputDevice headDevice;
    private XRInputDevice leftDevice;
    private XRInputDevice rightDevice;
    private bool previousRightTrigger;
    private bool previousLeftPrimary;
    private bool previousRightPrimary;
    private bool previousMenu;
    private bool previousCameraPrevious;
    private bool previousCameraNext;
    private float desktopPitch;
    private bool trackingOriginCalibrated;
    private Vector3 initialHeadTrackingPosition;
    private Quaternion trackingYawCorrection = Quaternion.identity;

    public Transform InteractionRayOrigin => rightDevice.isValid && rightHand != null ? rightHand : head;
    public bool IsUsingDesktopFallback => desktopFallbackEnabled && !headDevice.isValid;
    public bool IsUsingDesktopInputFallback => desktopFallbackEnabled && (!leftDevice.isValid || !rightDevice.isValid);
    public bool InteractPressedThisFrame { get; private set; }
    public bool MonitorPressedThisFrame { get; private set; }
    public bool PausePressedThisFrame { get; private set; }
    public bool ConfirmPressedThisFrame { get; private set; }
    public int CameraSwitchDirectionThisFrame { get; private set; }

    public void Configure(NightShiftGameController controller, Transform headTransform, Transform leftHandTransform, Transform rightHandTransform, LineRenderer rayLine = null)
    {
        gameController = controller;
        head = headTransform;
        leftHand = leftHandTransform;
        rightHand = rightHandTransform;
        pointerLine = rayLine;
    }

    public void SetFixedPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    public void RecenterTrackingOrigin()
    {
        trackingOriginCalibrated = false;
        trackingYawCorrection = Quaternion.identity;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        HideDebugControllerMarker(leftHand, "Left Controller Marker");
        HideDebugControllerMarker(rightHand, "Right Controller Marker");
    }

    private void OnEnable()
    {
        InputDevices.deviceConnected += OnDeviceChanged;
        InputDevices.deviceDisconnected += OnDeviceChanged;
        RefreshDevices();
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceChanged;
        InputDevices.deviceDisconnected -= OnDeviceChanged;
    }

    private void Update()
    {
        if (!headDevice.isValid || !leftDevice.isValid || !rightDevice.isValid)
            RefreshDevices();

        if (IsUsingDesktopFallback)
        {
            UpdateDesktopPose();
        }
        else
        {
            UpdateVrTrackedPoses();
        }

        if (UseDesktopInputFallback)
            UpdateDesktopButtonEdges();
        else
            UpdateVrButtonEdges();

        UpdatePointerLine();

        if (gameController != null && gameController.PlayerHasControl)
        {
            UpdateCharacterCollider();

            if (!UseDesktopInputFallback)
                UpdateVrTurning();
        }
    }

    private bool UseDesktopInputFallback => IsUsingDesktopInputFallback;

    private void UpdateVrButtonEdges()
    {
        bool rightTrigger = ReadButton(rightDevice, CommonUsages.triggerButton) || ReadAxis(rightDevice, CommonUsages.trigger) > 0.65f;
        bool leftPrimary = ReadButton(leftDevice, CommonUsages.primaryButton);
        bool rightPrimary = ReadButton(rightDevice, CommonUsages.primaryButton);
        bool menu = ReadButton(leftDevice, CommonUsages.menuButton) || ReadButton(rightDevice, CommonUsages.menuButton);
        Vector2 rightStick = ReadVector2(rightDevice, CommonUsages.primary2DAxis);
        bool cameraPrevious = rightStick.x < -cameraSwitchDeadzone;
        bool cameraNext = rightStick.x > cameraSwitchDeadzone;

        InteractPressedThisFrame = rightTrigger && !previousRightTrigger;
        MonitorPressedThisFrame = leftPrimary && !previousLeftPrimary;
        PausePressedThisFrame = menu && !previousMenu;
        ConfirmPressedThisFrame = InteractPressedThisFrame || MonitorPressedThisFrame || (rightPrimary && !previousRightPrimary);
        CameraSwitchDirectionThisFrame = ReadCameraSwitchDirection(cameraPrevious, cameraNext);

        previousRightTrigger = rightTrigger;
        previousLeftPrimary = leftPrimary;
        previousRightPrimary = rightPrimary;
        previousMenu = menu;
        previousCameraPrevious = cameraPrevious;
        previousCameraNext = cameraNext;
    }

    private void UpdateDesktopButtonEdges()
    {
        InputSystemKeyboard keyboard = InputSystemKeyboard.current;
        InputSystemMouse mouse = InputSystemMouse.current;

        bool interact = (mouse != null && mouse.leftButton.isPressed) || (keyboard != null && keyboard.enterKey.isPressed);
        bool monitor = keyboard != null && (keyboard.tabKey.isPressed || keyboard.mKey.isPressed);
        bool pause = keyboard != null && keyboard.escapeKey.isPressed;
        bool confirm = interact || (keyboard != null && keyboard.spaceKey.isPressed);
        bool cameraPrevious = keyboard != null && (keyboard.qKey.isPressed || keyboard.leftArrowKey.isPressed);
        bool cameraNext = keyboard != null && (keyboard.eKey.isPressed || keyboard.rightArrowKey.isPressed);

        InteractPressedThisFrame = interact && !previousRightTrigger;
        MonitorPressedThisFrame = monitor && !previousLeftPrimary;
        PausePressedThisFrame = pause && !previousMenu;
        ConfirmPressedThisFrame = confirm && !previousRightPrimary;
        CameraSwitchDirectionThisFrame = ReadCameraSwitchDirection(cameraPrevious, cameraNext);

        previousRightTrigger = interact;
        previousLeftPrimary = monitor;
        previousRightPrimary = confirm;
        previousMenu = pause;
        previousCameraPrevious = cameraPrevious;
        previousCameraNext = cameraNext;
    }

    private int ReadCameraSwitchDirection(bool previousPressed, bool nextPressed)
    {
        if (gameController == null || !gameController.IsMonitorOpen)
            return 0;

        if (previousPressed && !previousCameraPrevious)
            return -1;

        if (nextPressed && !previousCameraNext)
            return 1;

        return 0;
    }

    private void UpdateDesktopPose()
    {
        if (head == null)
            return;

        InputSystemMouse mouse = InputSystemMouse.current;
        bool hasControl = gameController != null && gameController.PlayerHasControl;

        Cursor.lockState = hasControl ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !hasControl;

        if (hasControl && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(Vector3.up, delta.x * desktopMouseSensitivity, Space.World);
            desktopPitch = Mathf.Clamp(desktopPitch - delta.y * desktopMouseSensitivity, -75f, 75f);
        }

        head.localPosition = new Vector3(0f, seatedEyeHeight, 0f);
        head.localRotation = Quaternion.Euler(desktopPitch, 0f, 0f);

        if (rightHand != null)
        {
            rightHand.position = head.position;
            rightHand.rotation = head.rotation;
        }

        if (leftHand != null)
        {
            leftHand.position = head.position + transform.TransformDirection(new Vector3(-0.25f, -0.25f, 0.2f));
            leftHand.rotation = head.rotation;
        }
    }

    private void UpdateVrTurning()
    {
        Vector2 axis = ReadVector2(rightDevice, CommonUsages.primary2DAxis);
        if (Mathf.Abs(axis.x) < deadzone)
            return;

        transform.Rotate(Vector3.up, axis.x * turnSpeed * Time.deltaTime, Space.World);
    }

    private void UpdateCharacterCollider()
    {
        if (head == null)
            return;

        float trackedHeight = Mathf.Clamp(head.localPosition.y, 1.1f, 2.1f);
        Vector3 center = characterController.center;
        center.x = head.localPosition.x;
        center.y = trackedHeight * 0.5f;
        center.z = head.localPosition.z;
        characterController.center = center;

        characterController.height = trackedHeight;
        characterController.radius = 0.28f;
    }

    private void UpdatePointerLine()
    {
        if (pointerLine == null || InteractionRayOrigin == null)
            return;

        bool canInteract = gameController != null && gameController.PlayerHasControl;
        Ray ray = new Ray(InteractionRayOrigin.position, InteractionRayOrigin.forward);
        RaycastHit hit = default;
        bool hasTarget = canInteract
            && Physics.Raycast(ray, out hit, pointerLength, ~0, QueryTriggerInteraction.Collide)
            && hit.collider.GetComponentInParent<NightShiftInteractable>() != null;

        pointerLine.enabled = hasTarget;
        if (!hasTarget)
            return;

        pointerLine.widthMultiplier = 0.003f;
        pointerLine.startColor = new Color(0.65f, 0.9f, 1f, 0.7f);
        pointerLine.endColor = new Color(0.35f, 0.7f, 1f, 0.18f);
        pointerLine.positionCount = 2;
        pointerLine.SetPosition(0, InteractionRayOrigin.position);
        pointerLine.SetPosition(1, hit.point);
    }

    private static void HideDebugControllerMarker(Transform controller, string markerName)
    {
        if (controller == null)
            return;

        Transform marker = controller.Find(markerName);
        if (marker != null)
            marker.gameObject.SetActive(false);
    }

    private void UpdateVrTrackedPoses()
    {
        if (head == null || !TryReadTrackedPose(headDevice, out Vector3 headPosition, out Quaternion headRotation))
            return;

        if (!trackingOriginCalibrated)
            CalibrateTrackingOrigin(headPosition, headRotation);

        ApplyTrackedPose(head, headPosition, headRotation);
        UpdateTrackedPose(leftDevice, leftHand);
        UpdateTrackedPose(rightDevice, rightHand);
    }

    private void CalibrateTrackingOrigin(Vector3 headPosition, Quaternion headRotation)
    {
        initialHeadTrackingPosition = headPosition;

        Vector3 horizontalForward = Vector3.ProjectOnPlane(headRotation * Vector3.forward, Vector3.up);
        float yawCorrection = horizontalForward.sqrMagnitude > 0.0001f
            ? Vector3.SignedAngle(horizontalForward, Vector3.forward, Vector3.up)
            : 0f;
        trackingYawCorrection = Quaternion.AngleAxis(yawCorrection, Vector3.up);

        trackingOriginCalibrated = true;
    }

    private void UpdateTrackedPose(XRInputDevice device, Transform target)
    {
        if (target == null || !TryReadTrackedPose(device, out Vector3 position, out Quaternion rotation))
            return;

        ApplyTrackedPose(target, position, rotation);
    }

    private void ApplyTrackedPose(Transform target, Vector3 position, Quaternion rotation)
    {
        Vector3 positionFromInitialHead = trackingYawCorrection * (position - initialHeadTrackingPosition);
        target.localPosition = positionFromInitialHead + Vector3.up * seatedEyeHeight;
        target.localRotation = trackingYawCorrection * rotation;
    }

    private static bool TryReadTrackedPose(XRInputDevice device, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) && !isTracked)
            return false;

        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);
        return hasPosition && hasRotation;
    }

    private void RefreshDevices()
    {
        XRInputDevice refreshedHeadDevice = GetDeviceAtNode(XRNode.Head);
        bool headDeviceChanged = headDevice.isValid != refreshedHeadDevice.isValid
            || (headDevice.isValid && refreshedHeadDevice.isValid && !headDevice.Equals(refreshedHeadDevice));

        headDevice = refreshedHeadDevice;
        leftDevice = GetDeviceAtNode(XRNode.LeftHand);
        rightDevice = GetDeviceAtNode(XRNode.RightHand);

        if (headDeviceChanged)
            RecenterTrackingOrigin();
    }

    private XRInputDevice GetDeviceAtNode(XRNode node)
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);
        return devices.Count > 0 ? devices[0] : default(XRInputDevice);
    }

    private static bool ReadButton(XRInputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    private static float ReadAxis(XRInputDevice device, InputFeatureUsage<float> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out float value) ? value : 0f;
    }

    private static Vector2 ReadVector2(XRInputDevice device, InputFeatureUsage<Vector2> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out Vector2 value) ? value : Vector2.zero;
    }

    private void OnDeviceChanged(XRInputDevice device)
    {
        RefreshDevices();
        previousRightTrigger = false;
        previousLeftPrimary = false;
        previousRightPrimary = false;
        previousMenu = false;
        previousCameraPrevious = false;
        previousCameraNext = false;
        CameraSwitchDirectionThisFrame = 0;
    }
}
