using System.Collections;
using UnityEngine;
using Minesweeper3D.Core;

namespace Minesweeper3D.Unity
{
    /// <summary>
    /// Creates ALL NxNxN cells as a 3D grid. The active slice is opaque and
    /// interactive; other slices render as wireframe ghosts.
    /// Animated 0.15s fade transition when changing slices.
    /// </summary>
    public class SliceController : MonoBehaviour
    {
        private int _size;
        private int _currentSlice;
        private GameController _game;
        private CellView[,,] _cells;
        private Coroutine _transition;

        public const float Spacing = 1.3f;
        private const float CubeScale = 0.6f;
        private const float TransitionDuration = 0.15f;

        public int CurrentSlice => _currentSlice;
        public int Size => _size;

        public CellView GetCell(Coord3 c) => _cells[c.X, c.Y, c.Z];

        public void Init(int size, GameController game)
        {
            _size = size;
            _game = game;
            _currentSlice = 0;
            _cells = new CellView[size, size, size];

            // Pass inspector-assigned materials to CellView before building grid
            CellView.SetSharedMaterials(game.OpaqueMaterial, game.GhostMaterial);

            BuildGrid();
            CreateFloorPlane();
            RefreshAll();
        }

        private void BuildGrid()
        {
            float offset = (_size - 1) * Spacing * 0.5f;

            for (int z = 0; z < _size; z++)
            {
                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = $"Cell_{x}_{y}_{z}";
                        go.transform.SetParent(transform);
                        go.transform.localPosition = new Vector3(
                            x * Spacing - offset,
                            y * Spacing - offset,
                            z * Spacing - offset
                        );
                        go.transform.localScale = Vector3.one * CubeScale;

                        var cell = go.AddComponent<CellView>();
                        cell.Init(x, y, z);
                        _cells[x, y, z] = cell;
                    }
                }
            }
        }

        private void CreateFloorPlane()
        {
            float offset = (_size - 1) * Spacing * 0.5f;
            float floorY = -offset - 0.5f;
            float planeSize = _size * Spacing * 1.5f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "FloorPlane";
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0f, floorY, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * planeSize;

            // Remove collider so it doesn't interfere with cell raycasts
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Use inspector-assigned floor material — no Shader.Find, no new Material
            var renderer = go.GetComponent<Renderer>();
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (_game.FloorMaterial != null)
                renderer.sharedMaterial = _game.FloorMaterial;
        }

        public void SetSlice(int z)
        {
            z = Mathf.Clamp(z, 0, _size - 1);
            if (z == _currentSlice) return;

            int from = _currentSlice;
            _currentSlice = z;

            // Cancel previous transition if slice changes rapidly
            if (_transition != null)
                StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionSlice(from, z));
        }

        private IEnumerator TransitionSlice(int from, int to)
        {
            Board board = _game.Board;
            bool gameOver = board != null && board.Status != GameStatus.Playing;

            // Immediately update colliders (new slice interactive right away)
            for (int y = 0; y < _size; y++)
                for (int x = 0; x < _size; x++)
                {
                    _cells[x, y, from].SetActiveSlice(false);
                    _cells[x, y, to].SetActiveSlice(true);
                }

            // Animate over TransitionDuration
            float elapsed = 0f;
            while (elapsed < TransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / TransitionDuration);

                // Fade old slice from active to ghost
                float oldAlpha = Mathf.Lerp(1f, 0.08f, t);
                for (int y = 0; y < _size; y++)
                    for (int x = 0; x < _size; x++)
                        _cells[x, y, from].SetTransitionAlpha(oldAlpha);

                // Fade new slice from ghost to active
                float newAlpha = Mathf.Lerp(0.08f, 1f, t);
                for (int y = 0; y < _size; y++)
                    for (int x = 0; x < _size; x++)
                        _cells[x, y, to].SetTransitionAlpha(newAlpha);

                yield return null;
            }

            // Final state — full refresh to clean up
            RefreshAll();
            _transition = null;
        }

        public void RefreshAll()
        {
            Board board = _game.Board;
            bool gameOver = board != null && board.Status != GameStatus.Playing;

            for (int z = 0; z < _size; z++)
            {
                bool active = (z == _currentSlice);

                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        var cell = _cells[x, y, z];
                        cell.SetActiveSlice(active);

                        if (board != null)
                        {
                            var coord = new Coord3(x, y, z);
                            cell.UpdateVisual(
                                board.GetState(coord),
                                board.GetCount(coord),
                                board.IsMine(coord),
                                gameOver
                            );
                        }
                        else
                        {
                            cell.UpdateVisual(CellState.Hidden, 0, false, false);
                        }
                    }
                }
            }
        }
    }
}
