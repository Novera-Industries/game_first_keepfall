# Keepfall — Roster (24 units)

> Traces to **`docs/00-source-of-truth.md`** §2 (unlock cost ladder) and §3 (roster, 6 roles).
> The C# source of truth for these values is
> `unity/Assets/Scripts/Data/RosterCatalog.cs`; this table mirrors it. Any change must be made
> in both places and is guarded by `RosterIntegrityTests`.

**Authoritative count: 24 = 6 Starter + 10 Core + 6 Specialist + 2 Master**, across 6 roles.
Each role has exactly **1 Starter + 1 Specialist**; the **10 Core** are distributed across
roles; the **2 Master** units are **cross-role lateral options — never strict upgrades** (§3).

- **Currencies:** units unlock with **Stone only** (earned). No unit is ever bought with Shards
  or gated by money (§10.2). There is no Shard price on any unit, by design.
- **Tiles** come only from winning combat (§2). No spend grants a unit or a tile.
- **Elixir costs** are chosen so a legal 8-card deck averages **2.6–3.0** (§5) and an F2P player
  (starter + core only) can build a legal deck — verified by `RosterIntegrityTests` and
  `DeckValidatorTests` (a concrete F2P deck averages 2.875).

## All 24 units

| #  | Name           | Role        | Tier       | Elixir | Stone  |
|----|----------------|-------------|------------|:------:|-------:|
| 1  | Bulwark        | Vanguard    | Starter    | 4      | 0      |
| 2  | Hound          | Skirmisher  | Starter    | 2      | 0      |
| 3  | Longshot       | Archer      | Starter    | 3      | 50     |
| 4  | Spark          | Mage        | Starter    | 3      | 100    |
| 5  | Tower          | Engineer    | Starter    | 4      | 100    |
| 6  | Captain        | Champion    | Starter    | 4      | 150    |
| 7  | Rampart        | Vanguard    | Core       | 5      | 900    |
| 8  | Outrider       | Skirmisher  | Core       | 2      | 400    |
| 9  | Cutpurse       | Skirmisher  | Core       | 1      | 300    |
| 10 | Marksman       | Archer      | Core       | 3      | 600    |
| 11 | Slinger        | Archer      | Core       | 2      | 450    |
| 12 | Frostweaver    | Mage        | Core       | 3      | 800    |
| 13 | Cinder         | Mage        | Core       | 2      | 500    |
| 14 | Bombard        | Engineer    | Core       | 4      | 1,000  |
| 15 | Warden         | Champion    | Core       | 4      | 1,100  |
| 16 | Reaver         | Champion    | Core       | 4      | 1,200  |
| 17 | Standardbearer | Vanguard    | Specialist | 4      | 2,500  |
| 18 | Pathfinder     | Skirmisher  | Specialist | 2      | 2,800  |
| 19 | Volley         | Archer      | Specialist | 4      | 4,000  |
| 20 | Wildfire       | Mage        | Specialist | 4      | 4,500  |
| 21 | Snare          | Engineer    | Specialist | 2      | 3,200  |
| 22 | Berserker      | Champion    | Specialist | 5      | 6,000  |
| 23 | Twinblade      | Champion\*  | Master     | 4      | 12,000 |
| 24 | Lodestone      | Engineer\*  | Master     | 4      | 15,000 |

\* Master "role" is the primary slot. Both Masters play laterally across two roles by design
(Twinblade: Skirmisher/Champion · Lodestone: Vanguard/Engineer).

## Tier / role matrix

| Role        | Starter        | Core                    | Specialist     | Master      |
|-------------|----------------|-------------------------|----------------|-------------|
| Vanguard    | Bulwark        | Rampart                 | Standardbearer | (Lodestone) |
| Skirmisher  | Hound          | Outrider, Cutpurse      | Pathfinder     | (Twinblade) |
| Archer      | Longshot       | Marksman, Slinger       | Volley         | —           |
| Mage        | Spark          | Frostweaver, Cinder     | Wildfire       | —           |
| Engineer    | Tower          | Bombard                 | Snare          | Lodestone   |
| Champion    | Captain        | Warden, Reaver          | Berserker      | Twinblade   |

(Masters in parentheses indicate the secondary role they play into laterally.)

## Stone-cost ladder bands (§2)

| Tier       | Count | Stone band       |
|------------|:-----:|------------------|
| Starter    | 6     | 0 – 150          |
| Core       | 10    | 300 – 1,200      |
| Specialist | 6     | 2,500 – 6,000    |
| Master     | 2     | 10,000 – 15,000  |

## Why Masters are lateral (anti-treadmill, §3)

Masters cost the most Stone but are **deliberately not the strongest cards**. Each trades raw
power for cross-role flexibility, so picking one is a sideways choice, not an obligatory upgrade:

- **Twinblade** (260 hp / 52 dmg) has **less hp than Captain** (300) and **less damage than
  Berserker** (70). It is fast and flexible, not dominant.
- **Lodestone** (380 hp / 24 dmg) has **less hp than Rampart** (560) and modest damage. It pulls
  aggro like a tank and anchors like a building, but tops neither role.

`RosterIntegrityTests.MastersDoNotDominateLowerTierUnitsOnEveryStat` enforces this invariant in
CI: no Master may be a strict (pareto) upgrade over the lower tiers.
