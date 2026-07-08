# Heyra the Horned (Revived) — Patch Notes

---

## v1.0.12 — Beast Form Fixes (the claws work everywhere now)

### Claws now attach on all game languages

Beast form's claw attacks were silently missing on non-English clients. The transformation matched body parts by their *translated* labels ("left hand" / "right hand"), so on 简体中文 and other localized games the claw hediffs never attached — the beast transformed, but unarmed. Body parts are now matched by their internal definition name, which is identical in every language. 中文玩家们，你们的爪子回来了！

### Gear is restored on every way the transformation can end

Previously, worn apparel and weapons stashed during transformation were only re-equipped when you manually toggled the form off. If the transformation **timed out** naturally or ended because the pawn was **downed**, she reverted bare-skinned with all her gear sitting in her inventory — no armor, and an unhappy mood to match. Gear restoration now runs on every removal path: manual revert, timer expiry, and downed-revert alike.

### Other fixes

- **English equip-block messages**: trying to equip items while transformed now shows a proper message instead of the raw text `PawnInChangedFormApparel` (the translation key existed only in the Chinese language files — sorry, English players)
- **Work restrictions no longer leak between pawns**: transformed pawns briefly inherited the *first-ever* transformed pawn's personal work incapacities (from her backstory/traits) for the duration of beast form. Each pawn now computes her own
- **Transformation lightning strikes the right map**: with multiple colonies, the transform VFX bolt could fire on whichever map you were currently viewing instead of the transforming pawn's map

---

## v1.0.11 — Armor Balance Pass

### Military Armor & Helmets — Values Reduced

Stackable military armor and helmets have been dialed back slightly. These pieces can be worn over clothing layers, and the combined totals were overshooting Spacer-tier gear in some configurations. Layer-hogging suits (Stealth Suit, Eldritch Robe) and HMAA Spacer armor are unchanged — their high values compensate for blocking multiple equipment layers.

**Body armor** (base Sharp / StuffMult):
- Soldier's armor: 0.75/0.65 → **0.65/0.55**
- Koi armor: 0.78/0.65 → **0.68/0.55**
- Forbidden Army panoply: 0.85/0.65 → **0.75/0.55**
- Sword master's panoply: 1.05 → **0.95** (fixed, no stuff)

**Helmets** (base Sharp / StuffMult):
- Soldier's helmet: 0.48/0.45 → **0.42/0.40**
- Forbidden Army helmet: 0.58/0.50 → **0.50/0.42**

Blunt values reduced proportionally. Heat values unchanged. HMAA-Light, HMAA-Heavy, and all Spacer helmets unchanged.

---

## v1.0.10 — Custom Heyra Faction Traders

### Heyran Traders Now Sell Heyran Goods

Heyra faction traders no longer use generic vanilla neolithic stock lists. Caravans, settlement traders, and visitors now carry Heyran weapons, apparel, food, and culturally appropriate materials.

- **Merchant caravans** carry Heyran clothing, food, jade, and general goods
- **Armorer caravans** carry Heyran weapons, armor, and helmets
- **Settlement traders** stock the full range — including a rare chance to carry **Ancient Heyra Blood** (0~1 at 2000 silver)
- **Jade** reliably stocked across all trader types — no longer dependent on vanilla exotic traders
- Ancient Heyra Blood can now also be purchased from traders (was previously sell-only)
- **Psylink Neuroformers** and **random Psycast trainers** available at settlement traders (requires Royalty or Biotech DLC) — fits Heyras' natural psychic sensitivity

---

## v1.0.9 — Gunblade Brawler Fix + Bug Fix

### Rong Gunblade — Brawler Trait Compatibility

The Rong Gunblade no longer triggers the Brawler mood debuff or the "Brawler has ranged weapon" alert. As a hybrid melee weapon with an integrated cannon, Brawler pawns can now equip it without penalty.

- Brawler pawns still prefer melee in auto-combat — sabot cannon remains available for manual targeting
- Uses MVCF's `brawlerCaresAbout` system (no new C# code)

### Bug Fix

- Fixed `Failed to find Verse.ThingDef named Alien_Rong` error at startup
- The north-facing head/hair render fix (v1.0.8) now correctly applies to Rong pawns as well

---

## v1.0.8 — North-Facing Render Fix

### Tail & Head/Hair North-Facing Render Fix

Fixed tail and head/hair rendering behind the body and apparel when pawns face north (back to camera). Previously visible on apparel with large north-facing sprites like the Stealth Suit and Soldier's Armor.

- Tail now correctly renders in front of the body when north-facing
- Head, hair, eyes, and beard now render in front of apparel when north-facing
- Affects both Heyra and Rong races

---
## v1.0.7 — Crafting Filter Fix

### Armor Crafting — Precious Materials Off by Default

Armor and headgear crafting bills no longer default to allowing Gold, Silver, Plasteel, Jade, and Uranium. These precious materials are now unchecked by default, matching vanilla behavior. You can still manually enable them in the bill's stuff filter if you want to craft with them.

- Also disables Bioferrite (Anomaly) and Obsidian (Odyssey) when those DLCs are active
- Affects all metallic armor, helmets, and Rong masks

---

## v1.0.6 — Combat Overhaul: Legendary Weapon + Bug Fix

### Soul Great Bow — Legendary Rework

The Soul Great Bow has been overhauled from a mid-tier sniper into a true endgame legendary weapon with dual-mode firing.

- **Base mode**: 30 damage, 0.55 AP, 10 EMP per hit — gradual mechanoid disruption
- **Awakened mode**: 45 damage, 0.65 AP, 100 EMP per hit — one-shot stuns small mechs, two-shot stuns Centipedes. Only available to pawns who have undergone the Awakening ritual
- Range increased to **46.0** — outranges Pikeman needle guns
- Accuracy improved across all ranges
- Equipped bonuses: Shooting Accuracy +2.0, Hunting Stealth +0.50, Psychic Sensitivity +0.15
- HP increased to **350**, zero deterioration
- Crafting cost increased to match legendary status: 50 Plasteel, 30 Steel, 2 Components, 1 Spacer Component, 1 Ancient Heyra Blood

### New Research: Legendary Armaments

New endgame research node (3000 cost, Spacer) gating the Soul Great Bow and future legendary weapons. Requires both Forbidden Army Armaments and Exotic Gears.

### Bug Fix

- Fixed garbled text on bean milk machine inspector panel

---

## v1.0.5 — Tail & Horn Fixes

- HMAA-Light and HMAA-Heavy body armor now covers and visually hides the tail
- Fixed child Heyra horn positioning for south and east facing directions

---

## v1.0.4 — Combat Overhaul: Apparel Rebalance

### Apparel Rebalance — All Armor & Clothing

All Heyran armor and clothing has been rebalanced for competitive protection against mid-to-late game threats.

**Armor**
- All armor pieces now use a hybrid base + stuff scaling system
- Steel loadouts achieve meaningful protection without plasteel going overboard
- Full military kit (Soldier): **2.24 Sharp** | Full military kit (Forbidden Army): **2.47 Sharp**
- HP increased across the board

**Stealth Suit — Shinobi Rework**
- Rethemed into a dedicated stealth/hunter piece
- Removed: Toxic Environment Resistance
- Added: Hunting Stealth +0.80, Melee Dodge Chance +3, Move Speed +0.15
- Insulation increased to compensate for multi-layer coverage
- Cost increased: now requires 160 stuff + 2 Components

**Clothing**
- OnSkin items now have unique stat identities instead of being interchangeable
- Role attire (Hierarch's, Shaman's, Prophetess') differentiated with role-appropriate bonuses
- Worker's belt food poisoning reduced from +35% to +10%

---

## v1.0.3 — Combat Overhaul: Weapon Rebalance

### Weapon Rebalancing — All Craftable Weapons

Comprehensive stat pass on the entire Heyra weapon arsenal. All weapons now have thematic equipped stat offsets reflecting Heyra's graceful combat style.

**Tier 1 (Heyran Blacksmithing)**
- Sword, dagger-axe, halberd: damage and armor penetration increased
- Crossbow: faster warmup and cooldown, higher bolt speed, improved accuracy
- Blast crossbow: larger blast radius, faster cooldown
- All melee weapons gain Dodge Chance or Hit Chance bonuses

**Tier 2 (Forbidden Army)**
- FA Sword and FA Halberd: significant damage and AP buffs
- Dodge Chance and Hit Chance bonuses reflecting elite training

**Rong Branch (Exotic Gears)**
- Greatsword and gunblade: damage and AP increased
- Hit Chance bonuses reflecting feral combat instinct

### New Research: Forbidden Army Armaments

New medieval-tier research node (3000 cost) gating all Forbidden Army weapons and armor. Requires both Heyran Blacksmithing and Heyran Armor Construction.

### Research Tree Restructure

- Exotic Gears (Rong) now requires both Heyran Blacksmithing and Heyran Armor Construction
- Research tree layout updated for visual clarity

---

## v1.0.2 — New Weapon: Soul Great Bow

### Heyran Soul Great Bow

A long-range sniper bow filling the precision rifle niche in the Heyra weapon arsenal.

- Damage: 25, AP: 0.45, Stopping Power: 2.0
- Range: 38.9 — longest bow in the mod
- Accuracy: poor up close, excellent at long range
- Crafted at the Heyra Workbench with Plasteel, Steel, and Components
- Soul-infused arrows trail spectral light

---

## v1.0.1 — Hotfix

### Bug Fix: Narrow Comfort Gene

- Fixed inverted temperature comfort range on the Narrow Comfort gene
- Was creating an impossible range where the minimum was higher than the maximum
- Now provides a tight but valid 6°C comfort window (18°C – 24°C)

---

## v1.0.0 — Initial Release

Full RimWorld 1.6 revival of Heyra the Horned.

### Races
- **Heyra** — Refined, horned, all-female race with Chinese-influenced culture and bean-based agriculture
- **Rong** — Primal, bestial variant with steppe-warrior culture and dark rituals

### Beast Form Transformation
- Toggle-able transformation via ability gizmo — use again while transformed to revert early
- Monster form: 3.5x body scale, animated flamehair, claw attacks, breath beam, electric field turret
- Post-transformation fatigue with lethal stacking prevents spam

### Combat Systems
- Dragon breath beam (burst fire, burn + EMP damage)
- Electric field turret (4 range bands, auto-fires while standing still, visible spark ring)
- Melee slash trail on Khanzio Sword hits
- Celestial Whisper bow with beam projectile

### Factions & World
- Three factions: Traditional Heyra, Renovated Heyra, Rong
- Custom faction, settlement, and ideology naming systems
- Custom pawn name generators (Chinese-style for Heyra, steppe-warrior for Rong)
- KCSG quest site generation with two bunker layouts
- Three scenarios: New Horizons, The Long March, The Blade Unsheathed

### Races & Genes
- Two xenotypes: Teller-Blessed (Heyra) and Severed (Rong)
- Custom genes: Narrow Comfort, Umbral Adaptation, Prehensile Tail, and more
- Tail body part with cosmetic hediff
- Biohorn visual swap system (10 horn variants via surgery)

### Quality of Life
- Hair visible under lightweight headwear (clips, headbands, masks)
- Workbench auto-production machines (Bean Milk, Biscuit, Synthread Loom)
- Melee Animation compatibility (optional, weapon tweak data included)
- Full English text audit (70+ fixes across backstories, items, and quests)
- Chinese (Simplified) localization maintained

### Dependencies (Required)
- Harmony
- Humanoid Alien Races (HAR)
- Vanilla Expanded Framework (VEF)
- AncotLibrary
