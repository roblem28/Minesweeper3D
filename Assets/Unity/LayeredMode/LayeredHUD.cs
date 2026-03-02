using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity.LayeredMode
{
    /// <summary>
    /// HUD for LayeredMode. Layer selector buttons, mine/flag counts,
    /// timer, status, flag mode toggle, new game button.
    /// </summary>
    public class LayeredHUD : MonoBehaviour
    {
        private LayeredModeController _game;
        private Canvas _canvas;

        // Info labels
        private TextMeshProUGUI _mineText;
        private TextMeshProUGUI _flagText;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _statusText;

        // Layer buttons
        private Button[] _layerButtons;
        private Image[] _layerButtonBgs;

        // Action buttons
        private Image _flagToggleBg;

        // End panel
        private GameObject _endPanel;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endTime;
        private TextMeshProUGUI _endScore;

        // Controls hint
        private TextMeshProUGUI _controlsHint;

        private static readonly Color ActiveColor = new Color(0.3f, 0.55f, 0.85f, 1f);
        private static readonly Color InactiveColor = new Color(0.25f, 0.25f, 0.30f, 1f);

        private bool _inputEnabled = true;

        public void Init(LayeredModeController game)
        {
            _game = game;
            BuildUI();
            Refresh();
        }

        private void Update()
        {
            if (_game != null && _game.Timer != null && _timerText != null)
                _timerText.text = $"Time: {_game.Timer.FormattedTime}";
        }

        // ========== UI CONSTRUCTION ==========

        private void BuildUI()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            gameObject.AddComponent<GraphicRaycaster>();

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // SafeArea root
            var safeObj = new GameObject("SafeArea");
            safeObj.transform.SetParent(_canvas.transform, false);
            var safeRt = safeObj.AddComponent<RectTransform>();
            safeRt.anchorMin = Vector2.zero;
            safeRt.anchorMax = Vector2.one;
            safeRt.offsetMin = Vector2.zero;
            safeRt.offsetMax = Vector2.zero;
            safeObj.AddComponent<SafeAreaHandler>();

            EnsureEventSystem();

            BuildInfoBar(safeObj.transform);
            BuildLayerSelector(safeObj.transform);
            BuildButtonBar(safeObj.transform);
            BuildEndPanel(safeObj.transform);
            BuildControlsHint(safeObj.transform);
        }

        private void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var obj = new GameObject("EventSystem");
                es = obj.AddComponent<EventSystem>();
                obj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) DestroyImmediate(legacy);
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private void BuildInfoBar(Transform parent)
        {
            var panel = new GameObject("InfoBar");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 70f);
            rt.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.18f, 0.7f);
            bg.raycastTarget = false;

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 3, 3);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _mineText   = MakeLabel(panel.transform, "MineText",   $"Mines: {_game.MineCount}", 28, 40f);
            _flagText   = MakeLabel(panel.transform, "FlagText",   "Flags: 0",                   28, 40f);
            _timerText  = MakeLabel(panel.transform, "TimerText",  "Time: 00:00",                28, 40f);
            _statusText = MakeLabel(panel.transform, "StatusText", "Tap to start",               28, 40f);
        }

        private void BuildLayerSelector(Transform parent)
        {
            var panel = new GameObject("LayerSelector");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(500f, 70f);
            rt.anchoredPosition = new Vector2(0f, 10f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.18f, 0.6f);
            bg.raycastTarget = false;

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _layerButtons = new Button[_game.GridDepth];
            _layerButtonBgs = new Image[_game.GridDepth];

            for (int i = 0; i < _game.GridDepth; i++)
            {
                int layerIdx = i;
                var btnGo = MakeButton(panel.transform, $"Btn_Layer{i + 1}",
                    $"Layer {i + 1}", new Vector2(140f, 55f), 24, () =>
                    {
                        _game.SetLayer(layerIdx);
                        RefreshLayerHighlight();
                    });
                _layerButtons[i] = btnGo.GetComponent<Button>();
                _layerButtonBgs[i] = btnGo.GetComponent<Image>();
            }

            RefreshLayerHighlight();
        }

        private void BuildButtonBar(Transform parent)
        {
            var panel = new GameObject("ButtonBar");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 300f);
            rt.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.18f, 0.5f);
            bg.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Flag Mode toggle
            var flagGo = MakeButton(panel.transform, "Btn_FlagToggle", "Flag Mode",
                new Vector2(150f, 60f), 24, () =>
                {
                    _game.ToggleFlagMode();
                    _flagToggleBg.color = _game.IsFlagMode ? ActiveColor : InactiveColor;
                });
            _flagToggleBg = flagGo.GetComponent<Image>();

            // New Game
            MakeButton(panel.transform, "Btn_NewGame", "New Game",
                new Vector2(150f, 60f), 24, () =>
                {
                    _endPanel.SetActive(false);
                    _inputEnabled = true;
                    _game.RestartGame();
                });

            // Quit
            MakeButton(panel.transform, "Btn_Quit", "Quit",
                new Vector2(150f, 60f), 24, () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
        }

        private void BuildEndPanel(Transform parent)
        {
            _endPanel = new GameObject("EndPanel");
            _endPanel.transform.SetParent(parent, false);
            var rt = _endPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, 350f);
            rt.anchoredPosition = Vector2.zero;

            var bg = _endPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var layout = _endPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _endTitle = MakeLabel(_endPanel.transform, "EndTitle", "", 48, 50f);
            _endTime  = MakeLabel(_endPanel.transform, "EndTime", "", 30, 36f);
            _endScore = MakeLabel(_endPanel.transform, "EndScore", "", 34, 36f);

            MakeButton(_endPanel.transform, "Btn_EndNewGame", "New Game",
                new Vector2(280f, 65f), 24, () =>
                {
                    _endPanel.SetActive(false);
                    _inputEnabled = true;
                    _game.RestartGame();
                });

            _endPanel.SetActive(false);
        }

        private void BuildControlsHint(Transform parent)
        {
            var obj = new GameObject("ControlsHint");
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(600f, 40f);
            rt.anchoredPosition = new Vector2(-10f, 10f);
            _controlsHint = obj.AddComponent<TextMeshProUGUI>();
            _controlsHint.text = Application.isMobilePlatform
                ? "Tap: Reveal | Hold: Flag | 2-Tap: Chord | Layer buttons below"
                : "LMB: Reveal | RMB: Flag | 2-Click: Chord | Layer buttons below";
            _controlsHint.fontSize = 20;
            _controlsHint.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            _controlsHint.raycastTarget = false;
        }

        // ========== REFRESH ==========

        public void Refresh()
        {
            RefreshLayerHighlight();

            Board board = _game.Board;
            if (board == null)
            {
                _statusText.text = "Tap to start";
                _statusText.color = Color.white;
                _mineText.text = $"Mines: {_game.MineCount}";
                _flagText.text = "Flags: 0";
                _endPanel.SetActive(false);
                return;
            }

            _mineText.text = $"Mines: {_game.MineCount}";
            _flagText.text = $"Flags: {board.FlagCount}";

            switch (board.Status)
            {
                case GameStatus.Playing:
                    _statusText.text = $"Layer {_game.CurrentLayer + 1}/{_game.GridDepth}";
                    _statusText.color = Color.white;
                    _endPanel.SetActive(false);
                    break;
                case GameStatus.Won:
                    _statusText.text = "YOU WIN!";
                    _statusText.color = new Color(0.2f, 0.9f, 0.2f);
                    ShowEndPanel(true);
                    break;
                case GameStatus.Lost:
                    _statusText.text = "GAME OVER";
                    _statusText.color = new Color(0.9f, 0.2f, 0.2f);
                    ShowEndPanel(false);
                    break;
            }
        }

        private void ShowEndPanel(bool isWin)
        {
            _endPanel.SetActive(true);
            _inputEnabled = false;

            if (isWin)
            {
                _endTitle.text = "YOU WIN!";
                _endTitle.color = new Color(0.2f, 0.9f, 0.2f);
                _endScore.gameObject.SetActive(true);
                int score = ScoreController.Calculate(
                    _game.GridWidth, _game.Timer.ElapsedSeconds, _game.HintsUsed);
                _endScore.text = $"Score: {score}";
            }
            else
            {
                _endTitle.text = "GAME OVER";
                _endTitle.color = new Color(0.9f, 0.2f, 0.2f);
                _endScore.gameObject.SetActive(false);
            }

            _endTime.text = $"Time: {_game.Timer.FormattedTime}";
        }

        private void RefreshLayerHighlight()
        {
            for (int i = 0; i < _layerButtons.Length; i++)
            {
                bool on = (i == _game.CurrentLayer);
                _layerButtonBgs[i].color = on ? ActiveColor : InactiveColor;

                float scale = on ? 1.05f : 1f;
                _layerButtons[i].transform.localScale = Vector3.one * scale;
                var pressEffect = _layerButtons[i].GetComponent<LayeredButtonPressEffect>();
                if (pressEffect != null) pressEffect.SetBaseScale(scale);
            }
        }

        // ========== UI FACTORY ==========

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
            int fontSize, float height = 28f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject MakeButton(Transform parent, string name, string label,
            Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = size.x;
            le.minHeight = size.y;
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.30f, 1f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            go.AddComponent<LayeredButtonPressEffect>();

            return go;
        }
    }

    public class LayeredButtonPressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 _baseScale = Vector3.one;
        private Coroutine _anim;

        public void SetBaseScale(float scale)
        {
            _baseScale = Vector3.one * scale;
            if (_anim == null)
                transform.localScale = _baseScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(ScaleTo(_baseScale * 0.92f));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(ScaleTo(_baseScale));
        }

        private IEnumerator ScaleTo(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float duration = 0.04f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                transform.localScale = Vector3.Lerp(start, target, eased);
                yield return null;
            }
            transform.localScale = target;
            _anim = null;
        }
    }
}
