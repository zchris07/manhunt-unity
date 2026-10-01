# unity-vision

Single-player Unity foundation for a top-down 2.5D horror game. Scope is only the **lighting,
perspective and world geometry**: an orthographic, ~70° pitch camera over a stylized, flat-shaded
low-poly diorama built from triangle meshes, lit by a 3D port of the 2D visibility-polygon
illumination system.

Unity **6000.6.3f1**, URP 17 (Render Graph), Input System. Open `Assets/Vision/Scenes/VisionSandbox.unity`
and press Play.

## Controls

| Key | Action |
|---|---|
| WASD / Shift | Move / run |
| Mouse | Aim the flashlight cone |
| E | Open or close the nearest door or window shutter |
| F | Toggle the see-through cone |
| F1 | Draw the visibility polygons |
| F2 | Cycle view: final, mask RGB, lit amount, raw scene |
| F3 | Hide the stats overlay |

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

The world (`Runtime/World/`) is generated procedurally on Play from flat triangles.
`LowPolyMeshBuilder` gives every triangle its own vertices, face normal and slightly jittered
colour, with sRGB vertex colours converted to linear. Its primitives are faceted tubes and cones,
jittered icospheres, irregular hexahedra and a jittered, triangulated ground grid. `LowPolyModels`
builds the props from them: gnarled dead trees, boulders, plank and stone walls, doors and shutters,
crates, campfires, lanterns, crows and the humanoid player and wandering figure. The models use the
same footprints as the colliders and occluders.

## Commands

Run these from this folder:

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.CreateSandbox
```
Regenerates the materials, the scene and the URP shadow settings (also in the **Vision** menu).

```bash
unity test . --editor-version 6000.6.3f1 --mode EditMode
```
Runs the 20 EditMode tests: visibility polygons, doors, spatial hash, triangle winding and normals,
model sizes and determinism.

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
