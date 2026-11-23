using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// IA da criatura: patrulha, perseguicao, captura e morte
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Alvo")]
    public Transform player;

    [Header("Deteccao")]
    public float detectionRange = 15f;
    public float loseSightRange = 25f;
    public float loseSightTime = 4f;
    public bool requireLineOfSight = true;
    public LayerMask obstacleMask = ~0;

    [Header("Patrulha")]
    public Transform[] waypoints;
    public Transform patrolCenter;
    public float patrolRadius = 20f;
    public float patrolSpeed = 1.5f;
    public float minWait = 2f;
    public float maxWait = 4f;

    [Header("Perseguicao")]
    public float runSpeed = 4.5f;
    public float catchDistance = 1.5f;
    public float headHeight = 1.0f;

    [Header("Fase 2")]
    public float stalkRadius = 12f;
    public float stalkSpeed = 2.5f;
    public float stalkDetection = 20f;
    public float huntSpeed = 4.8f;
    public float giveUpTime = 2f;
    public float giveUpRest = 8f;

    [Header("Portas")]
    public float doorReach = 0.9f;
    public float doorDelay = 0.7f;

    [Header("Animacao")]
    public Animator animator;
    public string speedParam = "Speed";
    public string attackParam = "Attack";
    public string dieParam = "Die";

    [Header("Som")]
    public bool footsteps = true;

    enum State { Patrol, Chase, Caught, Dead }
    State state = State.Patrol;
    NavMeshAgent agent;
    Vector3 homePos;
    int wpIndex;
    int huntLevel;
    float waitUntil = -1f;
    float lastSeenTime;
    float restUntil;
    bool disabled;
    Vector3 lastSeenPos;
    Vector3 lurePoint;
    float lureRange;
    float noPathTime;
    float doorWaitUntil;
    float nextDoorCheck;
    Collider[] doorHits = new Collider[16];

    public Vector3 HeadPosition { get { return transform.position + Vector3.up * headHeight; } }
    public bool IsDead { get { return state == State.Dead; } }
    public bool IsDisabled { get { return disabled; } }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        homePos = transform.position;
        if (footsteps && GetComponent<CreatureFootsteps>() == null)
            gameObject.AddComponent<CreatureFootsteps>();
    }

    void Start()
    {
        if (patrolCenter != null) homePos = patrolCenter.position;
        FindReferences();
        if (state == State.Patrol)
        {
            agent.speed = PatrolSpeed();
            NextPatrolPoint();
        }
    }

    void FindReferences()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (player == null)
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
    }

    void Update()
    {
        if (state == State.Dead) return;
        if (animator != null) animator.SetFloat(speedParam, agent.velocity.magnitude);
        if (disabled) return;
        if (state == State.Caught || player == null || Time.timeScale == 0f) return;
        if (GameFlow.Instance != null && !GameFlow.Instance.IsPlaying) return;

        if (!agent.isOnNavMesh) return;

        // Espera a porta abrir
        if (Time.time < doorWaitUntil)
        {
            agent.isStopped = true;
            return;
        }
        if (agent.isStopped) agent.isStopped = false;
        OpenDoors();

        float dist = Vector3.Distance(transform.position, player.position);

        if (state == State.Patrol)
        {
            float range = huntLevel >= 1 ? stalkDetection : detectionRange;
            if (Time.time < restUntil) Patrol();
            else if (Lured() || (dist <= range && CanSeePlayer())) StartChase();
            else Patrol();
            return;
        }

        // Perseguindo
        bool sees = Lured() || (dist <= loseSightRange && CanSeePlayer());
        if (sees || dist < 3f) { lastSeenTime = Time.time; lastSeenPos = player.position; }
        agent.SetDestination(lastSeenPos);

        bool sameFloor = Mathf.Abs(player.position.y - transform.position.y) < 1.5f;
        if (dist <= catchDistance && sameFloor && CanSeePlayer()) { Catch(); return; }

        // Sem caminho ate o jogador: desiste por um tempo
        if (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) noPathTime += Time.deltaTime;
        else noPathTime = 0f;

        if (noPathTime > giveUpTime) GiveUp(giveUpRest);
        else if (Time.time - lastSeenTime > loseSightTime) GiveUp(0f);
    }

    // Volta a rondar
    void GiveUp(float rest)
    {
        state = State.Patrol;
        agent.speed = PatrolSpeed();
        waitUntil = -1f;
        noPathTime = 0f;
        if (rest > 0f)
        {
            restUntil = Time.time + rest;
            NextPatrolPoint();
        }
    }

    // No nivel 2, sabe onde o jogador esta quando ele chega perto da armadilha
    bool Lured()
    {
        if (huntLevel < 2 || player == null) return false;
        if (lureRange <= 0f) return true;
        return Vector3.Distance(player.position, lurePoint) <= lureRange;
    }

    // Define a area em volta da armadilha
    public void SetLure(Vector3 point, float range)
    {
        lurePoint = point;
        lureRange = range;
    }

    // Abre as portas fechadas no caminho
    void OpenDoors()
    {
        if (Time.time < nextDoorCheck) return;
        nextDoorCheck = Time.time + 0.2f;
        if (agent.desiredVelocity.sqrMagnitude < 0.01f) return;

        Vector3 dir = agent.desiredVelocity.normalized;
        Vector3 point = transform.position + Vector3.up + dir * doorReach;
        int n = Physics.OverlapSphereNonAlloc(point, doorReach, doorHits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            Door door = doorHits[i].GetComponentInParent<Door>();
            if (door == null || door.IsOpen) continue;
            door.Open();
            doorWaitUntil = Time.time + doorDelay;
        }
    }

    // Nivel de cacada: 0 normal, 1 ronda perto do jogador, 2 persegue sempre
    public void SetHuntLevel(int level)
    {
        huntLevel = level;
        FindReferences();
        if (state == State.Dead || state == State.Caught) return;

        if (Lured()) StartChase();
        else if (state == State.Patrol)
        {
            agent.speed = PatrolSpeed();
            waitUntil = -1f;
            NextPatrolPoint();
        }
    }

    // Volta para o ponto inicial e ignora o jogador por alguns segundos
    public void Rest(float seconds)
    {
        if (state == State.Dead || state == State.Caught) return;
        FindReferences();
        if (player != null && Vector3.Distance(homePos, player.position) > 10f) WarpTo(homePos);
        state = State.Patrol;
        agent.speed = PatrolSpeed();
        restUntil = Time.time + seconds;
        waitUntil = -1f;
    }

    // Liga ou desliga a criatura (tecla de teste)
    public void SetDisabled(bool off)
    {
        if (state == State.Dead || state == State.Caught) return;
        disabled = off;
        state = State.Patrol;
        waitUntil = -1f;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (off)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }
        else
        {
            agent.isStopped = false;
            agent.speed = PatrolSpeed();
            NextPatrolPoint();
        }
    }

    float PatrolSpeed()
    {
        if (huntLevel >= 2) return stalkSpeed * 1.2f;
        return huntLevel >= 1 ? stalkSpeed : patrolSpeed;
    }

    void StartChase()
    {
        if (player == null) return;
        state = State.Chase;
        agent.speed = huntLevel >= 2 ? huntSpeed : runSpeed;
        lastSeenTime = Time.time;
        lastSeenPos = player.position;
        waitUntil = -1f;
        noPathTime = 0f;
    }

    void Patrol()
    {
        if (agent.pathPending) return;
        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (waitUntil < 0f) waitUntil = Time.time + Random.Range(minWait, maxWait);
            else if (Time.time >= waitUntil) { waitUntil = -1f; NextPatrolPoint(); }
        }
    }

    // Escolhe o proximo ponto da patrulha
    void NextPatrolPoint()
    {
        if (!agent.isOnNavMesh) return;
        bool stalking = huntLevel >= 1 && player != null && Time.time >= restUntil;

        if (!stalking && waypoints != null && waypoints.Length > 0)
        {
            Transform wp = waypoints[wpIndex % waypoints.Length];
            wpIndex++;
            if (wp != null) agent.SetDestination(wp.position);
            return;
        }

        if (stalking && RandomPointAround(player.position, huntLevel >= 2 ? stalkRadius * 0.6f : stalkRadius)) return;
        RandomPointAround(homePos, patrolRadius);
    }

    // Vai para um ponto alcancavel em volta do centro
    bool RandomPointAround(Vector3 center, float radius)
    {
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < 15; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 2.5f, NavMesh.AllAreas)) continue;
            if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetPath(path);
                return true;
            }
        }
        return false;
    }

    void Catch()
    {
        state = State.Caught;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        if (animator != null && !string.IsNullOrEmpty(attackParam)) animator.SetTrigger(attackParam);
        if (GameFlow.Instance != null) GameFlow.Instance.PlayerCaught(this);
    }

    // Mata a criatura
    public void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;
        FindReferences();

        if (agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false;
        }

        CreatureFootsteps steps = GetComponent<CreatureFootsteps>();
        if (steps != null) steps.enabled = false;

        if (animator != null)
        {
            animator.SetFloat(speedParam, 0f);
            if (!string.IsNullOrEmpty(dieParam)) animator.SetTrigger(dieParam);
            StartCoroutine(FreezeAfterDeath());
        }
    }

    // Para a animacao no fim da queda
    IEnumerator FreezeAfterDeath()
    {
        float length = 2f;
        if (animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name.Contains("Death")) length = clip.length;
        }
        yield return new WaitForSeconds(length * 0.95f);
        animator.speed = 0f;
    }

    // Move a criatura para um ponto do NavMesh
    public void WarpTo(Vector3 position)
    {
        if (agent != null && agent.enabled) agent.Warp(position);
        else transform.position = position;
    }

    bool CanSeePlayer()
    {
        if (!requireLineOfSight) return true;
        Vector3 origin = HeadPosition;
        Vector3 target = player.position + Vector3.up * 1.5f;
        Vector3 dir = target - origin;
        float len = dir.magnitude;
        if (len < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir / len, len, obstacleMask, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        Transform first = null;
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.distance < closest) { closest = h.distance; first = h.transform; }
        }
        return first == null || first.IsChildOf(player);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
        Gizmos.color = Color.cyan;
        Vector3 c = patrolCenter != null ? patrolCenter.position : (Application.isPlaying ? homePos : transform.position);
        Gizmos.DrawWireSphere(c, patrolRadius);
    }
}
