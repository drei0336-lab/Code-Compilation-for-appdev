using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARCHIV3D
{
    /// <summary>
    /// The in-game pause menu, laid out like the title screen: logo, splash line, Resume / How to Play /
    /// Settings / Main Menu, the same pop-up panels, and a fade + loading bar back to the title screen.
    /// Opens with the pause button (top-right of the game controls), Esc on a computer, or the Android back
    /// button, but never while a book, computer, dialogue or the name screen is open (Esc belongs to those).
    /// While paused the game stops (time, sound, the player and the controls).
    /// Built by Tools > ARCHIV3D > Build Pause Menu (Main Map); every reference below can be changed in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/Pause Menu")]
    public class PauseMenu : MonoBehaviour
    {
        [Header("Main menu")]
        [Tooltip("Scene the Main Menu button goes back to. It must be in File > Build Profiles / Build Settings.")]
        public string mainMenuScene = "TITLESCREEN";

        [Header("Opening")]
        [Tooltip("The pause button on the game controls.")]
        public Button pauseButton;
        [Tooltip("Esc (computer) and the Android back button open and close the pause menu.")]
        public bool backButtonOpens = true;
        [Tooltip("On a phone, pause when the game goes to the background (call, home button).")]
        public bool pauseWhenAppHidden = true;

        [Header("Menu")]
        [Tooltip("Everything on the main pause screen (backdrop, logo, buttons, footer).")]
        public CanvasGroup menuGroup;
        public Button resumeButton;
        public Button howToPlayButton;
        public Button settingsButton;
        public Button mainMenuButton;

        [Header("Panels")]
        public CanvasGroup howToPlayPanel;
        public CanvasGroup settingsPanel;
        public Button[] backButtons;

        [Header("Settings")]
        public Slider volumeSlider;
        public TMP_Text volumeValue;
        public Slider lookSlider;
        public TMP_Text lookValue;
        [Tooltip("0 = Auto, 1 = Low, 2 = Medium, 3 = High (see DevicePerformance).")]
        public Slider graphicsSlider;
        public TMP_Text graphicsValue;

        [Header("Fade and loading")]
        public CanvasGroup fader;
        public CanvasGroup loadingGroup;
        public RectTransform loadingFill;
        public float fadeSeconds = 0.25f;

        [Header("Footer")]
        public TMP_Text versionText;
        public string versionPrefix = "ARCHIV3D v";
        [Tooltip("Shows the character's name, e.g. \"Playing as Ralph\".")]
        public TMP_Text playerText;
        public string playerPrefix = "Playing as ";

        /// <summary>True while the game is paused.</summary>
        public static bool IsPaused { get; private set; }

        /// <summary>Raised when the game pauses (true) or resumes (false).</summary>
        public static event Action<bool> PausedChanged;

        CanvasGroup _openPanel;
        bool _busy;              // fading in/out or leaving for the main menu
        bool _leaving;
        float _timeScaleBefore = 1f;
        bool _overlayLastFrame;
        float _appliedLook = -1f;

        // other screens that own Esc while they're open
        BookScreen _book;
        DialogueScreen _dialogue;
        BlackCompuQuiz.QuizScreen _quiz;
        float _nextQuizSearch;

        // player and controls while paused
        readonly List<Behaviour> _frozen = new List<Behaviour>();
        CursorLockMode _previousLock;
        bool _previousCursorVisible;
        CanvasGroup _hud;
        float _hudAlpha;
        bool _hudRaycasts, _hudInteractable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsPaused = false;
            PausedChanged = null;
        }

        void Awake()
        {
            if (pauseButton) pauseButton.onClick.AddListener(Pause);
            if (resumeButton) resumeButton.onClick.AddListener(Resume);
            if (howToPlayButton) howToPlayButton.onClick.AddListener(() => OpenPanel(howToPlayPanel));
            if (settingsButton) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            if (mainMenuButton) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (backButtons != null)
                foreach (var b in backButtons)
                    if (b) b.onClick.AddListener(ClosePanel);

            if (volumeSlider)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; RefreshLabels(); });
            }
            if (lookSlider)
            {
                lookSlider.minValue = GameSettings.MinLook;
                lookSlider.maxValue = GameSettings.MaxLook;
                lookSlider.onValueChanged.AddListener(v => { GameSettings.LookSensitivity = v; ApplyLook(v); RefreshLabels(); });
            }
            if (graphicsSlider)
            {
                graphicsSlider.minValue = 0f;
                graphicsSlider.maxValue = 3f;
                graphicsSlider.wholeNumbers = true;
                graphicsSlider.onValueChanged.AddListener(v => { GameSettings.Graphics = (GraphicsChoice)Mathf.RoundToInt(v); RefreshLabels(); });
            }

            HideAll();
        }

        void OnDestroy()
        {
            // Leaving the scene while paused: never leave the game frozen.
            if (IsPaused)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                IsPaused = false;
                PausedChanged?.Invoke(false);
            }
        }

        void Update()
        {
            if (_leaving) return;
            bool back = backButtonOpens && BackPressed();

            if (IsPaused)
            {
                if (back && !_busy)
                {
                    if (_openPanel != null) ClosePanel();
                    else Resume();
                }
                return;
            }

            // Only when nothing else was open this frame or the last one: their Esc closes them, not opens this.
            if (back && !_overlayLastFrame && !OverlayOpen()) Pause();
        }

        void LateUpdate()
        {
            _overlayLastFrame = !IsPaused && OverlayOpen();
        }

        void OnApplicationPause(bool paused)
        {
#if !UNITY_EDITOR
            if (paused && pauseWhenAppHidden && Application.isMobilePlatform && !IsPaused && !OverlayOpen()) Pause();
#endif
        }

        static bool BackPressed()
        {
            // Android's back button arrives as Escape in both input systems.
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        /// <summary>A book, computer, dialogue or the name screen is on screen.</summary>
        bool OverlayOpen()
        {
            if (CharacterNameScreen.IsAsking) return true;
            if (_book == null) _book = UnityEngine.Object.FindAnyObjectByType<BookScreen>();
            if (_book != null && _book.IsOpen) return true;
            if (_dialogue == null) _dialogue = UnityEngine.Object.FindAnyObjectByType<DialogueScreen>();
            if (_dialogue != null && _dialogue.IsOpen) return true;
            if (_quiz == null && Time.unscaledTime >= _nextQuizSearch)
            {
                _quiz = UnityEngine.Object.FindAnyObjectByType<BlackCompuQuiz.QuizScreen>();
                _nextQuizSearch = Time.unscaledTime + 1f;   // made the first time a computer is used
            }
            return _quiz != null && _quiz.IsOpen;
        }

        // ------------------------------------------------------------------ pause / resume

        /// <summary>Stops the game and shows the pause menu.</summary>
        public void Pause()
        {
            if (IsPaused || _busy || _leaving || OverlayOpen() || menuGroup == null) return;
            IsPaused = true;

            _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            FreezePlayer(true);
            HideHud(true);

            if (volumeSlider) volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            if (lookSlider) lookSlider.SetValueWithoutNotify(GameSettings.LookSensitivity);
            if (graphicsSlider) graphicsSlider.SetValueWithoutNotify((int)GameSettings.Graphics);
            if (_appliedLook < 0f) _appliedLook = GameSettings.LookSensitivity;   // what the game started with
            RefreshLabels();
            if (versionText) versionText.text = versionPrefix + Application.version;
            if (playerText) playerText.text = playerPrefix + LibraryProgress.PlayerName;

            HidePanel(howToPlayPanel);
            HidePanel(settingsPanel);
            _openPanel = null;
            menuGroup.gameObject.SetActive(true);
            menuGroup.alpha = 0f;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
            StartCoroutine(FadeTo(menuGroup, 1f, fadeSeconds, null, true));
            PausedChanged?.Invoke(true);
        }

        /// <summary>Hides the pause menu and carries on playing.</summary>
        public void Resume()
        {
            if (!IsPaused || _busy || _leaving) return;
            GameSettings.Save();
            if (_openPanel != null) HidePanel(_openPanel);
            _openPanel = null;
            menuGroup.interactable = false;
            StartCoroutine(FadeTo(menuGroup, 0f, fadeSeconds * 0.8f, FinishResume, true));
        }

        void FinishResume()
        {
            menuGroup.gameObject.SetActive(false);
            Time.timeScale = _timeScaleBefore;
            AudioListener.pause = false;
            HideHud(false);
            IsPaused = false;
            // Give control back one frame later so the tap on Resume isn't read as a tap on the world.
            StartCoroutine(UnfreezeNextFrame());
            PausedChanged?.Invoke(false);
        }

        IEnumerator UnfreezeNextFrame()
        {
            yield return null;
            FreezePlayer(false);
        }

        /// <summary>Back to the title screen (progress is already saved as you play).</summary>
        public void GoToMainMenu()
        {
            if (_leaving || _busy) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError("[ARCHIV3D] Can't load scene \"" + mainMenuScene + "\". Add it in File > Build Profiles (Scene List).");
                return;
            }
            StartCoroutine(LeaveToMainMenu());
        }

        IEnumerator LeaveToMainMenu()
        {
            _leaving = true;
            GameSettings.Save();

            if (fader)
            {
                fader.gameObject.SetActive(true);
                fader.blocksRaycasts = true;
                yield return FadeTo(fader, 1f, fadeSeconds + 0.2f);
            }
            if (loadingGroup)
            {
                loadingGroup.gameObject.SetActive(true);
                yield return FadeTo(loadingGroup, 1f, 0.15f);
            }
            SetProgress(0f);

            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;
            PausedChanged?.Invoke(false);

            var op = SceneManager.LoadSceneAsync(mainMenuScene);
            op.allowSceneActivation = false;
            float shown = 0f;
            while (op.progress < 0.9f || shown < 0.99f)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, real, Time.unscaledDeltaTime * 2f);
                SetProgress(shown);
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.1f);
            op.allowSceneActivation = true;
        }

        void SetProgress(float p)
        {
            if (loadingFill == null) return;
            loadingFill.anchorMax = new Vector2(Mathf.Clamp01(p), loadingFill.anchorMax.y);
        }

        // ------------------------------------------------------------------ panels (same as the title screen)

        public void OpenPanel(CanvasGroup panel)
        {
            if (!IsPaused || _busy || panel == null) return;
            if (_openPanel != null && _openPanel != panel) HidePanel(_openPanel);
            _openPanel = panel;
            panel.gameObject.SetActive(true);
            panel.blocksRaycasts = true;
            panel.interactable = true;
            StartCoroutine(PanelIn(panel));
        }

        public void ClosePanel()
        {
            if (_openPanel == null) return;
            GameSettings.Save();
            var panel = _openPanel;
            _openPanel = null;
            panel.blocksRaycasts = false;
            panel.interactable = false;
            StartCoroutine(FadeTo(panel, 0f, 0.15f, () => panel.gameObject.SetActive(false)));
        }

        void HideAll()
        {
            IsPaused = false;
            if (menuGroup) { menuGroup.alpha = 0f; menuGroup.gameObject.SetActive(false); }
            HidePanel(howToPlayPanel);
            HidePanel(settingsPanel);
            if (fader) { fader.alpha = 0f; fader.blocksRaycasts = false; fader.gameObject.SetActive(false); }
            if (loadingGroup) { loadingGroup.alpha = 0f; loadingGroup.gameObject.SetActive(false); }
        }

        static void HidePanel(CanvasGroup panel)
        {
            if (panel == null) return;
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
            panel.gameObject.SetActive(false);
        }

        IEnumerator PanelIn(CanvasGroup panel)
        {
            var card = panel.transform.childCount > 1 ? panel.transform.GetChild(1) : panel.transform;
            float t = 0f;
            const float dur = 0.2f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - x, 3f);
                panel.alpha = e;
                card.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
                yield return null;
            }
            panel.alpha = 1f;
            card.localScale = Vector3.one;
        }

        IEnumerator FadeTo(CanvasGroup group, float target, float seconds, Action done = null, bool blocking = false)
        {
            if (blocking) _busy = true;
            float start = group.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            group.alpha = target;
            if (blocking) _busy = false;
            done?.Invoke();
        }

        // ------------------------------------------------------------------ settings

        void RefreshLabels()
        {
            if (volumeValue && volumeSlider) volumeValue.text = Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
            if (lookValue && lookSlider) lookValue.text = lookSlider.value.ToString("0.0") + "x";
            if (graphicsValue && graphicsSlider)
            {
                var choice = (GraphicsChoice)Mathf.RoundToInt(graphicsSlider.value);
                graphicsValue.text = choice == GraphicsChoice.Auto
                    ? "Auto <size=70%><alpha=#99>(" + DevicePerformance.TierFor(choice) + ")</size>"
                    : choice.ToString();
            }
        }

        /// <summary>Look sensitivity takes effect right away (the game applied the old value when it started).</summary>
        void ApplyLook(float look)
        {
            if (_appliedLook <= 0f || Mathf.Approximately(look, _appliedLook)) return;
            var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null)
            {
                float k = look / _appliedLook;
                player.rotationSpeed *= k;
                player.touchSensitivity *= k;
            }
            _appliedLook = look;
        }

        // ------------------------------------------------------------------ player and controls

        void FreezePlayer(bool freeze)
        {
            if (freeze)
            {
                _frozen.Clear();
                var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
                if (player != null && player.enabled) { player.enabled = false; _frozen.Add(player); }
                var interactor = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
                if (interactor != null && interactor.enabled) { interactor.enabled = false; _frozen.Add(interactor); }
                _previousLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                foreach (var b in _frozen)
                    if (b != null) b.enabled = true;
                _frozen.Clear();
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        /// <summary>Hides the joystick / Run / Interact / pause button canvas while the menu is up.</summary>
        void HideHud(bool hide)
        {
            if (hide)
            {
                _hud = null;
                Canvas hudCanvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>(true) : null;
                if (hudCanvas == null)
                {
                    var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
                    if (player != null && player.mobileJoystick != null) hudCanvas = player.mobileJoystick.GetComponentInParent<Canvas>(true);
                }
                if (hudCanvas == null) return;
                hudCanvas = hudCanvas.rootCanvas;
                if (hudCanvas.transform == transform || hudCanvas.transform.IsChildOf(transform)) return;

                _hud = hudCanvas.GetComponent<CanvasGroup>();
                if (_hud == null) _hud = hudCanvas.gameObject.AddComponent<CanvasGroup>();
                _hudAlpha = _hud.alpha;
                _hudRaycasts = _hud.blocksRaycasts;
                _hudInteractable = _hud.interactable;
                _hud.alpha = 0f;
                _hud.blocksRaycasts = false;
                _hud.interactable = false;
            }
            else if (_hud != null)
            {
                _hud.alpha = _hudAlpha;
                _hud.blocksRaycasts = _hudRaycasts;
                _hud.interactable = _hudInteractable;
                _hud = null;
            }
        }
    }
}
