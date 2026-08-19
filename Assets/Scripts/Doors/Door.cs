using UnityEngine;

/// <summary>
/// Drop this component on any GameObject with a Collider to make it a door.
/// The door will open when the player presses the Interaction key (default: E)
/// and close when they move away or press the key again.
///
/// Configure the door in the Inspector:
/// - Open angle (how far it swings in degrees)
/// - Open speed (degrees per second)
/// - Whether it uses an Animator (if you have door animation clips)
/// - Audio clips for open/close
/// - Whether it's one-time only (doesn't close automatically)
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Opening")]
    [Tooltip("How far the door swings open, in degrees (e.g. 90 for a quarter-turn).")]
    public float openAngle = 90f;

    [Tooltip("How fast the door swings, in degrees per second.")]
    public float openSpeed = 120f;

    [Tooltip("If checked, the door uses its Animator to play open/close clips. " +
             "Otherwise it rotates the transform directly.")]
    public bool useAnimator = false;

    [Tooltip("Animation trigger parameter name when using an Animator (e.g. \"Open\").")]
    public string animatorTrigger = "Open";

    [Header("Audio")]
    [Tooltip("Sound played when the door opens.")]
    public AudioClip openSound;

    [Tooltip("Sound played when the door closes.")]
    public AudioClip closeSound;

    [Header("Behaviour")]
    [Tooltip("If true, the door stays open until explicitly closed (e.g. one-way door).")]
    public bool oneWay = false;

    [Tooltip("Layers that can interact with this door (usually just the player).")]
    public LayerMask interactLayers = ~0;

    // Internal state
    private bool _isOpen;

    // Cached references
    private Transform _tr;
    private Quaternion _startRot;
    private Quaternion _targetRot;
    private AudioSource _audioSource;
    private Animator _animator;

    // Cached references
    private void Awake()
    {
        _tr = transform;
        _startRot = _tr.localRotation;
        _targetRot = _startRot;

        // Try to get an Animator if requested
        if (useAnimator)
            _animator = GetComponentInChildren<Animator>(true);

        // Make sure we have an AudioSource for door sounds
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanInteract(other)) return;
        // Player entered trigger — they can press E to open
    }

    private void OnTriggerExit(Collider other)
    {
        if (!CanInteract(other)) return;

        // Player left trigger — auto-close if not one-way
        if (!oneWay && !_isOpen)
            CloseDoor();
    }

    private bool CanInteract(Collider other)
    {
        if (((1 << other.gameObject.layer) & interactLayers.value) == 0) return false;
        // Could add additional checks here (e.g., third-person controller tag)
        return true;
    }

    // Call this from PlayerInteractor or another input handler
    public void OpenDoor()
    {
        if (oneWay && _isOpen) return;

        _isOpen = true;
        _targetRot = Quaternion.Euler(
            _startRot.eulerAngles.x,
            _startRot.eulerAngles.y + openAngle,
            _startRot.eulerAngles.z);

        if (openSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(openSound);
        }

        if (useAnimator && _animator != null)
            _animator.SetTrigger(animatorTrigger);
    }

    public void CloseDoor()
    {
        _isOpen = false;
        _targetRot = _startRot;

        if (closeSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(closeSound);
        }

        if (useAnimator && _animator != null)
            _animator.SetTrigger(animatorTrigger == "Open" ? "Close" : animatorTrigger);
    }

    private void Update()
    {
        // Rotate toward the target angle each frame
        _tr.localRotation = Quaternion.Slerp(
            _tr.localRotation,
            _targetRot,
            Time.deltaTime * openSpeed * Mathf.Deg2Rad);

        // If we've reached the target and are not moving much, snap to it
        if (Quaternion.Angle(_tr.localRotation, _targetRot) < 0.1f)
        {
            _tr.localRotation = _targetRot;
        }
    }

    // Expose these publicly so PlayerInteractor can call them
    /// <summary>True if the door is currently open.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Toggle the door (open if closed, close if open).</summary>
    public void Toggle()
    {
        if (_isOpen)
            CloseDoor();
        else
            OpenDoor();
    }
}