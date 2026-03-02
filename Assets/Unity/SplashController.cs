using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Minesweeper3D.Unity
{
    public class SplashController : MonoBehaviour
    {
        private const int GridSize = 4;
        private const int CubeCount = GridSize * GridSize * GridSize;
        private const float CubeSpacing = 1.1f;
        private const float CubeScale = 0.45f;

        // Phase timings
        private const float AssembleEnd = 1.0f;
        private const float HoldEnd = 1.5f;
        private const float ExplodeEnd = 2.5f;
        private const float TotalDuration = 2.75f;

        private Transform[] _cubes;
        private Vector3[] _scatterPositions;
        private Vector3[] _gridPositions;
        private Vector3[] _explodeVelocities;
        private Vector3[] _explodeTumble;
        private float[] _staggerDelays;
        private int _mineIndex;

        private Material _cubeMat;
        private Material _mineMat;

        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _shadowText;
        private Image _fadeImage;

        private Camera _cam;
        private float _time;
        private AsyncOperation _loadOp;
        private bool _started;

        private void Start()
        {
            LoadMaterials();
            SetupCamera();
            CreateCubeGrid();
            CreateUI();
            _started = true;
        }

        private void LoadMaterials()
        {
            var opaqueMat = Resources.Load<Material>("Materials/Opaque");

            if (opaqueMat != null)
            {
                _cubeMat = new Material(opaqueMat);
                _cubeMat.color = new Color(0.35f, 0.55f, 0.85f);
                _mineMat = new Material(opaqueMat);
                _mineMat.color = new Color(0.9f, 0.2f, 0.2f);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                          ?? Shader.Find("Standard");
                _cubeMat = new Material(shader);
                _cubeMat.color = new Color(0.35f, 0.55f, 0.85f);
                _mineMat = new Material(shader);
                _mineMat.color = new Color(0.9f, 0.2f, 0.2f);
            }
        }

        private void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camObj = new GameObject("SplashCamera");
                _cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.08f, 0.08f, 0.16f);
            _cam.fieldOfView = 60f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 100f;
            _cam.transform.position = new Vector3(0f, 0f, -8f);
            _cam.transform.LookAt(Vector3.zero);
        }

        private void CreateCubeGrid()
        {
            _cubes = new Transform[CubeCount];
            _scatterPositions = new Vector3[CubeCount];
            _gridPositions = new Vector3[CubeCount];
            _explodeVelocities = new Vector3[CubeCount];
            _explodeTumble = new Vector3[CubeCount];
            _staggerDelays = new float[CubeCount];

            var rng = new System.Random(42);
            float offset = (GridSize - 1) * CubeSpacing * 0.5f;
            Vector3 center = Vector3.zero;

            int i = 0;
            for (int x = 0; x < GridSize; x++)
            for (int y = 0; y < GridSize; y++)
            for (int z = 0; z < GridSize; z++)
            {
                var gridPos = new Vector3(
                    x * CubeSpacing - offset,
                    y * CubeSpacing - offset,
                    z * CubeSpacing - offset
                );
                _gridPositions[i] = gridPos;

                float r = 8f + (float)rng.NextDouble() * 7f;
                float theta = (float)(rng.NextDouble() * Mathf.PI * 2);
                float phi = (float)(rng.NextDouble() * Mathf.PI);
                _scatterPositions[i] = new Vector3(
                    r * Mathf.Sin(phi) * Mathf.Cos(theta),
                    r * Mathf.Sin(phi) * Mathf.Sin(theta),
                    r * Mathf.Cos(phi)
                );

                var dir = (gridPos - center).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
                _explodeVelocities[i] = dir * (6f + (float)rng.NextDouble() * 6f)
                    + new Vector3(
                        (float)(rng.NextDouble() - 0.5) * 4f,
                        (float)(rng.NextDouble() - 0.5) * 4f,
                        (float)(rng.NextDouble() - 0.5) * 4f
                    );

                _explodeTumble[i] = new Vector3(
                    (float)(rng.NextDouble() - 0.5) * 720f,
                    (float)(rng.NextDouble() - 0.5) * 720f,
                    (float)(rng.NextDouble() - 0.5) * 720f
                );

                float dist = gridPos.magnitude;
                float maxDist = offset * Mathf.Sqrt(3f);
                _staggerDelays[i] = (dist / maxDist) * 0.3f;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Cube_{i}";
                cube.transform.localScale = Vector3.one * CubeScale;
                cube.transform.position = _scatterPositions[i];
                cube.GetComponent<Renderer>().material = _cubeMat;
                var col = cube.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _cubes[i] = cube.transform;

                i++;
            }

            _mineIndex = CubeCount / 2 + GridSize / 2;
        }

        private void CreateUI()
        {
            var canvasObj = new GameObject("SplashCanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<GraphicRaycaster>();

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Shadow text
            var shadowObj = new GameObject("TitleShadow");
            shadowObj.transform.SetParent(canvasObj.transform, false);
            _shadowText = shadowObj.AddComponent<TextMeshProUGUI>();
            _shadowText.text = "Minesweep3D";
            _shadowText.fontSize = 72;
            _shadowText.alignment = TextAlignmentOptions.Center;
            _shadowText.color = new Color(0f, 0f, 0f, 0f);
            var shadowRect = shadowObj.GetComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.15f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.15f);
            shadowRect.anchoredPosition = new Vector2(3f, -3f);
            shadowRect.sizeDelta = new Vector2(800f, 100f);

            // Title text
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(canvasObj.transform, false);
            _titleText = titleObj.AddComponent<TextMeshProUGUI>();
            _titleText.text = "Minesweep3D";
            _titleText.fontSize = 72;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(1f, 1f, 1f, 0f);
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.15f);
            titleRect.anchorMax = new Vector2(0.5f, 0.15f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(800f, 100f);

            // Fade overlay (fullscreen black Image)
            var fadeObj = new GameObject("FadeOverlay");
            fadeObj.transform.SetParent(canvasObj.transform, false);
            _fadeImage = fadeObj.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = false;
            var fadeRect = fadeObj.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (!_started) return;
            _time += Time.deltaTime;

            if (_time < AssembleEnd)
                UpdateAssemble();
            else if (_time < HoldEnd)
                UpdateHold();
            else if (_time < TotalDuration)
                UpdateExplode();
            else
                ActivateScene();
        }

        private void UpdateAssemble()
        {
            float phase = _time / AssembleEnd;
            for (int i = 0; i < CubeCount; i++)
            {
                float t = Mathf.Clamp01((phase - _staggerDelays[i]) / (1f - _staggerDelays[i]));
                t = BounceEase(t);
                _cubes[i].position = Vector3.LerpUnclamped(_scatterPositions[i], _gridPositions[i], t);
                _cubes[i].localScale = Vector3.one * CubeScale * Mathf.Clamp01(t * 2f);
            }
        }

        private void UpdateHold()
        {
            for (int i = 0; i < CubeCount; i++)
            {
                _cubes[i].position = _gridPositions[i];
                _cubes[i].localScale = Vector3.one * CubeScale;
            }

            float holdT = (_time - AssembleEnd) / (HoldEnd - AssembleEnd);

            // Title fade in
            SetTmpAlpha(_titleText, holdT);
            SetTmpAlpha(_shadowText, holdT * 0.5f);

            // Mine reveal
            if (holdT > 0.3f)
            {
                _cubes[_mineIndex].GetComponent<Renderer>().material = _mineMat;
                float pop = 1f + 0.3f * Mathf.Sin((holdT - 0.3f) / 0.7f * Mathf.PI);
                _cubes[_mineIndex].localScale = Vector3.one * CubeScale * pop;
            }
        }

        private void UpdateExplode()
        {
            float explodeT = (_time - HoldEnd) / (ExplodeEnd - HoldEnd);
            float fadeT = Mathf.Clamp01((_time - (HoldEnd + 0.3f)) / (ExplodeEnd - HoldEnd - 0.3f));

            for (int i = 0; i < CubeCount; i++)
            {
                float dt = _time - HoldEnd;
                _cubes[i].position = _gridPositions[i] + _explodeVelocities[i] * dt;
                _cubes[i].Rotate(_explodeTumble[i] * Time.deltaTime);
                float shrink = Mathf.Clamp01(1f - explodeT * 1.2f);
                _cubes[i].localScale = Vector3.one * CubeScale * shrink;
            }

            // Title fade out
            SetTmpAlpha(_titleText, 1f - explodeT);
            SetTmpAlpha(_shadowText, (1f - explodeT) * 0.5f);

            // Fade to black
            if (_fadeImage != null)
            {
                var c = _fadeImage.color;
                c.a = fadeT;
                _fadeImage.color = c;
            }

            // Start async load
            if (_loadOp == null)
            {
                _loadOp = SceneManager.LoadSceneAsync("SampleScene");
                _loadOp.allowSceneActivation = false;
            }
        }

        private void ActivateScene()
        {
            if (_loadOp != null)
                _loadOp.allowSceneActivation = true;
        }

        private static void SetTmpAlpha(TextMeshProUGUI tmp, float alpha)
        {
            if (tmp == null) return;
            var c = tmp.color;
            c.a = Mathf.Clamp01(alpha);
            tmp.color = c;
        }

        private static float BounceEase(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.7f)
                return (t / 0.7f) * (t / 0.7f) * ((2.5f * (t / 0.7f)) - 1.5f) + 0.3f * (t / 0.7f);
            float bt = (t - 0.7f) / 0.3f;
            return 1f + 0.1f * Mathf.Sin(bt * Mathf.PI);
        }
    }
}
