# Manhunt (Unity)

Unity port of the 2D browser game **Manhunt** (github.com/zchris07/manhunt, `main`), the blueprint for its
map, objectives, items and rules: a top-down 2.5D horror game on an orthographic, 60°
pitch camera over a flat-shaded low-poly world, lit by a 3D port of the 2D visibility-polygon
illumination system. It opens on the title screen: **Create lobby** or **Join** to play online, or **Testing mode** alone.

The map is the original's at **3 cm per original unit**: a 180 x 180 m forest (6000 units) with the
procedural 36 m central building (1200 units) in the middle, its north exit gate and fenced yard, the
survivors' spawn in the south, clearings joined by footpaths, three cabins, a lake with a dock, fences,
logs, tall grass to hide in, power lines, a graveyard, an abandoned playground and a hanging tree, and
about 2,350 evergreens and dead trees. Every map is generated at runtime from a seed (about 3.8 s);
**New map** in the game menu builds another. Survivors repair the generators, then open the gate and
escape; Zach Branch hunts them. The rules are the original's, ported line for line (see *The match*). The level is authored in design units (a person is 1.8 m) and its root is scaled by `WorldScale.S`
= 2, so the camera sits twice as close. Characters are 222-triangle mannequins animated by a procedural
walk and sprint that adapts to any slope and, at the original's speeds, strides like a sprinter.

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
map), so bindings can be changed there or rebound at runtime. The keys follow the original's.

| Keyboard and mouse | Gamepad | Action |
|---|---|---|
| WASD or arrows | Left stick | Move (crawl while downed) |
| Shift | Left stick press | Run (Zach: sprint) |
| C or Ctrl | B / Circle | Crouch |
| Mouse | Right stick | Aim the light cone (whichever moved last) |
| E | X / Square | Interact: pick up, open or close a door, hide or leave, revive, heal, unstake; **hold** on a generator or the gate lever. Zach: pick up a downed survivor, stake, search a hiding spot, damage a generator |
| 1-9, 0, -, = or the wheel | D-pad left/right | Select an inventory slot (twelve in testing mode) |
| Left mouse | Right trigger | Use the selected item (survivors); swing the machete, hold to charge a heavy swing (Zach) |
| Right mouse | Left trigger | Zach: lunge |
| Space | A / Cross | Slam a pallet down |
| G | D-pad down | Drop the selected item |
| F / Q / R | Y / LB / RB | Zach: Soundcloud Burst / Penjamin or Hemp Battery / Hemp Beam |
| Y / N | D-pad up | Answer yes or no |
| T | – | Testing: switch between Zach and a survivor where you stand |
| M | – | Full map (the minimap is always on) |
| V | – | Speed mode (testing): full sprint, +500% speed |
| R | – | Get back up when downed (testing, survivors) |
| Esc | Start | Game menu (does not pause) |
| F1-F5 | – | Visibility polygons, debug views, stats, look panel, camera effects |

M, V, Esc and F1-F5 read the keyboard directly and are not part of the actions asset.

### Online play

- **Create lobby** opens a lobby on TCP port 7777 and shows its room code, this machine's `IP:port` (Copy puts it on the
  clipboard). Others type it in **Room code** and **Join**. On a LAN that is all; over the internet the host forwards port
  7777 on their router (or everyone joins one VPN). The first time a lobby opens, Windows asks whether to let the game
  through its firewall: allow it on private networks for others to reach you.
- **The lobby** is the original's: the players (the host crowned, everyone's readiness and ping), your role preference
  (Survivor, Zach or Either) and Ready, and the host's settings: hunters and survivors (1-9 each), a map seed (blank for
  random), testing mode, a preview of the night with its auto-balance, **Shuffle roles** and **Start the night**; a chat
  beside them. The host can fix anyone's role by clicking it (auto, Zach, survivor, spectator); otherwise roles are split
  as in the original (hunter slots by preference, survivors up to the cap, the rest spectate). A match needs a Zach and a
  survivor unless testing mode is on, and then anyone may manage the lobby.
- **The night**: everyone builds the host's map from its seed (its fingerprint is checked, so different versions can't
  mix), and plays at the host's pace. The host runs the rules for everyone; each player moves their own avatar and sends
  their input and position 30 times a second; the host sends each player only what changed (20 snapshots a second,
  about 15 KB/s each), with the events meant for them. Zach is never told where a hidden survivor is. Spectators and the
  eliminated watch the others (left click or the arrow keys). A player whose connection drops has 30 s to come back to
  their place. When the night ends everyone sees the results; the host's **Back to lobby** brings them all back.
- `Vision.Net`: `Wire` (framing and the message types), `Transport` (a TCP connection with its own reader and writer
  threads, the listener), `FieldPlan` and `Snapshot` (the state codec: each section field by field, sent when changed),
  `Lobby`, `NetServer`, `NetClient`, and `NetSession`, which joins them to the game.

### Menus and testing mode

- **The look** (`Vision.UI.UiKit`): the original's interface theme, bone text on near-black, Oswald for headings and
  buttons, Special Elite for typewritten labels, IBM Plex Mono for body text, red accents, square corners, thin
  borders, registration marks on cards, film grain and scanlines on the title screen.
- **Title screen** (`GameHud.Menus`), the original's landing page: the red-outlined kicker, MANHUNT with its red and
  cyan fringe and 6 s flicker, the blurb, dark pines along the bottom, and the card: your name (2-16 letters or
  numbers, remembered), **Create lobby**, a room code field (`IP:port`) with **Join**, the dashed **Testing mode**
  button, and How to play, Look settings and Quit (see *Online play*).
- **How to play**: the original's controls tables for survivors, Zach and both.
- **Screen effects** (`ScreenOverlays`, `MatchPresenter`): the original's pictures and timings. The Soundcloud Burst
  scare (black, the picture covering the screen and shaking, 2.5 s with 0.6 s fades, and a random 2.6 s slice of the
  song), The Grapes of Wrath flash on Zach (one of four pictures, 0.8 s, shaking) and the Waz-slain flash, notes (an
  old photo on torn, yellowed paper until clicked away), centre messages (STUNNED, You are down, THE GATE IS OPEN),
  big announcements (JARVIS ONLINE), camera shakes, stun stars orbiting a stunned head, the vine boom (louder the
  nearer) and the Penjamin gas loop.
- **Sound** (`AudioManager`): one-shots, a restartable clip, the gas loop and positional loops with the original's
  falloff curves, on three volume buses (Master, "Soundcloud Burst", "Sexton's reel") set in the settings and saved.
- **Testing mode**: the match restarts under testing rules. Survivors get the original's whole testing kit
  in twelve slots (shotgun, golden pump, P250, 0.50 cal, bottles, piss jars and books nine each, goggles,
  mini shield, Mr Beast bar, gas trap, Doctor Pepper), never used up; Zach gets two Hemp Batteries and his
  beam charges. **T** switches between Zach and a survivor where you stand. Nobody wins. The whole map is
  revealed. **Speed mode** (menu or V) keeps the sprint meter full and moves six times as fast (+500%).
- **Settings** (Esc) leave the world running, as the original does: Resume, Speed mode, **Pace**, New
  map, Look settings, Full screen, How to play, Quit to main menu, the three volume sliders, and the controls for
  your role. Testing mode adds the original's **TEST EFFECTS** panel (left): every stun and flash played on yourself,
  Respawn NPCs, and **dummies**: inert survivors and Zachs spawned in front of you to down, carry, stake, cut down,
  revive, heal, shoot or stun.
- **Other players** (`PlayerPuppets`): dummies (and, online, the other players) are drawn where the match has them,
  walking at the speed they move, lying down when downed, over Zach's shoulder when carried, gone when hidden or out of
  the match, with stun stars when stunned. Once you are out, the camera follows someone still in (click or the arrow
  keys switch who). The Hemp Battery zooms Zach's view out, eased in and out, as the original. **Pace** scales every movement speed from 30% to
  125% of the original's: 100% is the original game (a survivor walks 4.3 m/s and runs 6.8 m/s), about
  37% is this port's earlier 1.6 m/s walk. The choice is saved; online, the host's applies.
- **Maps** (`MapHud`, `MapPainter`, `FogOfWar`): the minimap (top right, about 57 m across) and the full
  map (M) show the level painted from above, under a fog of war that stays black until your own light
  has been there. Supplies, generators and the gate appear once seen; the **YOU** arrow and ring follow
  you on both. Testing mode shows everything, and a click on the full map teleports you there.

### HUD and game systems

- **The match** (`Vision.Game`): the original's host simulation, ported from its TypeScript into plain C#
  (no MonoBehaviours): `Balance` (every number of `balance.ts`), `MatchRules` (scaling to the player
  count, the win check), `MatchSim` (players, prompts, interactions, combat, items, abilities, objectives,
  senses), `Movement` (stamina, lunge, knockback, crouch, crawl, wading). `MatchHost` runs it at the
  original's 30 ticks a second, feeds it the local input, and puts its state on the level (generators,
  gate, doors, pallets, supplies, windows). Each player moves themselves and reports where they are; the
  rules move a player only by placing them. This is the seam online play plugs into.
- **No clock**: the night ends when no survivor is left standing (survivors win if at least half
  escaped) or every hunter has left.
- **Health and shield**, bottom left. A survivor's health never regenerates; the blue shield takes damage
  first. Zach's machete takes a third (two thirds charged); at zero you are **downed** and crawl. Zach
  carries the downed to a stake; a teammate can cut them down, and the second staking (or a minute on the
  stake) eliminates.
- **Stamina**: eight seconds of sprint (Zach six); running dry locks it for 1.5 s while it refills.
- **Supplies** (`Pickup`, `Inventory`): the original's twelve kinds at its counts (96 in all, and five more beside the ambulance). Eight
  slots; identical items stack; each gun takes its own slot and keeps its rounds; a full inventory drops
  the last slot's item for the new one. Select a slot, left click to use it. Duck confit heals to full,
  a Mr Beast bar gives 20%, a mini shield is drunk over 2 s for 25% shield.
- **Generators and the gate**: hold E for the original's repair time (70 s, scaled to the player count;
  +25% for each extra survivor on it). With enough running, hold E at the lever for 20 s and the gate
  opens onto the yard; walking out escapes.
- **Stakes**: the original's scarecrow stakes, three candidate spots per clearing (not the spawn's), away from
  generators, cabins, campfires and the spawn, spread out by farthest-first picking; a match uses survivors + 4 of
  them (6 to 12). Zach carries the downed there; teammates see a pulsing red ring over a staked survivor anywhere.
- **Generators under attack**: Zach kicks a part-repaired generator (2 s): 8% off at once, then it keeps running
  down until a survivor works on it.
- **Hiding**: tall grass, wardrobes, beds, lockers and barrels (in 0.6 s, out 0.5 s). Hidden, the stamina bar shows
  your breath: Space holds it, and running out makes you gasp. Zach searches a spot and drags out whoever is in it.
  **Pallets** beside doorways (Space).
- **Pallets, doors and windows**: Space slams a pallet (Zach caught under it is stunned); two machete hits break a
  dropped pallet, two break a closed door (it stays open for good, the panel gone), one smashes a window, which
  anyone can then climb through slowly. **Crouching** (C or Ctrl) lowers the hips and bends the knees.
- **Effects** (`Vfx`, the `Vision/Fx` shader): particle bursts, rings and ribbons, either hidden outside the viewer's
  light like characters (splinters, glass, sparks, blood, the scent trail) or drawn above the dark by a senses camera
  stacked on the main one (breathing Zach hears through a hiding spot's door). Zach sees survivors' scent as red smoke
  wisps where they ran and blood where the hurt have been, fading over ten seconds.
- **Zach's kit** (`MatchEffects`, `SoundBank`, `GameHud.Hunter`): his HUD is the original's ability bar (machete or
  golden pump, lunge, Soundcloud Burst, Hemp Battery, Penjamin, Hemp Beam) with keys, cooldown shades, charges and
  meters, his status lines (speed lost to his wounds, recovery, stunned, abilities off) and the generators he has
  heard being repaired. The machete charges with a red ring at his feet and a rising swell, swings with a swish
  (heavier when charged) and leaves a smear (pale, or dark blood-red when heavy); the lunge leaves speed lines; the
  Burst is a purple concave lens racing across the map through walls with fading echoes; Penjamin rolls out soft
  yellow puffs along a narrow cone (blue for 50 Nic) that show above the dark; the Hemp Battery is a pulsing green
  disc; the Hemp Beam gathers an orb then fires a white-green beam with the original's repulsor loop. Guns flash and
  leave pellet tracers. Sounds come from the fetched CC0 packs, picked at random per cue with pitch and volume
  jitter, placed and panned where they happen.
- **Items in play** (`ItemViews`): thrown bottles, books and jars spin through the air; gas traps sit on the ground
  and blink once armed; galaxy gas billows purple, pink and blue with twinkling stars; dropped items lie where they
  fell (with models for the P250, the 0.50 cal, the jar of piss and the golden pump). G drops the item in hand; **Tab**
  opens the original's inventory editor (click a slot, then another, to swap them); clicking a slot takes it in hand.
  JARVIS shows the user the whole map; night vision tints the view green. Drinks, bites, throws, glass, books,
  splashes, traps, pick-ups and reloads all have their sounds.
- **Sound polish**: footsteps on every character's footfalls (concrete in the building, boards in the cabins, grass
  outside; louder running, softer crouched, heavier for Zach). You hear your own, and others only up close, since the
  original has none. Doors creak open and bang shut whoever moves them (NPCs too), shotguns take a shell before the
  rack, guns click empty, notes rustle. The original's robot announcements ("Jarvis online", and "Hemp battery
  activated" the first time ever) are the Windows voice, baked and pitched down. "SHANE JEANS HAS BEEN ALERTED"
  flashes as in the original. Characters climb through broken windows and bow over the notes they read.
- **Results**: the original's end screen (who won and why, the time, the generators, and each player's numbers), and
  a match clock under the compass.
- **NPCs** (`Game/Npcs`, `NpcViews`, `GameHud.Npcs`): the original's townsfolk, each with their own look and rules.
  **Shane Jeans** wanders with a faint light; crowd him or keep your flashlight on him and his alert bar fills, then a
  red "!" and he tails you as a beacon for Zach (to the sound of his footsteps) until Zach comes close, two bottles
  or a shotgun blast shake him off, or he tires. **Jaden Nguyen** is alerted the same way but draws a pistol, hangs
  back and shoots until you have lost half your health; three survivor hits kill him and drop his pistol, and Zach
  who slays him gets a lunge charge and more reach. **Marc Cortez** starts in the building, opens doors and hands out
  duck confit. **Waz** takes a looksie (you see 10% more); slaying him flashes his picture. **Njaaron** asks "you
  wanna go to the Y later?" (Y or N): yes and he follows you and fights Zach off, no and he fights you; he explodes
  when he dies. **Soham** says Hi and explodes. **Thomas Bourgeois** hands over a full Hemp Beam. **Monique
  Bourgeois** gives an arrow to Zach, then Mr Beast bars, and shoots whoever attacks her with a 0.50 cal. Name tags,
  speech bubbles and alert bars show over the NPCs you can see; testing mode shows them all, and on the map.
  NPCs that chase find their way with `NavGrid` (A* over a 25-unit grid of the level, cached per radius).
  **Sexton Science** wanders with his reel playing around him; talk to him, press E again to keep listening, and he
  hands you JARVIS and walks off. Hit him as a survivor and he defends himself with Hemp Beams; Zach slays him in three
  and takes his beam. **Chris Zelley** paces round his ambulance until a survivor enlists him; then the first survivor
  left downed or staked too long gets a sprinting paramedic, a revive, and a halo and wings as he ascends. **Plasma.TTV**
  says "ggs" and hands out golden pumps; attack him and GAMER RAGE turns him into a purple beast who punches you down
  (only the beast can die, and drops his pump). **Chacko** sits on the lounge couch with his controller: a Dr Pepper for
  survivors, 50 Nic for Zach; kill him as a survivor and Jaden, Plasma and Shane hunt you; as Zach, he explodes. Every
  NPC carries a faint light, as in the original.
- **The ambulance, the lounge and the Four Notes** (`SandboxWorld.Story`): Chris's ambulance parks in the woods off the
  paths with a row of supplies beside it and a flashing light bar; the building's biggest room without a generator is
  the lounge, a couch facing a flickering TV; the Four Notes lie beside the paths, far apart. Zach's map shows every stake
  in play.
- The old **wanderer** stands in for an entity in the captures only.
- `GameHud` builds the HUD in code with uGUI, scaled from a 1080p reference.

### Look panel (F4)

Live sliders for finding the palette. Each slider shows its current value, **Copy values** puts all of
them on the clipboard (and in the log) as one line of text, and **Reset** restores the defaults. Values
persist between runs. The defaults are the tuned look.

| Slider | Range | Default | Effect |
|---|---|---|---|
| Contrast | 0-2 | 1.00 | Global contrast, pivoting on mid-grey |
| Saturation | 0-2 | 1.00 | Global saturation of every colour (0 = greyscale) |
| Lit brightness | 0-2 | 1.00 | Multiplier on everything lit (flashlight, campfires, lanterns) |
| Unlit brightness | 0-5 | 1.00 | Multiplier on the unlit ground and objects |
| Beam intensity | 0.5-2 | 1.15 | Strength of the flashlight beam only |
| Beam edge falloff | 1-6 | 2.50 | Exponent of the beam's fade toward the screen edge |
| Blur start / end (m) | 0-30 | 5 / 13 | Distance from the player where the distance blur begins and is full |
| Blur max (px) | 0-8 | 3.00 | Full distance-blur radius in screen pixels |
| Camera effects | on/off | on | Vignette, film grain, light flicker and the distance blur (also F5) |

## How the lighting works

All visibility math is 2D on the ground plane (world X,Z), then projected back onto the 3D world.

1. **Polygons** (`Runtime/Visibility/VisibilityComputer.cs`). An angular sweep casts rays at every
   nearby occluder endpoint and at ±ε around it, plus evenly spaced arc rays. Hits are sorted by angle.
   With many segments nearby, a fan of 1,024 coarse rays first drops every segment that lies wholly
   behind what they hit, so the cost follows what is visible rather than everything in range.
   `OccluderSet` holds the segments in a spatial hash. Its `Version` bumps whenever a door opens or
   closes, which throws away the cached light polygons. Occluders are box or N-gon footprints
   (`Occluder.cs`) for walls, trunks, rocks, crates, closed doors and shutters.
   - Cone: fixed half-angle; it reaches the edge of the screen (the distance along the beam to the edge
     of the visible ground, plus 6%) at any resolution.
   - Proximity circle: small 360° polygon around the viewer.
   - Light sources (`VisionLight.cs`): the 6 nearest each frame; static ones cache their polygon.
   - See-through cone: ignores occluders and is drawn at 70%.
2. **Mask** (`Runtime/Rendering/VisionMaskRenderer.cs`). The polygons are rasterised into a
   **world-space** square texture centred on the camera's ground focus. It runs at about half screen
   resolution and is blurred, more the farther from the light (below).
   - B = viewer light (cone, proximity, see-through) with distance falloff. The beam fades as
     1 - (d / reach)^p (p = 2.5): bright near the player, dropping faster toward the screen edge. It also
     fades over the outer 35% of its half angle (`VisionViewer.coneEdgeSoftness`).
   - G is unused: light-source light shows whether or not the player has a line of sight to it.
   - R = light sources with distance falloff.
   - A = character shadows (below).

   The mask is world-space, not screen-space, because the camera is pitched and the world has height.
   Every shader samples it at a fragment's world X,Z.
3. **Composite** (`VisionCompositePass.cs`, `Shaders/VisionComposite.shader`). This is a Render Graph
   full-screen pass injected per camera. It reconstructs each pixel's world position from depth.
   - The scene is blurred with distance from the player (Darkwood-style; a camera effect).
   - `lit = max(B × beam intensity, R)`: lit areas are visible through walls; entities in them are not.
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

`BuildingPlan` ports the original's warehouse generator (`shared/src/map/warehouse.ts`), scaled to the
building: a 16 x 16 grid of 2.25 m cells (the original: 10 x 10 of 3.6 m). The roof is never drawn.

- **Footprint**: not a perfect rectangle. Whole cells are cut out of one or two corners: an L, a T or U,
  an S or a square with a small loading-dock notch. The notch is flat open ground and the walls turn
  with it.
- **Rooms**: the original's BSP splits the grid into leaves of at most 6 x 6 cells; about half become
  rooms (the two largest always), most shrunk a cell inside their leaf so corridors run round them.
  The **loading bay** sits on the north wall behind the gate.
- **Maze**: the original's recursive backtracker carves a maze through every cell (entering a room
  visits all of it), then 16% of the remaining walls are knocked out for loops. The corridors twist,
  dead-end and loop like a maze rather than a real office; straight runs become one hallway each.
- **Openings**: corridor meets corridor with no wall; a corridor meets a room through a doorway, half of
  them with a door as in the original (restrooms always), some standing open. Outside: the original's
  entrances (two south, one north, one or two east and west; 40% open with a pallet), a window in
  every third cell of wall (a quarter boarded), and the **north exit gate** with its lever and the
  chain-link **yard**. Up to seven barricades.
- **Room types** (office-like): offices, storage, break room, restrooms, locker room, workshop,
  electrical and server rooms, a studio set for the night shoot, the loading bay and a boiler room, each
  furnished by its own recipe.
- **Generators**: two, in any room with the space for one (never a corridor), spread apart.
- **Fittings**: lockers along the corridors (up to 8), wardrobes, beds and barrels to hide in; pipes,
  ducts, breaker panels, vent grilles, posters, clocks, stains, cobwebs and dead plants. Furniture keeps
  every doorway clear and covers under half of each room.
- **Light**: only part of the building is lit, each light by something that belongs there: fluorescent
  fittings in corridors and offices, bare bulbs in storage and workshops, desk lamps, the vending
  machine's glow, the boiler's fire, server-rack lights, stage lights and green exit signs. 6-16 work;
  the rest are dead fixtures.

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

- **Models** (`CharacterSpec`, `CharacterBuilder`): every character is built on the same 51-bone rig, flat-shaded and
  vertex-coloured, at most **500 triangles**: a ten-sided torso, an eight-sided head with jaw, brow, nose and ears,
  six-sided limbs with knee, elbow and boot rings, mitten hands with a thumb, wedge feet. The **survivor** stays grey
  and identical for everyone (tonal greys: darker trousers, gloves and soles). **Zach** is Jason Voorhees: 2 m, broad
  and heavy-limbed with a slight hunch, a white hockey mask with red chevrons, eye holes and vents, a torn dark work
  shirt hanging in tails, bare forearms and gloves. The **NPCs** use the original's look table (skin, hair, shirt,
  jacket, trousers, shoes) with features to fine-tune later: hair styles, a backwards cap, glasses, a headset, hoods,
  hi-vis bands, a star of life, a checked flannel, a jersey band.
- **Props** (`PropModels`): separate meshes of at most 130 triangles (machete, shotgun, golden pump, P250, 0.50 cal,
  flashlight, magnifier, tablet, controller, bottle, book, jar, can, bar) held on hand sockets.
- **Animation**: the procedural walk and sprint (`GaitSolver`, `HumanoidAnimator`) adapts to slopes and, at the
  original's speeds, strides like a sprinter. On top of it, `ActionLayer` plays keyframed clips (`ActionClips`,
  Catmull-Rom through the keys, eased blends, events such as "release", "hit" and "kick") on the bones each clip owns,
  so the legs keep walking under an upper-body action; hits add a damped flinch from the blow's direction.
  `CharacterView` picks the model, the held prop and the clip from the match: repairing, the lever, healing and
  reviving, picking up, hiding, drinking, eating, talking, planting a trap, aiming and recoil, goggles, JARVIS,
  stunned, coughing in Penjamin's gas, the scare, staked, carried, crawling; Zach's charge, light and heavy swings,
  lunge, carry, lift, stake, search, kicks, burst, Penjamin, Hemp Battery and beam.
- **Animation lab** (`CharacterTests`): every clip is played through at 240 Hz and checked for pops (bone speeds under
  1000 deg/s, 2600 for strikes and throws), single-firing events and a clean end; key poses are checked for where the
  hands go (in front for the lever, at the mouth when drinking, overhead on a stake, the gun pointing forward).
  Captures `82_character_lineup` and `83_action_sheet` show every model and every clip.

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
Runs the 121 EditMode tests: visibility polygons, doors, triangle winding and normals, model sizes and
determinism, the polygon budget, the mannequin, the gait on flat ground and steep ramps, the terrain and
paths, hidden-segment culling, the map layout (the original's scale and rules, the lake), the building
(spaces tile the notched footprint on the grid, a maze of corridors with dead ends and loops, every room
reachable, openings in walls, the original's entrances and the north gate, generators within their
rooms' space, furniture clear of doorways, lamps that fit their rooms), supplies at
the original's counts, health and shield, downing, stacking, healing, generators and the gate, the fog
of war and the painted map (bound to a level built before the HUD), the menus and testing mode, New map,
generation time and the saved prefabs.

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

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindowsDev
./Builds/WindowsDev/VisionSandbox.exe -visionPerf Logs/perf.txt -screen-fullscreen 1 -screen-width 2560 -screen-height 1600
```
A frame-time check under play: from the title screen into testing mode, it walks and sprints the player
from spawn through the building for 30 s, then writes frame-time percentiles, the costliest profiler
markers (development build) and the slowest frames, screenshots the minimap and full map, and quits.
It also runs on the release build without the markers.

## Known gaps

- CI's Unity licence activation has been failing since early October 2026 ("Access token is unavailable",
  then the runner kills Unity); the tests pass locally. The `UNITY_LICENSE`, `UNITY_EMAIL` and
  `UNITY_PASSWORD` secrets need refreshing (and old activations returned on the Unity account).
- Online play has no host migration: if the host leaves, the match ends for everyone. A dropped player can rejoin only
  from the same running game (within 30 s), not after restarting it.
- Media and fonts are credited in `CREDITS.md`; `tools/fetch_sounds.py` re-fetches the sound packs (hash-checked).
- The polygon pass runs on the main thread. That is fine for this map, but a much larger one will want
  Burst/Jobs.
- Hills are visual only: a crest does not hide what is behind it.
- Tree canopies can hide the player when they walk behind one.
- The distance blur also softens the edges of near objects that overlap far ground.

