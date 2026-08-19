using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Header("Sneak (Crouch)")]
		[Tooltip("Move speed while sneaking (crouching), in m/s.")]
		public float SneakSpeed = 2.0f;
		[Tooltip("Key used to sneak / crouch.")]
		public KeyCode sneakKey = KeyCode.LeftControl;
		[Tooltip("If true, press the key once to toggle sneak on/off. If false, hold the key to sneak.")]
		public bool sneakToggle = false;
		[Tooltip("How much the camera and capsule lower while sneaking (metres).")]
		public float CrouchHeightReduction = 0.4f;
		[Tooltip("How fast the camera/capsule blends in and out of the sneak pose.")]
		public float CrouchBlendSpeed = 10f;

		[Header("Sprint Stamina (Cooldown)")]
		[Tooltip("Total seconds you can sprint before the stamina bar empties and sprint goes on cooldown.")]
		public float MaxStamina = 5f;
		[Tooltip("Total cooldown in seconds: from a fully-drained bar until you can sprint again. " +
		         "The stamina recovery rate is auto-calculated to match this.")]
		public float SprintCooldown = 7f;
		[Tooltip("Delay after sprinting stops before stamina starts regenerating.")]
		public float StaminaRegenDelay = 1f;
		[Tooltip("Minimum stamina (as a fraction of MaxStamina) needed before you can sprint again. " +
		         "Higher = you must wait longer before sprinting again.")]
		[Range(0f, 1f)] public float MinStaminaToSprint = 0.25f;
		[Tooltip("Show the on-screen sprint stamina / cooldown bar.")]
		public bool showStaminaBar = true;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// sneak / sprint-stamina state
		private bool _sneakHeld;
		private float _crouchBlend;
		private float _stamina;
		private float _staminaRegenDelayTimer;
		private float _regenRate;
		private bool _sprinting;
		private float _controllerBaseHeight;
		private float _controllerBaseCenterY;
		private Vector3 _camTargetBasePos;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;

			// remember our base sizes/positions so sneak can lower them cleanly
			_stamina = MaxStamina;
			_controllerBaseHeight = _controller.height;
			_controllerBaseCenterY = _controller.center.y;
			if (CinemachineCameraTarget != null) _camTargetBasePos = CinemachineCameraTarget.transform.localPosition;

			RecalculateRegenRate();

			// build the on-screen sprint stamina / cooldown bar
			if (showStaminaBar) SprintStaminaUI.Ensure(this);
		}

		// Derive the per-second recovery rate so that a fully-drained bar reaches the
		// "can sprint again" threshold exactly after SprintCooldown seconds.
		private void RecalculateRegenRate()
		{
			float regenWindow = Mathf.Max(0.01f, SprintCooldown - StaminaRegenDelay);
			_regenRate = (MinStaminaToSprint * MaxStamina) / regenWindow;
		}

		// Keep the recovery rate in sync while editing in the Inspector.
		private void OnValidate()
		{
			RecalculateRegenRate();
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			UpdateSneak();
			UpdateStamina();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// Sneaking overrides sprint; sprint needs stamina remaining (cooldown).
			bool sneaking = _sneakHeld;
			bool sprintRequested = _input.sprint && !sneaking && CanSprint();

			float targetSpeed;
			if (sneaking) targetSpeed = SneakSpeed;
			else if (sprintRequested) targetSpeed = SprintSpeed;
			else targetSpeed = MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		// ------------------------------------------------------------------ //
		//  Sneak (crouch) + sprint stamina                                    //
		// ------------------------------------------------------------------ //

		/// <summary>Current sprint stamina (0..MaxStamina). Read by the HUD bar.</summary>
		public float Stamina => _stamina;
		/// <summary>Max sprint stamina.</summary>
		public float StaminaMax => MaxStamina;
		/// <summary>True while the player is actually sprinting this frame.</summary>
		public bool IsSprinting => _sprinting;
		/// <summary>True when there is enough stamina left to sprint.</summary>
		public bool SprintReady => _stamina > MinStaminaToSprint * MaxStamina;
		/// <summary>True while the player is currently sneaking (crouched).</summary>
		public bool IsSneaking => _sneakHeld;

		private void UpdateSneak()
		{
			// read the sneak key (hold to sneak, or toggle if sneakToggle is on)
			if (sneakToggle)
			{
				if (Input.GetKeyDown(sneakKey)) _sneakHeld = !_sneakHeld;
			}
			else
			{
				_sneakHeld = Input.GetKey(sneakKey);
			}

			// blend smoothly between standing and crouching
			float targetBlend = _sneakHeld ? 1f : 0f;
			_crouchBlend = Mathf.MoveTowards(_crouchBlend, targetBlend, CrouchBlendSpeed * Time.deltaTime);

			// shrink the capsule and lower the camera so it's a real crouch
			_controller.height = Mathf.Lerp(_controllerBaseHeight, _controllerBaseHeight - CrouchHeightReduction, _crouchBlend);
			Vector3 center = _controller.center;
			center.y = Mathf.Lerp(_controllerBaseCenterY, _controllerBaseCenterY - CrouchHeightReduction * 0.5f, _crouchBlend);
			_controller.center = center;

			if (CinemachineCameraTarget != null)
			{
				Vector3 p = CinemachineCameraTarget.transform.localPosition;
				p.y = Mathf.Lerp(_camTargetBasePos.y, _camTargetBasePos.y - CrouchHeightReduction, _crouchBlend);
				CinemachineCameraTarget.transform.localPosition = p;
			}
		}

		private void UpdateStamina()
		{
			bool moving = _input.move != Vector2.zero;
			bool sprintingNow = _input.sprint && CanSprint() && moving && !_sneakHeld;

			if (sprintingNow)
			{
				// drain stamina while sprinting
				_stamina = Mathf.Max(0f, _stamina - Time.deltaTime);
				_staminaRegenDelayTimer = StaminaRegenDelay;
			}
			else
			{
				// after the delay, stamina regenerates (the cooldown recovery)
				if (_staminaRegenDelayTimer > 0f)
				{
					_staminaRegenDelayTimer -= Time.deltaTime;
				}
				else if (_stamina < MaxStamina)
				{
					_stamina = Mathf.Min(MaxStamina, _stamina + _regenRate * Time.deltaTime);
				}
			}

			_sprinting = sprintingNow;
		}

		private bool CanSprint()
		{
			return _stamina > MinStaminaToSprint * MaxStamina;
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}