# Character Skills Implementation

## Gameplay and Controls

The five existing heroes now use the skill definitions in `CHARACTORS.md`:

| Asset / character | Identity | Basic | Active (Q) | Passive | Ultimate (E) |
|---|---|---|---|---|---|
| New | Tanky | Light Crystal Shot | Astral Shield Wall | Astral Absorption | Meteor Binding |
| Nueng | Shadow | Shadow Pierce | Kunai Overdrive | Endless Excitement | Dark Kunai Rain |
| Peng | Magician | Ricochet Cards | Explosive Card Trap | Illusion Dodge | Jackpot |
| Ling | Oracle Witch | Arcane Orb | Time Distortion Field | Distorted Trajectory | Silent Judgment |
| Nae | Daphne | Dual Celestial Attack | Celestial Control | Celestial Transition | Eclipse |

**Skill status:** all 20 abilities above are implemented, connected to gameplay, and covered by the passing verification suites below. Their presentation uses placeholders; final artwork and balance are not marked complete.

Move with WASD/arrows. Hold Space/J to fire in the facing direction, or hold left click to aim at the cursor. Q activates the active skill; E activates the ultimate. Tab switches Daphne between Solar and Lunar. Ground skills aim toward the cursor within range and the map boundary; without a mouse, use the facing direction. Magician places traps at the player's feet.

Abilities use cooldowns without mana or charge. They start ready on each level. Daphne shares her active cooldown across stances, and switching does not reset basic attacks or ultimate cooldowns. Death and scene changes cancel owned effects and pending attacks.

After defeating the last enemy, combat skills stop but movement remains enabled so the player can enter the open portal. Movement is blocked only on death, game over, victory, or by an active movement-restricting status. Stage-clear movement is checked separately from permission to use combat skills.

Enemy ranged attacks calculate a safe firing point using the projectile's hitbox size. This prevents Skeleton arrows and Dragon fireballs from spawning outside the map or overlapping straight, angled, or adjoining boundary colliders when the enemy is pushed against a wall. Aim is recalculated from that firing point. Projectiles still stop when they subsequently hit a map wall.

## Single-Player Adaptations

Tanky's **Astral Shield Wall** grants defense only to the player while the player stands behind the wall. The original design also grants defense to allies behind it. When multiplayer exists, extend the same directional region check to living friendly actors, with per-recipient expiration and cleanup; do not simply apply the buff to every teammate regardless of position.

The wall still blocks hostile projectiles approaching its front and ignores friendly shots. Oracle's designated bypass attacks ignore magical blocking, but map boundaries always stop projectiles. No ally units, networking, or Wyvern have been added. Lunar root supports the existing Dragon without a forced landing mechanic.

## Configuration and Balance

Edit the five assets in `Assets/Game/CombatConfigs/` in the Inspector. Each existing animation set references its corresponding combat asset. `CharacterCombatConfig` groups basic/active/ultimate damage, cooldowns, durations, radii, stacks, defense, healing, stance, and status settings. Projectile prefabs retain their existing speed, lifetime, and sprite configuration; Oracle overrides speed/lifetime through its combat config.

Unspecified numbers are **provisional balance values**, not finished tuning: basic damage 25 (Oracle 35; Daphne Lunar 35), active damage 50 where applicable, ultimate total damage 100 per target, active/ultimate cooldowns 10/30 seconds, typical duration 4 seconds, radius 2 units, stance cooldown 1 second, and Eclipse healing 30 HP. Storm damage is divided across ticks, rather than applying the total every tick.

Tanky retains the specified 30% distance-defense cap, 10% actual-damage conversion, 2.5-second meteor root, and 30% attack reduction. Shield capacity starts at 50 HP. Shadow stacks cap at 10 and reset after 2 seconds without successful hits. Reapplying the same buff refreshes it; it does not accumulate indefinitely. Area slow ends on exit or area destruction. Different slows use the strongest value.

Physical and magic defense start at zero to preserve existing balance. Mitigation uses `damage * 100 / (100 + defenseAfterPenetration)`, then percentage reduction, shields, and HP. Actual damage includes shield absorption and excludes overkill. Guaranteed hits ignore evasion while retaining general invulnerability. Enemy attack debuffs affect attacks when fired, rather than rewriting projectiles already in flight.

## Simple Effects and Future Artwork

Finished skill effect assets are absent by design. Skills use generated rings, lines, tinted existing projectiles, shield outlines, impact flashes, dark kunai trails, and visual lifts for knock-up and Shadow's ultimate leap. Cosmetic lifts leave ground collision unchanged. Solar is orange/gold; Lunar is purple. Visual creation is independent of damage and targeting. Missing projectile artwork falls back to a simple moving ring, and transient effects are capped at 96.

Replace the presentation in `SkillVfx` and optional projectile artwork when final assets are ready; keep the combat callbacks and timing unchanged. Large screen attacks use snapshot targeting and at most 32 decorative target effects per stage, rather than spawning thousands of projectiles. Cinematic animation and final VFX remain artwork work.

## Integration and Modified Areas

- Existing `Health` and `Projectile` handle contextual damage, shields, deduplicated hits, explosions, ricochet, and statuses. The old damage and projectile launch APIs remain available.
- `PlayerController`, `EnemyController`, `MainMenu`, and `LevelManager` retain movement, AI, selection, HUD, and progression responsibilities. `PlayerSkills` adds the missing ability behavior; `StatusEffects` owns temporary modifiers.
- `CombatWorld` reuses the enemy registry for targets and existing colliders for safe movement. It supports the authored edge outlines as well as generated rectangular levels.
- Existing animation sets, player/enemy prefabs, and seven scenes reference the new setup. Assets and their GUIDs are preserved. `MagicGame.Runtime` allows the gameplay code to be referenced by test assemblies.
- **Tools > Magic Game > Update Character Skills** updates existing content in place and preserves tuned combat assets. Unsaved scenes are skipped, with runtime boundary detection as a fallback. `GameBuilder` also wires skill configuration during future generation. **Build Everything** still recreates scenes as before; use the update command for existing authored levels.

## Verification

Verification uses Unity **6000.6.3f1** and Unity Test Framework. Edit Mode covers damage, shields, overkill, penetration, defense types, evasion, invulnerability, buffs, healing, saved mappings, and repeatable setup. Play Mode covers every hero's skills, collision and effect cleanup, actual scenes, seven-stage progression, restart, and victory.

Initial verification: **2026-10-25**. Enemy projectile collision regressions updated and verified **2026-10-26**:

- Unity compilation passed; the live visual check reported zero current Console errors or warnings.
- **14/14 Edit Mode tests passed**, including two repeated asset updates without changes to saved tuning, prefabs, or authored scenes.
- **28/28 Play Mode tests passed**, including actual keyboard controls, all five saved selections, all seven stage transitions, victory, restart, boundary detection, cleanup, and placeholder effects. Regressions cover walking into the open Stage 1 portal, Skeleton arrows damaging the player in Stages 2/3/5/6, Skeleton shooting while retreating against an upper wall, and Dragon fireball hitboxes clearing an angled wall before their first physics step.
- The Unity asset/GUID checker inspected **268 files**, reporting **zero errors and zero warnings**. Source whitespace checks passed. Unity's generated YAML retains its standard empty-field formatting.
- Live game frames advanced during checks; Tanky's wall and Daphne's Lunar indicator were visually inspected. Screenshots are in `Logs/CharacterSkills-Tanky.png` and `Logs/CharacterSkills-Daphne.png`.
- Detailed local reports are `Logs/CharacterSkills-EditMode.json` and `Logs/CharacterSkills-PlayMode.json`. `Logs/` is intentionally ignored by Git.

Run the suites through **Window > General > Test Runner**, or with a connected Editor:

```powershell
unity command run_tests --mode editor --filter MagicGame.Tests --async_tests true --caller plugin --skill unity-cli
unity command test_status --caller plugin --skill unity-cli
# After completion:
unity command run_tests --mode playmode --filter MagicGame.Tests --async_tests true --caller plugin --skill unity-cli
unity command test_status --caller plugin --skill unity-cli
```

## Remaining Work

Replace placeholder presentation with finished artwork and animations, then playtest balance and visual readability. Multiplayer ally protection remains a future adaptation as described above. Automated checks establish functional behavior; they do not establish final game balance or production performance on every target device.
