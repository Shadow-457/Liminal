using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu / pause manager. Builds a black main menu with PLAY GAME and SETTINGS at
/// runtime (no assets needed), lets you change mouse sensitivity, freezes the game while
/// the menu is open, and provides a global "IsPlaying" gate so gameplay scripts stop.
///
/// The static IsPlaying / MouseSensitivity are read by the Gun, Player, Enemy and health
/// scripts so they automatically freeze while in the menu or dead.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>True while the player is actually alive and in gameplay (not menu / dead).</summary>
    public static bool IsPlaying { get; private set; } = false;

    /// <summary>0.1–10 mouse sensitivity multiplier (1 = default).</summary>
    public static float MouseSensitivity { get; private set; } = 1f;

    private Canvas _canvas;
    private GameObject _menuRoot;      // background + anything menu related
    private GameObject _mainButtons;   // title / play / settings
    private GameObject _settingsPanel;   // sensitivity slider + back
    private Slider _sensSlider;
    private Text _sensLabel;

    private Object _lookTarget;         // the active FirstPersonController
    private FieldInfo _rotField;        // RotationSpeed or mouseSensitivity field
    private float _baseRot = 1f;

    /// <summary>Make sure a GameManager exists (called by Helper scripts).</summary>
    public static GameManager Ensure()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("GameManager");
        return go.AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        BuildUI();
        OpenMainMenu();
    }

    void Update()
    {
        // ESC toggles between the game and the pause menu.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_menuBtns != null && _menuBtns.activeSelf && !_settingsPanel.activeSelf)
                StartGame();          // resume from menu
            else if (IsPlaying)
                OpenMainMenu();        // pause to menu
        }
    }

    public static void SetPlaying(bool on)
    {
        IsPlaying = on;
        Time.timeScale = on ? 1f : 0f;
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }

    public static void SetSensitivity(float v)
    {
        MouseSensitivity = Mathf.Clamp(v, 0.1f, 10f);
        if (Instance != null) Instance.ApplySensitivity();
// ------------------------------------------------------------------ //
    //  Public menu actions                                              //
    // ------------------------------------------------------------------ //

    public void OpenMainMenu()
    {
        SetPlaying(false);
        if (_mainButtons != null) _mainButtons.SetActive(true);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
    }

    public void StartGame()
    {
        if (_mainButtons != null) _mainButtons.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        SetPlaying(true);
    }

    public void ShowSettings()
    {
        if (_mainButtons != null) _mainButtons.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(true);
        if (_sensSlider != null) _sensSlider.value = MouseSensitivity;
    }

    public void BackFromSettings()
    {
        OpenMainMenu();
    }

    // ------------------------------------------------------------------ //
    //  Mouse sensitivity                                                 //
    // ------------------------------------------------------------------ //

    // Find the active FirstPersonController and remember its look field so we can
    // scale it. Works for both StarterAssets (RotationSpeed) and Modular (mouseSensitivity).
    private void FindLookController()
    {
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            string t = mb.GetType().Name;
            if (!t.Contains("FirstPersonController")) continue;

            FieldInfo f = mb.GetType().GetField("RotationSpeed");
            if (f == null) f = mb.GetType().GetField("mouseSensitivity");
            if (f == null) f = mb.GetType().GetField("rotationSpeed");
            if (f != null)
            {
                _lookTarget = mb;
                _rotField = f;
                object val = f.GetValue(mb);
                _baseRot = val is float fl ? fl : 1f;
                if (_baseRot <= 0.001f) _baseRot = 1f;
                return;
            }
        }
        _lookTarget = null;
        _rotField = null;
    }

    private void ApplySensitivity()
    {
        if (_lookTarget == null || _rotField == null) FindLookController();
        if (_rotField != null && _lookTarget != null)
            _rotField.SetValue(_lookTarget, _baseRot * MouseSensitivity);
    }
    }