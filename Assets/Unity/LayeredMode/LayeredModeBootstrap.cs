using UnityEngine;

namespace Minesweeper3D.Unity.LayeredMode
{
    /// <summary>
    /// Simple bootstrap MonoBehaviour placed in the LayeredMode scene.
    /// Adds LayeredModeController to this GameObject on Awake.
    /// </summary>
    public class LayeredModeBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // Add the controller to this GameObject
            gameObject.AddComponent<LayeredModeController>();

            Debug.Log("[LayeredMode] Bootstrap initialized.");
        }
    }
}
