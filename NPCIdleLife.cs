using System.Collections.Generic;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>
    /// Small touches that make an idle character feel alive: looks at the player when they're close,
    /// blinks now and then, and moves its mouth while its text is typing out.
    /// Put it on the character model (the object with the Animator). Works with Ready Player Me style
    /// faces (eyeBlinkLeft / eyeBlinkRight / jawOpen / viseme_aa blend shapes); anything missing is skipped.
    /// Looking at the player needs a Humanoid rig and "IK Pass" on the Animator layer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("ARCHIV3D/NPC Idle Life")]
    public class NPCIdleLife : MonoBehaviour
    {
        [Header("Look at the player")]
        public bool lookAtPlayer = true;
        [Tooltip("Starts looking when the player is this close (meters).")]
        public float lookRange = 6f;
        [Range(0f, 1f)] public float lookWeight = 0.8f;
        [Tooltip("Only looks when the player is within this angle of where the body faces.")]
        public float lookMaxAngle = 110f;

        [Header("Face")]
        public bool blink = true;
        [Tooltip("Seconds between blinks (random between the two).")]
        public Vector2 blinkInterval = new Vector2(2.2f, 5.5f);
        public bool moveMouthWhileTalking = true;
        [Range(0f, 100f)] public float smile = 18f;

        /// <summary>Set by NPCGuide while the dialogue text is typing.</summary>
        public bool Talking { get; set; }

        class Face
        {
            public SkinnedMeshRenderer r;
            public int blinkL = -1, blinkR = -1, jaw = -1, aa = -1, smile = -1;
        }

        Animator _anim;
        readonly List<Face> _faces = new List<Face>();
        Transform _target;
        float _look;
        float _nextBlink, _blinkT = -1f;
        float _mouth;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh;
                if (m == null || m.blendShapeCount == 0) continue;
                var f = new Face
                {
                    r = r,
                    blinkL = m.GetBlendShapeIndex("eyeBlinkLeft"),
                    blinkR = m.GetBlendShapeIndex("eyeBlinkRight"),
                    jaw = m.GetBlendShapeIndex("jawOpen"),
                    aa = m.GetBlendShapeIndex("viseme_aa"),
                    smile = m.GetBlendShapeIndex("mouthSmile"),
                };
                if (f.blinkL >= 0 || f.jaw >= 0 || f.aa >= 0 || f.smile >= 0) _faces.Add(f);
            }
            _nextBlink = Time.time + Random.Range(blinkInterval.x, blinkInterval.y);
        }

        void Update()
        {
            if (_target == null && Camera.main != null) _target = Camera.main.transform;

            // Blink: quick close, short hold, slower open
            float blinkW = 0f;
            if (blink)
            {
                if (_blinkT < 0f && Time.time >= _nextBlink) _blinkT = 0f;
                if (_blinkT >= 0f)
                {
                    _blinkT += Time.deltaTime;
                    const float close = 0.07f, hold = 0.04f, open = 0.12f;
                    if (_blinkT < close) blinkW = _blinkT / close;
                    else if (_blinkT < close + hold) blinkW = 1f;
                    else if (_blinkT < close + hold + open) blinkW = 1f - (_blinkT - close - hold) / open;
                    else
                    {
                        _blinkT = -1f;
                        // Sometimes a quick double blink
                        _nextBlink = Time.time + (Random.value < 0.15f ? 0.18f : Random.Range(blinkInterval.x, blinkInterval.y));
                    }
                }
            }

            // Mouth: syllable-like open/close while the text types
            float targetMouth = 0f;
            if (moveMouthWhileTalking && Talking)
            {
                float t = Time.time;
                float syllable = Mathf.Abs(Mathf.Sin(t * 13f));
                float variety = 0.45f + 0.55f * Mathf.PerlinNoise(t * 3.1f, 0.37f);
                targetMouth = syllable * variety;
            }
            _mouth = Mathf.MoveTowards(_mouth, targetMouth, Time.deltaTime * 9f);

            foreach (var f in _faces)
            {
                if (f.r == null) continue;
                if (f.blinkL >= 0) f.r.SetBlendShapeWeight(f.blinkL, blinkW * 100f);
                if (f.blinkR >= 0) f.r.SetBlendShapeWeight(f.blinkR, blinkW * 100f);
                if (f.jaw >= 0) f.r.SetBlendShapeWeight(f.jaw, _mouth * 28f);
                if (f.aa >= 0) f.r.SetBlendShapeWeight(f.aa, _mouth * 70f);
                if (f.smile >= 0) f.r.SetBlendShapeWeight(f.smile, smile * (1f - _mouth * 0.6f));
            }
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (_anim == null) return;
            float want = 0f;
            if (lookAtPlayer && _target != null)
            {
                Vector3 to = _target.position - transform.position;
                float dist = to.magnitude;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 bodyFwd = _anim.isHuman ? _anim.bodyRotation * Vector3.forward : transform.forward;
                Vector3 fwd = new Vector3(bodyFwd.x, 0f, bodyFwd.z);
                if (dist < lookRange && flat.sqrMagnitude > 0.0001f && Vector3.Angle(fwd, flat) < lookMaxAngle)
                    want = lookWeight;
            }
            _look = Mathf.MoveTowards(_look, want, Time.deltaTime * 2.5f);
            _anim.SetLookAtWeight(_look, 0.12f, 0.6f, 1f, 0.5f);
            if (_target != null) _anim.SetLookAtPosition(_target.position);
        }
    }
}
