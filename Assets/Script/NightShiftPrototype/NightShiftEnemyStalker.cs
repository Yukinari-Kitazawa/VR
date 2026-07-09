using UnityEngine;

public sealed class NightShiftEnemyStalker : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float baseMoveInterval = 20f;
    [SerializeField] private float intervalJitter = 5f;
    [SerializeField] private float doorAttackDelay = 4.5f;
    [SerializeField] private float visualMoveSpeed = 2.2f;

    private NightShiftGameController gameController;
    private int stageIndex;
    private float moveTimer;
    private float nextMoveDelay;
    private float attackTimer;
    private bool isAttackingDoor;
    private bool rushMode;

    public int StageIndex => stageIndex;
    public bool IsAtDoor => waypoints != null && waypoints.Length > 0 && stageIndex >= waypoints.Length - 1;

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

        if (isAttackingDoor)
        {
            UpdateDoorAttack();
            return;
        }

        float monitorSlow = gameController.IsMonitorOpen ? 0.55f : 1f;
        float rushMultiplier = rushMode ? 4.5f : 1f;
        moveTimer += Time.deltaTime * monitorSlow * rushMultiplier;

        if (moveTimer >= nextMoveDelay)
            AdvanceStage();
    }

    public void ResetForNight()
    {
        stageIndex = 0;
        moveTimer = 0f;
        isAttackingDoor = false;
        rushMode = false;
        ScheduleNextMove();

        if (waypoints != null && waypoints.Length > 0)
            transform.position = waypoints[0].position;
    }

    public void BeginPowerOutRush()
    {
        rushMode = true;
        if (waypoints == null || waypoints.Length == 0)
            return;

        stageIndex = Mathf.Max(stageIndex, Mathf.Max(0, waypoints.Length - 2));
        moveTimer = 0f;
        nextMoveDelay = 2f;
    }

    public string GetFeedText()
    {
        if (waypoints == null || waypoints.Length == 0)
            return "CAM OFFLINE";

        if (IsAtDoor)
            return "CAM 01 / OFFICE DOOR\nSignal: unstable\nThe figure is outside.";

        if (stageIndex >= waypoints.Length - 2)
            return "CAM 02 / MAIN HALL\nSignal: noisy\nMovement close to the office.";

        if (stageIndex >= 1)
            return "CAM 03 / BACK HALL\nSignal: noisy\nA silhouette is visible.";

        return "CAM 04 / STORAGE\nSignal: weak\nNo motion detected.";
    }

    private void AdvanceStage()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        stageIndex = Mathf.Min(stageIndex + 1, waypoints.Length - 1);
        moveTimer = 0f;

        if (IsAtDoor)
        {
            isAttackingDoor = true;
            attackTimer = doorAttackDelay;
            gameController.SetDanger("Door contact detected.");
        }
        else
        {
            gameController.ClearDanger();
            ScheduleNextMove();
        }
    }

    private void UpdateDoorAttack()
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f)
            return;

        if (gameController.IsDoorClosed)
        {
            stageIndex = Mathf.Max(1, waypoints.Length / 2);
            isAttackingDoor = false;
            moveTimer = 0f;
            ScheduleNextMove();
            gameController.BlockEnemyAtDoor();
            return;
        }

        gameController.LoseNight("The figure entered the office.");
    }

    private void ScheduleNextMove()
    {
        float jitter = Random.Range(-intervalJitter, intervalJitter);
        nextMoveDelay = Mathf.Max(5f, baseMoveInterval + jitter);
    }

    private void UpdateVisualPosition()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Vector3 target = waypoints[Mathf.Clamp(stageIndex, 0, waypoints.Length - 1)].position;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * visualMoveSpeed);
    }
}
