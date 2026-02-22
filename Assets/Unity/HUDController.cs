using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    public class HUDController : MonoBehaviour
    {
        private GameController _game;
        private SliceController _slice;
        private InputManager _input;
        private Canvas _canvas;
        private RectTransform _safeAreaRoot;

        // Info labels
        private TextMeshProUGUI _sliceText;
        private TextMeshProUGUI _mineText;
        private TextMeshProUGUI _flagText;
        private TextMeshProUGUI _timerText;
        private TextMeshProUGUI _statusText;

        // Buttons
        private Button[] _difficultyButtons = new Button[3];
        private Button _hintBtn;
        private TextMeshProUGUI _hintBtnText;
        private Button _newGameBtn;
        private bool _hintCooldown;

        // End panel
        private GameObject _endPanel;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endTime;
        private TextMeshProUGUI _endHints;
        private TextMeshProUGUI _endScore;
        private TextMeshProUGUI _endDifficulty;

        // Slice nav (right side)
        private TextMeshProUGUI _sliceNavText;

        // Slice flash (center)
        private TextMeshProUGUI _sliceFlash;
        private Coroutine _sliceFlashCoroutine;

        // Tutorial overlay
        private GameObject _tutorialOverlay;
        private static readonly string TutorialShownKey = "MineSweep3D_TutorialShown";


        // Remaining mine count (Feature 2)
        private TextMeshProUGUI _remainingInfoText;
        private GameObject _remainingInfoObj;

        // Slice arrow pulse
        private GameObject _sliceUpBtn;
        private GameObject _sliceDownBtn;
        private Coroutine _pulseCoroutine;

        // Mobile flag toggle
        private bool _flagMode;
        private Image _flagToggleBg;
        public bool IsFlagMode => _flagMode;

        public void Init(GameController game, SliceController slice, InputManager input)
        {
            _game = game;
            _slice = slice;
            _input = input;
            BuildUI();
            Refresh();
        }

        public void Rebind(SliceController newSlice)
        {
            _slice = newSlice;
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

            // SafeArea root — all UI panels go inside this
            var safeObj = new GameObject("SafeArea");
            safeObj.transform.SetParent(_canvas.transform, false);
            _safeAreaRoot = safeObj.AddComponent<RectTransform>();
            _safeAreaRoot.anchorMin = Vector2.zero;
            _safeAreaRoot.anchorMax = Vector2.one;
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
            safeObj.AddComponent<SafeAreaHandler>();

            // EventSystem
            EnsureEventSystem();

            // === TOP BAR: info labels ===
            BuildInfoBar();

            // === BUTTON BAR: [Easy] [Medium] [Hard] [Hint] [New Game] ===
            BuildButtonBar();

            // === END PANEL (hidden) ===
            BuildEndPanel();

            // === SLICE NAV (right side) ===
            BuildSliceNav();

            // === SLICE FLASH (center) ===
            BuildSliceFlash();

            // === REMAINING INFO (below info bar) ===
            BuildRemainingInfo();

            // === TUTORIAL OVERLAY (one-time) ===
            BuildTutorialOverlay();

        }

        private void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<EventSystem>();
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

        private void BuildInfoBar()
        {
            var panel = new GameObject("InfoBar");
            panel.transform.SetParent(_safeAreaRoot, false);
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

            _sliceText  = MakeLabel(panel.transform, "SliceText",  "Slice 1/6",      28, 40f);
            _mineText   = MakeLabel(panel.transform, "MineText",   "Mines: 10",      28, 40f);
            _flagText   = MakeLabel(panel.transform, "FlagText",   "Flags: 0",       28, 40f);
            _timerText  = MakeLabel(panel.transform, "TimerText",  "Time: 00:00",    28, 40f);
            _statusText = MakeLabel(panel.transform, "StatusText", "Click to start", 28, 40f);
        }

        private void BuildButtonBar()
        {
            var panel = new GameObject("ButtonBar");
            panel.transform.SetParent(_safeAreaRoot, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(176f, 400f);
            rt.anchoredPosition = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.18f, 0.5f);
            bg.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Difficulty buttons
            string[] labels = { "Easy", "Medium", "Hard" };
            Difficulty[] diffs = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                string name = labels[i];
                var btn = MakeButton(panel.transform, $"Btn_{name}", name, new Vector2(165f, 66f), 26, () =>
                {
                    _game.ApplyDifficulty(diffs[idx]);
                    RefreshDifficultyHighlight();
                });
                _difficultyButtons[i] = btn.GetComponent<Button>();
            }

            // Hint button
            var hintGo = MakeButton(panel.transform, "Btn_Hint", "Hint", new Vector2(165f, 66f), 26, () =>
            {
                OnHintClicked();
            });
            _hintBtn = hintGo.GetComponent<Button>();
            _hintBtnText = hintGo.GetComponentInChildren<TextMeshProUGUI>();

            // New Game button
            var ngGo = MakeButton(panel.transform, "Btn_NewGame", "New Game", new Vector2(165f, 66f), 26, () =>
            {
                _game.RestartGame();
                RefreshDifficultyHighlight();
            });
            _newGameBtn = ngGo.GetComponent<Button>();

            // Quit button
            MakeButton(panel.transform, "Btn_Quit", "Quit", new Vector2(165f, 66f), 26, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // Mobile flag toggle (since long press is now highlight, not flag)
            var flagGo = MakeButton(panel.transform, "Btn_FlagToggle", "Flag Mode", new Vector2(165f, 66f), 26, () =>
            {
                _flagMode = !_flagMode;
                _flagToggleBg.color = _flagMode ? ActiveColor : InactiveColor;
            });
            _flagToggleBg = flagGo.GetComponent<Image>();

            RefreshDifficultyHighlight();
        }

        private void BuildEndPanel()
        {
            _endPanel = new GameObject("EndPanel");
            _endPanel.transform.SetParent(_safeAreaRoot, false);
            var rt = _endPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 500f);
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

            _endTitle      = MakeLabel(_endPanel.transform, "EndTitle", "", 48, 40f);
            _endTime       = MakeLabel(_endPanel.transform, "EndTime", "", 30, 36f);
            _endHints      = MakeLabel(_endPanel.transform, "EndHints", "", 30, 36f);
            _endScore      = MakeLabel(_endPanel.transform, "EndScore", "", 34, 36f);
            _endDifficulty = MakeLabel(_endPanel.transform, "EndDifficulty", "", 26, 36f);
            _endDifficulty.color = new Color(0.7f, 0.7f, 0.7f);

            MakeButton(_endPanel.transform, "Btn_EndNewGame", "New Game", new Vector2(280f, 65f), 24, () =>
            {
                _endPanel.SetActive(false);
                _input?.SetEnabled(true);
                _game.RestartGame();
                RefreshDifficultyHighlight();
            });

            _endPanel.SetActive(false);
        }

        private void BuildSliceNav()
        {
            var panel = new GameObject("SliceNav");
            panel.transform.SetParent(_safeAreaRoot, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 260f);
            rt.anchoredPosition = new Vector2(-5f, 0f);

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.18f, 0.4f);
            bg.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // UP arrow
            _sliceUpBtn = MakeButton(panel.transform, "Btn_SliceUp", "\u25B2", new Vector2(90f, 90f), 24, () =>
            {
                _game.HandleSliceChangePublic(1);
            });

            // Slice label
            _sliceNavText = MakeLabel(panel.transform, "SliceNavText", "1/4", 30, 60f);
            _sliceNavText.GetComponent<RectTransform>().sizeDelta = new Vector2(90f, 60f);

            // DOWN arrow
            _sliceDownBtn = MakeButton(panel.transform, "Btn_SliceDown", "\u25BC", new Vector2(90f, 90f), 24, () =>
            {
                _game.HandleSliceChangePublic(-1);
            });
        }

        private void BuildSliceFlash()
        {
            var go = new GameObject("SliceFlash");
            go.transform.SetParent(_safeAreaRoot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 80f);
            rt.anchoredPosition = Vector2.zero;
            _sliceFlash = go.AddComponent<TextMeshProUGUI>();
            _sliceFlash.fontSize = 48;
            _sliceFlash.color = Color.clear;
            _sliceFlash.alignment = TextAlignmentOptions.Center;
            _sliceFlash.raycastTarget = false;
        }

        public void FlashSliceIndicator()
        {
            if (_sliceFlashCoroutine != null)
                StopCoroutine(_sliceFlashCoroutine);
            _sliceFlashCoroutine = StartCoroutine(SliceFlashRoutine());
        }

        private IEnumerator SliceFlashRoutine()
        {
            _sliceFlash.text = $"{_slice.CurrentSlice + 1}/{_slice.Size}";
            _sliceFlash.color = new Color(1f, 1f, 1f, 0.9f);
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(0.9f, 0f, elapsed / duration);
                _sliceFlash.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            _sliceFlash.color = Color.clear;
            _sliceFlashCoroutine = null;
        }

        private void BuildTutorialOverlay()
        {
            if (PlayerPrefs.GetInt(TutorialShownKey, 0) == 1) return;

            _tutorialOverlay = new GameObject("TutorialOverlay");
            _tutorialOverlay.transform.SetParent(_canvas.transform, false);
            var rt = _tutorialOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            // Semi-transparent backdrop
            var bg = _tutorialOverlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = true;

            // Tutorial text
            var textGo = new GameObject("TutorialText");
            textGo.transform.SetParent(_tutorialOverlay.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(700f, 250f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Tap to reveal  |  Long press to flag  |  Arrows to change slice";
            tmp.fontSize = 30;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // Dismiss hint
            var hintGo = new GameObject("TapHint");
            hintGo.transform.SetParent(_tutorialOverlay.transform, false);
            var hintRt = hintGo.AddComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0.3f);
            hintRt.anchorMax = new Vector2(0.5f, 0.3f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.sizeDelta = new Vector2(400f, 50f);
            var hint = hintGo.AddComponent<TextMeshProUGUI>();
            hint.text = "Tap anywhere to start";
            hint.fontSize = 20;
            hint.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.raycastTarget = false;

            // Invisible button to catch tap
            var btnGo = new GameObject("DismissBtn");
            btnGo.transform.SetParent(_tutorialOverlay.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = Vector2.zero;
            btnRt.anchorMax = Vector2.one;
            btnRt.sizeDelta = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = Color.clear;
            btnImg.raycastTarget = true;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                _tutorialOverlay.SetActive(false);
                PlayerPrefs.SetInt(TutorialShownKey, 1);
                PlayerPrefs.Save();
            });
        }

        private void BuildRemainingInfo()
        {
            _remainingInfoObj = new GameObject("RemainingInfo");
            _remainingInfoObj.transform.SetParent(_safeAreaRoot, false);
            var rt = _remainingInfoObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(400f, 40f);
            rt.anchoredPosition = new Vector2(0f, -75f);
            _remainingInfoText = _remainingInfoObj.AddComponent<TextMeshProUGUI>();
            _remainingInfoText.fontSize = 24;
            _remainingInfoText.color = new Color(1f, 0.9f, 0.3f);
            _remainingInfoText.alignment = TextAlignmentOptions.Center;
            _remainingInfoText.raycastTarget = false;
            _remainingInfoText.text = "";
            _remainingInfoObj.SetActive(false);
        }

        public void ShowRemainingInfo(int cellCount, int remaining)
        {
            if (_remainingInfoObj == null) return;
            _remainingInfoText.text = $"Selected: {cellCount} ({remaining} remaining)";
            _remainingInfoObj.SetActive(true);
        }

        public void HideRemainingInfo()
        {
            if (_remainingInfoObj != null)
                _remainingInfoObj.SetActive(false);
        }

        public void PulseSliceArrow(int direction)
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            GameObject target = direction > 0 ? _sliceUpBtn : _sliceDownBtn;
            if (target != null)
                _pulseCoroutine = StartCoroutine(PulseArrowRoutine(target));
        }

        private IEnumerator PulseArrowRoutine(GameObject arrowBtn)
        {
            var img = arrowBtn.GetComponent<Image>();
            if (img == null) yield break;
            Color original = img.color;
            Color pulse = new Color(1f, 0.9f, 0.2f, 1f);
            float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.PingPong(elapsed * 4f, 1f);
                img.color = Color.Lerp(original, pulse, t);
                yield return null;
            }
            img.color = original;
            _pulseCoroutine = null;
        }

        // ========== REFRESH ==========

        public void Refresh()
        {
            if (_slice == null) return;

            _sliceText.text = $"Slice {_slice.CurrentSlice + 1}/{_slice.Size}";
            _timerText.text = $"Time: {_game.Timer.FormattedTime}";
            if (_sliceNavText != null)
                _sliceNavText.text = $"{_slice.CurrentSlice + 1}/{_slice.Size}";

            Board board = _game.Board;
            if (board == null)
            {
                _statusText.text = "Click to start";
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
                    _statusText.text = "Playing";
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
            _input?.SetEnabled(false);

            if (isWin)
            {
                _endTitle.text = "YOU WIN!";
                _endTitle.color = new Color(0.2f, 0.9f, 0.2f);
                _endScore.gameObject.SetActive(true);
                _endScore.text = $"Score: {ScoreController.Calculate(_game.GridSize, _game.Timer.ElapsedSeconds, _game.HintsUsed)}";
            }
            else
            {
                _endTitle.text = "GAME OVER";
                _endTitle.color = new Color(0.9f, 0.2f, 0.2f);
                _endScore.gameObject.SetActive(false);
            }

            _endTime.text = $"Time: {_game.Timer.FormattedTime}";
            _endHints.text = $"Hints Used: {_game.HintsUsed}";
            _endDifficulty.text = _game.IsCustomGame
                ? $"Difficulty: Custom ({_game.GridSize}^3, {_game.MineCount} mines)"
                : $"Difficulty: {_game.CurrentDifficulty}";
        }

        // ========== DIFFICULTY HIGHLIGHT ==========

        private static readonly Color ActiveColor = new Color(0.3f, 0.55f, 0.85f, 1f);
        private static readonly Color InactiveColor = new Color(0.25f, 0.25f, 0.30f, 1f);

        private void RefreshDifficultyHighlight()
        {
            Difficulty[] diffs = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
            for (int i = 0; i < 3; i++)
            {
                bool on = !_game.IsCustomGame && diffs[i] == _game.CurrentDifficulty;
                _difficultyButtons[i].GetComponent<Image>().color = on ? ActiveColor : InactiveColor;
            }
        }

        // ========== HINT ==========

        private void OnHintClicked()
        {
            if (_hintCooldown) return;
            Board board = _game.Board;
            if (board == null || board.Status != GameStatus.Playing) return;

            var solver = new Solver();
            var steps = solver.SolveStep(board);
            var safeStep = steps.FirstOrDefault(s => !s.InferredMine);

            if (safeStep != null && safeStep.AffectedCells.Length > 0)
            {
                var coord = safeStep.AffectedCells[0];
                _game.HintsUsed++;
                _hintCooldown = true;

                if (coord.Z != _slice.CurrentSlice)
                    _slice.SetSlice(coord.Z);

                _game.Slice.GetCell(coord).HighlightHint();
                StartCoroutine(ResetHintCooldown(3f));
            }
            else
            {
                _hintBtnText.text = "No hints";
                StartCoroutine(ResetHintText(2f));
            }
        }

        private IEnumerator ResetHintCooldown(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hintCooldown = false;
        }

        private IEnumerator ResetHintText(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hintBtnText.text = "Hint";
        }

        // ========== UI FACTORY ==========

        private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text, int fontSize, float height = 28f)
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

        private static GameObject MakeButton(Transform parent, string name, string label, Vector2 size, int fontSize, UnityEngine.Events.UnityAction onClick)
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

            return go;
        }
    }
}
