using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Shootable NPC / dummy / enemy. Put this on any GameObject that has a Collider
// (one is auto-added if missing) and it will take damage from GunController,
// flash white while hit and become a real physics body (Rigidbody) when it dies.
// The body does NOT shrink away — it is knocked back and tumbles like a real object.
//
// If the object has no renderers a simple capsule dummy (body + head) is built
// at runtime so you can test immediately — swap in your own model later.
public class EnemyController : MonoBehaviour, IDamageable, IReadOnlyHealth
{
        [Header("Health")]
    public int maxHealth = 200;

    [Header("Health Bar (overhead)")]
    [Tooltip("Height above the object the floating health bar sits at.")]
    public float healthBarHeight = 2.4f;
    [Tooltip("World-space width/height of the overhead health bar (metres).")]
    public Vector2 healthBarSize = new Vector2(1.9f, 0.22f);

    public int MaxHealth => maxHealth;
    public int CurrentHealth => _hp;
    public bool IsAlive => !_dead;

    [Header("Model")]
    [Tooltip("Optional: a real NPC/body mesh to swap for the placeholder dummy. Drag your model prefab here.\nIt inherits all ragdoll physics on death.")]
    public GameObject bodyModelPrefab;

    [Header("Death")]
    [Tooltip("How hard the killing hit throws the body (higher = flies further).")]
    public float deathImpulse = 5f;
    [Tooltip("Random spin torque applied to the corpse on death.")]
    public float deathSpinMax = 360f;
    [Tooltip("Empty/-1 = stays until end of match. Otherwise auto-removes body after this many seconds.\n" +
            "If left at -1, a safety cleanup still removes the corpse after corpseCleanupDelay seconds.")]
    public float autoDestroyDelay = -1f;
    public float corpseCleanupDelay = 20f;

    [Header("Audio")]
    public AudioClip damageSound;
    [Range(0f, 1f)] public float damageVolume = 1f;
    public AudioClip deathSound;
    [Range(0f, 1f)] public float deathVolume = 1f;
    [Tooltip("Sound played when the AI shoots (optional).")]
    public AudioClip attackSound;
    [Range(0f, 1f)] public float attackVolume = 1f;

    [Header("AI (enemy)")]
    [Tooltip("Master switch. Disable to turn this into a static training dummy.")]
    public bool aiEnabled = true;

    [Header("AI - Senses")]
    [Tooltip("Distance at which the AI first notices the player.")]
    public float detectionRange = 34f;
    [Tooltip("Distance at which the AI gives up (forgets) the player.")]
    public float loseRange = 45f;
    [Range(10f, 180f)] public float fieldOfView = 130f;
    [Tooltip("Layers that block the AI's line of sight (and shots).")]
    public LayerMask sightMask = ~0;

    [Header("AI - Movement")]
    [Tooltip("How fast the AI closes in on the player.")]
    public float moveSpeed = 4.5f;
    public float turnSpeed = 7f;
    [Tooltip("Use a NavMeshAgent to path around walls when a NavMesh is baked. Falls back to walking straight if none available.")]
    public bool useNavMesh = true;
    [Tooltip("Distance the AI tries to keep from the player while fighting (melee = hugging distance).")]
    public float preferredRange = 0.6f;
    [Tooltip("Within this range the AI attacks (short = melee fists).")]
    public float attackRange = 1.1f;
    [Tooltip("How far the AI strafes sideways during combat.")]
    public float strafeDistance = 0.4f;
    [Tooltip("How often the AI switches strafe direction.")]
    public float strafeChangeTime = 1.6f;

    [Header("AI - Combat")]
    [Tooltip("Lowest damage an AI shot deals.")]
    public int aiDamageMin = 10;
    [Tooltip("Highest damage an AI shot deals (inclusive).")]
    public int aiDamageMax = 20;
    [Tooltip("How far AI bullets travel (short = melee range).")]
    public float aiFireRange = 1.3f;
    [Tooltip("Seconds between shots inside a burst.")]
    public float fireCooldown = 0.8f;
    [Tooltip("Shots per burst (1 = single punch/swing).")]
    public int burstCount = 1;
    [Tooltip("Pause between bursts.")]
    public float burstPause = 0.3f;
    [Tooltip("Aiming inaccuracy in degrees (higher = less accurate).")]
    public float aimSpread = 6f;
    [Tooltip("Height above the ground the AI aims / fires from.")]
    public float aimHeight = 1.45f;
    [Tooltip("Optional effect spawned at the enemy's muzzle when it fires.")]
    public GameObject attackEffectPrefab;

    private enum AIState { Idle, Chase, Combat }

    private int _hp;
    private Renderer[] _renderers;
    private Color[] _baseColors;
    private bool _dead;

    // AI runtime state
    private AIState _aiState = AIState.Idle;
    private PlayerHealth _aiTargetHealth;
    private Transform _aiTarget;
    private float _strafeDir = 1f;
    private float _strafeTimer;
    private bool _inBurst;
    private int _burstsLeft;
    private float _burstPauseEnd;
    private float _nextFireTime;
    private NavMeshAgent _agent;

    public bool IsDead => _dead;

    void Awake()
    {
        _hp = maxHealth;
        _renderers = GetComponentsInChildren<Renderer>(true);

        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material m = _renderers[i].material;
            if (m.HasProperty("_BaseColor")) _baseColors[i] = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) _baseColors[i] = m.GetColor("_Color");
            else _baseColors[i] = Color.white;
        }

                if (GetComponent<Collider>() == null)
            gameObject.AddComponent<CapsuleCollider>();

        // NavMesh pathfinding so the AI walks around walls (if a NavMesh is baked).
        // Falls back to direct movement automatically when none is available.
        if (aiEnabled && useNavMesh && GetComponent<NavMeshAgent>() == null)
        {
            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.speed = moveSpeed;
            _agent.angularSpeed = turnSpeed * 40f;
            _agent.acceleration = 10f;
            _agent.stoppingDistance = Mathf.Max(0.5f, preferredRange);
            _agent.updateRotation = false; // we face the player ourselves
            _agent.updatePosition = true;
        }

        // Optional custom body model. Spawned as a child so it tumbles with the root
        // on death. Re-cache renderers/base colors so hit flashes tint the real model.
        if (bodyModelPrefab != null)
        {
            GameObject model = Instantiate(bodyModelPrefab, transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material m = _renderers[i].material;
                if (m.HasProperty("_BaseColor")) _baseColors[i] = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) _baseColors[i] = m.GetColor("_Color");
                else _baseColors[i] = Color.white;
            }
        }

        if (_renderers.Length == 0) BuildPlaceholderDummy();

        // Floating, animated health bar above this NPC (faces the camera on its own).
        HealthBarUI.CreateWorld(this, transform, healthBarHeight, healthBarSize);
    }

        public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (_dead || amount <= 0) return;

        _hp = Mathf.Max(0, _hp - amount);
        SoundFX.Play(transform, damageSound, damageVolume);
        StopAllCoroutines();
        StartCoroutine(HitFlashRoutine());

        if (_hp <= 0) Die(hitDirection, hitPoint);
    }

    private IEnumerator HitFlashRoutine()
    {
        SetAllColors(Color.white);
        yield return new WaitForSeconds(0.08f);
        if (!_dead) RestoreColors();
    }

    private void Die(Vector3 hitDirection, Vector3 hitPoint)
    {
        _dead = true;
        SoundFX.Play(transform, deathSound, deathVolume);

        StopAllCoroutines();

        // Stop pathfinding before we turn the corpse into a physics body.
        if (_agent != null) _agent.enabled = false;

        // Become a real physics body: give the root a Rigidbody (create one if the
        // model didn't have one) and knock it back with the killing shot impulse.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 5f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.AddForce(hitDirection * deathImpulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * deathSpinMax, ForceMode.Impulse);

        // Remove the head shrink animation: the body now falls/tumbles on its own.
        if (autoDestroyDelay > 0f)
            Destroy(gameObject, autoDestroyDelay);
        else
            Destroy(gameObject, corpseCleanupDelay); // safety so bodies don't pile forever
    }

    private void SetAllColors(Color c)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material m = _renderers[i].material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material m = _renderers[i].material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", _baseColors[i]);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", _baseColors[i]);
        }
    }

    // Builds a simple grey capsule dummy if no model was assigned.
    private void BuildPlaceholderDummy()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        Color c = new Color(0.55f, 0.30f, 0.30f); // nice "target" red
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        body.transform.localScale = new Vector3(0.7f, 0.8f, 0.7f);
        DestroyC(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = mat;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        head.transform.localScale = Vector3.one * 0.32f;
        DestroyC(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().material = mat;

        // Re-grab renderers for hit flashes.
        _renderers = GetComponentsInChildren<Renderer>(true);
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = c;
    }

    private void DestroyC(Collider collider)
    {
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }
    }

    // ------------------------------------------------------------------ //
    //  AI BRAIN                                                           //
    // ------------------------------------------------------------------ //

    void Update()
    {
        if (_dead || !aiEnabled) return;
        UpdateAI();
    }

    // Cache the player once. Uses PlayerHealth if present, else the main camera.
    private void ResolveTarget()
    {
        if (_aiTarget != null) return;
        if (_aiTargetHealth == null)
            _aiTargetHealth = FindFirstObjectByType<PlayerHealth>();
        _aiTarget = _aiTargetHealth != null ? _aiTargetHealth.transform
                   : (Camera.main != null ? Camera.main.transform : null);
    }

    private Vector3 Eyes => transform.position + Vector3.up * aimHeight;

    private bool InFieldOfView(Vector3 dir)
        => Vector3.Angle(transform.forward, dir) <= fieldOfView * 0.5f;

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 origin = Eyes;
        Vector3 dir = targetPos - origin;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, loseRange, sightMask))
            return IsTargetHit(hit.collider);
        return true; // nothing in the way -> clear sight
    }

    private bool IsTargetHit(Collider c)
    {
        if (_aiTarget == null) return true;
        Transform t = c.transform;
        return t == _aiTarget || t.IsChildOf(_aiTarget);
    }

    private void UpdateAI()
    {
        ResolveTarget();
        if (_aiTarget == null) { _aiState = AIState.Idle; return; }

        Vector3 toT = _aiTarget.position - transform.position;
        float dist = toT.magnitude;
        Vector3 flatToT = toT; flatToT.y = 0f;

        bool facing = InFieldOfView(flatToT);
        bool sight = HasLineOfSight(_aiTarget.position + Vector3.up * 0.6f);

        // Fully lost the player -> idle scan.
        if (dist > loseRange || (!sight && !facing && _aiState != AIState.Combat))
        {
            _aiState = AIState.Idle;
            IdleBehaviour();
            return;
        }

        // Heard/saw you but no clear shot, or too far -> chase/close in.
        if (!sight || dist > attackRange)
        {
            _aiState = AIState.Chase;
            ChaseBehaviour(flatToT, dist);
            return;
        }

        // Clean line of sight and in range -> fight.
        _aiState = AIState.Combat;
        CombatBehaviour(flatToT, dist);
    }

    private void FaceTowards(Vector3 flatDir)
    {
        if (flatDir.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(flatDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void IdleBehaviour()
    {
        // Stop drifting if we were pursuing.
        SetNavMeshActive(false);
        // Slow scan so it may spot you from an angle.
        transform.Rotate(Vector3.up, 20f * Time.deltaTime);
    }

    private void ChaseBehaviour(Vector3 flatToT, float dist)
    {
        FaceTowards(flatToT);

        // Path around walls when a NavMesh is available; otherwise walk straight.
        if (CanPath())
        {
            SetNavMeshActive(true);
            _agent.SetDestination(_aiTarget.position);
        }
        else
        {
            SetNavMeshActive(false);
            transform.position += flatToT.sqrMagnitude > 0.001f
                ? flatToT.normalized * moveSpeed * Time.deltaTime
                : Vector3.zero;
        }
    }

    // NavMesh pathing is only usable if one is baked; otherwise fall back to walking straight.
    private bool CanPath() => _agent != null && useNavMesh && _agent.isOnNavMesh;

    private void SetNavMeshActive(bool on)
    {
        if (_agent != null) _agent.enabled = on;
    }

    private void CombatBehaviour(Vector3 flatToT, float dist)
    {
        // Direct (manually controlled) movement while fighting so we can strafe / kite.
        SetNavMeshActive(false);
        FaceTowards(flatToT);

        // Periodically flip strafe direction so it's harder to hit.
        _strafeTimer += Time.deltaTime;
        if (_strafeTimer >= strafeChangeTime)
        {
            _strafeTimer = 0f;
            _strafeDir = Random.value < 0.5f ? 1f : -1f;
        }

        Vector3 toDir = flatToT.normalized;
        Vector3 strafe = Vector3.Cross(Vector3.up, toDir) * _strafeDir;

        // Back off if too close, close in if too far, and always drift sideways.
        float rangeError = Mathf.Clamp(dist - preferredRange, -1f, 1f);
        Vector3 move = (toDir * (rangeError * 0.8f) + strafe * 0.6f).normalized;
        transform.position += move * moveSpeed * Time.deltaTime;

        UpdateBurst();
    }

    // Fires in short, realistic bursts (e.g. 3 shots, then a pause).
    private void UpdateBurst()
    {
        if (_inBurst)
        {
            if (Time.time >= _nextFireTime)
            {
                FireOneShot();
                _burstsLeft--;
                _nextFireTime = Time.time + fireCooldown;
                if (_burstsLeft <= 0)
                {
                    _inBurst = false;
                    _burstPauseEnd = Time.time + burstPause;
                }
            }
        }
        else if (Time.time >= _burstPauseEnd)
        {
            _burstsLeft = burstCount;
            _inBurst = true;
        }
    }

    private void FireOneShot()
    {
        if (_aiTarget == null) return;
        Vector3 origin = Eyes;
        Vector3 targetPoint = _aiTarget.position + Vector3.up * 0.7f;
        // Hard melee cap: it cannot damage you unless it is right next to you.
        if (Vector3.Distance(origin, targetPoint) > aiFireRange) return;
        Vector3 toward = targetPoint - origin;

        // Slight, random inaccuracy so it's fair to fight.
        Vector3 dir = Quaternion.Euler(Random.Range(-aimSpread, aimSpread),
                                       Random.Range(-aimSpread, aimSpread), 0f) * toward.normalized;

        SoundFX.Play(transform, attackSound, attackVolume);

        if (attackEffectPrefab != null)
        {
            GameObject fx = Instantiate(attackEffectPrefab, origin, Quaternion.LookRotation(dir));
            Destroy(fx, 0.08f);
        }

        if (Physics.Raycast(origin, dir, out RaycastHit hit, aiFireRange, sightMask))
        {
            IDamageable dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null && !ReferenceEquals(dmg, this))
            {
                int dmgAmount = Random.Range(aiDamageMin, aiDamageMax + 1); // 10–20
                dmg.TakeDamage(dmgAmount, hit.point, -dir);
                BloodVFX.Spawn(hit.point, hit.normal); // so you can see you're being hit
            }
        }
    }
}