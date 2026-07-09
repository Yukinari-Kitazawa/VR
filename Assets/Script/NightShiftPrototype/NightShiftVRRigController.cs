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
    private float desktopPitch;

    public Transform InteractionRayOrigin => rightDevice.isValid && rightHand != null ? rightHand : head;
    public bool IsUsingDesktopFallback => desktopFallbackEnabled && !headDevice.isValid;
    public bool IsUsingDesktopInputFallback => desktopFallbackEnabled && (!leftDevice.isValid || !rightDevice.isValid);
    public bool InteractPressedThisFrame { get; private set; }
    public bool MonitorPressedThisFrame { get; private set; }
    public bool PausePressedThisFrame { get; private set; }
    public bool ConfirmPressedThisFrame { get; private set; }

    public void Configure(NightShiftGameController controller, Transform headTransform, Transform leftHandTransform, Transform rightHandTransform, LineRenderer rayLine = null)
    {
        gameController = controller;
        head = headTransform;
        leftHand = leftHandTransform;
        rightHand = rightHandTransform;
        pointerLine = rayLine;
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
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
            UpdateTrackedPose(headDevice, head);
            UpdateTrackedPose(leftDevice, leftHand);
            UpdateTrackedPose(rightDevice, rightHand);
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

        InteractPressedThisFrame = rightTrigger && !previousRightTrigger;
        MonitorPressedThisFrame = leftPrimary && !previousLeftPrimary;
        PausePressedThisFrame = menu && !previousMenu;
        ConfirmPressedThisFrame = InteractPressedThisFrame || MonitorPressedThisFrame || (rightPrimary && !previousRightPrimary);

        previousRightTrigger = rightTrigger;
        previousLeftPrimary = leftPrimary;
        previousRightPrimary = rightPrimary;
        previousMenu = menu;
    }

    private void UpdateDesktopButtonEdges()
    {
        InputSystemKeyboard keyboard = InputSystemKeyboard.current;
        InputSystemMouse mouse = InputSystemMouse.current;

        bool interact = (mouse != null && mouse.leftButton.isPressed) || (keyboard != null && keyboard.enterKey.isPressed);
        bool monitor = keyboard != null && (keyboard.tabKey.isPressed || keyboard.mKey.isPressed);
        bool pause = keyboard != null && keyboard.escapeKey.isPressed;
        bool confirm = interact || (keyboard != null && keyboard.spaceKey.isPressed);

        InteractPressedThisFrame = interact && !previousRightTrigger;
        MonitorPressedThisFrame = monitor && !previousLeftPrimary;
        PausePressedThisFrame = pause && !previousMenu;
        ConfirmPressedThisFrame = confirm && !previousRightPrimary;

        previousRightTrigger = interact;
        previousLeftPrimary = monitor;
        previousRightPrimary = confirm;
        previousMenu = pause;
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
            transform.RotateAround(head.position, Vector3.up, delta.x * desktopMouseSensitivity);
            desktopPitch = Mathf.Clamp(desktopPitch - delta.y * desktopMouseSensitivity, -75f, 75f);
        }

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

        Vector3 pivot = head != null ? head.position : transform.position;
        transform.RotateAround(pivot, Vector3.up, axis.x * turnSpeed * Time.deltaTime);
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

        pointerLine.enabled = true;
        pointerLine.positionCount = 2;
        pointerLine.SetPosition(0, InteractionRayOrigin.position);
        pointerLine.SetPosition(1, InteractionRayOrigin.position + InteractionRayOrigin.forward * pointerLength);
    }

    private static void UpdateTrackedPose(XRInputDevice device, Transform target)
    {
        if (!device.isValid || target == null)
            return;

        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            target.localPosition = position;

        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            target.localRotation = rotation;
    }

    private void RefreshDevices()
    {
        headDevice = GetDeviceAtNode(XRNode.Head);
        leftDevice = GetDeviceAtNode(XRNode.LeftHand);
        rightDevice = GetDeviceAtNode(XRNode.RightHand);
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
    }
}
