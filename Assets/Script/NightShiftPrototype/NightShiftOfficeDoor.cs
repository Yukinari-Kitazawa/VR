using UnityEngine;

public sealed class NightShiftOfficeDoor : MonoBehaviour
{
    [SerializeField] private Transform doorSlab;
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Light warningLight;
    [SerializeField] private Vector3 openLocalPosition = new Vector3(0f, 3.35f, 0f);
    [SerializeField] private Vector3 closedLocalPosition = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private Color openColor = new Color(0.1f, 0.55f, 0.2f);
    [SerializeField] private Color closedColor = new Color(0.85f, 0.08f, 0.04f);
    [SerializeField] private Color offlineColor = new Color(0.08f, 0.08f, 0.08f);

    private bool isClosed;
    private bool hasPower = true;

    public bool IsClosed => isClosed && hasPower;

    public void Configure(Transform slab, Renderer indicator, Light light)
    {
        doorSlab = slab;
        indicatorRenderer = indicator;
        warningLight = light;
        ApplyVisuals();
    }

    private void Update()
    {
        if (doorSlab == null)
            return;

        Vector3 target = IsClosed ? closedLocalPosition : openLocalPosition;
        doorSlab.localPosition = Vector3.Lerp(doorSlab.localPosition, target, Time.deltaTime * moveSpeed);
    }

    public void SetClosed(bool closed)
    {
        if (!hasPower)
            closed = false;

        isClosed = closed;
        ApplyVisuals();
    }

    public void ForceOpen()
    {
        isClosed = false;
        ApplyVisuals();
    }

    public void SetPowered(bool powered)
    {
        hasPower = powered;
        if (!hasPower)
            isClosed = false;

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        Color color = hasPower ? (IsClosed ? closedColor : openColor) : offlineColor;

        if (indicatorRenderer != null)
            indicatorRenderer.material.color = color;

        if (warningLight != null)
        {
            warningLight.enabled = hasPower;
            warningLight.color = color;
        }
    }
}
