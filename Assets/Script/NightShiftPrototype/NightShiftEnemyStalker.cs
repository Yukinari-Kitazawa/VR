using UnityEngine;

public sealed class NightShiftEnemyStalker : MonoBehaviour
{
    [SerializeField] private NightShiftGameController gameController;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private Vector2 moveCheckIntervalSeconds = new Vector2(5f, 8f);
    [SerializeField, Range(0f, 1f)] private float baseAdvanceChance = 0.18f;
    [SerializeField, Range(0f, 0.25f)] private float hourlyAdvanceChanceBonus = 0.07f;
    [SerializeField, Range(0f, 1f)] private float observedChanceMultiplier = 0.12f;
    [SerializeField, Min(1)] private int maximumFailedMoveChecks = 5;
    [SerializeField] private float doorAttackDelay = 4.5f;
    [SerializeField] private Vector2 powerOutRushDelaySeconds = new Vector2(6f, 11f);
    [SerializeField] private float visualMoveSpeed = 2.2f;

    private int stageIndex;
    private int failedMoveChecks;
    private float moveCheckTimer;
    private float attackTimer;
    private float powerOutRushTimer;
    private bool isAttackingDoor;
    private bool powerOutRush;

    public int StageIndex => stageIndex;
    public int CameraCount => waypoints != null ? waypoints.Length : 0;
    public bool IsAtDoor => CameraCount > 0 && stageIndex >= CameraCount - 1;

    private void Awake()
    {
        NormalizeTuning();
        if (gameController == null)
            gameController = GetComponentInParent<NightShiftGameController>();
    }

    public void Configure(NightShiftGameController controller, Transform[] route)
    {
        gameController = controller;
        waypoints = route;
    }

    private void Start()
    {
        ResetForNight();
    }

    private void Update()
    {
        if (gameController == null || !gameController.IsPlaying)
            return;

        UpdateVisualPosition();

        if (powerOutRush)
        {
            UpdatePowerOutRush();
            return;
        }

        if (isAttackingDoor)
        {
            UpdateDoorAttack();
            return;
        }

        moveCheckTimer -= Time.deltaTime;
        if (moveCheckTimer > 0f)
            return;

        EvaluateMoveOpportunity();
        ScheduleNextMoveCheck();
    }

    public void ResetForNight()
    {
        stageIndex = 0;
        isAttackingDoor = false;
        powerOutRush = false;
        attackTimer = 0f;
        powerOutRushTimer = 0f;
        failedMoveChecks = 0;
        ScheduleNextMoveCheck();

        if (CameraCount > 0)
            transform.position = waypoints[0].position;
    }

    public void BeginPowerOutRush()
    {
        powerOutRush = true;
        isAttackingDoor = false;
        float minimum = Mathf.Min(powerOutRushDelaySeconds.x, powerOutRushDelaySeconds.y);
        float maximum = Mathf.Max(powerOutRushDelaySeconds.x, powerOutRushDelaySeconds.y);
        powerOutRushTimer = Random.Range(minimum, maximum);
    }

    public string GetCameraFeedText(int cameraIndex)
    {
        if (CameraCount == 0)
            return "CAM -- / OFFLINE\nSIGNAL LOST";

        cameraIndex = Mathf.Clamp(cameraIndex, 0, CameraCount - 1);
        string cameraName = GetCameraName(cameraIndex);
        bool subjectVisible = !powerOutRush && stageIndex == cameraIndex;

        string subjectStatus;
        if (subjectVisible && isAttackingDoor)
            subjectStatus = "SUBJECT AT OFFICE ENTRANCE";
        else if (subjectVisible)
            subjectStatus = "SUBJECT DETECTED";
        else
            subjectStatus = "NO MOTION DETECTED";

        return cameraName + "\n" + subjectStatus + "\n" + BuildCameraMap(cameraIndex);
    }

    private void EvaluateMoveOpportunity()
    {
        if (CameraCount == 0 || IsAtDoor)
            return;

        float advanceChance = baseAdvanceChance + gameController.CurrentHourIndex * hourlyAdvanceChanceBonus;
        if (gameController.IsCameraWatching(stageIndex))
            advanceChance *= observedChanceMultiplier;

        failedMoveChecks++;
        bool forceAdvance = failedMoveChecks >= maximumFailedMoveChecks;
        if (!forceAdvance && Random.value > Mathf.Clamp01(advanceChance))
            return;

        failedMoveChecks = 0;
        stageIndex = Mathf.Min(stageIndex + 1, CameraCount - 1);
        if (IsAtDoor)
        {
            isAttackingDoor = true;
            attackTimer = doorAttackDelay;
            gameController.SetDanger("Movement detected at the office entrance.");
        }
    }

    private void UpdateDoorAttack()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        if (gameController.IsDoorClosed)
        {
            stageIndex = Mathf.Max(1, CameraCount - 3);
            isAttackingDoor = false;
            failedMoveChecks = 0;
            ScheduleNextMoveCheck();
            gameController.BlockEnemyAtDoor();
            return;
        }

        gameController.LoseNight("The figure entered the office.");
    }

    private void UpdatePowerOutRush()
    {
        powerOutRushTimer -= Time.deltaTime;
        if (powerOutRushTimer > 0f)
            return;

        powerOutRush = false;
        stageIndex = Mathf.Max(0, CameraCount - 1);
        isAttackingDoor = true;
        attackTimer = Random.Range(2f, 4f);
    }

    private void ScheduleNextMoveCheck()
    {
        float minimum = Mathf.Max(1f, Mathf.Min(moveCheckIntervalSeconds.x, moveCheckIntervalSeconds.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(moveCheckIntervalSeconds.x, moveCheckIntervalSeconds.y));
        moveCheckTimer = Random.Range(minimum, maximum);
    }

    private void NormalizeTuning()
    {
        if (moveCheckIntervalSeconds.x <= 0f || moveCheckIntervalSeconds.y <= 0f)
            moveCheckIntervalSeconds = new Vector2(5f, 8f);
        if (baseAdvanceChance <= 0f)
            baseAdvanceChance = 0.18f;
        if (hourlyAdvanceChanceBonus <= 0f)
            hourlyAdvanceChanceBonus = 0.07f;
        if (observedChanceMultiplier <= 0f)
            observedChanceMultiplier = 0.12f;
        if (maximumFailedMoveChecks <= 0)
            maximumFailedMoveChecks = 5;
        if (powerOutRushDelaySeconds.x <= 0f || powerOutRushDelaySeconds.y <= 0f)
            powerOutRushDelaySeconds = new Vector2(6f, 11f);
    }

    private string GetCameraName(int cameraIndex)
    {
        string location = waypoints[cameraIndex] != null ? waypoints[cameraIndex].name.ToUpperInvariant() : "OFFLINE";
        int cameraNumber = CameraCount - cameraIndex;
        return "CAM " + cameraNumber.ToString("00") + " / " + location;
    }

    private string BuildCameraMap(int selectedCamera)
    {
        string map = "";
        for (int i = 0; i < CameraCount; i++)
        {
            if (i > 0)
                map += "-";

            int cameraNumber = CameraCount - i;
            string marker = cameraNumber.ToString("00");
            map += i == selectedCamera ? "[>" + marker + "<]" : "[" + marker + "]";
        }

        return map + ">OFFICE";
    }

    private void UpdateVisualPosition()
    {
        if (CameraCount == 0)
            return;

        Vector3 target = waypoints[Mathf.Clamp(stageIndex, 0, CameraCount - 1)].position;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * visualMoveSpeed);
    }
}
