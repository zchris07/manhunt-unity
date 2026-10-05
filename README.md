# Manhunt (Unity)

Single-player Unity port of the 2D browser game **Manhunt** (github.com/zchris07/manhunt, `main`), the
blueprint for its map, objectives, items and rules: a top-down 2.5D horror game on an orthographic, 60°
pitch camera over a flat-shaded low-poly world, lit by a 3D port of the 2D visibility-polygon
illumination system. It opens on the title screen; **Testing mode** is the only way in so far.

The map is the original's at **3 cm per original unit**: a 180 x 180 m forest (6000 units) with the
procedural 36 m central building (1200 units) in the middle, its north exit gate and fenced yard, the
survivors' spawn in the south, clearings joined by footpaths, three cabins, a lake with a dock, fences,
logs, tall grass to hide in, power lines, a graveyard, an abandoned playground and a hanging tree, and
about 2,350 evergreens and dead trees. Every map is generated at runtime from a seed (about 3.8 s);
**New map** in the game menu builds another. Start all five generators, then pull the lever to open the
gate. The level is authored in design units (a person is 1.8 m) and its root is scaled by `WorldScale.S`
= 2, so the camera sits twice as close. Characters are 222-triangle mannequins animated by a procedural
walk and sprint that adapts to any slope.

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
| WASD or arrows | Left stick | Move (crawl while downed) |
| Shift | Left stick press | Run |
| Mouse | Right stick | Aim the flashlight cone (whichever moved last) |
| E | X / Square | Pick up a supply, open or close a door, hide or leave a hiding spot, drop a pallet; **hold** on a generator or the gate lever |
| F | Y / Triangle | Toggle the see-through cone |
| 1-8 | – | Use the item in that inventory slot |
| M | – | Full map (the minimap is always on) |
| V | – | Speed mode (testing): full sprint, +100% speed |
| R | – | Get back up when downed (testing) |
| Esc | Start | Game menu (does not pause): Resume, Speed mode, New map, Look settings, Full screen, Quit to main menu |
| F1 | – | Draw the visibility polygons |
| F2 | – | Cycle view: final, mask RGB, lit amount, raw scene, character shadows |
| F3 | – | Show the stats overlay (off by default) |
| F4 | – | Open or close the look panel (sliders, below) |
| F5 | – | Toggle all camera effects |

The number keys, M, V, R, Esc and F1-F5 read the keyboard directly and are not part of the actions asset.

### Menus and testing mode

- **Title screen** (`GameHud`): MANHUNT, the original's kicker ("Crystal Lake · Night shoot") and blurb,
  **Testing mode** and **Quit**. The level runs behind it.
- **Testing mode** (`GameSession`): the original's testing kit (bottle and book nine each, goggles,
  shotgun, mini shield, Mr Beast bar, gas trap, and a Doctor Pepper for the pistol this game lacks),
  never used up, and no win condition. **Speed mode** (menu or V) keeps the sprint meter full and doubles
  movement speed.
- **Game menu** (Esc) leaves the world running, as the original does. **New map** regenerates everything
  from a new random seed; the menu and the full map show the seed. **Quit to main menu** returns to the
  title screen; Testing mode from there starts on a fresh map.
- **Maps** (`MapHud`, `MapPainter`, `FogOfWar`): the minimap (top right, about 57 m across) and the full
  map (M) show the level painted from above, under a fog of war that stays black until your own light
  has been there. Supplies, generators (yellow, green running) and the gate (grey, yellow powered, green
  open) appear once seen, with an arrow for where you face. Testing mode adds **Reveal all** and
  click-to-teleport on the full map.

### HUD and game systems

- **Health and shield** (`Vitals`, `PlayerStats`), bottom left, as in the original. Health runs 0-100% and
  never regenerates; the blue shield bar takes damage first. At 0 you are **downed**: you lie prone and
  crawl at 32/120 of walking pace and see 0.6 as far (testing: R gets back up with a third of the bar).
- **Stamina**: sprinting drains 18 a second; it refills at 12 a second after a 0.8 s pause. Running dry
  locks sprint until a quarter is back.
- **Supplies** (`Pickup`, `Inventory`): the original's nine, at its counts (86 in all): bottle 20,
  The Grapes of Wrath 4, night vision goggles 3, shotgun 2, Doctor Pepper 8, galaxy gas trap 8, duck
  confit 6, Mr Beast bar 15, mini shield 20. They are placed by its rules (spots in the building's rooms,
  the cabins, around the clearings and beside the paths, at least 4.8 m apart). Eight slots; identical
  items stack without limit; each shotgun takes its own slot. Duck confit heals to full, a Mr Beast bar
  gives 20%, and a mini shield is drunk over 2 s (moving spills it) for 25% shield. The others are only
  collected so far.
- **Generators and the gate** (`GeneratorObjective`, `ExitGate`): five generators, two in the building
  and three in the woods. Hold E for the original's 70 s to start one; running, it shakes and glows. With
  all five running, hold E at the lever beside the north gate for 20 s and the roll-up door opens onto
  the yard. The objective (top left) counts them: "Generators n/5".
- **Hiding**: tall grass, wardrobes, beds, lockers and barrels (E to hide, E to leave). **Pallets** stand
  beside some doorways; E drops one across the gap.
- The **wanderer** walks a loop near the spawn and takes 20% when it walks into you.
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

## The central building

`BuildingPlan` generates the 36 x 36 m single-storey building from the seed (the roof is never drawn):

- **Hallways** are cut through the footprint recursively: a 2.4-3 m spine end to end, then narrower
  branches (1.4-2.4 m), so they differ in width and length.
- **Rooms** (3 x 3 to 10 x 8 m) split the blocks between them, preferring cuts that keep every room on a
  hallway. Each gets a door or doorway onto a hallway (some get two), back rooms a door through a
  neighbour, and a few rooms connect to each other, so everything is reachable with loops.
- **Room types**: offices, storage, break room, restrooms, locker room, workshop, electrical and server
  rooms, a studio set for the night shoot (a fake cabin bedroom with lights, a camera and mannequins), the
  loading bay at the gate and a boiler room, each furnished by its own recipe.
- **Outside**: entrances as in the original (two south, one north, one or two east and west), some open
  with a pallet beside them; windows, a quarter of them boarded; the **north exit gate** with its lever and
  the chain-link **yard** beyond it.
- **Generators**: two, in rooms and never hallways. With its working space the generator takes at most
  30% of its room's floor and keeps a walkway to every wall.
- **Fittings**: lockers in the hallways (up to 8), wardrobes, beds and barrels to hide in; pipes, ducts,
  breaker panels, junction boxes and vent grilles; posters, clocks, stains, cobwebs and dead plants.
  Furniture keeps every doorway clear and covers under half of each room.
- **Lamps** have the original's 9 m radius: 6-14 work (some flicker), the rest are dead fixtures.

`SandboxWorld.Building.cs` builds it: cinder-block outer walls, painted inner walls with dark tops so the
plan reads from above, tiled and concrete floors, hinged doors, windows (glass stops you, not your
sight), headers, the roll-up gate and every item with its collider, occluder (tall pieces) and hiding
spot.

## Terrain

`TerrainField` is the single source of ground height. Four sine waves at seeded headings make rolling
hills and a ridged term cuts ditches: about 5 m of relief and slopes up to about 35° (sines, so the
steepest slope is bounded). The ground flattens to pads under the building and its yard, the cabins, the clearings,
the cover pieces and the special sites, across every footpath, and toward the walls; the lake sits in a
basin you can wade (at 0.45 of your speed). The finished field is baked to a 0.25 m grid for speed. The ground is built in
16 m chunks, each with a `MeshCollider`, whose vertices sit exactly on the field.

Trees, walls, doors and lantern posts stand upright, sunk to the lowest point under them; rocks,
crates, wrecks and crows tilt to the slope. Hills are visual: sight and light stay 2D. Any grade can be walked: the
controller follows the ground, uphill steps shorten and the body leans into the climb.

`PathNetwork` joins the points of interest with a minimum spanning tree and routes each link with A*
over a grid whose cost rises steeply with slope, then smooths it. The land changes gradually: slow
noise fields choose between evergreens (seven forms, including spiky ones) and twelve dead-tree designs,
and colour the ground from earth and clay to moss and straw, with rock grey on steep slopes, mud in the
ditches and packed dirt on the paths.

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

The level is generated when the scene starts (`SandboxWorld.generateOnAwake`), from `seed`; the props are
saved as ordinary Unity content:

| Folder | Contents |
|---|---|
| `Assets/Vision/Prefabs/` | One prefab per prop variant (12 dead-tree designs and 7 evergreen forms in three sizes, rocks from pebbles to boulders, crates, car wrecks, a generator, a burning barrel and a campfire with animated flames, a lantern post, crows, wanderer, player), each with its collider, occluder or light, plus `PropLibrary.asset` listing them. |
| `Assets/Vision/Meshes/Props/` | The mesh behind each prop prefab, and `Mannequin.asset` shared by the player and the wanderer. |
| `Assets/Vision/Materials/` | `LowPoly`, `LowPolyEntity` (hidden outside the viewer's light) and `LowPolyGlow`. |

`MapLayout` ports the original's map generator (clearings, cabins, generators and their cover pieces,
the lake, grass, fences, logs, fires, the power line and the special sites); `BuildingPlan` is the
building. Use the `Sandbox World` component's **Generate Level** context menu to see a level in the
Editor without pressing Play.

The art itself is procedural. `LowPolyMeshBuilder` gives every triangle its own vertices, face normal
and slightly jittered colour, with sRGB vertex colours converted to linear. Its primitives are faceted
tubes and cones, jittered icospheres, irregular hexahedra and a jittered, triangulated ground grid.
`LowPolyModels` builds the props, furniture and fittings from them (at the resolution `PolyBudget`
sets), and `PropFactory` adds each prop's collider, occluder and light. The meshes under
`Assets/Vision/Meshes/` are generated, so they are stored in Git LFS.

## Commands

Run these from this folder:

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.CreateSandbox
```
Bakes the materials, the URP settings (real-time shadows off), every prop mesh and prefab, the prop
library and the scene (also **Vision → Bake Level and Scene** in the Editor). The level itself is built
at runtime.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.ConfigureProject
```
Sets the company and product names and switches the build target to Windows 64-bit.

```bash
unity test . --editor-version 6000.6.3f1 --mode EditMode
```
Runs the 117 EditMode tests: visibility polygons, doors, triangle winding and normals, model sizes and
determinism, the polygon budget, the mannequin, the gait on flat ground and steep ramps, the terrain and
paths, the map layout (the original's scale and rules, the lake), the building (rooms tile the footprint,
hallway widths and lengths, every room reachable with loops, openings in walls, the original's entrances
and the north gate, generators under 30% of their rooms, furniture clear of doorways, lamps), supplies at
the original's counts, health and shield, downing, stacking, healing, generators and the gate, the fog
of war and the painted map, the menus and testing mode, New map, generation time and the saved prefabs.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindows
```
Builds `Builds/Windows/VisionSandbox.exe`.

```bash
./Builds/Windows/VisionSandbox.exe -visionCapture Captures -screen-width 1600 -screen-height 900 -screen-fullscreen 0
```
Skips the title screen and stages the test situations (cone, doors, entity hiding, walking and sprinting,
close-ups, the look sliders, soft shadows, the lake, graveyard, playground, hanging tree, power line, tall
grass and the woods generators, the building's hallway, gate, yard and one room of each kind, the whole
map and the building from above, the HUD, downed, a generator being started and running, the gate
lever and the open gate, the nine supplies, testing mode with the minimap, the game menu, the full map
under its fog and revealed, the title screen and a New map) and the tree and gait sheets, saves a
screenshot of each plus `perf.txt` and
`characters.txt` (renderer state of each character and the feet's gaps to the ground on a slope) to
`Captures/`, then quits.

## Known gaps

- CI's Unity licence activation has been failing since early October 2026 ("Access token is unavailable",
  then the runner kills Unity); the tests pass locally. The `UNITY_LICENSE`, `UNITY_EMAIL` and
  `UNITY_PASSWORD` secrets need refreshing (and old activations returned on the Unity account).
- Bottles, books, goggles, the shotgun, Doctor Pepper and gas traps are collected but not usable yet.
- There is no hunter: the wanderer only walks its loop. Nothing ends a match (testing mode has no win
  condition).
- The polygon pass runs on the main thread. That is fine for this map, but a much larger one will want
  Burst/Jobs.
- Hills are visual only: a crest does not hide what is behind it.
- Tree canopies can hide the player when they walk behind one.
- The distance blur also softens the edges of near objects that overlap far ground.

