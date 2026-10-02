# Manhunt (Unity)

Single-player Unity foundation for a top-down 2.5D horror game. Scope is only the **lighting,
perspective and world geometry**: an orthographic, 60° pitch camera over a stylized, flat-shaded
low-poly diorama built from triangle meshes, lit by a 3D port of the 2D visibility-polygon
illumination system. The level is authored in design units (a person is 1.8 m) and its root is
scaled by `WorldScale.S` = 2, so the camera sits twice as close. Characters are plain 198-triangle
mannequins animated by a procedural walk and sprint.

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
| F2 | – | Cycle view: final, mask RGB, lit amount, raw scene, character shadows |
| F3 | – | Hide the stats overlay |
| F4 | – | Open or close the look panel (sliders, below) |
| F5 | – | Toggle all camera effects |

The F1-F5 debug keys read the keyboard directly and are not part of the actions asset.

### Look panel (F4)

Live sliders for finding the palette. Each slider shows its current value, **Copy values** puts all of
them on the clipboard (and in the log) as one line of text, and **Reset** restores the defaults. Values
persist between runs. The defaults are the tuned look.

| Slider | Range | Default | Effect |
|---|---|---|---|
| Contrast | 0-2 | 1.00 | Global contrast, pivoting on mid-grey |
| Saturation | 0-2 | 1.00 | Global saturation of every colour (0 = greyscale) |
| Lit brightness | 0-2 | 1.00 | Multiplier on everything lit (flashlight, campfires, lanterns) |
| Unlit brightness | 0-2 | 1.00 | Multiplier on the unlit ground and objects |
| Beam intensity | 0.5-2 | 1.15 | Strength of the flashlight beam only |
| Blur start / end (m) | 0-30 | 5 / 13 | Distance from the player where the distance blur begins and is full |
| Blur max (px) | 0-8 | 3.00 | Full distance-blur radius at 1080p |
| Camera effects | on/off | on | Vignette, film grain, light flicker and the distance blur (also F5) |

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
   - B = viewer light (cone, proximity, see-through) with distance falloff. The cone also fades over
     the outer 35% of its half angle (`VisionViewer.coneEdgeSoftness`), so the beam has a soft edge.
   - G = line of sight.
   - R = light sources with distance falloff.
   - A = character shadows (below).

   The mask is world-space, not screen-space, because the camera is pitched and the world has height.
   Every shader samples it at a fragment's world X,Z.
3. **Composite** (`VisionCompositePass.cs`, `Shaders/VisionComposite.shader`). This is a Render Graph
   full-screen pass injected per camera. It reconstructs each pixel's world position from depth.
   - The scene is blurred with distance from the player (Darkwood-style; a camera effect).
   - `lit = max(B × beam intensity, R × smoothstep(G))`
   - Lit ground is the scene × lit × lit brightness; light-source light is tinted warm.
   - Unlit ground is a near-neutral grey (saturation under 0.03), about 20% of the scene's luminance ×
     unlit brightness.
   - `out = mix(grey, lit, smoothstep(lit))`, then vignette and grain, then the global saturation and
     contrast from the look panel.

   The mask is sampled slightly along each surface's normal, so a wall face picks up the light on its
   side.
4. **Entity occlusion** (`Shaders/LowPoly.shader`, "Entity" toggle). Dynamic objects discard every
   fragment where channel **B alone** is below a hard threshold. They are invisible outside your own
   light even when standing in a campfire's glow. Static terrain is never culled.
5. **Shadows: the flashlight's only.** No asset has a shadow of its own: the moon casts none, URP
   real-time shadows are off and every renderer has shadow casting and receiving off. The only shadows
   are the flashlight's: its visibility polygon, and soft character shadows (`CharacterShadow`) drawn
   into mask channel A pointing away from the flashlight. They only darken already-lit ground (about
   45%), never block light or sight, are never drawn for the player's own body, and an entity's shadow
   only appears while the entity itself is visible. Campfires and lanterns cast no character shadows.

## Characters

`MannequinBuilder` builds one plain, dark grey mannequin of 198 triangles (the cap is 200): an
8-sided torso, a 6-sided head with a ridge down the face, 4-sided boxy limbs, mitten hands and wedge
feet, with no clothing, hair, face or equipment. It is skinned to the 51-bone `HumanoidSkeleton`
(Drillis-Contini proportions, 1.8 m), with joint rings weighted half to each bone. The player and the
wanderer share the mesh; the wanderer uses the entity material.

`GaitSolver` and `HumanoidAnimator` animate it procedurally: heel strike, flat foot and toe-off so the
feet never slide, a flight phase when sprinting, arms swinging opposite the legs, two-bone leg IK with
forward-only knees, and a torso that twists toward the aim (the legs walk backwards when you aim
behind you). Walk 3.2 and sprint 5.2 world units per second.

## Polygon budget

`PolyBudget` derives one resolution rule for everything from the player. The mannequin's surface area
divided by its triangle count gives a reference facet edge (0.158 design units). Under the orthographic
camera an edge of a given length covers the same pixels anywhere, so every model uses that facet size,
relaxed by form class because large or flat forms read well with bigger facets:

| Class | Facet edge × | Used for |
|---|---|---|
| Character | 1 | The mannequin |
| Prop | 1.5 | Crates, campfire, lantern, door boards, crows |
| Rock | 2 | Rock subdivision level |
| Tree | 2.5 | Trunk and branch sides and segments |
| Wall | 4 | Stone courses and block widths, plank widths |
| Ground | 5 | Terrain cell size |

Ring sides come from circumference ÷ edge, tube segments from length ÷ (edge × 3), and icosphere
subdivisions from the ellipsoid's area ÷ the class's triangle size. The budget is a cap: flat, boxy
props such as crates use fewer, larger facets. The bake logs each prop's triangle count and measured
facet size against its class.

## Level assets

The level is saved as ordinary Unity content, so you can open `VisionSandbox.unity` and see and edit it
without pressing Play:

| Folder | Contents |
|---|---|
| `Assets/Vision/Prefabs/` | One prefab per prop variant (6 dead trees, 4 rocks, 3 crates, campfire, lantern post, 2 crows, wanderer, player), each with its collider, occluder or light, plus `PropLibrary.asset` listing them. |
| `Assets/Vision/Meshes/Props/` | The mesh behind each prop prefab, and `Mannequin.asset` shared by the player and the wanderer. |
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
`LowPolyModels` builds the props from them (at the resolution `PolyBudget` sets), and `PropFactory` adds
each prop's collider, occluder and light. The models use the same footprints as the colliders and occluders. The meshes under
`Assets/Vision/Meshes/` are generated, so they are stored in Git LFS.

## Commands

Run these from this folder:

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.CreateSandbox
```
Bakes the whole level: materials, the URP settings (real-time shadows off), every prop mesh and prefab, the prop library, a mesh
asset for each ground, wall and door object, and the scene with the level laid out in it (also **Vision →
Bake Level and Scene** in the Editor).

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.ConfigureProject
```
Sets the company and product names and switches the build target to Windows 64-bit.

```bash
unity test . --editor-version 6000.6.3f1 --mode EditMode
```
Runs the 63 EditMode tests: visibility polygons, doors, spatial hash, triangle winding and normals,
model sizes and determinism, the polygon budget, the mannequin (triangle cap, size, skinning,
colour), the gait (no foot sliding, flight phase, joint limits), the flashlight-only shadow rule and
beam falloff, the look settings, edit-mode level generation (no renderer casts shadows; characters are
renderable) and the saved prefabs.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindows
```
Builds `Builds/Windows/VisionSandbox.exe`.

```bash
./Builds/Windows/VisionSandbox.exe -visionCapture Captures -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
Stages the test situations (cone, doors, entity hiding, shutter, see-through cone, walking, sprinting,
strafing, backpedalling, close-ups, the player from three sides, the wanderer in the beam and its
shadow, camera effects off, each look slider low and high) and a gait sheet, saves a screenshot of each
plus `perf.txt` and `characters.txt` (renderer state of each character) to `Captures/`, then quits.

## Known gaps

- The polygon pass runs on the main thread. That is fine for this arena (about 2 ms per frame in
  total), but a large map will want Burst/Jobs.
- Wall tops sit inside their own footprint, so they always read dark. That matches Darkwood, but
  there is no option to light them.
- There is no Darkwood-style canopy overlay or fog cards yet, and no flicker on the viewer's own light.
- The distance blur also softens the edges of near objects that overlap far ground.
