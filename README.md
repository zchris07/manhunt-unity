# Manhunt (Unity)

Single-player Unity foundation for a top-down 2.5D horror game. Scope is only the **lighting,
perspective and world geometry**: an orthographic, ~70° pitch camera over a stylized, flat-shaded
low-poly diorama built from triangle meshes, lit by a 3D port of the 2D visibility-polygon
illumination system.

Unity **6000.6.3f1**, URP 17 (Render Graph), Input System, Windows desktop. Open
`Assets/Vision/Scenes/VisionSandbox.unity` and press Play.

## First-time setup for a clone

Each new clone needs these once:

```bash
git lfs install --local
```

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
```

```bash
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
```

`.gitattributes` stores text as LF, merges scenes, prefabs and assets with Unity's Smart Merge, and
sends meshes, textures, audio, video and fonts to Git LFS.

CI (`.github/workflows/ci.yml`, GameCI) runs the EditMode tests and a Windows build on every push. It
needs the `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` repository secrets; the workflow file
explains where to find each one.

The `com.unity.pipeline` package lets the `unity` CLI drive an open Editor (`unity status`,
`unity command`).

## Controls

Input goes through the project-wide actions asset `Assets/InputSystem_Actions.inputactions` (`Player`
map), so bindings can be changed there or rebound at runtime.

| Keyboard and mouse | Gamepad | Action |
|---|---|---|
| WASD or arrows | Left stick | Move |
| Shift | Left stick press | Run |
| Mouse | Right stick | Aim the flashlight cone (whichever moved last) |
| E | X / Square | Open or close the nearest door or window shutter |
| F | Y / Triangle | Toggle the see-through cone |
| C | B / Circle | Crouch (bound, not used yet) |
| F1 | – | Draw the visibility polygons |
| F2 | – | Cycle view: final, mask RGB, lit amount, raw scene |
| F3 | – | Hide the stats overlay |

The F1-F3 debug keys read the keyboard directly and are not part of the actions asset.

## How the lighting works

All visibility math is 2D on the ground plane (world X,Z), then projected back onto the 3D world.

1. **Polygons** (`Runtime/Visibility/VisibilityComputer.cs`). An angular sweep casts rays at every
   nearby occluder endpoint and at ±ε around it, plus evenly spaced arc rays. Hits are sorted by angle.
   `OccluderSet` holds the segments in a spatial hash. Its `Version` bumps whenever a door opens or
   closes, which throws away the cached light polygons. Occluders are box or N-gon footprints
   (`Occluder.cs`) for walls, trunks, rocks, crates, closed doors and shutters.
   - Cone: fixed half-angle, capped near the screen edge.
   - Proximity circle: small 360° polygon around the viewer.
   - 360° line of sight: long range. It lights nothing itself.
   - Light sources (`VisionLight.cs`): the 6 nearest each frame; static ones cache their polygon.
   - See-through cone: ignores occluders and is drawn at 70%.
2. **Mask** (`Runtime/Rendering/VisionMaskRenderer.cs`). The polygons are rasterised into a
   **world-space** square texture centred on the camera's ground focus. It runs at about half screen
   resolution and is blurred.
   - B = viewer light (cone, proximity, see-through) with distance falloff.
   - G = line of sight.
   - R = light sources with distance falloff.

   The mask is world-space, not screen-space, because the camera is pitched and the world has height.
   Every shader samples it at a fragment's world X,Z.
3. **Composite** (`VisionCompositePass.cs`, `Shaders/VisionComposite.shader`). This is a Render Graph
   full-screen pass injected per camera. It reconstructs each pixel's world position from depth.
   - `lit = max(B, R × smoothstep(G))`
   - Lit ground is the scene × lit; light-source light is tinted warm.
   - Unlit ground is desaturated and dimmed to a faint grey, about 20% of its luminance.
   - `out = mix(grey, lit, smoothstep(lit))`, plus vignette and grain.

   The mask is sampled slightly along each surface's normal, so a wall face picks up the light on its
   side.
4. **Entity occlusion** (`Shaders/LowPoly.shader`, "Entity" toggle). Dynamic objects discard every
   fragment where channel **B alone** is below a hard threshold. They are invisible outside your own
   light even when standing in a campfire's glow, and they cast no shadows. Static terrain is never
   culled.

## Level assets

The level is saved as ordinary Unity content, so you can open `VisionSandbox.unity` and see and edit it
without pressing Play:

| Folder | Contents |
|---|---|
| `Assets/Vision/Prefabs/` | One prefab per prop variant (6 dead trees, 4 rocks, 3 crates, campfire, lantern post, 2 crows, wanderer, player), each with its collider, occluder or light, plus `PropLibrary.asset` listing them. |
| `Assets/Vision/Meshes/Props/` | The mesh behind each prop prefab. |
| `Assets/Vision/Meshes/Level/` | One mesh per ground, wall and door object in the scene. |
| `Assets/Vision/Materials/` | `LowPoly`, `LowPolyEntity` (hidden outside the viewer's light) and `LowPolyGlow`. |

The scene's `Sandbox World` object has `generateOnAwake` off, because the level is already in the scene.
Trees, rocks and the rest are prefab instances, so editing a prefab updates every copy; moving an
instance or deleting one is a normal scene edit. Turn `generateOnAwake` on, or use the component's
**Generate Level** context menu, to rebuild the level from the seed instead. Rebaking overwrites the
assets in place, so references to them keep working, but it discards hand edits to the scene.

The art itself is procedural. `LowPolyMeshBuilder` gives every triangle its own vertices, face normal
and slightly jittered colour, with sRGB vertex colours converted to linear. Its primitives are faceted
tubes and cones, jittered icospheres, irregular hexahedra and a jittered, triangulated ground grid.
`LowPolyModels` builds the props from them, and `PropFactory` adds each prop's collider, occluder and
light. The models use the same footprints as the colliders and occluders. The meshes under
`Assets/Vision/Meshes/` are generated, so they are stored in Git LFS.

## Commands

Run these from this folder:

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.CreateSandbox
```
Bakes the whole level: materials, the URP shadow settings, every prop mesh and prefab, the prop library, a mesh
asset for each ground, wall and door object, and the scene with the level laid out in it (also **Vision →
Bake Level and Scene** in the Editor).

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.ConfigureProject
```
Sets the company and product names and switches the build target to Windows 64-bit.

```bash
unity test . --editor-version 6000.6.3f1 --mode EditMode
```
Runs the 25 EditMode tests: visibility polygons, doors, spatial hash, triangle winding and normals,
model sizes and determinism, edit-mode level generation and the saved prefabs.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindows
```
Builds `Builds/Windows/VisionSandbox.exe`.

```bash
./Builds/Windows/VisionSandbox.exe -visionCapture Captures -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
Stages 11 situations, saves a screenshot of each plus `perf.txt` to `Captures/`, then quits.

## Known gaps

- The polygon pass runs on the main thread. That is fine for this arena (about 2 ms per frame in
  total), but a large map will want Burst/Jobs.
- Wall tops sit inside their own footprint, so they always read dark. That matches Darkwood, but
  there is no option to light them.
- There is no Darkwood-style canopy overlay, fog cards or flicker on the viewer's light yet. Lights do
  flicker.
