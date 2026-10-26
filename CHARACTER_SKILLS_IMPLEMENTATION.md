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

**Balance round 1 (2026-10-26)** targets accessible solo play, a 45–75 second Dragon fight, and approximately 15–25% difference between the fastest and slowest boss clears at comparable familiarity. These are playtest targets, not measured human completion times. All attack patterns, targeting, damage types, status rules and stage rosters are retained.

Edit the five assets in `Assets/Game/CombatConfigs/` in the Inspector. Each animation set references its corresponding combat asset. `CharacterCombatConfig` now also holds `maxHp` and `moveSpeed`: `PlayerController.Awake` applies them once after resolving the selected hero. `PlayerSkills.Initialize` never applies base stats or refills health. Unassigned/test configs retain defaults of 100 HP and speed 4; existing designer tuning is preserved by **Update Character Skills**. New configs use `CharacterSkillSetup.ApplyInitialBalance` only when created.

| Hero / asset | HP | Move speed | Basic damage / interval | Active cooldown | Ultimate damage / cooldown |
|---|---:|---:|---|---:|---|
| Tanky / New | 120 | 4.0 | 22 / 0.40s | 12s | 100 / 30s |
| Shadow / Nueng | 110 | 4.3 | 12 per kunai / 0.30s | 18s | 70 / 30s |
| Magician / Peng | 115 | 4.1 | 24 per target / 0.40s | 8s | 180 total / 30s |
| Oracle Witch / Ling | 110 | 4.0 | 48 / 0.92s | 12s | 75 / 30s |
| Daphne / Nae | 120 | 4.0 | Solar 19, Lunar 25 / 0.42s | 9s | 60 / 35s |

The only calibration from the initial proposal is Daphne's Lunar damage **26 → 25**. The first aimed automated Dragon runs averaged 48.89s for Daphne versus 61.95s for the slowest hero (26.7% spread). Three calibrated repeats took 50.52/50.41/50.44s, reducing the spread to about 22.8% without changing the stance, root, healing or cooldown rules.

- **Tanky:** distance reduction caps at 20% at 6 units; converts 2.5% of actual damage into shield, capped at 30 HP. Wall duration 3s, defense reduction 15%; meteor radius 2.4, root 2s and attack reduction 20% for 3s. The wall's projectile blocking and directional region checks are unchanged.
- **Shadow:** 10 stacks, +3% attack speed each, reset after 3s without hits. Overdrive gives +20% attack speed for 2s; the three-kunai, 30-degree fan and 2-unit retreat over 0.2s remain unchanged. Armor penetration remains 20%.
- **Magician:** three bounces within 3 units, 12% evasion; card lifetime 1.6s at its original speed 9. Trap damage 50, lifetime 8s, trigger radius 0.8, knock-up 0.7s. Trap explosion/storm radius 2.4. Storm lasts 4s, ticks every 0.5s and divides its 180 total damage across the eight ticks. Storm remains at the original location after teleport.
- **Oracle:** orb speed 4, lifetime 3s, explosion/slow-field radius 1.6; slow 45% for a 3.5s field. Defense/obstacle bypass and guaranteed ultimate hit rules are unchanged.
- **Daphne:** Solar burn 4 damage per second for 3s, explosion/active radius 1.4; Lunar active root 1.5s. Active damage 45 in either stance, stance cooldown 3s, transition buff 15% for 3s, Eclipse healing 20 HP. Stances share the active cooldown and retain the existing replacement/refresh rules.

| Enemy | HP | Physical / magic defense | Damage | Attack interval | Wind-up | Projectile speed |
|---|---:|---|---:|---:|---:|---:|
| Goblin | 80 | 0 / 0 | 8 | 1.2s | 0.40s | unchanged (melee) |
| Skeleton | 60 | 5 / 5 | 8 | 2.0s | 0.50s | 7 |
| Frost Witch | 1,200 | 10 / 10 | 12 | 2.2s | 0.65s | 5 |
| Golem | 1,600 | 35 / 35 | 20 | 2.6s | 0.80s | 4 |
| Dragon | 2,800 | 20 / 20 | 18 | 3.0s | 0.65s | 3 |

`GameBuilder.ApplyEnemyBalance` supplies the same values to regenerated enemies. The existing Level7 overrides already match the Dragon's interval 3 and projectile speed 3; authored scenes, sky movement, projectile counts and burst patterns are preserved. Player defenses remain zero and post-hit invulnerability remains 0.4s. Other unspecified stats are unchanged.

Mitigation still uses `damage * 100 / (100 + defenseAfterPenetration)`, then percentage reduction, shields, and HP. All current hero attacks, including Shadow's kunai, use magic damage, so enemy magic defense now gives both penetration and bypass an actual benefit. Actual damage includes shield absorption and excludes overkill. Guaranteed hits ignore evasion while retaining general invulnerability. Enemy attack debuffs affect attacks when fired, rather than rewriting projectiles already in flight. Reapplying a buff refreshes it; different slows use the strongest value and area slow ends on exit/destruction.

The rough model behind these starting numbers assumes successful basic-attack output for 80% of combat time, fully established Shadow stacks and one to three fan blades landing during Overdrive. It gives approximately 59–72 seconds against a 2,800 HP / 20-defense Dragon. It excludes travel time, stack ramp, geometry and human movement, and gives Magician no credit for ground areas against the flying boss. It must not be reported as a live-play result.

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

### Balance round 1 — 2026-10-26

Automated verification uses the saved hero/enemy assets; the combat probes do not grant HP, invulnerability, damage bonuses, kill enemies directly, or change enemy AI. They hold basic fire and use ordinary skills with their actual cooldowns. Temporary probe scripts and detailed reports are outside tracked assets in `Temp/` and `Logs/`.

| Hero | Dragon clear times (game seconds) | Mean |
|---|---|---:|
| Tanky | 62.22 / 61.76 / 61.86 | 61.95s |
| Shadow | 53.90 / 52.33 / 53.46 | 53.23s |
| Magician | 56.97 / 56.89 / 56.94 | 56.93s |
| Oracle Witch | 64.49 / 59.36 / 61.96 | 61.94s |
| Daphne, Lunar 25 | 50.52 / 50.41 / 50.44 | 50.46s |

All 15 final Dragon attempts cleared within the 45–75s target. The probes ran at **2x time scale** and report elapsed game time. Aimed runs use velocity-based projectile lead, arena positioning and projectile avoidance; these are automated combat measurements, not human playtests or proof of beginner accessibility. Earlier capped-lead runs are retained separately, including two Oracle deaths, to make the controller's influence visible.

Regression suites cover the 35 hero/stage spawn combinations, full spawn HP, configured movement speed, no healing/resurrection on skill reinitialization, saved enemy/generator consistency, meaningful armor penetration/bypass, and all existing skill, UI and progression behavior. Reports are `Logs/BalanceRound1-EditMode.json`, `Logs/BalanceRound1-PlayMode.json`, `Logs/BalanceRound1-AimedProbe.jsonl` and `Logs/BalanceRound1-DaphneProbe.jsonl`. The first 15-run baseline in the aimed report uses Lunar 26; the three calibrated Daphne runs replace those rows for the table above.

Final compilation passed; **35/35 Edit Mode and 39/39 Play Mode tests passed** after calibration. The Unity asset/GUID checker inspected **511 files with zero errors and zero warnings**. Source whitespace checks passed. No authored scene, artwork, core skill implementation, status rules or enemy AI files were changed.

Stages 1–6 were also attempted with all five heroes: 26/30 initial attempts cleared. Shadow/Oracle/Daphne died in Stage5 and Magician died in Stage6. The four targeted repeats used the projectile and player hurtbox extents instead of a fixed projectile clearance; all four cleared at 36.92s/35.50s/38.42s/11.58s respectively. Combined with the Dragon attempts, every hero has at least one solo clear of every saved stage in this controlled probe. These repeats changed the test controller, not the enemy attack patterns or defenses. Reports retain both outcomes in `Logs/BalanceRound1-GroundProbe.jsonl` and `Logs/BalanceRound1-GroundRetest.jsonl`. Human playtesting is still required for difficulty, readability, and long-term character preference.

After the Lunar calibration, Daphne also repeated Stages1–4 successfully at 3.53s/6.51s/22.80s/10.82s; Stage5's successful repeat and Stage6's initial clear already used Lunar 25. `Logs/BalanceRound1-DaphneGround.jsonl` records these final checks. The final saved numbers therefore have a successful automated solo clear for every hero/stage combination, with failed earlier attempts retained rather than discarded.

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
