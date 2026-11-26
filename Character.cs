using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float lookSensitivity = 0.15f;
    public Transform playerCamera;

    private CharacterController controller;
    private float verticalRotation;

    [Header("Mobile Controls")]
    public VirtualJoystick joystick;

    private Vector2 lookStart;
    private bool looking;
    private int lookFingerId = -1;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        float x = joystick != null ? joystick.Horizontal
                                  : Input.GetAxis("Horizontal");
        float z = joystick != null ? joystick.Vertical
                                  : Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.SimpleMove(move * speed);

        HandleLook();
    }

    void HandleLook()
    {
        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.position.x > Screen.width * 0.4f)
                {
                    if (touch.phase == TouchPhase.Began && !looking)
                    {
                        looking = true;
                        lookFingerId = touch.fingerId;
                        lookStart = touch.position;
                    }
                    else if (looking && touch.fingerId == lookFingerId)
                    {
                        if (touch.phase == TouchPhase.Moved)
                        {
                            ApplyLook(touch.deltaPosition);
                        }

                        if (touch.phase == TouchPhase.Ended ||
                            touch.phase == TouchPhase.Canceled)
                        {
                            looking = false;
                            lookFingerId = -1;
                        }
                    }
                }
            }
        }
        else
        {
            looking = false;
            lookFingerId = -1;

            if (Input.GetMouseButton(0))
                ApplyLook(new Vector2(Input.GetAxis("Mouse X") * 10f,
                                      Input.GetAxis("Mouse Y") * 10f));
        }
    }

    void ApplyLook(Vector2 delta)
    {
        transform.Rotate(Vector3.up * delta.x * lookSensitivity);

        verticalRotation -= delta.y * lookSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -80f, 80f);

        if (playerCamera != null)
            playerCamera.localRotation =
                Quaternion.Euler(verticalRotation, 0f, 0f);
    }
}