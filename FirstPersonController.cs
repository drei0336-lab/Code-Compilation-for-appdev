using UnityEngine;

public class FirstPersonController : MonoBehaviour
{ 
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSpeed = 10f;
    public float movementSmoothTime = 0.1f;

    [Header("Head Bob")]
    public float bobSpeed = 10f;
    public float bobAmount = 0.1f;

    [Header("Sprint FOV")]
    public float normalFov = 60f;
    public float sprintFov = 75f;
    public float fovSmoothTime = 0.2f;

    // Internal Variables
    private CharacterController controller;
    private Camera playerCam;
    private Animator animator;
    private Vector3 moveDirection;
    private Vector3 smoothMoveVelocity;
    private float currentFov;
    private float bobTimer;
    private float baseCamY;
    private float fovVelocity;
    
    // Mobile Controls
    public FixedJoystick mobileJoystick;
    public bool isTouchingSprintButton = false;

        // Touch Camera Controls
    private Vector2 touchStartPos;
    private Vector2 touchDelta;
    public float touchSensitivity = 0.1f;
    public bool useTouchControls = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        playerCam = Camera.main;
        
        if (playerCam != null)
        {
            currentFov = playerCam.fieldOfView;
            baseCamY = playerCam.transform.localPosition.y;
        }
       
    }

    void Update()
    {
        HandleMovement();
        HandleMouseLook();
        HandleHeadBob();
        HandleSprintFOV();
    }

    void HandleMovement()
    {
        // Get Input from Joystick or Keyboard
        float h = mobileJoystick != null ? mobileJoystick.Horizontal : Input.GetAxisRaw("Horizontal");
        float v = mobileJoystick != null ? mobileJoystick.Vertical : Input.GetAxisRaw("Vertical");

        // 2. Create movement direction (relative to where the player faces)
        Vector3 direction = transform.right * h + transform.forward * v;
        moveDirection = direction;

        // Calculate speed BEFORE adding gravity!
        float speed = new Vector2(h, v).magnitude;

        // 3. Apply Gravity (Simple version to keep you on the floor)
        if (controller.isGrounded)
        {
            direction.y = -0.1f; // Push down slightly to stay grounded
        }
        else
        {
            direction.y += Physics.gravity.y * Time.deltaTime; // Fall if in air
        }

        // 4. Determine Speed
        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || isTouchingSprintButton)
        {
            currentSpeed = sprintSpeed;
        }

        // 5. THE MAGIC LINE: Actually move the Character Controller
        controller.Move(direction * currentSpeed * Time.deltaTime);

        // Update Animator
        if (animator != null)
        {
            if (isTouchingSprintButton || Input.GetKey(KeyCode.LeftShift))
            {
                speed = speed > 0.1f ? 2f : 0f;
            }
            animator.SetFloat("Speed", speed);
        }
    }

        void HandleMouseLook()
    {
        float mouseX = 0;
        float mouseY = 0;

        // Check if we're using touch (mobile)
        if (useTouchControls || Application.isMobilePlatform)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                
                // Only use touch if NOT touching the joystick area (left side)
                if (touch.position.x > Screen.width * 0.3f) // Right 70% of screen
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        touchStartPos = touch.position;
                    }
                    else if (touch.phase == TouchPhase.Moved)
                    {
                        touchDelta = touch.position - touchStartPos;
                        mouseX = touchDelta.x * touchSensitivity;
                        mouseY = touchDelta.y * touchSensitivity;
                        touchStartPos = touch.position;
                    }
                }
            }
        }
        else
        {
            // Desktop mouse controls
            mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;
        }

        // Rotate the character body (Y-axis)
        transform.Rotate(Vector3.up * mouseX);

        // Rotate the camera up/down (X-axis)
        if (playerCam != null)
        {
            Vector3 currentCamRot = playerCam.transform.localRotation.eulerAngles;
            float newRotX = currentCamRot.x - mouseY;
            
            if (newRotX > 180) newRotX -= 360;
            newRotX = Mathf.Clamp(newRotX, -90f, 90f);
            
            playerCam.transform.localRotation = Quaternion.Euler(newRotX, currentCamRot.y, currentCamRot.z);
        }
    }

    void HandleHeadBob()
    {
        if (playerCam == null) return;
        
        if (controller.isGrounded && moveDirection.magnitude > 0.1f) 
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float newY = baseCamY + Mathf.Sin(bobTimer) * bobAmount;
            playerCam.transform.localPosition = new Vector3(0, newY, 0);
        }
        else
        {
            playerCam.transform.localPosition = Vector3.Lerp(
                playerCam.transform.localPosition, 
                new Vector3(0, baseCamY, 0), 
                Time.deltaTime * 10f
            );
            bobTimer = 0;
        }
    }

    void HandleSprintFOV()
    {
        if (playerCam == null) return;

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || isTouchingSprintButton;
        float targetFov = isSprinting ? sprintFov : normalFov;
        
        playerCam.fieldOfView = Mathf.SmoothDamp(
            playerCam.fieldOfView, 
            targetFov, 
            ref fovVelocity, 
            fovSmoothTime
        );
    }
}
        
    
        


    