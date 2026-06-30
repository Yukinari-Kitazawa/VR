using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Fire : MonoBehaviour
{
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform firePoint;
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField, Range(0f, 1f)] private float triggerThreshold = 0.5f;
    [SerializeField] private float fireForce = 1000f;

    private readonly List<InputDevice> inputDevices = new List<InputDevice>();
    private InputDevice controller;
    private bool wasFirePressed;

    void OnEnable()
    {
        InputDevices.deviceConnected += OnDeviceChanged;
        InputDevices.deviceDisconnected += OnDeviceChanged;
        TryInitializeController();
    }

    void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceChanged;
        InputDevices.deviceDisconnected -= OnDeviceChanged;
    }

    void Update()
    {
        if (!controller.isValid)
            TryInitializeController();

        bool isFirePressed = IsFirePressed();
        if (isFirePressed && !wasFirePressed)
            OnFire();

        wasFirePressed = isFirePressed;
    }

    public void OnFire()
    {
        if (bullet == null)
        {
            Debug.LogWarning("Bullet prefab is not assigned.", this);
            return;
        }

        Transform origin = firePoint != null ? firePoint : transform;
        GameObject firedBullet = Instantiate(bullet, origin.position, origin.rotation);

        if (!firedBullet.TryGetComponent(out Rigidbody rigidBody))
            rigidBody = firedBullet.AddComponent<Rigidbody>();

        rigidBody.AddForce(origin.forward * fireForce);
    }

    private void TryInitializeController()
    {
        inputDevices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, inputDevices);
        controller = inputDevices.Count > 0 ? inputDevices[0] : default(InputDevice);
    }

    private bool IsFirePressed()
    {
        if (!controller.isValid)
            return false;

        if (controller.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButtonPressed) &&
            triggerButtonPressed)
            return true;

        return controller.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) &&
            triggerValue >= triggerThreshold;
    }

    private void OnDeviceChanged(InputDevice device)
    {
        controller = default(InputDevice);
        wasFirePressed = false;
    }
}
