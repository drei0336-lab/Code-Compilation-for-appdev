using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ARCHIV3D
{
    /// <summary>
    /// A character the player can talk to. It plugs into your Interactable / PlayerInteractor like the
    /// computers and bookshelves: look at the NPC until the hand button says "Talk to ...", then tap it
    /// (or tap / click the NPC). The lines below show one at a time in the Dialogue Screen at the
    /// bottom-center of the screen. While talking, the player can't move and the NPC turns to face them.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Interactable))]
    [AddComponentMenu("ARCHIV3D/NPC Guide")]
    public class NPCGuide : MonoBehaviour
    {
        [Header("Who")]
        public string npcName = "Filipe";
        [Tooltip("Small text next to the name. Leave empty for none.")]
        public string npcTitle = "Library Guide";

        [Header("What they say (one box per line, in order)")]
        [TextArea(2, 6)]
        [Tooltip("The first line is the greeting. You can use <b>bold</b>, <i>italic</i> and <color=#F0BE54>gold</color>.")]
        public List<string> lines = new List<string>();

        [Header("Facing the player")]
        public bool facePlayerWhileTalking = true;
        [Tooltip("Turn back to where they were facing after the talk.")]
        public bool turnBackAfterTalking = true;
        public float turnSpeed = 6f;

        [Header("Screen")]
        [Tooltip("Optional. Leave empty to use the ARCHIV3D Dialogue Screen in the scene.")]
        public DialogueScreen dialogueScreen;

        [Header("Player while talking")]
        [Tooltip("Scripts to switch off while talking. Leave empty to switch off " +
                 "FirstPersonController and PlayerInteractor automatically.")]
        public List<Behaviour> freezeWhileTalking = new List<Behaviour>();
        [Tooltip("Unlock and show the mouse cursor while talking (desktop).")]
        public bool freeCursorWhileTalking = true;

        [Header("Events")]
        public UnityEvent onTalkStarted = new UnityEvent();
        public UnityEvent onTalkFinished = new UnityEvent();

        public bool IsTalking { get; private set; }

        Interactable _interactable;
        NPCIdleLife _life;
        Quaternion _homeRotation;
        Transform _player;
        readonly List<Behaviour> _frozen = new List<Behaviour>();
        CursorLockMode _previousLock;
        bool _previousCursorVisible;
        float _blockedUntil;

        public static readonly string[] DefaultLines =
        {
            "Hello, Welcome to ARCHIV3D, my name is Filipe.",
            "I'm the guide of this library. <color=#F0BE54><b>ARCHIV3D</b></color> is a 3D virtual library where you can explore, read books and test what you've learned.",
            "To <color=#F0BE54><b>move</b></color>, drag the joystick on the left. To <color=#F0BE54><b>look around</b></color>, drag on the right side of the screen. Hold <color=#F0BE54><b>RUN</b></color> to go faster.\n<size=85%><color=#FFF6E0AA>On a computer: WASD to move, the mouse to look, Shift to run.</color></size>",
            "When you look at something you can use, the gold <color=#F0BE54><b>hand button</b></color> appears and tells you what it does, like \"Read Book\" or \"Use Computer\". Tap it, or tap the object itself.",
            "The <color=#F0BE54><b>bookshelves</b></color> are grouped by year: the 1st, 2nd and 3rd Year Sections. Open a book and turn its pages with the arrows or by swiping.",
            "The <color=#F0BE54><b>black computers</b></color> have quizzes about what you've read. Answer the questions, read the explanations, and try to beat your best score!",
            "You can change the volume, look sensitivity and graphics in <color=#F0BE54><b>Settings</b></color> on the title screen.",
            "That's all for now. Enjoy exploring ARCHIV3D, and come talk to me again whenever you need help!",
        };

        void Reset()
        {
            lines = new List<string>(DefaultLines);
            var it = GetComponent<Interactable>();
            if (it != null) it.prompt = "Talk to " + npcName;
        }

        void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _interactable.onInteract.AddListener(Talk);
            _life = GetComponentInChildren<NPCIdleLife>(true);
            _homeRotation = transform.rotation;
        }

        void OnDestroy()
        {
            if (_interactable != null) _interactable.onInteract.RemoveListener(Talk);
        }

        /// <summary>Starts the conversation. Called by Interactable; you can also hook it to a UI Button.</summary>
        [ContextMenu("Talk (Play mode)")]
        public void Talk()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (Time.unscaledTime < _blockedUntil) return; // ignore the tap that just closed the box

            if (dialogueScreen == null) dialogueScreen = DialogueScreen.GetOrCreate();
            if (dialogueScreen.IsOpen) return;

            IsTalking = true;
            FreezePlayer();
            dialogueScreen.Open(npcName, npcTitle, lines.Count > 0 ? lines : new List<string>(DefaultLines), HandleClosed);
            onTalkStarted.Invoke();
        }

        void HandleClosed()
        {
            IsTalking = false;
            if (_life != null) _life.Talking = false;
            _blockedUntil = Time.unscaledTime + 0.3f;
            // Give control back one frame later so the tap on "Close" isn't read as a tap on the NPC.
            if (isActiveAndEnabled) StartCoroutine(UnfreezeNextFrame());
            else UnfreezePlayer();
            onTalkFinished.Invoke();
        }

        IEnumerator UnfreezeNextFrame()
        {
            yield return null;
            UnfreezePlayer();
        }

        void Update()
        {
            if (_life != null) _life.Talking = IsTalking && dialogueScreen != null && dialogueScreen.IsTyping;

            Quaternion target = _homeRotation;
            bool turn = false;
            if (IsTalking && facePlayerWhileTalking)
            {
                if (_player == null) _player = FindPlayer();
                if (_player != null)
                {
                    Vector3 to = _player.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                    {
                        target = Quaternion.LookRotation(to.normalized, Vector3.up);
                        turn = true;
                    }
                }
            }
            else if (turnBackAfterTalking) turn = true;

            if (turn && Quaternion.Angle(transform.rotation, target) > 0.5f)
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        static Transform FindPlayer()
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform;
            var p = Object.FindAnyObjectByType<FirstPersonController>();
            return p != null ? p.transform : null;
        }

        void FreezePlayer()
        {
            _frozen.Clear();
            var targets = new List<Behaviour>();
            foreach (var b in freezeWhileTalking)
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

            if (freeCursorWhileTalking)
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

            if (freeCursorWhileTalking)
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
