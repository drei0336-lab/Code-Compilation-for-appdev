using System.Collections;
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
    /// Runs the title screen: Start / How to Play / Settings / Quit, the pop-up panels,
    /// and the fade + loading bar when the game starts.
    /// Built by Tools > ARCHIV3D > Build Title Screen; every reference below can be changed in the Inspector.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Game")]
        [Tooltip("Scene loaded by the Start button. It must be in File > Build Profiles / Build Settings.")]
        public string gameScene = "Main Map";

        [Header("Buttons")]
        public Button playButton;
        public Button howToPlayButton;
        public Button settingsButton;
        public Button quitButton;

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
        public float fadeSeconds = 0.6f;

        [Header("Footer")]
        public TMP_Text versionText;
        public string versionPrefix = "ARCHIV3D v";

        CanvasGroup _openPanel;
        bool _busy;

        void Awake()
        {
            Time.timeScale = 1f;
            if (playButton) playButton.onClick.AddListener(Play);
            if (howToPlayButton) howToPlayButton.onClick.AddListener(() => OpenPanel(howToPlayPanel));
            if (settingsButton) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            if (quitButton) quitButton.onClick.AddListener(Quit);
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
                lookSlider.onValueChanged.AddListener(v => { GameSettings.LookSensitivity = v; RefreshLabels(); });
            }
            if (graphicsSlider)
            {
                graphicsSlider.minValue = 0f;
                graphicsSlider.maxValue = 3f;
                graphicsSlider.wholeNumbers = true;
                graphicsSlider.onValueChanged.AddListener(v => { GameSettings.Graphics = (GraphicsChoice)Mathf.RoundToInt(v); RefreshLabels(); });
            }
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.volume = GameSettings.Volume;

            if (volumeSlider) volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            if (lookSlider) lookSlider.SetValueWithoutNotify(GameSettings.LookSensitivity);
            if (graphicsSlider) graphicsSlider.SetValueWithoutNotify((int)GameSettings.Graphics);
            RefreshLabels();

            if (versionText) versionText.text = versionPrefix + Application.version;

            HidePanel(howToPlayPanel);
            HidePanel(settingsPanel);
            if (loadingGroup) { loadingGroup.alpha = 0f; loadingGroup.gameObject.SetActive(false); }

            if (fader)
            {
                fader.gameObject.SetActive(true);
                fader.alpha = 1f;
                StartCoroutine(FadeTo(fader, 0f, fadeSeconds, () => fader.blocksRaycasts = false));
            }
        }

        void Update()
        {
            if (_busy || !BackPressed()) return;
            if (_openPanel != null) ClosePanel();
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------ buttons

        public void Play()
        {
            if (_busy) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError("[ARCHIV3D] Can't load scene \"" + gameScene + "\". Add it in File > Build Profiles (Scene List).");
                return;
            }
            StartCoroutine(LoadGame());
        }

        public void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenPanel(CanvasGroup panel)
        {
            if (_busy || panel == null) return;
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

        // ------------------------------------------------------------------ helpers

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

        IEnumerator FadeTo(CanvasGroup group, float target, float seconds, System.Action done = null)
        {
            float start = group.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            group.alpha = target;
            done?.Invoke();
        }

        IEnumerator LoadGame()
        {
            _busy = true;
            GameSettings.Save();

            if (fader)
            {
                fader.gameObject.SetActive(true);
                fader.blocksRaycasts = true;
                yield return FadeTo(fader, 1f, fadeSeconds);
            }

            if (loadingGroup)
            {
                loadingGroup.gameObject.SetActive(true);
                yield return FadeTo(loadingGroup, 1f, 0.2f);
            }
            SetProgress(0f);

            var op = SceneManager.LoadSceneAsync(gameScene);
            op.allowSceneActivation = false;
            float shown = 0f;
            while (op.progress < 0.9f || shown < 0.99f)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, real, Time.unscaledDeltaTime * 1.5f);
                SetProgress(shown);
                yield return null;
            }
            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.15f);
            op.allowSceneActivation = true;
        }

        void SetProgress(float p)
        {
            if (loadingFill == null) return;
            loadingFill.anchorMax = new Vector2(Mathf.Clamp01(p), loadingFill.anchorMax.y);
        }
    }
}
