using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BlackCompuQuiz
{
    /// <summary>
    /// Put this on a Blackcompu computer. It plugs into your existing Interactable / PlayerInteractor:
    /// walk up to the computer and tap it (Android) or click it (desktop) to open the quiz screen.
    /// The questions come from this computer's own quiz component (BlackCompu1Quiz, BlackCompu2Quiz, ...).
    /// While the screen is open the player can't move or look around, and the mouse cursor is freed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactable))]
    [AddComponentMenu("BlackCompu/BlackCompu Computer")]
    public class BlackCompuComputer : MonoBehaviour
    {
        [Header("Screen")]
        [Tooltip("Name shown at the top of the computer screen.")]
        public string screenTitle = "BlackCompu";

        [Tooltip("Optional. Leave empty and the quiz screen is made automatically. " +
                 "Add a Quiz Screen component to any object if you want to change its colors or font.")]
        public QuizScreen quizScreen;

        [Header("Player while the screen is open")]
        [Tooltip("Scripts to switch off while the computer is open. Leave empty to switch off " +
                 "FirstPersonController and PlayerInteractor automatically.")]
        public List<Behaviour> freezeWhileOpen = new List<Behaviour>();

        [Tooltip("Unlock and show the mouse cursor while the screen is open (desktop).")]
        public bool freeCursorWhileOpen = true;

        [Header("Sound (optional)")]
        public AudioClip openSound;

        [Header("Events")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();

        Interactable _interactable;
        readonly List<Behaviour> _frozen = new List<Behaviour>();
        CursorLockMode _previousLock;
        bool _previousCursorVisible;
        float _blockedUntil;
        AudioSource _audio;

        public bool IsScreenOpen => quizScreen != null && quizScreen.IsOpen;

        void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.onInteract.AddListener(Open);
            EnsureCollider();
        }

        void OnDestroy()
        {
            if (_interactable != null) _interactable.onInteract.RemoveListener(Open);
        }

        /// <summary>
        /// Opens the computer screen. Called by Interactable; you can also hook it to a UI Button.
        /// In Play mode you can also test it from this component's ⋮ menu in the Inspector.
        /// </summary>
        [ContextMenu("Open Quiz Screen (Play mode)")]
        public void Open()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (Time.unscaledTime < _blockedUntil) return; // ignore the tap that just closed the screen

            if (quizScreen == null) quizScreen = QuizScreen.GetOrCreate();
            if (quizScreen.IsOpen) return;

            FreezePlayer();
            if (openSound != null)
            {
                if (_audio == null)
                {
                    _audio = gameObject.AddComponent<AudioSource>();
                    _audio.playOnAwake = false;
                    _audio.spatialBlend = 0f;
                }
                _audio.PlayOneShot(openSound);
            }

            quizScreen.Open(screenTitle, GetComponents<ComputerQuiz>(), HandleClosed);
            onOpened.Invoke();
        }

        void HandleClosed()
        {
            _blockedUntil = Time.unscaledTime + 0.3f;

            // Give control back one frame later, so the tap that pressed "Close"
            // isn't also read by PlayerInteractor as a tap on the computer.
            if (isActiveAndEnabled) StartCoroutine(UnfreezeNextFrame());
            else UnfreezePlayer();

            onClosed.Invoke();
        }

        IEnumerator UnfreezeNextFrame()
        {
            yield return null;
            UnfreezePlayer();
        }

        void FreezePlayer()
        {
            _frozen.Clear();

            var targets = new List<Behaviour>();
            foreach (var b in freezeWhileOpen)
                if (b != null) targets.Add(b);

            if (targets.Count == 0)
            {
                var player = BlackCompuUtil.FindAny<FirstPersonController>();
                if (player != null)
                {
                    targets.Add(player);
                    StopWalkAnimation(player);
                }
                var interactor = BlackCompuUtil.FindAny<PlayerInteractor>();
                if (interactor != null) targets.Add(interactor);
            }

            foreach (var b in targets)
            {
                if (!b.enabled) continue; // e.g. still switched off by the Start screen
                b.enabled = false;
                _frozen.Add(b);
            }

            if (freeCursorWhileOpen)
            {
                _previousLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void UnfreezePlayer()
        {
            foreach (var b in _frozen)
                if (b != null) b.enabled = true;
            _frozen.Clear();

            if (freeCursorWhileOpen)
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        static void StopWalkAnimation(Component player)
        {
            var animator = player.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat("Speed", 0f);
                    return;
                }
            }
        }

        /// <summary>PlayerInteractor finds computers with a raycast, so the computer needs a collider.</summary>
        public void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>() != null) return;
            FitBoxCollider(gameObject.AddComponent<BoxCollider>());
        }

        /// <summary>Sizes a box collider to wrap every mesh of this computer.</summary>
        public void FitBoxCollider(BoxCollider box)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return; // keeps the default 1x1x1 box

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? world.min.x : world.max.x,
                    (i & 2) == 0 ? world.min.y : world.max.y,
                    (i & 4) == 0 ? world.min.z : world.max.z);
                Vector3 local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            Vector3 size = max - min;
            size.x = Mathf.Max(size.x, 0.01f);
            size.y = Mathf.Max(size.y, 0.01f);
            size.z = Mathf.Max(size.z, 0.01f);
            box.center = (min + max) * 0.5f;
            box.size = size;
        }
    }

    /// <summary>Small helpers.</summary>
    public static class BlackCompuUtil
    {
        public static T FindAny<T>() where T : Object
        {
            return Object.FindAnyObjectByType<T>();
        }
    }
}
