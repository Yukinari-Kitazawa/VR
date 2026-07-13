using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using InputSystemKeyboard = UnityEngine.InputSystem.Keyboard;
using InputSystemMouse = UnityEngine.InputSystem.Mouse;
using XRInputDevice = UnityEngine.XR.InputDevice;

public sealed class NightShiftSceneFlow : MonoBehaviour
{
    public const string TitleSceneName = "NightShiftTitle";
    public const string GameSceneName = "NightShiftPrototype";
    public const string ResultSceneName = "NightShiftResult";

    private const float MenuCanvasPlaneDistance = 1.5f;
    private static readonly Vector2 MenuCanvasSize = new Vector2(1600f, 900f);

    private static bool hasResult;
    private static bool lastRunWon;
    private static string lastResultTitle = "夜勤終了";
    private static string lastResultBody = "結果が記録されていません。";

    private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    private XRInputDevice headDevice;
    private XRInputDevice leftDevice;
    private XRInputDevice rightDevice;
    private Transform menuCameraTransform;
    private bool inputArmed;
    private bool previousVrConfirm;
    private bool transitionRequested;
    private bool isTitleScene;

    public static bool HasResult => hasResult;
    public static bool LastRunWon => lastRunWon;
    public static string LastResultTitle => lastResultTitle;
    public static string LastResultBody => lastResultBody;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapInitialMenuScene()
    {
        EnsureMenuSceneFlow(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureMenuSceneFlow(scene);
    }

    private static void EnsureMenuSceneFlow(Scene scene)
    {
        string sceneName = scene.name;
        if (sceneName != TitleSceneName && sceneName != ResultSceneName)
            return;

        if (GameObject.Find("Night Shift Scene Flow") != null)
            return;

        GameObject flowObject = new GameObject("Night Shift Scene Flow");
        if (flowObject.scene != scene)
            SceneManager.MoveGameObjectToScene(flowObject, scene);
        flowObject.AddComponent<NightShiftSceneFlow>();
    }

    public static void SetResult(bool won, string title, string body)
    {
        hasResult = true;
        lastRunWon = won;
        lastResultTitle = string.IsNullOrWhiteSpace(title) ? "夜勤終了" : title;
        lastResultBody = string.IsNullOrWhiteSpace(body) ? "夜勤が終了しました。" : body;
    }

    public static void ClearResult()
    {
        hasResult = false;
        lastRunWon = false;
        lastResultTitle = "夜勤終了";
        lastResultBody = "結果が記録されていません。";
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        isTitleScene = gameObject.scene.name == TitleSceneName;
        RefreshDevices();
        Camera menuCamera = CreateMenuCamera();
        menuCameraTransform = menuCamera.transform;
        BuildMenuUi(menuCamera);
    }

    private void Update()
    {
        if (transitionRequested)
            return;

        if (!headDevice.isValid || !leftDevice.isValid || !rightDevice.isValid)
            RefreshDevices();

        UpdateMenuCameraPose();

        InputSystemKeyboard keyboard = InputSystemKeyboard.current;
        InputSystemMouse mouse = InputSystemMouse.current;

        bool desktopHeld = (mouse != null && mouse.leftButton.isPressed)
            || (keyboard != null && (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed));
        bool desktopPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));
        bool vrConfirm = ReadVrConfirm();

        if (!inputArmed)
        {
            previousVrConfirm = vrConfirm;
            inputArmed = !desktopHeld && !vrConfirm;
            return;
        }

        bool vrPressed = vrConfirm && !previousVrConfirm;
        previousVrConfirm = vrConfirm;

        if (desktopPressed || vrPressed)
            ActivatePrimaryAction();
    }

    private void ActivatePrimaryAction()
    {
        transitionRequested = true;
        Time.timeScale = 1f;

        if (isTitleScene)
        {
            ClearResult();
            SceneManager.LoadScene(GameSceneName);
            return;
        }

        ClearResult();
        SceneManager.LoadScene(TitleSceneName);
    }

    private bool ReadVrConfirm()
    {
        return ReadButton(rightDevice, CommonUsages.triggerButton)
            || ReadAxis(rightDevice, CommonUsages.trigger) > 0.65f
            || ReadButton(rightDevice, CommonUsages.primaryButton)
            || ReadButton(leftDevice, CommonUsages.primaryButton);
    }

    private void RefreshDevices()
    {
        headDevice = GetDeviceAtNode(XRNode.CenterEye);
        leftDevice = GetDeviceAtNode(XRNode.LeftHand);
        rightDevice = GetDeviceAtNode(XRNode.RightHand);
    }

    private void UpdateMenuCameraPose()
    {
        if (menuCameraTransform == null || !headDevice.isValid)
            return;

        if (headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            menuCameraTransform.localPosition = position;

        if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            menuCameraTransform.localRotation = rotation;
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

    private static Camera CreateMenuCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            mainCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            return mainCamera;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.006f, 0.008f, 0.01f);
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 20f;
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private void BuildMenuUi(Camera menuCamera)
    {
        Color accent = isTitleScene
            ? new Color(0.18f, 0.82f, 0.62f)
            : LastRunWon
                ? new Color(0.28f, 0.92f, 0.58f)
                : new Color(0.94f, 0.18f, 0.14f);

        GameObject canvasObject = new GameObject("Scene UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = menuCamera;
        canvas.planeDistance = MenuCanvasPlaneDistance;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.SetParent(transform, false);
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = MenuCanvasSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvasObject.transform;
        Image background = CreateImage("Background", root, new Color(0.006f, 0.008f, 0.01f, 1f));
        StretchFullScreen(background.rectTransform);

        Image topRule = CreateImage("Top Rule", root, accent);
        SetRect(topRule.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 5f));

        string header = isTitleScene ? "警備システム" : "勤務報告";
        CreateText("Header", root, header, 22f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -52f), new Vector2(650f, 60f), new Color(0.7f, 0.76f, 0.74f));

        string title = isTitleScene ? "夜勤警備" : HasResult ? LastResultTitle : "夜勤終了";
        string subtitle = isTitleScene ? "NightSecurity" : HasResult ? LastResultBody : "結果が記録されていません。";
        string action = isTitleScene ? "夜勤を開始" : "タイトルへ戻る";

        TextMeshProUGUI titleLabel = CreateText("Title", root, title, 86f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1300f, 150f), Color.white);
        titleLabel.fontStyle = FontStyles.Bold;

        TextMeshProUGUI subtitleLabel = CreateText("Subtitle", root, subtitle, 28f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1120f, 150f), new Color(0.72f, 0.78f, 0.76f));
        subtitleLabel.textWrappingMode = TextWrappingModes.Normal;

        Image actionBackground = CreateImage("Primary Action", root, new Color(accent.r, accent.g, accent.b, 0.16f));
        SetRect(actionBackground.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -225f), new Vector2(440f, 78f));
        Outline actionOutline = actionBackground.gameObject.AddComponent<Outline>();
        actionOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
        actionOutline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI actionLabel = CreateText("Primary Action Label", actionBackground.transform, action, 27f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white);
        actionLabel.fontStyle = FontStyles.Bold;
        StretchFullScreen(actionLabel.rectTransform);

        CreateText("Footer", root, "12 AM  /  警備室", 20f, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 48f), new Vector2(500f, 50f), new Color(accent.r, accent.g, accent.b, 0.78f));
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.characterSpacing = 0f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        NightShiftJapaneseFont.Apply(label);

        SetRect(label.rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, size);
        return label;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
