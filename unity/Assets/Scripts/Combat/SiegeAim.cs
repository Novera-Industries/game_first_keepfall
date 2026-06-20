using System;

namespace Keepfall.Combat
{
    /// <summary>One of the three lanes a siege deployment targets (source-of-truth §4 — 3 towers
    /// per side, one per lane). The mapping from a drag's horizontal position to a lane is in
    /// <see cref="SiegeAim"/>.</summary>
    public enum SiegeLane
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    /// <summary>
    /// The resolved result of a drag-and-release siege aim: which lane the unit deploys to and
    /// the launch arc (angle + normalized power) for the projectile/arc visualization. Plain
    /// data, no UnityEngine — the math is pure and testable; a MonoBehaviour adapter feeds it
    /// screen/world vectors from the Input System.
    /// </summary>
    public readonly struct SiegeAimResult
    {
        /// <summary>Lane the deployment targets (§4).</summary>
        public readonly SiegeLane Lane;

        /// <summary>Launch elevation angle in degrees, in [<see cref="SiegeAim.MinArcAngleDeg"/>,
        /// <see cref="SiegeAim.MaxArcAngleDeg"/>].</summary>
        public readonly float ArcAngleDegrees;

        /// <summary>Launch power normalized to [0, 1] from the drag distance (clamped).</summary>
        public readonly float Power;

        /// <summary>True if the drag was long enough to count as a release rather than a tap.</summary>
        public readonly bool IsValid;

        /// <summary>Creates a result.</summary>
        public SiegeAimResult(SiegeLane lane, float arcAngleDegrees, float power, bool isValid)
        {
            Lane = lane;
            ArcAngleDegrees = arcAngleDegrees;
            Power = power;
            IsValid = isValid;
        }
    }

    /// <summary>
    /// Pure drag-and-release siege aiming math (source-of-truth §4 "drag-and-release siege arc").
    /// Maps a drag vector (release point minus press point) plus the press's horizontal position
    /// to a target lane and a launch arc. Deliberately free of UnityEngine.Input — it takes
    /// plain numbers — so it is fully deterministic and EditMode-testable. A thin MonoBehaviour
    /// adapter (using the Input System, which this assembly already references) reads pointer
    /// events and calls <see cref="Resolve"/>.
    ///
    /// Convention: drag vector is in a Y-UP space where dragging DOWN/BACK (negative Y) loads the
    /// catapult (Angry-Birds-style "pull back to launch forward"). A longer pull → more power; a
    /// steeper pull → a higher arc. The press's normalized X (0=left edge, 1=right edge) selects
    /// the lane in even thirds.
    /// </summary>
    public static class SiegeAim
    {
        /// <summary>Minimum drag length (in normalized screen units) to register a launch.</summary>
        public const float MinDragToLaunch = 0.04f;

        /// <summary>Drag length (normalized) at which power saturates to 1.0.</summary>
        public const float MaxDragForFullPower = 0.5f;

        /// <summary>Flattest launch angle (degrees) — a low, fast, direct shot.</summary>
        public const float MinArcAngleDeg = 20f;

        /// <summary>Steepest launch angle (degrees) — a high lob over front defenses.</summary>
        public const float MaxArcAngleDeg = 75f;

        /// <summary>
        /// Resolves an aim from a drag.
        /// </summary>
        /// <param name="pressNormalizedX">Horizontal press position normalized to [0,1] across the
        /// play field (0 = far left, 1 = far right). Selects the lane in even thirds.</param>
        /// <param name="dragX">Drag delta X (release − press) in normalized screen units.</param>
        /// <param name="dragY">Drag delta Y (release − press) in normalized screen units, Y-up.
        /// Pulling back/down (negative) loads the shot.</param>
        public static SiegeAimResult Resolve(float pressNormalizedX, float dragX, float dragY)
        {
            float dragLength = (float)Math.Sqrt((dragX * dragX) + (dragY * dragY));

            SiegeLane lane = LaneFromNormalizedX(pressNormalizedX);

            // A drag shorter than the deadzone is a tap, not a launch.
            if (dragLength < MinDragToLaunch)
            {
                return new SiegeAimResult(lane, MinArcAngleDeg, 0f, false);
            }

            // Power scales with how far the player pulled, saturating at MaxDragForFullPower.
            float power = Clamp01(dragLength / MaxDragForFullPower);

            // Arc steepness comes from the pull's verticality: a pull that is mostly vertical
            // (|dragY| dominates) lobs high; a mostly-horizontal pull shoots flat. Use the
            // magnitude of the vertical fraction so pulling straight back gives the steepest arc.
            float verticalFraction = dragLength > 0f ? Math.Abs(dragY) / dragLength : 0f;
            float arc = Lerp(MinArcAngleDeg, MaxArcAngleDeg, Clamp01(verticalFraction));

            return new SiegeAimResult(lane, arc, power, true);
        }

        /// <summary>Maps a normalized X in [0,1] to one of three even lane thirds.</summary>
        public static SiegeLane LaneFromNormalizedX(float normalizedX)
        {
            float x = Clamp01(normalizedX);
            if (x < 1f / 3f)
            {
                return SiegeLane.Left;
            }

            return x < 2f / 3f ? SiegeLane.Center : SiegeLane.Right;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
    }
}
