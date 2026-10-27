# Legacy demo post-processing repair

The imported Hovl and Matthew demo content referenced Post Processing Stack v2,
which is not installed in this URP project. This caused 22 missing-script errors.

- Convert the five existing profile assets to URP VolumeProfiles, preserving their
  `.meta` GUIDs and root file IDs so the existing scene references still work.
- Replace the legacy Volume and camera Layer components in four demo scenes with
  `Volume` and `UniversalAdditionalCameraData`, preserving profile references,
  volume settings, layers and camera volume masks.
- Preserve bloom intensity/threshold and supported color grading, mixer and curve
  settings. Bloom diffusion uses the installed Unity converter's `scatter = diffusion * 0.05` mapping.
- Omit the disabled ambient occlusion effect (its intensity was zero). URP has no
  direct equivalents for the old LDR brightness/trackballs and custom tone-curve
  controls; follow Unity's PPv2 converter behavior for those settings. The old
  profiles remain recoverable from commit `60fbba2`.
- Rename CameraRotation/OrbitScript to CameraOrbit/RotationParticle with their
  original `.meta` files so Unity can bind the existing classes correctly.

Only demo scenes/profiles and those script filenames are changed. Gameplay scenes,
skill prefabs, audio, combat tuning, progression, package dependencies and build
scene order are preserved.

The conversion mappings come from the installed URP 17.6 package under
`Editor/Tools/Converters/PPv2`. Regression tests check profile identity and values,
scene component references and camera volume masks, and renamed script bindings.

## Separate vendor demo limitation

Opening `Scene Demo Auras FREE.unity` also reveals 32 prefab references whose
assets are absent from this project. These are independent of the 22 missing
post-processing scripts and are not reported by the script/GUID checker. Retain
the original references so the intended effects can be recovered if the source
prefabs become available. The scene tests accept only these known prefab import
errors and continue to reject missing scripts or other unexpected errors.
