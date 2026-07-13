using UnityEngine;
using UnityEngine.UI;

public sealed class NightShiftSecurityCameraSystem : MonoBehaviour
{
    [SerializeField] private Camera[] feedCameras;
    [SerializeField] private RawImage monitorImage;
    [SerializeField, Min(256)] private int textureWidth = 640;
    [SerializeField, Min(144)] private int textureHeight = 360;

    private RenderTexture renderTexture;
    private int selectedCameraIndex;
    private bool monitorOpen;

    public int CameraCount => feedCameras != null ? feedCameras.Length : 0;

    public void Configure(Camera[] cameras, RawImage outputImage)
    {
        feedCameras = cameras;
        monitorImage = outputImage;
        DisableAllCameras();
    }

    private void Awake()
    {
        DisableAllCameras();
        EnsureRenderTexture();
        ApplyState();
    }

    private void OnDisable()
    {
        DisableAllCameras();
    }

    private void OnDestroy()
    {
        if (renderTexture == null)
            return;

        renderTexture.Release();
        Destroy(renderTexture);
    }

    public void SetFeed(bool open, int cameraIndex)
    {
        monitorOpen = open;
        selectedCameraIndex = CameraCount > 0 ? Mathf.Clamp(cameraIndex, 0, CameraCount - 1) : 0;
        EnsureRenderTexture();
        ApplyState();
    }

    private void EnsureRenderTexture()
    {
        if (renderTexture != null)
            return;

        renderTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "Night Shift Camera Feed",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        renderTexture.Create();

        if (monitorImage != null)
            monitorImage.texture = renderTexture;
    }

    private void ApplyState()
    {
        DisableAllCameras();

        if (monitorImage != null)
        {
            monitorImage.texture = renderTexture;
            monitorImage.enabled = monitorOpen && renderTexture != null;
        }

        if (!monitorOpen || CameraCount == 0 || renderTexture == null)
            return;

        Camera selectedCamera = feedCameras[selectedCameraIndex];
        if (selectedCamera == null)
            return;

        selectedCamera.targetTexture = renderTexture;
        selectedCamera.enabled = true;
    }

    private void DisableAllCameras()
    {
        if (feedCameras == null)
            return;

        foreach (Camera feedCamera in feedCameras)
        {
            if (feedCamera == null)
                continue;

            feedCamera.enabled = false;
            feedCamera.targetTexture = null;
        }
    }
}
