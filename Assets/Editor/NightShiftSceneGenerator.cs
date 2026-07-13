using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NightShiftSceneGenerator
{
    private const string ScenePath = "Assets/Scenes/NightShiftPrototype.unity";
    private const string MaterialFolder = "Assets/Script/NightShiftPrototype/GeneratedMaterials";

    [MenuItem("Tools/Night Shift Prototype/Generate Scene")]
    public static void GenerateNightShiftPrototype()
    {
        EnsureFolder("Assets/Script/NightShiftPrototype/GeneratedMaterials");

        Material wallMaterial = CreateMaterial("NSP_Wall", new Color(0.105f, 0.1f, 0.12f));
        Material floorMaterial = CreateMaterial("NSP_Floor", new Color(0.06f, 0.06f, 0.072f));
        Material metalMaterial = CreateMaterial("NSP_DarkMetal", new Color(0.18f, 0.19f, 0.22f));
        Material deskMaterial = CreateMaterial("NSP_Desk", new Color(0.13f, 0.105f, 0.075f));
        Material monitorMaterial = CreateMaterial("NSP_MonitorGlow", new Color(0.02f, 0.42f, 0.25f), 0f, 0.35f, new Color(0.01f, 0.35f, 0.18f));
        Material redMaterial = CreateMaterial("NSP_RedIndicator", new Color(0.85f, 0.04f, 0.025f), 0f, 0.25f, new Color(0.5f, 0.02f, 0.01f));
        Material eyeMaterial = CreateMaterial("NSP_Eyes", new Color(1f, 0.03f, 0.02f), 0f, 0.6f, new Color(1.4f, 0f, 0f));
        Material enemyMaterial = CreateMaterial("NSP_Enemy", new Color(0.025f, 0.025f, 0.028f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.048f, 0.055f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.02f;

        GameObject root = new GameObject("Night Shift Prototype");
        NightShiftGameController gameController = root.AddComponent<NightShiftGameController>();

        Transform environment = new GameObject("Environment").transform;
        environment.SetParent(root.transform);
        BuildEnvironment(environment, wallMaterial, floorMaterial, metalMaterial, deskMaterial, monitorMaterial, redMaterial, gameController, out NightShiftOfficeDoor door, out Light officeLight, out Light hallwayLight, out Transform monitorUiAnchor);

        BuildPlayer(root.transform, gameController, out Camera playerCamera, out NightShiftVRRigController vrRig, out Transform interactionRayOrigin);
        BuildEnemy(root.transform, gameController, enemyMaterial, eyeMaterial, out NightShiftEnemyStalker enemyStalker);
        BuildUi(monitorUiAnchor, playerCamera, out TextMeshProUGUI timerText, out TextMeshProUGUI powerText, out TextMeshProUGUI statusText, out TextMeshProUGUI promptText, out TextMeshProUGUI dangerText, out TextMeshProUGUI monitorFeedText, out TextMeshProUGUI resultTitleText, out TextMeshProUGUI resultBodyText, out GameObject titlePanel, out GameObject pausePanel, out GameObject resultPanel, out GameObject monitorPanel, out GameObject jumpscarePanel, out Image staticOverlay);

        SerializedObject serializedController = new SerializedObject(gameController);
        SetObject(serializedController, "playerCamera", playerCamera);
        SetObject(serializedController, "vrRig", vrRig);
        SetObject(serializedController, "interactionRayOrigin", interactionRayOrigin);
        SetObject(serializedController, "officeDoor", door);
        SetObject(serializedController, "enemyStalker", enemyStalker);
        SetObject(serializedController, "officeLight", officeLight);
        SetObject(serializedController, "hallwayLight", hallwayLight);
        SetObject(serializedController, "timerText", timerText);
        SetObject(serializedController, "powerText", powerText);
        SetObject(serializedController, "statusText", statusText);
        SetObject(serializedController, "promptText", promptText);
        SetObject(serializedController, "dangerText", dangerText);
        SetObject(serializedController, "monitorFeedText", monitorFeedText);
        SetObject(serializedController, "resultTitleText", resultTitleText);
        SetObject(serializedController, "resultBodyText", resultBodyText);
        SetObject(serializedController, "titlePanel", titlePanel);
        SetObject(serializedController, "pausePanel", pausePanel);
        SetObject(serializedController, "resultPanel", resultPanel);
        SetObject(serializedController, "monitorPanel", monitorPanel);
        SetObject(serializedController, "jumpscarePanel", jumpscarePanel);
        SetObject(serializedController, "staticOverlay", staticOverlay);
        SetObject(serializedController, "monitorUiAnchor", monitorUiAnchor);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettings();

        Debug.Log("Generated " + ScenePath);
    }

    private static void BuildEnvironment(Transform parent, Material wallMaterial, Material floorMaterial, Material metalMaterial, Material deskMaterial, Material monitorMaterial, Material redMaterial, NightShiftGameController controller, out NightShiftOfficeDoor door, out Light officeLight, out Light hallwayLight, out Transform monitorUiAnchor)
    {
        CreateCube("Floor", parent, new Vector3(0f, -0.1f, -8.2f), new Vector3(8.5f, 0.2f, 27.5f), floorMaterial);
        CreateCube("Office Ceiling", parent, new Vector3(0f, 3.15f, -0.15f), new Vector3(8.4f, 0.18f, 6.8f), wallMaterial);
        CreateCube("Hall Ceiling", parent, new Vector3(0f, 3.1f, -11.8f), new Vector3(5.3f, 0.18f, 18f), wallMaterial);

        CreateCube("Office Left Wall", parent, new Vector3(-4.15f, 1.5f, -0.15f), new Vector3(0.25f, 3f, 6.8f), wallMaterial);
        CreateCube("Office Right Wall", parent, new Vector3(4.15f, 1.5f, -0.15f), new Vector3(0.25f, 3f, 6.8f), wallMaterial);
        CreateCube("Office Back Wall", parent, new Vector3(0f, 1.5f, 3.15f), new Vector3(8.4f, 3f, 0.25f), wallMaterial);
        CreateCube("Front Wall Left", parent, new Vector3(-3.05f, 1.5f, -3.35f), new Vector3(2.2f, 3f, 0.25f), wallMaterial);
        CreateCube("Front Wall Right", parent, new Vector3(3.05f, 1.5f, -3.35f), new Vector3(2.2f, 3f, 0.25f), wallMaterial);
        CreateCube("Front Wall Header", parent, new Vector3(0f, 2.85f, -3.35f), new Vector3(3.9f, 0.5f, 0.25f), wallMaterial);

        CreateCube("Hall Left Wall", parent, new Vector3(-2.65f, 1.5f, -11.8f), new Vector3(0.25f, 3f, 18f), wallMaterial);
        CreateCube("Hall Right Wall", parent, new Vector3(2.65f, 1.5f, -11.8f), new Vector3(0.25f, 3f, 18f), wallMaterial);
        CreateCube("Hall End Wall", parent, new Vector3(0f, 1.5f, -20.8f), new Vector3(5.3f, 3f, 0.25f), wallMaterial);

        CreateCube("Desk", parent, new Vector3(0f, 0.55f, 1.05f), new Vector3(3.6f, 1.1f, 1f), deskMaterial);
        CreateCube("Desk Top", parent, new Vector3(0f, 1.15f, 1.05f), new Vector3(3.9f, 0.18f, 1.25f), deskMaterial);
        GameObject monitor = CreateCube("Security Monitor", parent, new Vector3(0f, 1.65f, 0.72f), new Vector3(1.25f, 0.7f, 0.18f), monitorMaterial);
        monitor.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
        monitor.AddComponent<NightShiftInteractable>().Configure(controller, NightShiftInteractionAction.ToggleMonitor, "Use monitor");

        GameObject monitorAnchor = new GameObject("Monitor UI Anchor");
        monitorUiAnchor = monitorAnchor.transform;
        monitorUiAnchor.SetParent(parent, false);
        monitorUiAnchor.position = monitor.transform.position + monitor.transform.forward * 0.096f;
        monitorUiAnchor.rotation = monitor.transform.rotation;
        monitorUiAnchor.localScale = Vector3.one;

        GameObject doorRoot = new GameObject("Office Door");
        doorRoot.transform.SetParent(parent);
        doorRoot.transform.position = new Vector3(0f, 0f, -3.18f);
        door = doorRoot.AddComponent<NightShiftOfficeDoor>();
        Transform doorSlab = CreateCube("Door Slab", doorRoot.transform, new Vector3(0f, 3.35f, 0f), new Vector3(2.15f, 2.6f, 0.24f), metalMaterial).transform;

        GameObject doorButton = CreateCube("Door Control", parent, new Vector3(1.15f, 1.45f, 1.35f), new Vector3(0.42f, 0.62f, 0.2f), redMaterial);
        doorButton.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        doorButton.AddComponent<NightShiftInteractable>().Configure(controller, NightShiftInteractionAction.ToggleDoor, "Toggle door");

        GameObject lightButton = CreateCube("Light Control", parent, new Vector3(1.15f, 1.45f, 0.78f), new Vector3(0.42f, 0.62f, 0.2f), redMaterial);
        lightButton.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        lightButton.AddComponent<NightShiftInteractable>().Configure(controller, NightShiftInteractionAction.ToggleOfficeLight, "Toggle light");

        GameObject doorLamp = new GameObject("Door Indicator Light");
        doorLamp.transform.SetParent(parent);
        doorLamp.transform.position = new Vector3(1.05f, 1.85f, 1.35f);
        Light doorWarning = doorLamp.AddComponent<Light>();
        doorWarning.type = LightType.Point;
        doorWarning.range = 1.5f;
        doorWarning.intensity = 0.45f;
        doorWarning.color = Color.green;
        door.Configure(doorSlab, doorButton.GetComponent<Renderer>(), doorWarning);

        GameObject fillLightObject = new GameObject("Office Fill Light");
        fillLightObject.transform.SetParent(parent);
        fillLightObject.transform.position = new Vector3(0f, 2.4f, 0.95f);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 8f;
        fillLight.intensity = 0.85f;
        fillLight.color = new Color(0.84f, 0.86f, 0.9f);
        fillLight.shadows = LightShadows.None;

        GameObject officeLightObject = new GameObject("Office Light");
        officeLightObject.transform.SetParent(parent);
        officeLightObject.transform.position = new Vector3(0f, 2.65f, 0.7f);
        officeLight = officeLightObject.AddComponent<Light>();
        officeLight.type = LightType.Point;
        officeLight.range = 7f;
        officeLight.intensity = 1.7f;
        officeLight.color = new Color(1f, 0.82f, 0.58f);
        officeLight.shadows = LightShadows.None;

        GameObject hallLightObject = new GameObject("Hall Warning Light");
        hallLightObject.transform.SetParent(parent);
        hallLightObject.transform.position = new Vector3(0f, 2.35f, -8.8f);
        hallwayLight = hallLightObject.AddComponent<Light>();
        hallwayLight.type = LightType.Point;
        hallwayLight.range = 7f;
        hallwayLight.intensity = 0.65f;
        hallwayLight.color = new Color(0.7f, 0.04f, 0.03f);
        hallwayLight.shadows = LightShadows.None;
    }

    private static void BuildPlayer(Transform parent, NightShiftGameController controller, out Camera playerCamera, out NightShiftVRRigController vrRig, out Transform interactionRayOrigin)
    {
        GameObject player = new GameObject("XR Player Rig");
        player.transform.SetParent(parent);
        player.transform.position = new Vector3(0f, 0f, 1.65f);
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.78f;
        characterController.radius = 0.28f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.slopeLimit = 45f;

        vrRig = player.AddComponent<NightShiftVRRigController>();

        GameObject trackingSpace = new GameObject("Tracking Space");
        trackingSpace.transform.SetParent(player.transform);
        trackingSpace.transform.localPosition = Vector3.zero;
        trackingSpace.transform.localRotation = Quaternion.identity;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(trackingSpace.transform);
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        playerCamera = cameraObject.AddComponent<Camera>();
        playerCamera.fieldOfView = 70f;
        playerCamera.nearClipPlane = 0.03f;
        playerCamera.farClipPlane = 60f;
        playerCamera.backgroundColor = Color.black;
        cameraObject.AddComponent<AudioListener>();

        GameObject leftHand = new GameObject("Left Controller Anchor");
        leftHand.transform.SetParent(trackingSpace.transform);
        leftHand.transform.localPosition = new Vector3(-0.25f, 1.25f, 0.35f);
        leftHand.transform.localRotation = Quaternion.identity;

        GameObject rightHand = new GameObject("Right Controller Anchor");
        rightHand.transform.SetParent(trackingSpace.transform);
        rightHand.transform.localPosition = new Vector3(0.25f, 1.25f, 0.35f);
        rightHand.transform.localRotation = Quaternion.identity;
        LineRenderer pointerLine = rightHand.AddComponent<LineRenderer>();
        pointerLine.useWorldSpace = true;
        pointerLine.widthMultiplier = 0.003f;
        pointerLine.positionCount = 2;
        pointerLine.startColor = new Color(0.65f, 0.9f, 1f, 0.7f);
        pointerLine.endColor = new Color(0.35f, 0.7f, 1f, 0.18f);
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null)
            lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
            lineShader = Shader.Find("Standard");
        pointerLine.sharedMaterial = new Material(lineShader);

        interactionRayOrigin = rightHand.transform;
        vrRig.Configure(controller, cameraObject.transform, leftHand.transform, rightHand.transform, pointerLine);
    }

    private static void BuildEnemy(Transform parent, NightShiftGameController controller, Material enemyMaterial, Material eyeMaterial, out NightShiftEnemyStalker enemyStalker)
    {
        Transform routeRoot = new GameObject("Enemy Route").transform;
        routeRoot.SetParent(parent);

        Transform[] route =
        {
            CreateWaypoint(routeRoot, "Storage", new Vector3(0f, 0f, -19f)),
            CreateWaypoint(routeRoot, "Back Hall", new Vector3(-1.2f, 0f, -14.2f)),
            CreateWaypoint(routeRoot, "Main Hall", new Vector3(1.1f, 0f, -9.2f)),
            CreateWaypoint(routeRoot, "Door", new Vector3(0f, 0f, -4.25f))
        };

        GameObject enemy = new GameObject("Stalker");
        enemy.transform.SetParent(parent);
        enemyStalker = enemy.AddComponent<NightShiftEnemyStalker>();
        enemyStalker.Configure(controller, route);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(enemy.transform);
        body.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.78f, 0.4f);
        body.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(enemy.transform);
        head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        head.transform.localScale = new Vector3(0.7f, 0.58f, 0.58f);
        head.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
        Object.DestroyImmediate(head.GetComponent<Collider>());

        GameObject leftEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftEye.name = "Left Eye";
        leftEye.transform.SetParent(enemy.transform);
        leftEye.transform.localPosition = new Vector3(-0.18f, 2f, -0.3f);
        leftEye.transform.localScale = Vector3.one * 0.09f;
        leftEye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
        Object.DestroyImmediate(leftEye.GetComponent<Collider>());

        GameObject rightEye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightEye.name = "Right Eye";
        rightEye.transform.SetParent(enemy.transform);
        rightEye.transform.localPosition = new Vector3(0.18f, 2f, -0.3f);
        rightEye.transform.localScale = Vector3.one * 0.09f;
        rightEye.GetComponent<Renderer>().sharedMaterial = eyeMaterial;
        Object.DestroyImmediate(rightEye.GetComponent<Collider>());

        GameObject glow = new GameObject("Eye Glow");
        glow.transform.SetParent(enemy.transform);
        glow.transform.localPosition = new Vector3(0f, 2f, -0.35f);
        Light glowLight = glow.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = Color.red;
        glowLight.range = 3.5f;
        glowLight.intensity = 1.8f;
    }

    private static void BuildUi(Transform monitorUiAnchor, Camera playerCamera, out TextMeshProUGUI timerText, out TextMeshProUGUI powerText, out TextMeshProUGUI statusText, out TextMeshProUGUI promptText, out TextMeshProUGUI dangerText, out TextMeshProUGUI monitorFeedText, out TextMeshProUGUI resultTitleText, out TextMeshProUGUI resultBodyText, out GameObject titlePanel, out GameObject pausePanel, out GameObject resultPanel, out GameObject monitorPanel, out GameObject jumpscarePanel, out Image staticOverlay)
    {
        GameObject menuCanvasObject = CreateScreenSpaceOverlayCanvas("Menu UI Canvas", playerCamera);
        GameObject monitorCanvasObject = CreateWorldCanvas("Monitor Camera Canvas", monitorUiAnchor, playerCamera, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.0009f, new Vector2(1200f, 680f));
        Transform menuRoot = menuCanvasObject.transform;
        Transform monitorRoot = monitorCanvasObject.transform;

        timerText = CreateText("Timer", menuRoot, "12 AM", 42f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -70f), new Vector2(300f, 90f), Color.white);
        powerText = CreateText("Power", menuRoot, "POWER: 100%\nUSAGE [|...]", 34f, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -70f), new Vector2(440f, 120f), Color.white);
        statusText = CreateText("Status", menuRoot, "", 25f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 90f), new Vector2(700f, 120f), new Color(0.82f, 0.95f, 0.86f));
        promptText = CreateText("Interaction Prompt", menuRoot, "", 26f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(800f, 60f), Color.white);
        dangerText = CreateText("Danger", menuRoot, "", 32f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(900f, 80f), new Color(1f, 0.15f, 0.12f));
        dangerText.gameObject.SetActive(false);
        CreateText("Reticle", menuRoot, "+", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), new Color(1f, 1f, 1f, 0.55f));

        titlePanel = CreatePanel("Title Panel", menuRoot, new Color(0f, 0f, 0f, 0.96f));
        CreateText("Title", titlePanel.transform, "NIGHT SHIFT\nPROTOTYPE", 76f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(900f, 220f), Color.white);
        CreateText("Title Body", titlePanel.transform, "Survive from 12 AM to 6 AM.\nTrigger / Click: Start\nMonitor: Select cameras and track the subject", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1250f, 220f), new Color(0.75f, 0.9f, 0.78f));

        pausePanel = CreatePanel("Pause Panel", menuRoot, new Color(0f, 0f, 0f, 0.78f));
        CreateText("Pause Title", pausePanel.transform, "PAUSED", 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(680f, 110f), Color.white);
        CreateText("Pause Body", pausePanel.transform, "Right trigger / Enter / Menu / Esc: Resume", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(900f, 80f), Color.white);
        pausePanel.SetActive(false);

        resultPanel = CreatePanel("Result Panel", menuRoot, new Color(0f, 0f, 0f, 0.82f));
        resultTitleText = CreateText("Result Title", resultPanel.transform, "", 76f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(900f, 120f), Color.white);
        resultBodyText = CreateText("Result Body", resultPanel.transform, "", 31f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(900f, 160f), new Color(0.82f, 0.9f, 0.82f));
        resultPanel.SetActive(false);

        monitorPanel = CreatePanel("Monitor Panel", monitorRoot, new Color(0.005f, 0.02f, 0.012f, 0.95f));
        monitorFeedText = CreateText("Monitor Feed", monitorPanel.transform, "", 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 560f), new Color(0.58f, 1f, 0.66f));
        GameObject staticObject = new GameObject("Static");
        staticObject.transform.SetParent(monitorPanel.transform, false);
        staticOverlay = staticObject.AddComponent<Image>();
        staticOverlay.raycastTarget = false;
        RectTransform staticRect = staticObject.GetComponent<RectTransform>();
        StretchFullScreen(staticRect);
        monitorPanel.SetActive(false);

        jumpscarePanel = CreatePanel("Jumpscare Panel", menuRoot, new Color(0.42f, 0f, 0f, 0.92f));
        CreateText("Jumpscare Text", jumpscarePanel.transform, "TOO CLOSE", 96f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f), Color.white);
        jumpscarePanel.SetActive(false);
    }

    private static GameObject CreateWorldCanvas(string name, Transform parent, Camera worldCamera, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Vector2 size)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        canvasObject.transform.localPosition = localPosition;
        canvasObject.transform.localRotation = localRotation;
        canvasObject.transform.localScale = localScale;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldCamera;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        return canvasObject;
    }

    private static GameObject CreateScreenSpaceOverlayCanvas(string name, Camera worldCamera)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(worldCamera.transform.root, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvasObject;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;

        return cube;
    }

    private static Transform CreateWaypoint(Transform parent, string name, Vector3 position)
    {
        GameObject waypoint = new GameObject(name);
        waypoint.transform.SetParent(parent);
        waypoint.transform.position = position;
        return waypoint.transform;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return label;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        StretchFullScreen(panel.GetComponent<RectTransform>());
        return panel;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0f, Color? emission = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", metallic);
        SetMaterialFloat(material, "_Smoothness", smoothness);
        SetMaterialFloat(material, "_Glossiness", smoothness);

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            SetMaterialColor(material, emission.Value, "_EmissionColor");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color, string property = "_BaseColor")
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
            return;
        }

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void UpdateBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene => scene.path != ScenePath)
            .ToList();

        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
