using Keepfall.Combat;
using Keepfall.Core.Config;
using NUnit.Framework;

namespace Keepfall.Tests
{
    /// <summary>
    /// Milestone 06 ("difficulty curve locked", source-of-truth §4/§13). Pins
    /// <see cref="AiController.SelectTier"/>: the AI tier is chosen by UNLOCKED-UNIT COUNT (roster
    /// expansion), never by days, against the canonical <c>ai.threshold.*</c> values
    /// (0 / 8 / 12 / 18 / 22). The curve must be monotonic non-decreasing so progression never
    /// makes the game easier, and a full 24-unit roster faces Marshal.
    /// </summary>
    public sealed class DifficultyCurveTests
    {
        private static AiTier Tier(int unlockedUnits) =>
            AiController.SelectTier(unlockedUnits, new RemoteConfig());

        [Test]
        public void SelectTier_MapsRosterSizeToCanonicalTiers_AtBoundaries()
        {
            // Apprentice: 0–7
            Assert.AreEqual(AiTier.Apprentice, Tier(0));
            Assert.AreEqual(AiTier.Apprentice, Tier(7));
            // Adept: 8–11
            Assert.AreEqual(AiTier.Adept, Tier(8));
            Assert.AreEqual(AiTier.Adept, Tier(11));
            // Tactician: 12–17
            Assert.AreEqual(AiTier.Tactician, Tier(12));
            Assert.AreEqual(AiTier.Tactician, Tier(17));
            // Commander: 18–21
            Assert.AreEqual(AiTier.Commander, Tier(18));
            Assert.AreEqual(AiTier.Commander, Tier(21));
            // Marshal: 22+ (24 is the full roster)
            Assert.AreEqual(AiTier.Marshal, Tier(22));
            Assert.AreEqual(AiTier.Marshal, Tier(24));
        }

        [Test]
        public void SelectTier_IsMonotonicNonDecreasing_AcrossTheWholeRoster()
        {
            AiTier previous = Tier(0);
            for (int count = 1; count <= 24; count++)
            {
                AiTier current = Tier(count);
                Assert.GreaterOrEqual((int)current, (int)previous,
                    $"Tier must never drop as the roster grows (count {count}).");
                previous = current;
            }
        }

        [Test]
        public void SelectTier_FloorsAtApprentice_AndPeaksAtMarshal()
        {
            Assert.AreEqual(AiTier.Apprentice, Tier(0), "Empty roster faces the Apprentice floor.");
            Assert.AreEqual(AiTier.Marshal, Tier(100), "Beyond the top threshold stays Marshal.");
        }

        [Test]
        public void AllFiveTiers_AreReachable_AsTheRosterExpands()
        {
            var seen = new System.Collections.Generic.HashSet<AiTier>();
            for (int count = 0; count <= 24; count++)
            {
                seen.Add(Tier(count));
            }

            Assert.AreEqual(5, seen.Count, "All five tiers occur across a 0..24 roster.");
        }
    }
}
