# Grimoire UI art direction

Magic Ring Quest uses midnight navy leather, aged gold binding, parchment text,
and restrained cyan gem accents. Frames have stepped pixel contours, light from
the upper left, and a darker lower edge. Texture remains quiet under text; HUD
frames have less visual weight than menu panels. Existing maps, portraits, fonts,
skill symbols, and both joystick sprites are retained.

## Sources and provenance

`FrameSource.png` and `SurfaceSource.png` were generated for this project with the
built-in `image_gen` tool on 2026-10-27. The supplied Stable Diffusion Web image was
used as a visual reference for material depth and ornament placement; it is not
included in the shipped sprite set or copied into these assets.

Reference: https://imgcdn.stablediffusionweb.com/2025/7/11/81971ff7-694d-4510-ba23-e3145b82b522.jpg

These files are generated originals, not third-party stock art. No third-party
asset license or attribution requirement is asserted. Existing font licenses
remain with their fonts. Exact generation prompts are in `prompts.md`.

## Normalization and import

`Tools > Magic Game > Normalize Grimoire Art` rebuilds the derived PNGs from the
retained originals. The editor normalizer crops the alpha bounds, samples with
nearest-neighbor, thresholds frame alpha, and maps the frame to a fixed 26-color
palette. It extracts the small cyan jewel and the slider rail from that same
source, so the family shares its materials. The surface is sampled, reduced to
a low-contrast navy ramp, and mirrored to make opposing tile edges identical.
The white focus outline is a simple code-authored mask, independent of the art.

| Sprite | Size | Border (L/B/R/T) | Purpose |
| --- | --- | --- | --- |
| SurfaceTile | 256×256 | 0 | Repeatable leather, panels and page backgrounds |
| PanelFrame | 256×256 | 44 | Main menu and large window binding |
| ButtonFrame | 128×128 | 22 | Compact actions and card choices |
| HUDFrame | 96×96 | 17 | Lightweight combat information frames |
| FocusFrame | 64×64 | 8 | Tintable focus/interaction outline |
| SliderTrack | 64×12 | 6/4/6/4 | Horizontal aged-metal rail |
| SliderHandle | 24×24 | 0 | Cyan inset jewel |
| HeaderOrnament | 24×24 | 0 | Small jewel on heading rules |

All derived sprites use 100 PPU, Point filtering, no mipmaps, uncompressed RGBA,
and Read/Write disabled. Only SurfaceTile repeats. Panel and button material is
tiled independently from the sliced frame: resizing cannot stretch the grain.

`Tools > Magic Game > Set Up Mobile UI` loads the normalized art and rebuilds the
shared UI prefab. It does not replace the generated originals or re-normalize
existing PNGs. Normalize first only when intentionally changing the art.

## Layout defaults

At the 1920×1080 reference resolution: primary buttons are 84 units high,
secondary buttons 72, hero rows 120, ability rows 112, slider rows 156, and skill
buttons 144 in diameter. Their circular mesh reuses SurfaceTile, ButtonFrame,
and FocusFrame, wrapping the frame's rails and corner jewels around the rim.
Theme fields expose these defaults. Menu groups use layout
containers and grow with Text Size; list content scrolls. Action controls retain
Control Size and opacity settings and reserve extra label space at large text.
Both joysticks retain their original 280-unit size, position, ring/disc art,
pointer ownership, dead zone, and scaling behavior.
