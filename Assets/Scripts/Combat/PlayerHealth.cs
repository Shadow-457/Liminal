using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health for the player. Put this on any object you want to be the player
/// (the Main Camera / PlayerCapsule). It implements IDamageable so the gun and
/// any enemy can damage the player, and it auto-builds an animated HUD health
/// bar (see HealthBarUI) on the scene Canvas when the scene starts.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable, IReadOnlyHealth
{
    [Header("Health")]
    public int maxHealth = 100;

    [Header("Regeneration (optional)")]
    [Tooltip("Health recharged per second while at rest. 0 disables regen.")]
    public float regenPerSecond = 0f;
    [Tooltip("Seconds you must avoid taking damage before regen kicks in.")]
    public float regenDelay = 4f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => _hp;
    public bool IsAlive => _hp > 0;

    /// <summary>Fired with (current, max) whenever health changes (including regen).</summary>
    public event Action<int, int> OnHealthChanged;
    /// <summary>Fired once when health reaches zero.</summary>
    public event Action OnDeath;

    private int _hp;
    private float _lastHurtTime = -999f;
    private bool _dead;

    [Header("Respawn")]
    [Tooltip("Key to press to respawn after dying.")]
    public KeyCode respawnKey = KeyCode.R;
    [Tooltip("Seconds you must stay dead before respawn is allowed.")]
    public float respawnDelay = 1.0f;

    private Vector3 _spawnPos;
    private Quaternion _spawnRot;
    private float _diedAt;

    [Header("Hit Feedback")]
    [Tooltip("Red flash intensity when the player takes a hit.")]
    public float hitFlashIntensity = 0.32f;
    [Tooltip("How long the damage direction arrow stays visible.")]
    public float directionArrowTime = 0.7f;

    private Image _damageFlash;
    private Image _damageArrow;
    private Coroutine _flashRoutine;
    private Coroutine _arrowRoutine;

    private GameObject _deathPanel;
    private Image _deathDim;
    private Text _deathLabel;

    void Awake()
    {
        _hp = maxHealth;
        _dead = false;
        _spawnPos = transform.position;
        _spawnRot = transform.rotation;
    }

    void Start()
    {
        // If a HealthBarUI was already placed on a Canvas manually and wired to this
        // player health, don't create a second one.
        if (!HealthBarUI.AlreadyBoundTo(this))
            HealthBarUI.CreatePlayerHUD(this);

        BuildHitFeedback();
        BuildDeathOverlay();
    }

    void Update()
    {
        if (_dead)
        {
            // Respawn after a short delay once the respawn key is pressed.
            if (Time.time - _diedAt >= respawnDelay && Input.GetKeyDown(respawnKey))
                Respawn();
            return;
        }

        if (regenPerSecond <= 0f || _hp >= maxHealth) return;
        if (Time.time - _lastHurtTime < regenDelay) return;

        int before = _hp;
        _hp = Mathf.Min(maxHealth, _hp + Mathf.Max(1, Mathf.RoundToInt(regenPerSecond * Time.deltaTime)));
        if (_hp != before) OnHealthChanged?.Invoke(_hp, maxHealth);
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (_hp <= 0 || amount <= 0) return;
        int before = _hp;
        _hp = Mathf.Max(0, _hp - amount);
        _lastHurtTime = Time.time;

        TriggerHitFeedback(hitDirection);

        if (_hp != before)
        {
            OnHealthChanged?.Invoke(_hp, maxHealth);
            if (_hp <= 0 && before > 0) TriggerDeath();
        }
    }

    // ------------------------------------------------------------------ //
    //  Death & respawn                                                    //
    // ------------------------------------------------------------------ //

    private void TriggerDeath()
    {
        if (_dead) return;
        _dead = true;
        _diedAt = Time.time;

        FreezePlayer(true);
        ShowDeathOverlay();
        OnDeath?.Invoke();
    }

    private void Respawn()
    {
        _hp = maxHealth;
        _dead = false;

        transform.position = _spawnPos;
        transform.rotation = _spawnRot;

        UnfreezePlayer();
        HideDeathOverlay();
        OnHealthChanged?.Invoke(_hp, maxHealth);
    }

    // While dead we only lock movement (CharacterController); look, shooting and
    // inventory selection stay usable so you can keep fighting until you respawn.
    private void FreezePlayer(bool freeze)
    {
        CharacterController cc = GetComponentInChildren<CharacterController>(true);
        if (cc != null) cc.enabled = !freeze;
    }

    private void UnfreezePlayer() => FreezePlayer(false);

    /// <summary>Convenience so you can drop the player back to full health (debug/respawn).</summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int before = _hp;
        _hp = Mathf.Min(maxHealth, _hp + amount);
        if (_hp != before) OnHealthChanged?.Invoke(_hp, maxHealth);
    }

    // ------------------------------------------------------------------ //
    //  Hit feedback (red screen flash + damage direction arrow)          //
    // ------------------------------------------------------------------ //

    private void BuildHitFeedback()
    {
        Canvas canvas = HealthBarUI.FindHUDCanvas();
        if (canvas == null) return;

        // Full-screen red flash overlay.
        if (_damageFlash == null)
        {
            GameObject f = new GameObject("DamageFlash", typeof(RectTransform), typeof(Image));
            f.layer = 5;
            f.transform.SetParent(canvas.transform, false);
            RectTransform frt = f.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _damageFlash = f.GetComponent<Image>();
            _damageFlash.sprite = WhitePixel();
            _damageFlash.raycastTarget = false;
            _damageFlash.color = new Color(0.55f, 0f, 0f, 0f);
        }

        // Small direction arrow at the centre of the screen.
        if (_damageArrow == null)
        {
            GameObject a = new GameObject("DamageArrow", typeof(RectTransform), typeof(Image));
            a.layer = 5;
            a.transform.SetParent(canvas.transform, false);
            RectTransform art = a.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.5f, 0.5f);
            art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(70f, 12f);
            _damageArrow = a.GetComponent<Image>();
            _damageArrow.sprite = WhitePixel();
            _damageArrow.raycastTarget = false;
            _damageArrow.color = new Color(1f, 0.18f, 0.18f, 0f);
        }
    }

    private void TriggerHitFeedback(Vector3 hitDirection)
    {
        BuildHitFeedback();

        // Red flash.
        if (_damageFlash != null)
        {
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashFade());
        }

        // Direction arrow points toward where the damage came from (relative to the camera).
        if (_damageArrow != null)
        {
            Camera cam = Camera.main;
            if (cam != null && hitDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = hitDirection.normalized;
                float angle = Mathf.Atan2(Vector3.Dot(dir, cam.transform.right),
                                          Vector3.Dot(dir, cam.transform.forward)) * Mathf.Rad2Deg;
                _damageArrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            if (_arrowRoutine != null) StopCoroutine(_arrowRoutine);
            _arrowRoutine = StartCoroutine(ArrowFade());
        }
    }

    private IEnumerator FlashFade()
    {
        if (_damageFlash == null) yield break;
        Color c = _damageFlash.color;
        c.a = hitFlashIntensity;
        _damageFlash.color = c;

        float t = 0f;
        const float duration = 0.3f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = hitFlashIntensity * (1f - Mathf.Clamp01(t / duration));
            _damageFlash.color = c;
            yield return null;
        }
        c.a = 0f;
        _damageFlash.color = c;
        _flashRoutine = null;
    }

    private IEnumerator ArrowFade()
    {
        if (_damageArrow == null) yield break;
        Color c = _damageArrow.color;
        c.a = 0.9f;
        _damageArrow.color = c;

        float t = 0f;
        while (t < directionArrowTime)
        {
            t += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(t / directionArrowTime);
            c.a = 0.9f * fade;
            _damageArrow.color = c;
            yield return null;
        }
        c.a = 0f;
        _damageArrow.color = c;
        _arrowRoutine = null;
    }

    private static Sprite _white;
    private static Sprite WhitePixel()
    {
        if (_white != null) return _white;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false);
        _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        _white.hideFlags = HideFlags.HideAndDontSave;
        return _white;
    }

    // ------------------------------------------------------------------ //
    //  Death screen                                                       //
    // ------------------------------------------------------------------ //

    private void BuildDeathOverlay()
    {
        Canvas canvas = HealthBarUI.FindHUDCanvas();
        if (canvas == null) return;

        if (_deathPanel == null)
        {
            _deathPanel = new GameObject("DeathOverlay", typeof(RectTransform), typeof(CanvasGroup));
            _deathPanel.layer = 5;
            _deathPanel.transform.SetParent(canvas.transform, false);

            RectTransform prt = _deathPanel.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            // Dark full-screen dim.
            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.layer = 5;
            dim.transform.SetParent(_deathPanel.transform, false);
            RectTransform drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            _deathDim = dim.GetComponent<Image>();
            _deathDim.sprite = WhitePixel();
            _deathDim.color = new Color(0f, 0f, 0f, 0f);
            _deathDim.raycastTarget = false;

            // "YOU DIED" label.
            GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lbl.layer = 5;
            lbl.transform.SetParent(_deathPanel.transform, false);
            RectTransform lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(700f, 120f);
            _deathLabel = lbl.GetComponent<Text>();
            _deathLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _deathLabel.fontSize = 60;
            _deathLabel.alignment = TextAnchor.MiddleCenter;
            _deathLabel.color = new Color(0.85f, 0.12f, 0.12f, 1f);
            _deathLabel.raycastTarget = false;
        }

        _deathPanel.SetActive(false);
    }

    private void ShowDeathOverlay()
    {
        if (_deathDim != null)
        {
            Color c = _deathDim.color;
            c.a = 0.45f;
            _deathDim.color = c;
        }
        if (_deathLabel != null)
            _deathLabel.text = "YOU DIED\nPress " + respawnKey + " to respawn";
        if (_deathPanel != null) _deathPanel.SetActive(true);
    }

    private void HideDeathOverlay()
    {
        if (_deathDim != null)
        {
            Color c = _deathDim.color;
            c.a = 0f;
            _deathDim.color = c;
        }
        if (_deathPanel != null) _deathPanel.SetActive(false);
    }
}