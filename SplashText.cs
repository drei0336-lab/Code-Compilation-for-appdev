using TMPro;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>The tilted, pulsing yellow line next to the logo. Picks a random line each time the title screen opens.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class SplashText : MonoBehaviour
    {
        [Tooltip("One of these is picked at random. Add your own!")]
        public string[] splashes =
        {
            "Shh! Library rules!",
            "Now in 3D!",
            "Read all about it!",
            "Dewey approved!",
            "Bookmarks not included!",
            "Knowledge is power!",
            "Turn the page!",
            "Return books on time!",
            "Open 24/7!",
            "Mind the shelves!",
            "Quiz time!",
            "Archived for you!",
            "Learn something new!",
            "No late fees!",
            "Every book has a story!",
            "Find your next read!",
            "Silence is golden!",
            "Made by students!",
        };

        [Tooltip("Optional dark copy behind the text, used as a drop shadow.")]
        public TMP_Text shadow;
        public float pulsesPerSecond = 1.1f;
        public float pulseAmount = 0.07f;

        TMP_Text _text;
        Vector3 _baseScale = Vector3.one;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _baseScale = transform.localScale;
        }

        void OnEnable()
        {
            if (splashes == null || splashes.Length == 0) return;
            string line = splashes[Random.Range(0, splashes.Length)];
            _text.text = line;
            if (shadow != null) shadow.text = line;
        }

        void Update()
        {
            float s = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * pulsesPerSecond * Mathf.PI)) * pulseAmount;
            transform.localScale = _baseScale * s;
        }
    }
}
