using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Takes six 90° pictures of the library (a cube around one point) and turns them into a skybox,
    /// which the title screen camera slowly turns inside.
    /// </summary>
    public static class PanoramaCapture
    {
        public const string Dir = ArchiveAssets.Root + "/Title/Panorama";
        public const string MaterialPath = Dir + "/TitlePanorama.mat";
        const string InfoPath = Dir + "/panorama_info.txt";

        static readonly string[] Faces = { "front", "back", "left", "right", "up", "down" };
        static readonly string[] Props = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };
        static readonly Vector3[] Angles =
        {
            new Vector3(0, 0, 0),     // +Z
            new Vector3(0, 180, 0),   // -Z
            new Vector3(0, 90, 0),    // +X ("Left" in Unity's 6-sided skybox)
            new Vector3(0, -90, 0),   // -X ("Right")
            new Vector3(-90, 0, 0),   // +Y
            new Vector3(90, 0, 0),    // -Y
        };

        public static Material LoadMaterial() => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        public static float LoadYaw()
        {
            if (!File.Exists(InfoPath)) return 0f;
            float.TryParse(File.ReadAllText(InfoPath).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float yaw);
            return yaw;
        }

        /// <summary>Captures from the player's camera in the open scene.</summary>
        public static bool CaptureFromPlayer(Scene scene, int size, out string message)
        {
            var src = FindPlayerCamera(scene);
            if (src == null) { message = "No camera found in " + scene.name + "."; return false; }
            return Capture(src, src.transform.position + Vector3.up * 0.15f, src.transform.eulerAngles.y, src.transform.root, size, out message);
        }

        /// <summary>Captures from wherever the Scene view camera is right now.</summary>
        public static bool CaptureFromSceneView(int size, out string message)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null || sv.camera == null) { message = "Open a Scene view first."; return false; }
            var template = FindPlayerCamera(SceneManager.GetActiveScene());
            return Capture(template != null ? template : sv.camera, sv.camera.transform.position, sv.camera.transform.eulerAngles.y,
                template != null ? template.transform.root : null, size, out message);
        }

        static Camera FindPlayerCamera(Scene scene)
        {
            Camera first = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (!cam.gameObject.activeInHierarchy) continue;
                    if (cam.CompareTag("MainCamera")) return cam;
                    if (first == null) first = cam;
                }
            }
            return first;
        }

        static bool Capture(Camera template, Vector3 position, float yaw, Transform hideRoot, int size, out string message)
        {
            Directory.CreateDirectory(Dir);

            // Hide the player's own body while taking the pictures
            var hidden = new List<Renderer>();
            if (hideRoot != null)
                foreach (var r in hideRoot.GetComponentsInChildren<Renderer>(true))
                    if (!r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }

            var go = new GameObject("ARCHIV3D Panorama Camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            int written = 0;
            try
            {
                cam.CopyFrom(template);
                cam.enabled = false;
                cam.fieldOfView = 90f;
                cam.aspect = 1f;
                cam.rect = new Rect(0, 0, 1, 1);
                cam.transform.position = position;

                var srcData = template.GetComponent<UniversalAdditionalCameraData>();
                var data = cam.GetUniversalAdditionalCameraData();
                if (srcData != null && data != null)
                {
                    data.renderPostProcessing = srcData.renderPostProcessing;
                    data.antialiasing = srcData.antialiasing;
                    data.volumeLayerMask = srcData.volumeLayerMask;
                    data.renderShadows = srcData.renderShadows;
                }

                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                for (int i = 0; i < Faces.Length; i++)
                {
                    cam.transform.rotation = Quaternion.Euler(Angles[i]);
                    var tex = Render(cam, size, linear);
                    string path = Dir + "/pano_" + Faces[i] + ".png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    written++;
                }
            }
            catch (System.Exception e)
            {
                message = "Panorama capture failed: " + e.Message;
                Debug.LogException(e);
                return false;
            }
            finally
            {
                Object.DestroyImmediate(go);
                foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
            }

            File.WriteAllText(InfoPath, yaw.ToString(CultureInfo.InvariantCulture));
            AssetDatabase.Refresh();

            var mat = LoadMaterial();
            bool isNew = mat == null;
            if (isNew) mat = new Material(Shader.Find("Skybox/6 Sided"));
            for (int i = 0; i < Faces.Length; i++)
            {
                string path = Dir + "/pano_" + Faces[i] + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
                mat.SetTexture(Props[i], AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            message = "Captured " + written + " panorama pictures.";
            return true;
        }

        static Texture2D Render(Camera cam, int size, bool linear)
        {
            var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = linear,
                msaaSamples = 1,
            };
            var rt = RenderTexture.GetTemporary(desc);
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                bool done = false;
                try
                {
                    var request = new RenderPipeline.StandardRequest();
                    if (RenderPipeline.SupportsRenderRequest(cam, request))
                    {
                        request.destination = rt;
                        RenderPipeline.SubmitRenderRequest(cam, request);
                        done = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[ARCHIV3D] Render request failed, trying Camera.Render: " + e.Message);
                }
                if (!done) cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
