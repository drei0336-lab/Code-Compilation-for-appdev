using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private GameObject interactButton;   // on-screen button
    [SerializeField] private TMP_Text promptText;         // optional label
    [SerializeField] private bool allowTapOnObject = true;
    [SerializeField] private float maxTapMove = 20f;      // pixels, so camera drags don't count as taps

    private Interactable current;
    private Vector2 pressStart;

    void Update()
    {
        // 1. Look-based detection (drives the on-screen button)
        current = null;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            current = hit.collider.GetComponentInParent<Interactable>();

        if (interactButton != null) interactButton.SetActive(current != null);
        if (promptText != null) promptText.text = current != null ? current.prompt : "";

        // 2. Optional: tap directly on an object
        if (allowTapOnObject) HandleTap();
    }

    void HandleTap()
    {
#if ENABLE_INPUT_SYSTEM
        Pointer p = Pointer.current;
        if (p == null) return;

        if (p.press.wasPressedThisFrame)
            pressStart = p.position.ReadValue();

        if (p.press.wasReleasedThisFrame)
        {
            Vector2 end = p.position.ReadValue();
            if (Vector2.Distance(pressStart, end) > maxTapMove) return;      // it was a drag
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // tapped UI

            Ray tapRay = playerCamera.ScreenPointToRay(end);
            if (Physics.Raycast(tapRay, out RaycastHit tapHit, interactRange))
            {
                Interactable target = tapHit.collider.GetComponentInParent<Interactable>();
                if (target != null) target.Interact();
            }
        }
#endif
    }

    // Hook this to the on-screen Interact button's On Click
    public void Interact()
    {
        if (current != null) current.Interact();
    }
}