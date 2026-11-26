using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Interactable))]
public class Book1 : MonoBehaviour
{
    [SerializeField] private string sceneName = "Book1";   // exact scene name

    // Remembers where the player was, so you can come back to the same spot
    public static Vector3 returnPosition;
    public static Quaternion returnRotation;
    public static bool hasReturnPoint;

    void Awake()
    {
        GetComponent<Interactable>().onInteract.AddListener(Open);
    }

    public void Open()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null)
        {
            returnPosition = player.transform.position;
            returnRotation = player.transform.rotation;
            hasReturnPoint = true;
        }
        SceneManager.LoadScene(sceneName);
    }
}