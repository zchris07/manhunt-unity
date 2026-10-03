# Manhunt (Unity)

Single-player Unity foundation for a top-down 2.5D horror game, currently an **atmospheric
exploration sandbox**: an orthographic, 60° pitch camera over a stylized, flat-shaded low-poly world
built from triangle meshes, lit by a 3D port of the 2D visibility-polygon illumination system. The
80 x 80 m level has hills and ditches, four biomes, procedural footpaths, car wrecks, campfires and
supplies to find. The level is authored in design units (a person is 1.8 m) and its root is scaled by
`WorldScale.S` = 2, so the camera sits twice as close. Characters are plain 222-triangle mannequins
animated by a procedural walk and sprint that adapts to slopes. A HUD shows health, stamina, the
inventory, the objective and a compass.

Unity **6000.6.3f1**, URP 17 (Render Graph), Input System, Windows desktop.

The build starts borderless full screen at the display's native resolution (Alt+Enter for a window).
The view grows with the screen instead of zooming: `TopDownCamera` keeps 62.5 pixels per world unit
(7.2 half-height at 900 px), so a bigger or higher-resolution screen shows more of the level at the
same asset size, and the lighting mask grows to cover it. Command-line `-screen-*` options override
full screen. Open
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
| E | X / Square | Pick up the nearest supply, or open or close the nearest door or shutter |
| F | Y / Triangle | Toggle the see-through cone |
| C | B / Circle | Crouch (bound, not used yet) |
| 1-6 | – | Use the item in that inventory slot |
| Esc | Start | Pause menu (Resume, Look settings, Full screen, Quit) |
| R | – | Restart after dying |
| F1 | – | Draw the visibility polygons |
| F2 | – | Cycle view: final, mask RGB, lit amount, raw scene, character shadows |
| F3 | – | Show the stats overlay (off by default) |
| F4 | – | Open or close the look panel (sliders, below) |
| F5 | – | Toggle all camera effects |

The number keys, Esc, R and F1-F5 read the keyboard directly and are not part of the actions asset.

### HUD and game systems

- **Health and stamina** (`Vitals`, `PlayerStats`), bottom left. Sprinting drains 18 stamina a second; it
  refills at 12 a second after a 0.8 s pause. Running dry locks sprint until a quarter is back.
- **Inventory** (`Inventory`), bottom centre: six slots, stacks of five. Bandages heal 35, water restores
  60 stamina, canned food heals 15 and restores 25. Using one at full health keeps it.
- **Supplies** (`Pickup`): 22 lie by the campfires, the wrecks, the generator and in the cabin. The
  objective (top left) counts them; the compass (top centre) shows where the flashlight points.
- The **wanderer** hurts the player when it walks into them (10, at most once a second). At low health
  the screen edge turns red; at zero a death screen offers a restart.
- `GameHud` builds the HUD in code with uGUI, scaled from a 1080p reference, so it keeps its proportions
  at any resolution.

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
| Beam edge falloff | 1-6 | 2.50 | Exponent of the beam's fade toward the screen edge |
| Blur start / end (m) | 0-30 | 5 / 13 | Distance from the player where the distance blur begins and is full |
| Blur max (px) | 0-8 | 3.00 | Full distance-blur radius in screen pixels |
| Camera effects | on/off | on | Vignette, film grain, light flicker and the distance blur (also F5) |

## How the lighting works

All visibility math is 2D on the ground plane (world X,Z), then projected back onto the 3D world.

1. **Polygons** (`Runtime/Visibility/VisibilityComputer.cs`). An angular sweep casts rays at every
   nearby occluder endpoint and at ±ε around it, plus evenly spaced arc rays. Hits are sorted by angle.
   `OccluderSet` holds the segments in a spatial hash. Its `Version` bumps whenever a door opens or
   closes, which throws away the cached light polygons. Occluders are box or N-gon footprints
   (`Occluder.cs`) for walls, trunks, rocks, crates, closed doors and shutters.
   - Cone: fixed half-angle; it reaches the edge of the screen (the distance along the beam to the edge
     of the visible ground, plus 6%) at any resolution.
   - Proximity circle: small 360° polygon around the viewer.
   - 360° line of sight: long range. It lights nothing itself.
   - Light sources (`VisionLight.cs`): the 6 nearest each frame; static ones cache their polygon.
   - See-through cone: ignores occluders and is drawn at 70%.
2. **Mask** (`Runtime/Rendering/VisionMaskRenderer.cs`). The polygons are rasterised into a
   **world-space** square texture centred on the camera's ground focus. It runs at about half screen
   resolution and is blurred, more the farther from the light (below).
   - B = viewer light (cone, proximity, see-through) with distance falloff. The beam fades as
     1 - (d / reach)^p (p = 2.5): bright near the player, dropping faster toward the screen edge. It also
     fades over the outer 35% of its half angle (`VisionViewer.coneEdgeSoftness`).
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
5. **Shadows from every light, with a penumbra.** The moon casts none and URP real-time shadows are
   off. Trees, rocks, walls, crates and wrecks shadow the flashlight, campfires and lanterns through
   those lights' visibility polygons. The mask blur grows with distance from the light that casts each
   shadow (`Hidden/Vision/Blur`): from the player for the beam, from the nearest light source for
   theirs, at 0.055 per unit and capped at 1.1 world units so light cannot seep through walls. Edges are
   sharp near the light and soften farther away. Characters also cast soft shadows (`CharacterShadow`,
   mask channel A) away from the flashlight and the nearest 4 campfires and lanterns. These only darken
   already-lit ground and never block light or sight. The flashlight never shadows the player's own
   body, and an entity's shadow only appears while the entity itself is visible.

## Terrain

`TerrainField` is the single source of ground height. Four sine waves at seeded headings make rolling
hills and a ridged term cuts ditches: about 5 m of relief and slopes up to about 35° (sines, so the
steepest slope is bounded). The ground flattens to pads under the cabin, the spawn point, the campfires,
the wrecks and the generator, across every footpath, and toward the walls. The ground is built in
16 m chunks, each with a `MeshCollider`, whose vertices sit exactly on the field.

Trees, walls, doors and lantern posts stand upright, sunk to the lowest point under them; rocks,
crates, wrecks and crows tilt to the slope. Hills are visual: sight and light stay 2D.

`PathNetwork` joins the points of interest with a minimum spanning tree and routes each link with A*
over a grid whose cost rises steeply with slope, then smooths it. Region noise gives four biomes (dead
forest, woods, meadow, scrub) that choose the trees and plants; the ground is coloured straw to green
grass by moisture, with dark soil, light dirt, rock grey on steep slopes, mud in the ditches and packed
dirt on the paths.

## Characters

`MannequinBuilder` builds one plain, dark grey mannequin of 222 triangles (the cap is 250): an
8-sided torso, closed shoulder caps weighted half to the chest so the arms stay joined when they
swing, a 6-sided head with a ridge down the face, 4-sided boxy limbs, mitten hands and wedge feet, with
no clothing, hair, face or equipment. It is skinned to the 51-bone `HumanoidSkeleton`
(Drillis-Contini proportions, 1.8 m), with joint rings weighted half to each bone. The player and the
wanderer share the mesh; the wanderer uses the entity material.

`GaitSolver` and `HumanoidAnimator` animate it procedurally: heel strike, flat foot and toe-off so the
feet never slide, a flight phase when sprinting, arms swinging opposite the legs, two-bone leg IK with
forward-only knees, and a torso that twists toward the aim (the legs walk backwards when you aim
behind you). Walk 3.2 and sprint 5.2 world units per second. On slopes each foot plants on the ground
under it and tilts to it, the pelvis drops so the downhill leg can reach, the torso leans into climbs
while the head stays level, and the player is slower uphill and a little faster downhill.

## Polygon budget

`PolyBudget` derives one resolution rule for everything from the player. The mannequin's surface area
divided by its triangle count gives a reference facet edge (0.152 design units). Under the orthographic
camera an edge of a given length covers the same pixels anywhere, so every model uses that facet size,
relaxed by form class because large or flat forms read well with bigger facets:

| Class | Facet edge × | Used for |
|---|---|---|
| Character | 1 | The mannequin |
| Prop | 1.5 | Crates, campfire, lantern, barrel, generator, door boards, crows |
| Plant | 1.75 | Bushes, ferns, grass, reeds, shrubs, flowers, mushrooms |
| Rock | 2 | Rock subdivision level |
| Vehicle | 2.25 | Car wrecks |
| Tree | 2.5 | Trunk and branch sides and segments, canopies, conifer tiers |
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
| `Assets/Vision/Prefabs/` | One prefab per prop variant (6 dead trees, 4 leafy and autumn trees, 3 conifers, 8 rocks from pebbles to boulders, 3 crates, 3 car wrecks, a generator, a burning barrel and a campfire with animated flames, a lantern post, 2 crows, wanderer, player), each with its collider, occluder or light, plus `PropLibrary.asset` listing them. |
| `Assets/Vision/Meshes/Props/` | The mesh behind each prop prefab, and `Mannequin.asset` shared by the player and the wanderer. |
| `Assets/Vision/Meshes/Level/` | One mesh per ground chunk (with its plants merged in) and its collider, and per wall, door and supply in the scene. |
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
Runs the 92 EditMode tests: visibility polygons, doors, spatial hash, triangle winding and normals,
model sizes and determinism, the polygon budget, the mannequin (triangle cap, closed shoulders,
size, skinning, colour), the gait on flat ground and on 30° ramps up and down, the terrain (slope
limit, pads, mesh matches the field), paths (connected, gentle, flattened), the new models, the
shadow rules, penumbra and beam falloff, health, stamina, inventory and pickups, the HUD, the look
settings, level generation (biomes, nothing on the paths, no renderer casts shadows) and the saved
prefabs.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindows
```
Builds `Builds/Windows/VisionSandbox.exe`.

```bash
./Builds/Windows/VisionSandbox.exe -visionCapture Captures -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
Stages the test situations (cone, doors, entity hiding, shutter, see-through cone, walking, sprinting,
strafing, backpedalling, close-ups, the player from three sides, the wanderer in the beam and its
shadow, camera effects off, each look slider low and high, soft shadows by a campfire and in the beam,
the hills and the whole map from above, wrecks, the generator, each biome, fire frames and the HUD) and
a gait sheet with uphill and downhill rows, saves a screenshot of each plus `perf.txt` and
`characters.txt` (renderer state of each character and the feet's gaps to the ground on a slope) to
`Captures/`, then quits.

## Known gaps

- The polygon pass runs on the main thread. That is fine for this map (about 2.3 ms per frame in
  total at 1600x900), but a much larger one will want Burst/Jobs.
- Hills are visual only: a crest does not hide what is behind it.
- The wanderer only walks its loop; it does not hunt the player.
- The baked meshes are about 44 MB (Git LFS), mostly the ground chunks with their plants.
- Wall tops sit inside their own footprint, so they always read dark. That matches Darkwood, but
  there is no option to light them.
- There is no Darkwood-style canopy overlay, fog cards or foliage sway yet, and no flicker on the
  viewer's own light. Tree canopies can hide the player when they walk behind one.
- The distance blur also softens the edges of near objects that overlap far ground.
