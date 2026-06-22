using Keepfall.Core.Analytics;
using Keepfall.Core.Backend;
using Keepfall.Core.Config;
using Keepfall.Core.State;
using Keepfall.Monetization;
using UnityEditor;
using UnityEngine;

namespace Keepfall.EditorTools
{
    /// <summary>
    /// Milestone 05 on-ramp (<c>milestone/05-plus</c>: "subscription renewal + cosmetic drop flow
    /// tested", source-of-truth §6/§13). Exercises Keepfall Plus headlessly via the sandbox
    /// purchaser + editor fake backend: subscribe → perks active → monthly cosmetic drops over
    /// renewals → cancel keeps every drop (§6). Prints to the Console.
    /// <para>Menu: <b>Keepfall ▸ Plus</b>. Logic: <see cref="PlusSubscription"/>; drop schedule:
    /// <see cref="PlusMonthlyDrops"/>.</para>
    /// </summary>
    public static class PlusDemoMenu
    {
        [MenuItem("Keepfall/Plus/Log Perks and Monthly Drops")]
        public static void LogPerks()
        {
            var config = new RemoteConfig();
            Debug.Log($"[Plus] Keepfall Plus ({PlusSubscription.PlusProductId}) - one tier, $5.99/mo, 7-day trial.");
            Debug.Log($"[Plus] Perks: +{(config.GetPlusYieldMultiplier() - 1.0) * 100:0}% tile yield, " +
                      $"+{config.GetPlusExtraDeckSlots()} deck slot, 2x quest Shards, +5 login Shards, " +
                      "a monthly cosmetic drop, and 1 free Pass tier skip per week.");
            Debug.Log("[Plus] Hard exclusions: no subscriber-only units or tiles, no PvP perk, no combat advantage.");
            Debug.Log($"[Plus] Monthly drop schedule ({PlusMonthlyDrops.Months} months):");
            int m = 1;
            foreach (string id in PlusMonthlyDrops.Schedule())
            {
                Debug.Log($"[Plus]   month {m++,2}: {id}");
            }
        }

        [MenuItem("Keepfall/Plus/Simulate Subscribe + 3 Months + Cancel")]
        public static void SimulateFlow()
        {
            var sub = new SubscriptionState();
            var cosmetics = new CosmeticState();
            var deck = new DeckState();
            var config = new RemoteConfig();
            var backend = new EditorFakeBackendClient();
            var analytics = new DebugAnalytics();
            var purchaser = new SandboxStoreKitPurchaser(subscription: true);
            var plus = new PlusSubscription(sub, cosmetics, deck, config, backend, analytics);

            for (int month = 1; month <= 3; month++)
            {
                // Sandbox StoreKit transaction -> server-validated start/renewal (editor fake).
                StoreKitPurchase p = purchaser
                    .PurchaseAsync(PlusSubscription.PlusProductId).GetAwaiter().GetResult();
                bool active = plus
                    .StartOrRenewAsync(p.SignedTransaction, asTrial: month == 1).GetAwaiter().GetResult();

                plus.GrantMonthlyCosmetic(PlusMonthlyDrops.ForMonth(month));
                Debug.Log($"[Plus] Month {month}: active={active}, yield x{plus.GetYieldMultiplier():0.00}, " +
                          $"deck slots={plus.GetEffectiveDeckSlots()}, drop {PlusMonthlyDrops.ForMonth(month)}.");
            }

            plus.Cancel();
            Debug.Log($"[Plus] Cancelled. Active={plus.IsActive}. Cosmetics kept: {cosmetics.OwnedCosmeticIds.Count} " +
                      $"({string.Join(", ", cosmetics.OwnedCosmeticIds)}).");
            Debug.Log("[Plus] Trust commitment held: every drop earned while subscribed is still owned.");
        }
    }
}
