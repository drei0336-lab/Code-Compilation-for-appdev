using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public string prompt = "Use Computer";
    public UnityEvent onInteract;

    public void Interact()
    {
        onInteract.Invoke();
    }
}