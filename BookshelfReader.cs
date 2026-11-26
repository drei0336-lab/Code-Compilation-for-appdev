using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ARCHIV3D
{
    /// <summary>
    /// Put this on a bookshelf. Like the computers, it plugs into your Interactable / PlayerInteractor:
    /// walk up to the shelf and tap it (Android) or click it (desktop) to open its book.
    /// With a Year Bookshelf on the same object it lists that year's subjects, then their books;
    /// otherwise the pages come from the shelf's own book component (Year1Book, Year2Book, ...).
    /// While the book is open the player can't move or look around, and the mouse cursor is freed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactable))]
    [AddComponentMenu("ARCHIV3D/Bookshelf Reader")]
    public class BookshelfReader : MonoBehaviour
    {
        [Header("Shelf")]
        [Tooltip("Shown on the book's first page and when the shelf has more than one book.")]
        public string shelfName = "Bookshelf";

        [Tooltip("Optional. Leave empty to use the ARCHIV3D Book Screen in the scene.")]
        public BookScreen bookScreen;

        [Header("Player while the book is open")]
        [Tooltip("Scripts to switch off while the book is open. Leave empty to switch off " +
                 "FirstPersonController and PlayerInteractor automatically.")]
        public List<Behaviour> freezeWhileOpen = new List<Behaviour>();

        [Tooltip("Unlock and show the mouse cursor while the book is open (desktop).")]
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

        public bool IsBookOpen => bookScreen != null && bookScreen.IsOpen;

        void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.onInteract.AddListener(Open);
        }

        void OnDestroy()
        {
            if (_interactable != null) _interactable.onInteract.RemoveListener(Open);
        }

        /// <summary>Opens this shelf's book. Called by Interactable; you can also hook it to a UI Button.</summary>
        [ContextMenu("Open Book (Play mode)")]
        public void Open()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (Time.unscaledTime < _blockedUntil) return; // ignore the tap that just closed the book

            if (bookScreen == null) bookScreen = BookScreen.GetOrCreate();
            if (bookScreen.IsOpen) return;

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

            var year = GetComponent<YearBookshelf>();
            if (year != null)
            {
                // ERD: Bookshelf (year area) -> Shelf (semester) -> Subject -> Learning Materials.
                var loose = new List<LearningMaterial>();
                foreach (var b in GetComponents<ShelfBook>()) loose.Add(b.ToMaterial());
                string title = string.IsNullOrWhiteSpace(year.areaName) ? shelfName : year.areaName;
                bookScreen.OpenShelf(title, year.ShelvesToShow, loose, HandleClosed);
            }
            else bookScreen.Open(shelfName, GetComponents<ShelfBook>(), HandleClosed);
            onOpened.Invoke();
        }

        void HandleClosed()
        {
            _blockedUntil = Time.unscaledTime + 0.3f;
            // Give control back one frame later so the tap on "Close" isn't read as a tap on the shelf.
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
                var player = Object.FindAnyObjectByType<FirstPersonController>();
                if (player != null)
                {
                    targets.Add(player);
                    StopWalkAnimation(player);
                }
                var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
                if (interactor != null) targets.Add(interactor);
            }

            foreach (var b in targets)
            {
                if (!b.enabled) continue;
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
    }
}
