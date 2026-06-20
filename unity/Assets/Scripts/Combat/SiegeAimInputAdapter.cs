using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Keepfall.Combat
{
    /// <summary>
    /// MonoBehaviour adapter that turns drag-and-release pointer input into a <see cref="SiegeAimResult"/>
    /// via the pure <see cref="SiegeAim"/> math (source-of-truth §4). This is the ONLY siege-aim
    /// type that touches UnityEngine / the Input System; all the geometry lives in the pure,
    /// testable <see cref="SiegeAim"/> so the simulation stays deterministic and EditMode-coverable.
    ///
    /// It reads the active <see cref="Pointer"/> (touch on device, mouse in editor), normalizes
    /// press/release positions against the screen, and raises <see cref="OnSiegeAimed"/> on
    /// release with a resolved lane + arc. No gameplay decisions are made here — it only adapts
    /// input to the aim contract.
    /// </summary>
    public sealed class SiegeAimInputAdapter : MonoBehaviour
    {
        /// <summary>Raised on release with the resolved aim. Only fires for valid (non-tap) drags.</summary>
        public event Action<SiegeAimResult> OnSiegeAimed;

        /// <summary>Raised continuously while dragging, for live arc preview. May be invalid.</summary>
        public event Action<SiegeAimResult> OnSiegeAimPreview;

        private bool _dragging;
        private Vector2 _pressScreen;

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return; // no pointing device this frame.
            }

            bool isPressed = pointer.press.isPressed;
            Vector2 position = pointer.position.ReadValue();

            if (isPressed && !_dragging)
            {
                // Press began.
                _dragging = true;
                _pressScreen = position;
                return;
            }

            if (isPressed && _dragging)
            {
                // Dragging — emit a live preview so the UI can draw the arc.
                OnSiegeAimPreview?.Invoke(Resolve(_pressScreen, position));
                return;
            }

            if (!isPressed && _dragging)
            {
                // Released — resolve and fire if it was a real drag (not a tap).
                _dragging = false;
                SiegeAimResult result = Resolve(_pressScreen, position);
                if (result.IsValid)
                {
                    OnSiegeAimed?.Invoke(result);
                }
            }
        }

        private static SiegeAimResult Resolve(Vector2 pressScreen, Vector2 releaseScreen)
        {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);

            float pressNormalizedX = pressScreen.x / w;
            // Drag delta in normalized screen units, Y-up (matches SiegeAim's convention).
            float dragX = (releaseScreen.x - pressScreen.x) / w;
            float dragY = (releaseScreen.y - pressScreen.y) / h;

            return SiegeAim.Resolve(pressNormalizedX, dragX, dragY);
        }
    }
}
