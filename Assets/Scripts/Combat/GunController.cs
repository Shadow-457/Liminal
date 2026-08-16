using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Working hit-scan gun. Attach this to your gun model (or any empty object).
//
// Setup:
//  - Put this GameObject under the Main Camera for a first-person viewmodel,
//    or just drop it anywhere in the scene: the script will auto-parent it to
//    the camera at a viewmodel position if it isn't already a camera child.
//  - If the object has no visuals, a grey blocky pistol placeholder is built at
//    runtime so you can start shooting immediately (swap your real model in
//    later by making it a child of this object instead).
//
// Controls:
//  - Hold Left Mouse to fire (automatic). Right-click still does inventory pickup.
public class GunController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used to aim/shoot. Auto-found via Camera.main if empty.")]
    public Camera fireCamera;

    [Header("Field of View")]
    [Tooltip("Sets the camera's Field of View (degrees). Applied every frame so it always wins. Default 40 to match the Main Camera.")]
    [Range(30f, 120f)] public float fov = 40f;

    [Tooltip("The part that recoils and shows the muzzle flash.")]
    public Transform weaponPivot;

    [Header("Damage")]
    [Tooltip("Lowest damage any single bullet deals.")]
    public int damageMin = 35;
    [Tooltip("Highest damage any single bullet deals (inclusive).")]
    public int damageMax = 55;
    public float fireRate = 12f;      // shots per second
    public float range = 200f;
    public LayerMask shootMask = ~0;

    [Header("Feel")]
    public float recoilAmount = 0.03f;   // how far the gun kicks back (metres)
    public float recoilRestoreSpeed = 9f;
    public float kickPitch = 0.6f;       // gun jumps up this many degrees

    [Header("Ammo")]
    [Tooltip("Bullets per magazine (default 4, then you reload).")]
    public int magazineSize = 4;
    public float reloadTime = 1.4f;
    public bool infiniteAmmo = false;
    [Tooltip("Press this key to reload when the magazine is empty.")]
    public KeyCode reloadKey = KeyCode.R;
    [Tooltip("If true, the gun only fires while its reserved inventory slot (slot 4) is selected/equipped.")]
    public bool requireEquip = true;

    [Tooltip("For requireEquip. Auto-found if empty.")]
    public PlayerInteractor playerInteractor;
    [Tooltip("For requireEquip. Auto-found if empty.")]
    public InventoryManager inventoryManager;

            [Header("Model")]
    [Tooltip("Your real gun model. If empty and nothing has a renderer, a blocky placeholder pistol is built for you.\nThe model should be a child object so it inherits the gun's recoil.")]
    public GameObject weaponModelPrefab;

    [Tooltip("Uniform size multiplier for the gun (custom model AND placeholder). Default 1.")]
    public float modelSize = 1f;

    [Header("Viewmodel")]
    [Tooltip("Where the gun sits relative to the camera (x = right/left, y = up/down, z = forward). Increase x to move it to the right.")]
    public Vector3 viewmodelOffset = new Vector3(0.16f, -0.16f, 0.5f);
    [Tooltip("Extra rotation applied to the gun viewmodel (Euler degrees).")]
    public Vector3 viewmodelRotation = Vector3.zero;

    [Tooltip("Where flash/blood/rays come from. Defaults to this object (the gun root).")]
    public Transform muzzlePoint;

    [Header("Knockback")]
    [Tooltip("Impulse applied to Rigidbodies you shoot, so bodies fly like real objects.")]
    public float knockbackForce = 6f;

    [Header("VFX (optional - leave empty to use built-in blood)")]
    [Tooltip("If assigned, this is instantiated at the muzzle instead of the old cube flash (set to your own VFX later).")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("If assigned, this is spawned at the hit point instead of blood (e.g. sparks for a metal surface).")]
    public GameObject hitEffectPrefab;
    [Tooltip("Seconds a custom muzzle flash / hit effect is left before being cleaned up.")]
    public float vfxAutoDestroy = 10f;

    [Header("Audio")]
    public AudioClip shootSound;
    [Range(0f, 1f)] public float shootVolume = 1f;
    public AudioClip reloadSound;

    private int _ammo;
    private bool _reloading;
    private float _nextFireTime;

    private Vector3 _restPos;
    private Quaternion _restRot;

    private Material _metalMat;
    private Material _darkMat;

    void Awake()
    {
        if (fireCamera == null) fireCamera = Camera.main;

        // Auto-parent the whole gun to the camera (at a viewmodel position) if
        // the user just dropped it somewhere in the scene instead of dragging
        // it under the Main Camera manually.
        if (fireCamera != null && !transform.IsChildOf(fireCamera.transform))
        {
            transform.SetParent(fireCamera.transform, false);
            transform.localPosition = viewmodelOffset;              // e.g. increase x for more right
            transform.localRotation = Quaternion.Euler(viewmodelRotation);
        }
        if (weaponPivot == null) weaponPivot = transform;

        // Guard: the recoil must happen on this gun (or a child of it). If the
        // Main Camera / player was dragged into weaponPivot, every shot would
        // kick the whole view back — that feels like the player is being pushed.
        // Revert to the gun itself in that case.
        if (weaponPivot != null && transform.IsChildOf(weaponPivot))
            weaponPivot = transform;

                // Optional: drop your own gun model here. Parented to the gun root so it
        // inherits the recoil automatically.
        if (weaponModelPrefab != null)
        {
            GameObject model = Instantiate(weaponModelPrefab, transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = model.transform.localScale * Mathf.Max(0.01f, modelSize);
        }

        if (muzzlePoint == null) muzzlePoint = weaponPivot != null ? weaponPivot : transform;

        _restPos = weaponPivot.localPosition;
        _restRot = weaponPivot.localRotation;
        _ammo = magazineSize;

        CreateMaterials();
        if (!HasVisual()) BuildPlaceholderPistol();

        // The gun is a viewmodel: it must NEVER push the player. Any collider
        // or rigidbody on the gun (your model or the placeholder parts) is
        // disabled here so it can't shove the character around.
        DisableGunPhysics();

        // Cache references and add a small ammo counter to the HUD (only when finite ammo).
        if (playerInteractor == null) playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        if (inventoryManager == null) inventoryManager = FindFirstObjectByType<InventoryManager>();
        CreateAmmoUI();

        // Remember the gun's model parts so we can hide them when the gun isn't equipped.
        _visualRenderers = GetComponentsInChildren<Renderer>(true);
        UpdateGunVisibility();
    }

    // A simple HUD read-out for the magazine, e.g. "Ammo  3 / 4".
    private Text _ammoText;
    private Renderer[] _visualRenderers;
    private void CreateAmmoUI()
    {
        if (infiniteAmmo) return;
        Canvas canvas = HealthBarUI.FindHUDCanvas();
        if (canvas == null) return;

        GameObject go = new GameObject("Ammo", typeof(RectTransform), typeof(Text));
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-20f, 20f);
        rt.sizeDelta = new Vector2(140f, 26f);

        _ammoText = go.GetComponent<Text>();
        _ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _ammoText.alignment = TextAnchor.MiddleRight;
        _ammoText.fontSize = 18;
        _ammoText.color = new Color(1f, 0.92f, 0.4f, 1f);
        _ammoText.raycastTarget = false;
        _ammoText.gameObject.SetActive(false);
    }

    private void DisableGunPhysics()
    {
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = false;
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Update()
    {
        // Enforce camera FOV every frame (simple, reliable, wins over anything else).
        if (fireCamera != null && Mathf.Abs(fireCamera.fieldOfView - fov) > 0.01f)
            fireCamera.fieldOfView = fov;

        // Smoothly return the gun to its resting pose after recoil.
        weaponPivot.localPosition = Vector3.Lerp(weaponPivot.localPosition, _restPos, recoilRestoreSpeed * Time.deltaTime);
        weaponPivot.localRotation = Quaternion.Slerp(weaponPivot.localRotation, _restRot, recoilRestoreSpeed * Time.deltaTime);

        UpdateGunVisibility();
        UpdateAmmoUI();

        // Manual reload (R) whenever the mag isn't full.
        if (Input.GetKeyDown(reloadKey) && !_reloading && !infiniteAmmo)
        {
            StartCoroutine(ReloadRoutine());
            return;
        }

        if (_reloading) return;

        if (Input.GetMouseButton(0) && IsGunEquipped()) // Left mouse held to fire (gun must be equipped)
        {
            if (_ammo > 0 || infiniteAmmo)
            {
                if (Time.time >= _nextFireTime) Fire();
            }
            else if (!_reloading)
            {
                StartCoroutine(ReloadRoutine()); // all 4 bullets used -> auto reload
            }
        }
    }

    // The gun only fires while its reserved slot (slot 4) is selected, unless requireEquip is off.
    private bool IsGunEquipped()
    {
        if (!requireEquip) return true;
        int gun = inventoryManager != null ? inventoryManager.GunSlotIndex : -1;
        if (gun < 0) return true; // no reserved gun slot -> gun always usable
        return playerInteractor != null && playerInteractor.SelectedSlotIndex == gun;
    }

    private void UpdateAmmoUI()
    {
        if (_ammoText == null) return;
        bool equipped = IsGunEquipped();
        if (_ammoText.gameObject.activeSelf != equipped)
            _ammoText.gameObject.SetActive(equipped);
        if (!equipped) return;
        _ammoText.text = _reloading ? "Reloading..." : $"Ammo  {_ammo} / {magazineSize}";
    }

    // Show the gun's model only while its reserved slot (slot 4) is equipped; otherwise
    // hide it so you don't hold a gun you can't fire (and the inventory viewmodel shows instead).
    private void UpdateGunVisibility()
    {
        if (_visualRenderers == null || _visualRenderers.Length == 0) return;
        bool show = !requireEquip || IsGunEquipped();
        for (int i = 0; i < _visualRenderers.Length; i++)
        {
            Renderer r = _visualRenderers[i];
            if (r != null && r.enabled != show) r.enabled = show;
        }
    }

        private void Fire()
    {
        _nextFireTime = Time.time + 1f / fireRate;
        if (!infiniteAmmo) _ammo--;

        SoundFX.Play(muzzlePoint != null ? muzzlePoint : transform, shootSound, shootVolume);
        SpawnMuzzleFlash();
        ApplyKick();

        if (fireCamera == null) return;

        Vector3 origin = fireCamera.transform.position;
        Vector3 dir = fireCamera.transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, range, shootMask, QueryTriggerInteraction.Ignore))
        {
            // Deal randomized damage.
            IDamageable enemy = hit.collider.GetComponentInParent<IDamageable>();
            if (enemy != null)
            {
                int dmg = Random.Range(damageMin, damageMax + 1);
                enemy.TakeDamage(dmg, hit.point, -dir);
            }

            // Real physics: push anything with a Rigidbody back like a real body.
            if (knockbackForce > 0f && hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(dir * knockbackForce, hit.point, ForceMode.Impulse);

            // Hit effect: custom VFX if you added one, otherwise a blood squib
            // that bursts out from the exact point/normal we hit.
            if (hitEffectPrefab != null)
            {
                GameObject fx = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, vfxAutoDestroy);
            }
            else BloodVFX.Spawn(hit.point, hit.normal);
        }
        // (no tracer spark cube - removed on purpose)
    }

    private void ApplyKick()
    {
        weaponPivot.localPosition = _restPos + new Vector3(0f, 0f, -recoilAmount);
        weaponPivot.localRotation = _restRot * Quaternion.Euler(-kickPitch, Random.Range(-2f, 2f), 0f);
    }

            // Only drawn if you assign a muzzleFlashPrefab (your own VFX). The old blocky
    // cube flash is gone — plug your own effect in the inspector "VFX" section.
    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || muzzlePoint == null) return;
        GameObject fx = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
        Destroy(fx, vfxAutoDestroy);
    }

    // ---------------------------------------------------------------
    // Placeholder so the gun "just works" without any imported model.
    // ---------------------------------------------------------------
    private bool HasVisual()
    {
        return GetComponentInChildren<Renderer>() != null;
    }

    private void BuildPlaceholderPistol()
    {
        // All pieces point along +Z so the barrel faces forward.
        CreatePart("Slide", new Vector3(0.08f, 0.07f, 0.30f), new Vector3(0f, 0.02f, 0.15f), _metalMat);
        CreatePart("Barrel", new Vector3(0.045f, 0.045f, 0.14f), new Vector3(0f, 0.035f, 0.35f), _darkMat);
        CreatePart("FrontSight", new Vector3(0.02f, 0.04f, 0.02f), new Vector3(0f, 0.06f, 0.35f), _darkMat);
        CreatePart("Grip", new Vector3(0.07f, 0.16f, 0.09f), new Vector3(0f, -0.13f, 0.02f), _darkMat);
    }

    private void CreatePart(string name, Vector3 size, Vector3 localPos, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPos;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = size * Mathf.Max(0.01f, modelSize);
        RemoveCollider(part);
        Renderer r = part.GetComponent<Renderer>();
        if (r != null) r.material = material;
    }

            private void CreateMaterials()
    {
        _metalMat = MakeMat(new Color(0.30f, 0.32f, 0.35f));
        _darkMat = MakeMat(new Color(0.08f, 0.08f, 0.09f));
    }

    private Material MakeMat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        return m;
    }

    private void RemoveCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null)
        {
            // Disabling instantly (rather than waiting for Destroy) means
            // physics NEVER sees these FX colliders. Otherwise a muzzle flash
            // spawning near the player's capsule could shove the player out.
            c.enabled = false;
            Destroy(c);
        }
    }

            private IEnumerator ReloadRoutine()
    {
        _reloading = true;
        SoundFX.Play(transform, reloadSound, 0.6f);
        yield return new WaitForSeconds(reloadTime);
        _ammo = magazineSize;
        _reloading = false;
    }
}