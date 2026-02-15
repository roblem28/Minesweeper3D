using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Canvas-based HUD. Programmatically builds the entire UI hierarchy.
    /// Scales with screen size via CanvasScaler (1080x1920 reference, match 0.5).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        private GameController _game;
        private SliceController _slice;
        private InputManager _input;

        // Top bar texts
        private TextMeshProUGUI _sliceText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _mineText;
        private TextMeshProUGUI _flagText;
        private TextMeshProUGUI _safeText;
        private TextMeshProUGUI _timerText;

        // Bottom
        private TextMeshProUGUI _controlsHint;
        private GameObject _settingsPanel;

        // End panel
        private GameObject _endPanel;
        private TextMeshProUGUI _endTitle;
        private TextMeshProUGUI _endTime;
        private TextMeshProUGUI _endHints;
        private TextMeshProUGUI _endScore;
        private TextMeshProUGUI _endDifficulty;

        // Settings
        private Slider _gridSizeSlider;
        private Slider _mineCountSlider;
        private TextMeshProUGUI _gridSizeLabel;
        private TextMeshProUGUI _mineCountLabel;

        // Difficulty buttons
        private Button[] _difficultyButtons = new Button[3];
        private TextMeshProUGUI _customLabel;

        // Hint
        private GameObject _hintButton;
        private TextMeshProUGUI _hintButtonText;
        private bool _hintCooldown;

        // Mobile slice buttons
        private GameObject _mobileSlicePanel;

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

        public void Refresh()
        {
            if (_slice == null) return;

            _sliceText.text = $"Slice {_slice.CurrentSlice + 1}/{_slice.Size}";
            _timerText.text = $"Time: {_game.Timer.FormattedTime}";

            Board board = _game.Board;
            if (board == null)
            {
                _statusText.text = "Click to start";
                _statusText.color = Color.white;
                _mineText.text = $"Mines: {_game.MineCount}";
                _flagText.text = "Flags: 0";
                _safeText.text = "";
                _endPanel.SetActive(false);
                return;
            }

            _mineText.text = $"Mines: {_game.MineCount}";
            _flagText.text = $"Flags: {board.FlagCount}";
            _safeText.text = $"Safe Left: {board.TotalSafe - board.RevealedSafeCount}";

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

        private void BuildUI()
        {
            // Canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // EventSystem — required for UI buttons/sliders to receive clicks
            var existingES = FindFirstObjectByType<EventSystem>();
            if (existingES == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                existingES = eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            // Ensure correct input module — InputSystemUIInputModule for New Input System
            if (existingES.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                // Remove legacy module immediately (Destroy is deferred, DestroyImmediate is not)
                var legacy = existingES.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                    DestroyImmediate(legacy);
                existingES.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Top panel
            var topPanel = CreatePanel("TopPanel", canvas.transform);
            var topRect = topPanel.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, 100f);
            topRect.anchoredPosition = Vector2.zero;

            var topBg = topPanel.AddComponent<Image>();
            topBg.color = new Color(0f, 0f, 0f, 0.65f);
            topBg.raycastTarget = false; // Don't block clicks to elements underneath

            var topLayout = topPanel.AddComponent<HorizontalLayoutGroup>();
            topLayout.padding = new RectOffset(20, 20, 10, 10);
            topLayout.spacing = 30f;
            topLayout.childAlignment = TextAnchor.MiddleCenter;
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = true;
            topLayout.childForceExpandHeight = false;

            _sliceText = CreateLabel(topPanel.transform, "SliceText", "Slice 1/6", 32);
            _mineText = CreateLabel(topPanel.transform, "MineText", "Mines: 10", 32);
            _flagText = CreateLabel(topPanel.transform, "FlagText", "Flags: 0", 32);
            _safeText = CreateLabel(topPanel.transform, "SafeText", "", 32);
            _timerText = CreateLabel(topPanel.transform, "TimerText", "Time: 00:00", 32);
            _statusText = CreateLabel(topPanel.transform, "StatusText", "Click to start", 32);

            // Difficulty buttons row (below top bar)
            var diffPanel = CreatePanel("DifficultyPanel", canvas.transform);
            var diffRect = diffPanel.GetComponent<RectTransform>();
            diffRect.anchorMin = new Vector2(0f, 1f);
            diffRect.anchorMax = new Vector2(1f, 1f);
            diffRect.pivot = new Vector2(0.5f, 1f);
            diffRect.sizeDelta = new Vector2(0f, 60f);
            diffRect.anchoredPosition = new Vector2(0f, -100f);

            var diffLayout = diffPanel.AddComponent<HorizontalLayoutGroup>();
            diffLayout.padding = new RectOffset(20, 20, 5, 5);
            diffLayout.spacing = 15f;
            diffLayout.childAlignment = TextAnchor.MiddleLeft;
            diffLayout.childControlWidth = false;
            diffLayout.childControlHeight = true;
            diffLayout.childForceExpandWidth = false;
            diffLayout.childForceExpandHeight = false;

            string[] labels = { "Easy", "Medium", "Hard" };
            Difficulty[] values = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = CreateButton($"Diff_{labels[i]}", diffPanel.transform, labels[i], () =>
                {
                    _game.ApplyDifficulty(values[idx]);
                    SyncSlidersToGame();
                    RefreshDifficultyButtons();
                });
                var btnRect = btn.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(150f, 50f);
                btn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 26;
                _difficultyButtons[i] = btn.GetComponent<Button>();
            }

            // "Custom" label (shown when sliders don't match any preset)
            _customLabel = CreateLabel(diffPanel.transform, "CustomLabel", "Custom", 24);
            _customLabel.color = new Color(0.8f, 0.65f, 0.3f);
            var customRect = _customLabel.GetComponent<RectTransform>();
            customRect.sizeDelta = new Vector2(110f, 50f);
            var customLayout = _customLabel.gameObject.AddComponent<LayoutElement>();
            customLayout.preferredWidth = 110f;

            RefreshDifficultyButtons();

            // Hint button
            _hintButton = CreateButton("HintButton", diffPanel.transform, "Hint", OnHintClicked);
            var hintBtnRect = _hintButton.GetComponent<RectTransform>();
            hintBtnRect.sizeDelta = new Vector2(120f, 50f);
            _hintButtonText = _hintButton.GetComponentInChildren<TextMeshProUGUI>();
            _hintButtonText.fontSize = 26;

            // New Game button
            var newGameBtn = CreateButton("NewGameButton", diffPanel.transform, "New Game", () =>
            {
                _game.RestartGame();
                RefreshDifficultyButtons();
            });
            var ngRect = newGameBtn.GetComponent<RectTransform>();
            ngRect.sizeDelta = new Vector2(180f, 50f);
            newGameBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 26;

            // End-game panel (hidden by default)
            BuildEndPanel(canvas.transform);

            // Controls hint (bottom-left)
            var hintObj = new GameObject("ControlsHint");
            hintObj.transform.SetParent(canvas.transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(0f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.sizeDelta = new Vector2(500f, 80f);
            hintRect.anchoredPosition = new Vector2(20f, 10f);
            _controlsHint = hintObj.AddComponent<TextMeshProUGUI>();
            _controlsHint.text = "LMB: Reveal | RMB: Flag | Scroll: Slice | Ctrl+Scroll: Zoom | MMB: Orbit";
            _controlsHint.fontSize = 22;
            _controlsHint.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            _controlsHint.raycastTarget = false;

            // Settings button (top-right corner)
            var settingsBtn = CreateButton("SettingsButton", canvas.transform, "Settings", ToggleSettings);
            var sBtnRect = settingsBtn.GetComponent<RectTransform>();
            sBtnRect.anchorMin = new Vector2(1f, 1f);
            sBtnRect.anchorMax = new Vector2(1f, 1f);
            sBtnRect.pivot = new Vector2(1f, 1f);
            sBtnRect.sizeDelta = new Vector2(180f, 60f);
            sBtnRect.anchoredPosition = new Vector2(-20f, -110f);

            // Settings panel (hidden by default)
            BuildSettingsPanel(canvas.transform);

            // Mobile slice buttons
            BuildMobileSliceButtons(canvas.transform);
        }

        private void BuildEndPanel(Transform parent)
        {
            _endPanel = new GameObject("EndPanel");
            _endPanel.transform.SetParent(parent, false);

            var panelRect = _endPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 450f);
            panelRect.anchoredPosition = Vector2.zero;

            var bg = _endPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            var layout = _endPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 15f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _endTitle = CreateLabel(_endPanel.transform, "EndTitle", "", 48);
            _endTime = CreateLabel(_endPanel.transform, "EndTime", "", 32);
            _endHints = CreateLabel(_endPanel.transform, "EndHints", "", 32);
            _endScore = CreateLabel(_endPanel.transform, "EndScore", "", 36);
            _endDifficulty = CreateLabel(_endPanel.transform, "EndDifficulty", "", 28);
            _endDifficulty.color = new Color(0.7f, 0.7f, 0.7f);

            CreateButton("EndNewGame", _endPanel.transform, "New Game", () =>
            {
                _endPanel.SetActive(false);
                _input?.SetEnabled(true);
                _game.RestartGame();
                RefreshDifficultyButtons();
            });

            _endPanel.SetActive(false);
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

        private void BuildSettingsPanel(Transform parent)
        {
            _settingsPanel = new GameObject("SettingsPanel");
            _settingsPanel.transform.SetParent(parent, false);

            var panelRect = _settingsPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 400f);
            panelRect.anchoredPosition = Vector2.zero;

            var bg = _settingsPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            var layout = _settingsPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 30, 30);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Title
            CreateLabel(_settingsPanel.transform, "Title", "Settings", 40);

            // Grid size slider
            _gridSizeLabel = CreateLabel(_settingsPanel.transform, "GridSizeLabel",
                $"Grid Size: {_game.GridSize}", 28);
            _gridSizeSlider = CreateSlider(_settingsPanel.transform, "GridSizeSlider", 3, 10, _game.GridSize);
            _gridSizeSlider.wholeNumbers = true;
            _gridSizeSlider.onValueChanged.AddListener(v =>
            {
                _gridSizeLabel.text = $"Grid Size: {(int)v}";
                ClampMineCount();
            });

            // Mine count slider
            _mineCountLabel = CreateLabel(_settingsPanel.transform, "MineCountLabel",
                $"Mine Count: {_game.MineCount}", 28);
            int maxMines = MaxMinesForSize(_game.GridSize);
            _mineCountSlider = CreateSlider(_settingsPanel.transform, "MineCountSlider", 1, maxMines, _game.MineCount);
            _mineCountSlider.wholeNumbers = true;
            _mineCountSlider.onValueChanged.AddListener(v =>
            {
                _mineCountLabel.text = $"Mine Count: {(int)v}";
            });

            // Apply button
            CreateButton("ApplyButton", _settingsPanel.transform, "Apply", () =>
            {
                int gs = (int)_gridSizeSlider.value;
                int mc = (int)_mineCountSlider.value;
                _game.RestartWithSettings(gs, mc);
                _settingsPanel.SetActive(false);
                _input?.SetEnabled(true);
                RefreshDifficultyButtons();
            });

            // Close button
            CreateButton("CloseButton", _settingsPanel.transform, "Close", () =>
            {
                _settingsPanel.SetActive(false);
                _input?.SetEnabled(true);
            });

            _settingsPanel.SetActive(false);
        }

        private void BuildMobileSliceButtons(Transform parent)
        {
            _mobileSlicePanel = new GameObject("MobileSlicePanel");
            _mobileSlicePanel.transform.SetParent(parent, false);

            var panelRect = _mobileSlicePanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(120f, 250f);
            panelRect.anchoredPosition = new Vector2(-20f, 0f);

            var layout = _mobileSlicePanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var upBtn = CreateButton("SliceUp", _mobileSlicePanel.transform, "\u25b2", () =>
            {
                _slice?.SetSlice(_slice.CurrentSlice + 1);
                Refresh();
            });
            var upRect = upBtn.GetComponent<RectTransform>();
            upRect.sizeDelta = new Vector2(100f, 100f);

            var downBtn = CreateButton("SliceDown", _mobileSlicePanel.transform, "\u25bc", () =>
            {
                _slice?.SetSlice(_slice.CurrentSlice - 1);
                Refresh();
            });
            var downRect = downBtn.GetComponent<RectTransform>();
            downRect.sizeDelta = new Vector2(100f, 100f);

            // Only show on touch devices
            bool isTouchDevice = UnityEngine.InputSystem.Touchscreen.current != null;
            _mobileSlicePanel.SetActive(isTouchDevice);
        }

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
                _hintButtonText.text = "No hints";
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
            _hintButtonText.text = "Hint";
        }

        private static readonly Color DiffActiveNormal = new Color(0.3f, 0.55f, 0.85f, 0.95f);
        private static readonly Color DiffActiveHighlighted = new Color(0.5f, 0.75f, 1f, 1f);
        private static readonly Color DiffActivePressed = new Color(0.2f, 0.45f, 0.75f, 1f);

        private void RefreshDifficultyButtons()
        {
            Difficulty[] values = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
            bool custom = _game.IsCustomGame;

            for (int i = 0; i < _difficultyButtons.Length; i++)
            {
                bool active = !custom && values[i] == _game.CurrentDifficulty;
                var cb = _difficultyButtons[i].colors;
                cb.normalColor = active ? DiffActiveNormal : ButtonNormal;
                cb.highlightedColor = active ? DiffActiveHighlighted : ButtonHighlighted;
                cb.pressedColor = active ? DiffActivePressed : ButtonPressed;
                _difficultyButtons[i].colors = cb;
            }

            _customLabel.gameObject.SetActive(custom);
        }

        private void SyncSlidersToGame()
        {
            _gridSizeSlider.value = _game.GridSize;
            _gridSizeLabel.text = $"Grid Size: {_game.GridSize}";
            ClampMineCount();
            _mineCountSlider.value = _game.MineCount;
            _mineCountLabel.text = $"Mine Count: {_game.MineCount}";
        }

        private void ToggleSettings()
        {
            bool open = !_settingsPanel.activeSelf;
            _settingsPanel.SetActive(open);
            _input?.SetEnabled(!open);

            if (open)
                SyncSlidersToGame();
        }

        private void ClampMineCount()
        {
            int gs = (int)_gridSizeSlider.value;
            int maxMines = MaxMinesForSize(gs);
            _mineCountSlider.maxValue = maxMines;
            if (_mineCountSlider.value > maxMines)
                _mineCountSlider.value = maxMines;
        }

        private static int MaxMinesForSize(int gs)
        {
            // Max mines = total cells - 27 (first-click safety zone)
            int total = gs * gs * gs;
            return Mathf.Max(1, total - 27);
        }

        // ===== UI Factory Helpers =====

        private static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 50f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false; // Labels must not intercept button clicks
            return tmp;
        }

        private static readonly Color ButtonNormal = new Color(0.25f, 0.25f, 0.30f, 0.9f);
        private static readonly Color ButtonHighlighted = new Color(0.45f, 0.45f, 0.50f, 0.95f);
        private static readonly Color ButtonPressed = new Color(0.15f, 0.15f, 0.20f, 0.95f);
        private static readonly Color ButtonSelected = new Color(0.35f, 0.35f, 0.40f, 0.9f);

        private static GameObject CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 60f);

            var bg = go.AddComponent<Image>();
            bg.color = Color.white; // Button.colors multiplies against Image.color, so use white

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = ButtonNormal,
                highlightedColor = ButtonHighlighted,
                pressedColor = ButtonPressed,
                selectedColor = ButtonSelected,
                disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };
            btn.onClick.AddListener(onClick);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(go.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 30;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return go;
        }

        private static Slider CreateSlider(Transform parent, string name, float min, float max, float value)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 40f);

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(go.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f);

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.4f, 0.6f, 0.9f);

            // Handle slide area
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-20f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(30f, 0f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            // Slider component
            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            return slider;
        }
    }
}
