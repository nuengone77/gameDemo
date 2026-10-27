# Grimoire UI changes — 2026-10-27

ปรับ UI ทุกหน้าและ HUD เป็นธีมตำราเวทกรมท่า–ทอง เพิ่ม texture และกรอบ pixel art
ลดขนาดปุ่มที่เกินจำเป็น จัดกลุ่มเมนูให้โปร่งขึ้น และคงภาพ/ตำแหน่ง/การควบคุมของ joystick เดิม

## Requested outcome

Give all menus and the combat HUD a navy-and-gold pixel fantasy material style,
and reduce oversized buttons and empty control padding. Preserve both joysticks.

## Changes

- Added generated leather and antique-gold frame originals, reproducible pixel
  normalization, sprite imports, and a manifest with generation provenance.
- Separated tiled surfaces, sliced frames, and interaction outlines; disabled
  buttons dim their contents and pressed buttons darken their surface.
- Added shared theme defaults: primary 84, secondary 72, roster 120, ability 112,
  slider row 156, and skill button 144 units at reference resolution 1920×1080.
- Grouped Rebirth with main menu actions and View Build with pause actions.
  Menu groups grow with text size and scroll when the safe area cannot hold all
  rows. Keyboard/gamepad focus continues to scroll selected controls into view.
- Added textured page backgrounds, gem slider handles, and lightweight HUD
  framing. Existing scene art, fonts, skill symbols and gameplay rules remain.
- Clamped safe-area anchors to the current screen: the Editor can briefly report
  a safe area from an earlier resolution, which otherwise placed buttons outside
  the rendered view.
- Reserved a fixed footer height for 150% text, kept the Active/stance buttons
  separated at maximum control/text sizes, and moved earned-points text with the
  scaled HUD so it remains below the boss frame.
- Isolated prefab authoring in a temporary additive scene rather than creating
  UI objects in the scene currently open in the Editor.
- Added pointer-raycast and large-text menu regression tests to MobileUITests.

## Authoring and files

- `Assets/Art/UI/Grimoire/`: eight normalized runtime sprites and `.meta` files.
- `Assets/Game/UI/ArcaneTheme.asset`: sprite references and editable size defaults.
- `Assets/Resources/UI/GameUI.prefab`: rebuilt shared UI, including Progression.
- [`Art/Grimoire/README.md`](Art/Grimoire/README.md): art direction and normalization.
- [`Art/Grimoire/asset-manifest.json`](Art/Grimoire/asset-manifest.json): asset roles,
  dimensions, filtering, borders and sources.
- [`Art/Grimoire/prompts.md`](Art/Grimoire/prompts.md): exact built-in image generation
  prompts. The two original generated PNGs are retained beside this document.

Use **Tools > Magic Game > Normalize Grimoire Art** only to rebuild sprites from
the originals. **Set Up Mobile UI** loads those sprites and regenerates the shared
prefab. Build Everything also invokes the UI setup.

## Verification log

- Full Edit Mode run: **47/47 passed**, report `Logs/Grimoire-EditMode.json`.
- Full Play Mode run after the safe-area fix: **46/46 passed**, including combat,
  three complete loops for all five heroes, progression, independent touch
  pointers, navigation, settings, pause, and the two new UI regressions. Report:
  `Logs/Grimoire-PlayMode.json`.
- After the final footer adjustment, the compact-menu regression was rerun with
  an additional footer truncation assertion: **1/1 passed**. Report:
  `Logs/Grimoire-LargeTextRegression.json`.
- After the final earned-points placement adjustment, the existing large-text
  card/Rebirth test was extended to check separation from the boss frame and
  rerun: **1/1 passed**. Report: `Logs/Grimoire-HUDLargeTextRegression.json`.
- The initial full Play Mode run was 45/46: the pointer regression exposed the
  stale safe-area dimensions. The subsequent full run and targeted checks above
  passed after correction; the initial failure is retained in
  `Logs/Grimoire-PlayMode-Initial.json`.
- All eight sprites passed final QA: expected dimensions/import settings,
  Point filtering, no compression/mipmaps, at most 26 opaque colors, binary
  alpha and identical opposing tile edges. `Logs/Grimoire-ArtReport.json`.
- After removing temporary captures from Assets, the final GUID/script checker
  checked 591 files with **0 errors / 0 warnings**.
  `git diff --check` passed for changed source, tests and documentation.
- Consecutive UI rebuilds produced the same prefab and panel-frame SHA-256;
  the open scene remained clean. `Logs/Grimoire-RebuildReport.json`.
- Ring and Disc PNG SHA-256 hashes match the pre-change originals. The
  `BuildStick` layout and touch input code were not changed.

## Visual review and limits

Seventeen captures are under `Logs/UI/Grimoire/`. Reviewed main menu, hero
selection, settings, Rebirth, cards, gameplay, pause, help, build, ability dialog,
tutorial, defeat and victory styling at 1280×720. The victory view was inspected
as a style preview; the current endless-run game flow remains unchanged.

Reviewed 150% menu text with Continue and footer visible, and 1600×720 settings
and boss HUD with simulated 4% horizontal / 3% vertical safe-area insets, 150%
text and 120% controls. HUDs were reviewed against both bright grass and lava.
Native screenshot dimensions are recorded in the PNGs; wide captures are not
stretched to 16:9. Long help, ability and settings content remains scrollable.

The original PlayerPrefs save/settings, hero selection, tutorial flag, scene,
Game view selection and background-running setting are restored after review.
Save/settings, hero, tutorial and scene comparisons all passed in
`Logs/Grimoire-RestoreReport.json`.
No physical Android device or Android player build was used for this change;
device checks remain a release step. The pre-existing Progression work was
preserved, and gameplay scenes/configuration have no content changes from this UI
task.
