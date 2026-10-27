# Mobile UI

The game uses a shared uGUI prefab with a storybook fantasy theme. Open
`Assets/Scenes/MainMenu.unity` and press Play. Android is configured for both
landscape orientations; keyboard and mouse remain available in the Editor.

The grimoire skin uses tiled navy leather, sliced antique-gold frames, and cyan
gem accents. Menu actions use compact layout rows that expand with text size;
Rebirth and View Build sit inside their menu groups. The existing twin sticks
retain their original art and geometry. Art sources, exact generation prompts,
normalization, sprite sizes, and authoring defaults are documented in
[`Art/Grimoire/README.md`](Art/Grimoire/README.md).
The implementation record and validation results are in
[`GRIMOIRE_UI_CHANGES.md`](GRIMOIRE_UI_CHANGES.md).

## Screens

Main Menu leads to hero selection, How to Play, and Settings. A valid checkpoint
also enables Continue. The gameplay HUD includes pause, independent movement and
aim/fire sticks, Active and Ultimate buttons, and Daphne's stance switch.
Pause provides Resume, help, settings, stage restart, and Main Menu. Defeat offers
stage retry; Victory offers a new adventure. A skippable four-step guide appears
on the first visit to Stage 1 and can be replayed from How to Play.

The right stick fires while held outside its dead zone. Releasing it stops firing
and retains the last aim for skills. Stick magnitude controls ground-target
distance. Tanky's ultimate and Oracle Witch's active show a destination marker;
existing collision rules constrain placement.

## Authoring

- `Assets/Resources/UI/GameUI.prefab`: editable screen hierarchy, anchors, labels,
  controls, and panels. `GameUI` binds controls by their unique object names.
- `Assets/Game/UI/ArcaneTheme.asset`: fonts, character roster, backgrounds, and
  reusable sprites. Heading font: Cormorant Garamond; body: Liberation Sans.
  Their OFL licenses are included with the fonts.
- **Tools > Magic Game > Set Up Mobile UI** regenerates the shared theme and
  prefab without rebuilding gameplay scenes. It overwrites generated UI layout
  changes, so update `GameUILayout` for changes that must survive regeneration.
  Build Everything also runs this setup.
- Ability explanations use the existing combat configurations. Update the
  descriptions in `GameUI` if ability behavior changes.

Settings immediately persist control size (80–120%), opacity (30–100%), text
size (100–150%), and reduced UI motion. Large display headings keep their size.
Reduced motion affects UI transitions, while combat effects remain visible.

## Persistence and pause

Checkpoints store a version, scene ID, and hero ID in the local PlayerPrefs key
`MagicGame.Checkpoint.v1`. Stage entry saves a checkpoint; Continue restarts that
stage with fresh health and cooldowns. New Adventure replaces it only after
confirmation. Final victory clears it. Settings and tutorial completion use
separate keys; there is no cloud sync.

The UI navigation stack owns focus and input. Pause and app suspension freeze
simulation time without disabling combat components. Touch pointer state is
cleared on overlays, resizing, and suspension; returning to the app requires
Resume. Android Back opens Pause or closes the current overlay.

## Verification

Run `MagicGame.Tests` in Unity Test Runner in both Edit Mode and Play Mode.
`UISessionTests` covers checkpoint validation; `MobileUITests` covers navigation,
settings, confirmation, independent pointers, pause preservation, Continue,
tutorial completion, retry, and victory. Existing combat tests remain relevant.
Also inspect landscape 16:9 and 20:9 layouts, notches, maximum text/control sizes,
and real multi-touch on an Android device before shipping.

Validated in Unity 6000.6.3f1: 24 Edit Mode and 37 Play Mode tests passed, including
the existing combat suite. The asset/GUID checker reported no errors or warnings.
Screens were reviewed at 1280×720 and 1600×720, including simulated safe-area
insets, 150% text, and 120% controls. Review captures and test reports are under
the ignored `Logs/` directory. No physical Android device was connected; a native
Android build and device testing remain release checks.
