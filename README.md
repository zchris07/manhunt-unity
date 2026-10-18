# Manhunt (Unity)

Unity port of the 2D browser game **Manhunt** (github.com/zchris07/manhunt, `main`), the blueprint for its
map, objectives, items and rules: a top-down 2.5D horror game on an orthographic, 55°
pitch camera over a flat-shaded low-poly world, lit by a 3D port of the 2D visibility-polygon
illumination system. It opens on the title screen: **Create lobby** or **Join** to play online, or **Testing mode** alone.

The map is the original's at **3 cm per original unit**: a round forest 180 m across (6000 units; no wall round it, the
woods running on into the dark to the end of the world 33.6 m further out, see **The edge of the woods**) with the
procedural 36 m central building (1200 units) in the middle, its north exit gate and fenced yard, the
survivors' spawn in the south, clearings joined by footpaths, three cabins, a lake with a dock, fences,
logs, tall grass to hide in, power lines, a graveyard, an abandoned playground and a hanging tree, and
about 2,350 evergreens and dead trees. Every map is generated at runtime from a seed (about 3.8 s);
**New map** in the game menu builds another. Survivors repair the generators, then open one of the two exit gates
and escape (or, the last one left, jump into the hatch by the dock); Zach Branch hunts them. There is never a time
limit on the night. The rules are the original's, ported line for line (see *The match*). The level is authored in design units (a person is 1.8 m) and its root is scaled by `WorldScale.S`
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
| Ctrl | B / Circle | Crouch |
| Z / C | – | Turn the view 45° left / right (eased over a quarter of a second; W keeps moving up the screen, the minimap and full map turn with it; each match starts turned 45° off the world's grid, so walls and buildings run diagonally across the screen) |
| Mouse | Right stick | Aim the light cone (whichever moved last) |
| E | X / Square | Interact: pick up, open or close a door, hide or leave, revive, heal, unstake; **hold** on a generator or the gate lever. Zach: pick up a downed survivor, stake, search a hiding spot, damage a generator |
| 1-9, 0, -, = or the wheel | D-pad left/right | Select an inventory slot (twelve in testing mode; there the wheel zooms the view in and out instead) |
| Left mouse | Right trigger | Use the selected item (survivors); swing the machete, hold to charge a heavy swing (Zach) |
| Right mouse | Left trigger | Zach: lunge; survivors: aim the 0.50 cal (hold) |
| Space | A / Cross | Slam a pallet down |
| G | D-pad down | Drop the selected item |
| F / Q / R | Y / LB / RB | Zach: Soundcloud Burst / Penjamin or Hemp Battery / Hemp Beam; survivors: R fires the Hemp Beam once Thomas gives it |
| Y / N | D-pad up | Answer yes or no |
| T | – | Testing: switch between Zach and a survivor where you stand |
| Tab (hold) | – | Full map (the minimap is always on); survivors also drag items between slots, or off the bar to drop the stack |
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
  under them. Between them, **Balance & difficulty**: sliders anyone in the lobby (host or joiner) can move, everyone
  seeing them move: how many generators (2-8, five by default), each side's move speed, sprint duration and sprint
  recovery, the damage each side deals, the shotgun's, the golden pump's (the Legendary pump and Zach's alike), the
  P250's and the 0.50 cal's damage, and the rounds each of those guns starts with. A number off its default shows in
  amber; only the host can **Reset to defaults** (the game's own numbers). The host's own settings changes never undo
  someone's slider, the match plays by them on every machine, and offline the game's own numbers hold (`Game/Tuning`,
  `GameHud.Tuning`, the wire's `Tune` and `ResetTune`; the wire is version 2, so an older build is told it's a
  different version). The host can fix anyone's role by clicking it (auto, Zach, survivor, spectator); otherwise roles are split
  as in the original (hunter slots by preference, survivors up to the cap, the rest spectate). A match needs a Zach and a
  survivor unless testing mode is on, and then anyone may manage the lobby. The host can **lock the balance sliders**
  (the button beside Reset to defaults): while locked only the host moves them, everyone else seeing them greyed out,
  until the host unlocks them.
- **The night**: everyone builds the host's map from its seed (its fingerprint is checked, so different versions can't
  mix), and plays at the host's pace. The host runs the rules for everyone; each player moves their own avatar and sends
  their input and position 30 times a second; the host sends each player only what changed (20 snapshots a second,
  about 15 KB/s each), with the events meant for them. Zach is never told where a hidden survivor is. Spectators and the
  eliminated watch the others (left click or the arrow keys). Someone joining while a match is under way isn't turned
  away: they spectate it, and play from the next. A player whose connection drops has 30 s to come back to
  their place (past that they're out of the match, and it goes on). When the night ends everyone sees the results; the host's **Back to lobby** brings them all back (the results put away for good, never coming back up over the lobby), and **Leave** takes you to the title screen.
- `Vision.Net`: `Wire` (framing and the message types), `Transport` (a TCP connection with its own reader and writer
  threads, the listener), `FieldPlan` and `Snapshot` (the state codec: each section field by field, sent when changed),
  `Lobby`, `NetServer`, `NetClient`, and `NetSession`, which joins them to the game.

### Menus and testing mode

- **The look** (`Vision.UI.UiKit`): the original's interface theme, bone text on near-black, Oswald for headings and
  buttons, Special Elite for typewritten labels, IBM Plex Mono for body text, red accents, square corners, thin
  borders, registration marks on cards, film grain and scanlines on the title screen.
- **Title screen** (`GameHud.Menus`), the original's landing page: the red-outlined kicker, MANHUNT with its red and
  cyan fringe and a faint 6 s flicker (toned down, with lighter grain and scanlines), the blurb, dark pines along the bottom, and the card: your name (2-16 letters or
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
  beam charges. **T** goes from Zach to a survivor, to that survivor's ghost (their body left at their feet, to take
  straight back with E), and back to Zach, where you stand. A generator starts the moment anyone works it. Nobody
  wins. The whole map is revealed. **Speed mode** (menu or V) keeps the sprint meter full and moves six times as fast (+500%).
- **Settings** (Esc) leave the world running, as the original does: Resume, Speed mode, **Pace**, New
  map, Look settings, Full screen, How to play, Quit to main menu, the three volume sliders, and the controls for
  your role (a list that scrolls inside the panel, with the wheel or its bar). Testing mode adds the original's **TEST
  EFFECTS** panel (left): every stun and flash played on yourself
  (being shot by a 0.50 cal among them: thrown back, slowed, the sprint locked out),
  Respawn NPCs, and **dummies**: inert survivors and Zachs spawned in front of you to down, carry, stake, cut down,
  revive, heal, shoot or stun.
- **Other players** (`PlayerPuppets`): dummies (and, online, the other players) are drawn where the match has them,
  walking at the speed they move, lying down when downed, over Zach's shoulder when carried, gone when hidden or out of
  the match, with stun stars when stunned. Once you are out, the camera follows someone still in (click or the arrow
  keys switch who). The Hemp Battery zooms Zach's view out, eased in and out over half a second. Every character
  moves at **70%** of the original's speeds (walking, running, the lunge, knockbacks, the NPCs; rounds, thrown items and
  the Burst keep theirs). **Pace** scales that from 30% to 125%: at 100% a survivor walks 3.0 m/s and runs 4.8 m/s (the
  original's 4.3 and 6.8, slowed by 30%), and about 53% is this port's earlier 1.6 m/s walk. The choice is saved;
  online, the host's applies.
- **Maps** (`MapHud`, `MapPainter`, `FogOfWar`): the minimap (top right, about 57 m across) and the full
  map (hold Tab; the wheel zooms it up to four times about the pointer and dragging pans it, never past the edge of
  the world in its circle, the markers staying the same size and the map painted again at 2048 pixels off the main
  thread so it stays sharp; reset each time it opens; the wheel picks items otherwise) show the whole round world painted from above (black past its end), under a fog of war that stays black
  until your own light has been there; both turn with the view, so up on the map is up on the screen (labels and
  pictures stay upright). Every supply shows as its own inventory picture once seen, and generators and the gate
  appear once seen; the **YOU** arrow and ring follow you on both. Testing mode shows everything (the NPCs' dots
  turning and scrolling with the minimap like the rest of it), and a click on the full map teleports you there. The
  full map is a circle centred in its panel: the round world turns inside it with the view, so no square corner of
  the painted map ever shows.

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
- **Health and shield** over heads, as the original draws them: over every player you can see (yourself included) a
  health bar 5 units tall and 38 wide (46 for Zach, drawn at 60% against these slimmer models), with an ink border, a pale
  trail draining behind fresh damage, ticks at the thirds and a green fill; a survivor with shield has a thin blue
  shield bar above it, there only while they have shield (gone again once it's used up). A player's bars are opaque
  for five seconds after their health or shield last changed, then fade
  to 35%. NPCs your role can slay carry the same health bar (always opaque) over their heads, under their names.
  **Stamina** is the original's wide sprint bar centred above the inventory (SPRINT and the seconds left, cyan when
  boosted, red when locked; BREATH while hiding), with Zach's exact speed and recovery lines beside it. A survivor's
  health never regenerates; the blue shield takes damage first. Zach's machete takes a third (two thirds charged; it reaches 15% less far than the original's 124 units, and
  a charge takes a second longer: heavy at 1.85 s, letting go on its own at 4 s); at zero you are **downed** and crawl. Zach
  carries the downed to a stake; a teammate can cut them down, and the second staking (or a minute on the
  stake) eliminates.
- **Stamina**: eight seconds of sprint (Zach six); running dry locks it for 1.5 s while it refills. Zach can't sprint
  once he's more than halfway into a full charge (0.95 s of its 1.9 s); he can still lunge and use every ability.
- **The edge of the woods** (`MatchSim.Boundary`): three invisible rings round the map's centre: the first 4.68 m past the
  playable circle's 90 m (pushed out, so the stretch to the second is 30% shorter: 10.92 m), the second 15.6 m past the
  playable circle and the end of the world 18 m beyond that (50% further than at first; the world is made that much
  bigger). NPCs never wander past the first. Past it a survivor's
  light (the beam and the light round them together, evenly, the beam keeping its full reach) is plainly dying: it
  wavers, sags deep about every second, sputters nearly out and back through each sag, and now and then blinks almost
  out, from just past the first ring, growing (deeper and more often, quicker) the further out they go, at its full
  strength by the second; there's no warning sign between the rings, only the flicker (DON'T WANDER OFF and a big
  arrow pointing the way back in show only once the light is dying past the second). Night vision, JARVIS and the
  Hemp Battery still work between the rings and stop past the second (a JARVIS running ends); Zach's Hemp Beam won't
  fire past the first. Past the second the light grows fainter and fainter over three seconds,
  flickering faster, deeper and more erratically, until it's out and a survivor's whole screen is black; then a
  survivor loses 20 health straight through the shield every three seconds, the screen's edges pulsing red with each
  bite, and at zero is gone at once, not downed ("NAME wandered into the woods"). The third is where the world ends: a
  survivor who reaches it is gone the same way. Gone into the woods, their light stays out where they fell (no lit spot
  to come back to) and, in the black, "Sacrificed" is written out letter by letter in the middle of the screen
  (`WrittenText`, after the boss lines in the user's game Stroop: each letter, 68 ms after the last, stroked on as a bright
  pen line round its shape, then flooded with ink as the line thins, leaning in italic, each one morphing into place from
  larger, leaning further and set off down and to the right), then fades as the screen lifts to the dark so they can
  watch the rest. Zach's light dies too, but a small faint halo of light stays round him so he can see where he's going,
  and the dark never hurts him; he walks through the third ring and comes out on the far side of the map, mirrored
  through the centre (out on the left, in on the right), 20 health poorer, with "Zach crossed over" for everyone, at
  most once every ten seconds; while he can't cross (locked out, or 20 health or less) the ring stops him like a wall.
  On the maps the woods past the playable circle darken smoothly into the black of the world's end.
- **Supplies** (`Pickup`, `Inventory`): the original's twelve kinds at its counts (96 in all, and five more beside the
  ambulance). A quarter of them lie out in the open woods, at least 20 m from any path, spread evenly over the map (each
  placed at the spot farthest from those already down); the rest are where the original puts them, in the building,
  the cabins and the clearings and beside the paths, now anywhere 2-12 m from a path's edge; none within 10 m of the
  first ring. Eight
  slots; identical items stack; each gun takes its own slot and keeps its rounds; a full inventory drops
  the last slot's item for the new one. Select a slot, left click to use it. Duck confit heals to full,
  a Mr Beast bar gives 20%, a mini shield is drunk over 2 s for 25% shield.
- **Generators and the gate**: hold E for the repair time (52.5 s, a quarter
  off the original's 70, scaled to the player count; +25% for each extra survivor on it). **Skill checks**: while
  repairing, every 8 s a dial flashes up mid-screen with a gilded mark and a tick sweeping round it once, faster (350
  degrees a second, a little slower than at first so it's easier) and on a narrower mark (14 degrees) than any of the lock pick's; Space or a click on the mark takes
  4 s off the repair, a miss or a tick that gets all the way round costs nothing (`MatchSim.Checks`). Every generator
  on the map has to run (the lobby sets how many: the graveyard's always, one in the building, two from five on, the
  rest in the woods). Then both exit gates have power: the building's, and a second one of its own beside **Miga
  Sushi** (a chain-link yard with a roll-up gate facing the middle of the map, its lever on a post outside and a lamp
  over it that lights up with the power). Hold E at either lever for 10 s (half the original's 20) and that gate rolls
  up and stays open; walking into its yard escapes. The moment a gate opens **the end game** begins: a 2-minute timer
  (the clock under the compass turns red and counts it down) after which anyone still on the map is taken by the
  Entity. With only one survivor left on the map (of a night that started with two or more), **the hatch** on the bank
  by the lake's dock opens, its lid up and a cold light out of it: walking onto it is an instant escape, unless Zach
  gets there first and slams it shut (E), which powers the gates and starts the end game. Nothing else ends the night:
  there's no time limit (`MatchSim.Exits`, `World/Hatch`, `SandboxWorld.Exits`).
- **Stakes**: the original's scarecrow stakes, six candidate spots per clearing (not the spawn's), away from
  generators, cabins, campfires and the spawn, spread out by farthest-first picking; a match uses survivors + 4 of
  them (6 to 12). Zach carries the downed there; teammates see a pulsing red ring over a staked survivor anywhere.
- **Generators under attack**: Zach kicks a part-repaired generator (2 s): 8% off at once, then it keeps running
  down until a survivor works on it.
- **Hiding**: tall grass, wardrobes, beds, lockers and barrels (in 0.6 s, out 0.5 s). Hidden, the stamina bar shows
  your breath: Space holds it, and running out makes you gasp. Zach searches a spot and drags out whoever is in it.
  **Pallets** beside doorways (Space).
- **Smooth poses**: an action's clip blends out into the walk and the flashlight from wherever it was (several can fade
  at once), and the flinch from a hit is a spring integrated in small steps and kept to a believable angle, so a long
  frame can no longer fling an arm over the head and leave it there.
- **Pallets, doors and windows**: Space slams a pallet (Zach caught under it is stunned); two machete hits break a
  dropped pallet, two break a door, shut or standing open (struck across its doorway or on the panel swung out from
  its hinge; it stays open for good, the panel gone), one smashes a window, which
  anyone can then climb through slowly. **Crouching** (Ctrl) lowers the hips and bends the knees. **Fallen logs and
  fallen trees** (found by kind, so the baked prop library's fallen trees count too) are knee-high: everyone sees, lights and shoots over them (the ground past one shows in the dimmer
  see-through shade, as behind a wall with night vision, while people standing behind it show in full), Zach's Burst
  and thrown items fly over them, and every player vaults them automatically at the window-climbing pace, hands
  planted, knees tucked, the body lifted up and over the top (and over a broken window's sill the same way), hands
  too busy for anything else: no item, gun or ability is used while climbing (a charge held into a climb is dropped); NPCs walk
  round them. **Fences** are climbed the same way: the split-rail fences are vaulted automatically at the same pace,
  the body lifted up and over the top rail, while NPCs walk round them (they don't block sight or shots, and still hide
  what's behind them as anything standing does). (The building yard's tall chain-link fence stays solid: the way out is
  through the gate.) **The graveyard** has one way in, a pair of ornate wrought-iron gates between stone pillars (arched
  tops, bars rising into gilded spear points, rings along the foot, scrolls and a ringed cross), chained and padlocked
  at the start, and a generator always stands in its middle among the graves. It's 22 m by 17 m, half as much ground
  again as at first. Its fence stands 20% taller than at first, posts crowned with tall spikes and a collar of thorns
  and every bar rising past the top rail into a spear point, and no one climbs it, Zach included: it's a solid wall to
  every body (sight and shots pass between its bars), so the gates are the only way in. The graves stand in four plots
  of neat rows facing the gates, either side of the walk from the gates to the generator and of the walk across
  between its two light towers, where their cables run; a marble angel on a pedestal stands at the head of the walk
  behind the generator. Ten kinds of headstone, each carved: a round-topped tablet on a plinth with a bordered,
  lettered panel and a cross cut above it, a ringed Celtic cross on two steps with knotwork down its shaft, an obelisk
  on a die with a bronze plaque, a broken tablet whose top lies fallen on the grave, a weathered wooden cross hung with
  an ivy wreath inside a picket rail, a ledger slab over the whole grave with a cross in relief, the angel, a gothic
  arched tablet between pinnacled pilasters, a chest tomb with panelled sides and its lid knocked askew, and a double
  tablet for two. Most graves are kerbed in stone with corner posts and filled with chippings or earth, or heaped
  with earth, a few sunk with a footstone; some have a vase of flowers (some wilted) or a grave lantern, and moss
  creeps up the bases. A survivor at the gates picks the lock (E): a heavy iron dial
  fills the middle of the screen with a gilded mark on its ring and a bright tick sweeping round, and E, Space or a
  click stops it; five times on the mark (each spin faster, the mark narrower, turning the other way; every mark 10%
  narrower than at first, so it's 10% harder) and the lock gives, the chain falls away and the gates swing in; the
  click (or E, or Space) that opens it does nothing else while it's held, so a gun in hand never fires off it (nor
  off the last click of a generator's skill check); two misses and the pins drop, back to the first. Five pins and
  two miss lamps under the dial keep count, with a pulse of gold on each hit, a red jolt on a miss and START OVER when
  the pins drop; the match judges each press. Unlocked, survivors open and close the gates freely. Zach never opens
  them: shut again once unlocked, he can only smash them in, with four full charged swings (eight hits: two for each
  charged swing, one for a light one), or burn them away at once with a Hemp Beam; locked, they don't give at all,
  and nothing (the Hemp Beam, the 0.50 cal) breaks them. A fallen tree's torn-up root plate at its base is different: a solid wall that players, NPCs, thrown
  items and shots all run into and nobody vaults. It takes up only where the disc meets the ground, a thin upright
  ellipse across the trunk's end (as thick as the plate and the dirt banked at its foot, as wide as the plate), and it
  always stands straight up, never tilted with a tree lying on a slope (a lean would turn its side into a ramp the
  body walks up and over), its walls running from under the ground to above the plate. Every vault strip, the tree's
  own or any log lying into it, is cut back out of it. The central building's doors are standard office steel doors in steel frames: light-grey slabs with a
  lever handle, a kick plate and a narrow wired-glass vision panel.
- **Effects** (`Vfx`, the `Vision/Fx` shader): particle bursts, rings and ribbons, either hidden outside the viewer's
  light like characters (splinters, glass, sparks, blood, the scent trail) or drawn above the dark by a senses camera
  stacked on the main one (breathing Zach hears through a hiding spot's door). Zach sees survivors' scent as red smoke
  wisps where they ran and blood where the hurt have been, fading over ten seconds. The scent's thread is twice as thick
  and a fifth more opaque than it was (at its thinnest twice that again and at its thickest a fifth less, so it varies
  less; how solid it is varies at random along it, on its own, not with the swelling and thinning), and it's joined in the order it was laid (by how far along the trail each point
  is), never by its time: walking points are stamped four seconds older so they fade sooner, and joined by time they fell
  among the running points of four seconds before and the thread zig-zagged across itself; each piece of the thread
  reaches past its ends no further than the thread is thick, so a sharp turn never pokes out as a cross. The hurt drip blood at random moments
  (every 0.15-0.75 s), each drop landing somewhere about them as a miniature splatter a few centimetres across, laid on
  the ground's own facets (`BloodSplats`) and each its own shape: a round drop with a crown of specks, a teardrop with a
  tail of droplets flicked off it, a cluster of droplets or a star-burst with thin spikes, in dark crimson that dries
  darker as it fades. Standing still, no more than six drops land within half a metre of them; once they move on that
  far, the drops start again. Blood thrown off a hit or a charged cut is the same dark crimson.
- **Zach's kit** (`MatchEffects`, `SoundBank`, `GameHud.Hunter`): his HUD is the original's ability bar (machete or
  golden pump, lunge, Soundcloud Burst, Hemp Battery, Penjamin, Hemp Beam) with keys, icons, cooldown shades, charges and
  meters (the Hemp Beam's is the leaf), beside his stamina bar his health points (to a tenth, out of 100) and the exact speed his wounds take (continuous with his health, to 0.01%) and, with the Hemp Battery, his healing rate (1 + 10% of the charge left, to 0.0001x, with the charge to 0.01%), his status lines (stunned, abilities off) and the generators he has
  heard being repaired. His machete (always in his right hand) cuts in a wide **X**, back and forth along a figure 8:
  the forehand starts raised up beside his head, further back and out to the side, the blade lying back behind his
  head (as Jason holds it), comes over the top and falls from beside his right shoulder, mostly side to side, across
  his chest to his left waist, the arc swinging wide out in front, and loops on up over his left shoulder; the backhand
  falls from there across to his right waist and loops back up over his right shoulder; the torso turns well round
  with each cut, the shoulders leading the arm. Swings started within a second of the last one alternate, charged ones too; after a longer pause he starts
  again with the forehand. The torso winds toward the shoulder the cut starts from and unwinds through it, leaning in,
  the free arm reaching forward as a guard while the blade is up and swinging back against the cut. Charging, he sinks
  low and wide on bent knees with the blade cocked over the shoulder the next cut falls from (a forehand's raised beside
  his head, the blade back behind it), trembling (no ring at his
  feet); a charged cut drives his weight onto his front foot. Each swing comes with a swish (a full charged one pitched
  well down, with a deep, slow whoosh layered under it) and
  a swipe line through the air that shows the cut's reach: a band starting near the tip of the model's real machete and
  reaching out, the way the blade points, to the swing's actual attack radius (longer when charged), sweeping across with
  the blade: a pale steel sheen with a crisp bone-white edge along the reach for a light cut, gone in about an eighth of
  a second; dark blood red at its heart and brighter toward the edge for a charged one, flinging red droplets off the
  tip when it connects. The lunge leaves speed lines. The Burst is a translucent purple
  sound wave racing through walls on the rules' concave front: a bright trembling crest, a soft body, pressure bands
  rippling behind it and a faint smear of where it was a tenth of a second ago. Penjamin rolls out a cone of yellow smoke
  (blue for 50 Nic; Penjamin's 40% more opaque than 50 Nic's), and a survivor caught in it clamps both hands over their eyes while their
  light narrows and the dark goes pitch black, easing in and out over the original's 0.6 s, and their whole view is
  thrown out of focus: it swims (a slow wobble through the whole image), splits into two images drifting apart and back
  together (double vision), and both are blurred hard, the blur breathing in and out, the colour washed out and the
  edges closing in, strongest while they're in it and clearing over three seconds once the gas has worn off (a second
  longer than at first). 50 Nic's cone is 20% wider than Penjamin's (and reaches only 10% further), and for both
  gases the slow, the blur and the narrowed (and darkened) sight all last five seconds after the survivor is out of the
  cloud, falling off together, linearly, the sight widening back to its normal angle and every effect gone at once at
  the end of the five seconds (the choking damage still stops two seconds out). Penjamin reaches 30% less far than the original's, then 20% further than that, then 10%
  further again, then 15% further still (116.9% of the original's reach), in a cone 20% narrower than the original's,
  then 40% and then 30% narrower again, then 50% wider (5.04 degrees either side of the aim; 50 Nic's 20% wider than that), 30% fainter to begin with, its edges
  ragged and wobbling (the cone's radius pushed in and out by a slow noise rolling with the gas); it acts where it's drawn, the cone plus the width the
  cloud has right from his mouth, so a survivor standing in it close up is still caught. It goes straight through a shield to the health bar. Galaxy gas narrows Zach's
  light by 30% and blurs his view the same way, clearing three seconds after he's out of it. **The gases** (Penjamin, 50 Nic, galaxy gas and the scent trail; `VolumetricGas`, the
  `Vision/Gas` shader) are volumetric smoke after Black Ops Cold War's smoke grenade: each cloud is a box over the scene
  through which the view ray is marched, gathering a participating medium whose density is the cloud's shape (a cone for
  Penjamin, a blob sitting on the ground for galaxy gas, and for the scent a thin tube from each point of the trail to
  the next, overlapping so it runs on as one faint, unbroken, swaying thread (20% more solid and 30% thicker than at
  first) that swells and thins (half as much again as at first) on a strict repeat,
  thicker and more solid at its peaks, thinner and fainter in its valleys, peak to valley as far as a survivor sprints
  in two seconds) eroded by a tiling 3D Perlin-Worley
  noise made at load, so its surface billows in round lobes like cauliflower and frays into wisps, churning and drifting
  (carried along the way the gas blows: Penjamin's rolling out along the cone, whichever way he aims it). It's patchy
  rather than even: a much larger, slower pattern breaks Penjamin's, 50 Nic's and galaxy gas into thick pockets and thin
  stretches, never clear through: no part of a cloud is ever empty. (Blown wider, Penjamin and 50 Nic used to open
  holes and come apart: the patch pattern multiplied the gas down to nothing, the ragged edge pinched the cone to a
  sixth of its width, and the billows could eat its middle hollow. Now the thin stretches keep 40% of the gas, the edge
  pulls in at most half as far as it pushes out, and a cone's body always keeps a haze thickening toward its middle.)
  From the moment it's fired, Penjamin's whole cloud drifts slowly off on a wind of
  its own (a random direction for each cloud), spreading wider and taller and thinning as it's blown (the gas itself
  less dense as it fills more air, so it frays and opens up, not just fades), and it's seen for
  2 seconds longer before it's fully gone; all of that is only how it looks: where it acts doesn't move or grow, and it
  stops acting when it always did. Light is absorbed as it passes through (Beer-Lambert); each sample is lit by a
  short march toward a key light low to one side, so every lobe has a bright face and a shaded one, with a
  forward-scattering phase and ambient light. Each keeps its colour: Penjamin yellow, 50 Nic blue, galaxy gas purple,
  pink and blue (with its stars), the scent red. The gas never touches the scene: no collision, nothing conforming to the
  ground or objects; it simply covers the screen where it is (Penjamin unmasked, past the
  viewer's light, galaxy gas and the scent inside it; every cloud is drawn in the scene, since the senses overlay camera
  never drew the volumetric gas, which is why Penjamin's cloud had gone missing). The indicator rings left on the ground (Plasma's colour-cycling rings, the breath rings spreading
  from a hiding spot) are laid on the ground's own facets every 2.5 degrees round and every few centimetres across, so
  they follow each slope, bump and crease of the drawn terrain; the Hemp Battery and Zach's charge have no ring. The Hemp Beam (Zach's, a survivor's from Thomas, Sexton's) fires from the palm of the
  outstretched right arm like a repulsor: green-white motes spiral in and condense into one blinding point as it
  charges, then a layered beam, light green through and through rather than white at its heart (a bright pale green
  core, green body, wide glow, two twisting energy threads, shimmer flecks)
  runs to whatever stops it and splashes there with sparks spraying back and falling, embers, smoke and chips (drawn in
  the scene, so a wall or a tree between it and the camera hides it, as it does the bullets), to the
  original's repulsor loop; the body turns with the beam and the legs keep walking while the player moves. Every
  beam, whoever fires it, takes 1 HP ten times a second from anyone it touches (Zach, or survivors, the beamer's own
  side included; everyone has 100 HP) and gives NPCs a light hit every half second. Everything it can destroy goes the
  moment it touches it: glass in its path shatters, a door (shut, or standing open with its panel swung across it),
  a pallet (standing or dropped) and the graveyard's gates once unlocked and shut again are destroyed; never locked
  gates or a shutter. A player's bullets and pellets only strike an NPC
  that isn't already roused (alert, armed, angry, chasing) within 1.1 times of what their own screen shows (each
  client tells the host the rectangle of ground its screen covers), so a stray round never sets off an NPC they can't
  see; once roused, an NPC can be struck anywhere. Guns flash
  at the end of the barrel and every round (a pistol's, Jaden's, each pellet, the 0.50 cal's) flies out of it as a fast
  projectile drawn like Noita's: a single thin streak of white pixels, bright at its head and fading to nothing at its
  tail, at least one of the screen's art pixels thick (a few real pixels, as Noita's are), with no glow and no lingering
  trail, drawn crossing the screen at three quarters of the match's speed (the pistol's at 4500 units a second, pellets
  3750, the 0.50 cal 6000; the look only: what they hit, and when, is the match's), stopping where the shot stopped,
  pellets sparking there as they arrive (the sim's rounds start just ahead of the shooter). Rounds fly perfectly
  straight and level at the height they left the barrel, whatever the ground does below. **The laser and rounds are
  drawn over the ground by the GPU** (`Vision/FxOverGround`): every surface marks its pixels in the stencil as it's
  drawn, the ground (with its grass and paths) and fallen logs 1 and everything else 0; an overlay camera that keeps
  the scene's depth and stencil and comes after the darkness draws them, always on top where the ground is the nearest
  thing and depth-tested against what's actually drawn anywhere else, so no hill ever cuts them while every tree, wall,
  rock, prop and body nearer the camera hides them exactly where it's drawn. Where a line runs level under a rise, its
  points are first slid toward the camera along their view (the same spot on screen) to the barrel's height over the
  ground there, so what stands on the rise behind it never covers it. (Before, a ray was fired from points along the
  line at the colliders, which hid it wherever an invisible collision shape stood: a rock's 2.6 m wall cut a strip
  out of the laser well above a knee-high boulder.) Their sparks and the 0.50 cal's bursts are drawn the same way.
  Thrown bottles, books and jars also fly level at the height they left the hand, and where a rise would cover one
  it's drawn over the terrain unless something standing upright is in front of it. A thrown item never even looks to go into the ground: where the ground ahead rises
  toward it, it's lifted smoothly over the rise (it climbs a little before it gets there) and settles back to its level
  once past. Without its local sound the 0.50 cal cracks when it fires (a sound synthesised for the game in the
  character of a Noita rifle: a click-sharp, bright crack falling in pitch over a low thump, `tools/synth_sniper.py`);
  the charge is silent. **Local sounds**: a cue can name a
  preferred file (`local_*` in `Resources/Audio/Fx`, kept out of git because they're from other games, so they exist
  only in the user's own copy and builds): the P250's shot; the shotgun's (every shotgun, the golden pump and Daphne's included),
  half as loud as at first, its last stretch falling away by an inverse square; the 0.50 cal's (a bolt-action rifle's whole clip played to its end, the shot, the bolt
  worked and the ring of it, as loud as the shotgun, its last stretch falling away by an inverse square so the ringing
  never cuts off); the machete's slash landing on a survivor or an NPC (only a body: a door,
  window, barricade or anything else it strikes never plays it); the machete into wood (trees, evergreens, power
  poles, the cabins' doors, crates; one of five cracks, half as loud as at first, each fading out linearly to
  nothing), into sturdy metal (the building's doors, its exit gate, cars, light towers, lockers; one of
  three clangs, as loud and fading the same way), into other metal (generators, fences, the graveyard's gates, lampposts,
  barrels, racks; one of nine hits, as loud, held to the end) and into the wall of a building, cabin or shack (one of
  three slams); a window breaking; a bullet or pellet striking any body, a player's or an NPC's (one of three,
  quiet, held to the end); and a sting heard by everyone when a survivor is sacrificed (staked to the end, or gone
  into the woods). The first thing a 0.50 cal round goes into sounds as the machete striking it would. Without the
  files the slash, the window and the 0.50 cal fall back to their own sounds and the rest are silent. Every sound is
  heard by every player within 3000 units (the match sends each to everyone within hearing), falling off with
  distance from where it happens. Sounds come from
  the fetched CC0 packs, picked at random per cue with pitch and volume jitter, placed and panned where they happen.
- **Teammates**: a survivor sees the other survivors glow faintly, a soft light about each and a small bright disc that
  shows them through the fog, as in the original.
- **Item icons** (`ItemIcons`, `tools/icons`): every inventory slot and ability shows the original's own item art,
  rendered from its canvas drawing code: the bottle, the Grapes of Wrath, night vision goggles, the double-barrelled
  shotgun (held in the hand as a pump-action) and golden pump, Doctor Pepper, galaxy gas as a whippit tank in a galaxy wrap, duck confit, the Mr Beast bar,
  the mini shield as Fortnite's little flask of blue liquid, the P250, the 0.50 cal, the jar, JARVIS's tablet, the Hemp
  Battery and the leaf; the lunge, Burst, Penjamin and 50 Nic (blank in the original) get icons in the same style. A
  survivor's JARVIS and Hemp Beam sit beside the inventory as in the original.
- **Items in play** (`ItemViews`): thrown bottles, books and jars spin through the air; a gas trap is planted by
  holding the left mouse button the whole two seconds (let go before it's down and it starts over); gas traps sit on the ground
  and blink once armed (an armed one bursts too when a bullet, a pellet, a 0.50 cal round or a thrown bottle, book or
  jar passes over it, the thrown item landing there, though now only close to it: its trigger is 80% smaller than it
  was); galaxy gas billows purple, pink and blue with twinkling stars (its cloud, and the area it works over, 15% smaller and then 15% smaller again, and 30% less opaque than it was); dropped items lie where they
  fell (with models for the P250, the 0.50 cal, the jar of piss and the legendary pump). G drops the item in hand;
  holding **Tab** opens the full map and the inventory editor: drag an item onto another slot to swap them, or off the
  bar to drop its whole stack; clicking a slot takes it in hand. Empty slots are perfect squares; an item widens its
  slot smoothly into a rectangle that fits its picture and name (the book reads GRAPES OF WRATH, the trap GALAXY GAS
  TRAP, the bar MR BEAST BAR, the golden shotgun LEGENDARY PUMP, the goggles NIGHT VISION GOGGLES), and it eases back to the square when
  the item goes, or to fit whatever's swapped in, never smaller than the square.
  A shotgun picked up while carrying one adds whatever shells are left in it to the one carried (no second shotgun
  in the inventory); the Legendary pump is a different gun and never shares shells with a shotgun. Each pistol shot
  strays up to 2.5 degrees either side of the aim. Holding right click with the 0.50 cal aims it: the
  view widens 15%, you move 30% slower and can't sprint, and a red laser sight runs from the barrel along the aim to the first solid
  thing in its way (never the ground), seen by everyone: a perfectly straight line, over the ground and behind whatever
  stands nearer (see rounds above), drawn as Noita's laser sight. A faint red haze three pixels either side flickers in
  clumps and now and then drops out; the one-pixel core is red static, every pixel flickering on its own, broken into
  dashes that come and go, the odd pixel flaring white; and dust drifts slowly out along it away from the barrel,
  single pixels wandering a row off the line, fading in, catching the light and fading out, in clusters and empty
  stretches (all in the shader, from the beam's distance along it). A small dot flickers at the barrel, and where it
  meets something a bigger spot sputters and throws off the odd mote; aimed, the round goes exactly where it points, while from the hip it strays up to
  5 degrees. Its round races out along that same straight line at 8000 units a second and throws sparks, dust and chips
  out of every wall, tree or object it punches into and out of, as it gets there. It shoves Zach back a quarter less far
  than at first and slows him by half, the slow falling off linearly to nothing over a second; the first time a player
  takes it in hand they're told "Hold right click to aim". The shotgun stuns Zach half as long as at first (1.05 s). A jar
  of piss in Zach's face also blurs his view under a yellow filter, falling off linearly over two seconds. Zach's
  machete throws debris off every solid thing its swing meets (up to five in a swing, each where it was struck),
  by what it's made of (read from what was struck):
  wood throws long splinters, bark chips and sawdust (and cracks: local sounds cut from five wood breaks); stone grey
  chips, grit, a few sparks and a cloud of dust; metal a shower of sparks and dark flecks; glass bright shards; plaster
  and concrete white chips in a big dust cloud; more for a charged swing, and the charged swing plays at the light swing's speed (the same beats and length;
  only how it looks).
  JARVIS shows the user the whole map, with no animation of the body at all (no tablet held up); night vision tints the view green, with no green halo round the screen,
  and the goggles go on over the eyes (the hands stay free). Night vision and the Hemp Battery's x-ray fill the whole vision cone and run on past the edge of the
  screen whichever way you turn; as they come on, the light grows out of the torch to its full reach in half a second
  (the original's animation, quickened), and the Hemp Battery's zoom out and back in takes half a second too. Every attack item but galaxy
  gas breaks windows: a thrown bottle, book or jar smashes the glass and drops there, rounds go through it, and anyone
  can climb through the empty frame. Drinks, bites, throws, glass, books, splashes, traps and pick-ups have their
  sounds.
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
  or a shotgun blast shake him off, or he tires. **Jaden Nguyen** is alerted only by standing close to him (never by being
  seen or held in a beam), draws a pistol, hangs
  back and shoots until you have lost half your health; three survivor hits kill him and drop his pistol, and Zach
  who slays him gets a lunge charge and more reach. **Marc Cortez** starts in the building, opens doors and hands out
  duck confit. **Waz** takes a looksie (you see 10% more); slaying him flashes his picture. **Njaaron** asks "you
  wanna go to the Y later?" (Y or N): yes and he follows you and fights Zach off, no and he fights you. Zach saying yes
  gets his health coming back 10% faster for good (even after Njaaron's slain), and Njaaron goes with Zach: he follows
  him about and hunts down any survivor Zach lands a swing or an ability on, and any survivor who lands an attack on
  Zach; nothing of Zach's (his machete, lunge, beam, Penjamin, burst, golden pump) ever touches him; he runs 20% faster than the
  original's (210 units a second); he explodes when he dies. **Soham** says Hi and explodes. Whoever explodes (Chacko, Soham, Njaaron) goes up in a bloody blast: a hot flash, a cloud of red mist, blood sprayed out everywhere in streaking droplets with lumps of gore among them and smoke rolling up after, leaving dark blood stains on the ground for the rest of the match (a ragged pool where they stood, splashes and long streaks out to five metres, spatters beyond). **Thomas Bourgeois** hands his one full Hemp Beam to whoever talks to
  him first, Zach or a survivor (testing mode gives survivors one to try, in a thirteenth slot on the backslash key).
  A survivor's **Hemp Beam is an item** (from Thomas, from Sexton when he falls, or at Miga Sushi): a slot of its own,
  fired with a left click, its charges left kept on it, so dropped (G) and picked up by someone else it has the charges
  it had; Zach can pick one up too, its charges going into his own beam (R), up to a full set. **Daphne Cheng** (a young
  woman with long dark-brown hair) hands a P250 to each survivor who talks to her (once each); nobody can slay her, and
  slashed by Zach, or hit by a survivor's item or round, she stands her ground and blasts them with her shotgun (a
  survivor's shotgun with endless shells, its pump worked after every blast) for ten seconds or five blasts at most,
  then puts it away, every new attack starting her again, turning to keep them in her sights but never chasing them,
  firing whenever they're within 630 units and in sight. In testing mode **Respawn NPCs** (in the TEST EFFECTS panel) brings every NPC back new and unhurt at their spawn points, calls off the hunt for Chacko's attacker and clears Sexton's dropped beam, as the original's does. **Monique Bourgeois** gives an arrow to Zach (for 40 s a red arrow circles the survivor on their screen, pointing at Zach wherever he is, the seconds left under it), then Mr Beast bars, and shoots whoever attacks her (Zach only when he slashes her) with a 0.50 cal, reloading as a survivor's does, three shots at most an aggro cycle before she puts it away (an attack after that starts her again); Daphne keeps her gun up for ten seconds, holding fire while her target's down. Zach's Penjamin gas, Soundcloud Burst or Hemp Beam never set either of them shooting: they put their guns away and run. Name tags,
  speech bubbles and alert bars show over the NPCs you can see; testing mode shows them all, and on the map.
  NPCs that chase find their way with `NavGrid` (A* over a 25-unit grid of the level, cached per radius).
  **Sexton Science** wanders with his reel playing around him; talk to him, press E again to keep listening, and he
  hands you JARVIS and walks off. Hit him as a survivor and he defends himself with Hemp Beams; three hits slay him,
  Zach's machete or a survivor's bottles, books, jars, rounds or shotgun blasts alike (his health bar shows to both),
  and he drops his Hemp Beam on the ground for whoever takes it (E). **Chris Zelley** paces round his ambulance (40% smaller than at first) until a survivor enlists him; then the first survivor
  left downed or staked too long gets a sprinting paramedic, a revive, and a halo and wings as he ascends. Nobody can
  kill him, except Zach while he's at work on someone (reviving them or cutting them down): then he has 50 health,
  taken like a survivor's (a light swing 10, a full one two thirds of 100), his bar showing to Zach; on his way to a
  rescue nothing stops him, and if Zach picks up the survivor he's running to, he turns to anyone else downed or staked,
  or waits until they're put down or on a stake. **Plasma.TTV**
  says "ggs" and hands out a golden pump (twelve shells), one to the survivors and one to Zach, no more: anyone after
  that hears "sorry, i'm all out bro". Attack him and GAMER RAGE (to the gamer smash clip) turns him into a purple beast who punches you down
  (only the beast can die, and drops his pump); the beast keeps his size but squeezes through doorways like a man. **Chacko** sits on the lounge couch with his controller: a Dr Pepper for
  survivors, 50 Nic for Zach. One blow kills him, from Zach or a survivor (a bottle, a book, a jar, pellets, a bullet, a
  0.50 cal round), and he explodes in the same bloody blast, taking half his killer's health. His killer sets off a clip
  from the couch for everyone near. Only Jaden Nguyen, Plasma.TTV, Njaaron and Shane Jeans are set off by his death,
  whoever killed him; no other NPC is touched by it in any way. Njaaron, Jaden and Plasma race after them from
  wherever they are on the map with no time limit, each until he's dealt them a third of their health himself, or
  they're down (by anything), or he's slain, not giving up for time, distance or anyone else attacking them; Shane runs
  after them until he's stunned and driven off (two bottles, a shotgun blast or galaxy gas; Zach shakes him off with his
  machete or a Soundcloud Burst), tailing them even while they're down. Hide and they lose you, but the moment you're
  back out they're after you again. Every
  NPC carries a faint light, as in the original. Their looks: Sexton a white man with blond hair; Thomas white with
  grey hair; Plasma a tall white man with light brown hair (the beast stays purple); Waz a tall Indian man with curly
  hair and a short goatee; Soham a tall Indian man with a bowl cut; Njaaron a slightly muscular Asian man; Jaden an
  Asian man with cartoon-yellow skin; Monique a short Asian woman (tall is 1.88 m, short 1.62 m).
- **JARVIS** is an ability, used again and again: whoever has Sexton's tablet sets it going (Q), and for 20 s every
  survivor in the match gets all of it, its boot, its screen, the map revealed, their whole screen lit and everyone on
  it shown; the tablet then recharges for 90 s, counted from when it ends (its slot shows the seconds). Night vision
  can't be used while it runs. Out past the second ring it goes dark for that survivor (its time running on); back in, it
  comes back on without its boot. Its voice plays only the first time it's used in a match, its last second falling
  away by an inverse square. Coming on, it boots on every survivor's screen in
  two seconds, after Iron Man's JARVIS start-up (`JarvisHud`): a blue light streaks across the middle, an angular frame
  draws in round it, "INITIATING J.A.R.V.I.S." types out and counts 1... 2... 3... with a blinking block cursor, diagnostics flicker under
  it (network, protocol, memory, CHECKSUM: OK), a segmented bar fills and it all breaks up in a glitch. Psionic waves
  ripple out from them, close about the body first and sinking to the ground, then ring after ring spreading out over
  the ground, pulsing; the light spreads out from them over the whole screen in 0.8 s, and the view zooms out 30% over
  half a second. For as long as it runs: a blue wash, a vignette and scanlines, corner brackets, panels of ticking
  stats and metrics (uplink, satellite fix, core temperature, power, scan progress, the session counting down, the
  threat), turning range rings, a scrolling hex dump and a bank of meters; the cursor becomes a square target reticle
  with a cross in the middle (an airstrike marker); and everything on screen is outlined in blue, scanned (wherever the
  depth jumps: props, walls, trees, bodies), a scan ring sweeping out from them. Its voice: "I am J.A.R.V.I.S. at your
  service..." (a local file; without it, the old robot voice). The JARVIS slot sits beside the inventory, and night
  vision's slot (RMB) beside it.
- **Night vision** is an ability, not an item: goggles never take an inventory slot, each pair picked up adds its
  charge to the survivor's night vision, and holding right click uses it, whatever else they're holding or doing (with
  the 0.50 cal in hand, right click aims it and turns night vision on together, until the charge runs out). There's no
  animation of the body for it; the goggles go on over the eyes.
- **The ambulance, the lounge and the notes** (`SandboxWorld.Story`): Chris's ambulance parks in the woods off the
  paths with a row of supplies beside it and a flashing light bar, whose light shows the van's roof and the ground just
  round it (no further) under a soft red then blue tint that swells with each flash; the building's biggest room without a generator is
  the lounge, a couch facing a flickering TV. Six paper notes, each photo once in a random order: one lies beside every
  generator (the graveyard's always has one) and the rest beside the paths, far apart; next to a generator, whichever of
  the two you're nearer to is the one E works on. Notes are easy to spot: big sheets of pale paper (2.2 times the
  model) whose paper glows faintly, with a soft glint pulsing over each in your light every couple of seconds; once
  seen, the minimap and full map show each by its own icon (a sheet of old paper with a photo pinned to it,
  `tools/note_icon.py`). Zach's map shows every stake
  in play.
- **Miga Sushi and the Diddy Chamber** (`SandboxWorld.Places`): two buildings out on the edge of the playable map,
  far apart, each on flattened ground with its doorway facing in toward the middle, walled (blocking movement, sight
  and shots) and lit inside; nothing grows or lies about inside them (no grass, plants, stones, rocks or trees). Miga
  Sushi is a Japanese restaurant, 13 by 10 m, built of dark timber posts and beams over pale plaster: the noren curtain,
  a red sign with a fish and red paper lanterns at the door; a sushi bar across the back with a conveyor belt of
  coloured plates of nigiri, a glass case of fish and soy, ginger and tea at each of its wooden stools; behind it the
  chef's cabinet with a rice cooker and a cutting board, shelves of sake bottles and a row of wooden menu plaques; a
  tatami room to one side of the entrance behind a shoji screen, with two low tables set with tea and sushi, floor
  cushions and a hanging scroll of a carp; two wooden tables of four on the other side; red and white lanterns down
  the middle and over the bar, a beckoning cat and a bonsai on the bar. A Hemp Beam lies on the bar by the cat and a
  0.50 cal on a table. The Diddy Chamber is a very small dark dungeon of a room: chains on the walls, a big comfy bed with
  red satin sheets and a pile of pillows, and bottles of baby oil all over the floor. Walking into either, its name
  fades up on screen between two thin rules, holds and fades away.
- **Generator work sites** (`SandboxWorld.FloodSet`): every generator stands in a work site laid out by hand (one
  fixed arrangement, turned with the generator): three trailer light towers as tall as a big tree (a cream trailer with
  red and blue stripes, outriggers and a tow bar, a telescopic mast with four lamp heads on a frame at the top), each
  wired to it by a cable winding over the ground and throwing a loop of slack; four yellow work lights (a box light on
  a frame, a twin on a bar, a tripod with one head, a tripod with two), wired too; and the mess of a site gone to seed:
  broken glass, planks, corrugated metal, a cable spool, a jerry can, a toolbox, a small portable generator, loose
  wires, ferns and grass grown up through it. The graveyard's is bare: just the generator, two towers either side of
  it on the walk between the graves, and their two cables. Dark until the generator runs, then on for good: each
  tower lights the ground all round it (13 m) as brightly and as white as a player's own light, each work light a
  little round it. Anything that would stand in a wall, tree or rock moves to the nearest clear ground. The
  building's two generators have no work site: once either runs, the whole building is lit, every room and hallway
  by a grid of ceiling lights about 7 m apart (`BuildingPower`). (Light sources go into the
  mask at half and the composite doubles them, so a tower can reach a player's light; up to 16 are drawn at once.)
- **Survivors start scattered**: each at a spot of their own over the playable map, on open ground (not the lake, the
  building, a site or anything solid), at least 30 m apart where the map allows and 35 m from Zach's start, seeded by
  the level so every machine agrees (`SimMapBuilder.ScatteredSpawns`).
- The old **wanderer** stands in for an entity in the captures only.
- `GameHud` builds the HUD in code with uGUI, scaled from a 1080p reference.

### Ghosts, Zach's bar and the newer rules

- **Ghosts** (`MatchSim.Ghosts`, `GhostLook`, `GameHud.Ghost`, `CorpseViews`): a survivor sacrificed (staked to the
  end, or gone into the woods) leaves their body where they died (at the stake's foot; out of the woods, at the edge of
  the trees) and comes back four seconds later (out of the woods, once the black screen lifts) as a glowing white ghost
  of themselves in **Miga Sushi or the Diddy Chamber**, with a sprint that never runs out, passing through walls and
  anything solid. **Survivors never see a ghost** (Zach, the other ghosts and anyone watching do). They can't be hurt,
  still count as sacrificed for the night's result, and nothing they do touches an NPC. Their panel, where the
  inventory was, says **find a body, help your team**, and their map shows where the bodies lie.
- **Possession**: a ghost by a body (a sacrificed survivor's, or a slain NPC's; those who blew apart leave none) takes
  it with **E**: it levitates, rising off the ground and swinging upright with a pale glow, while the ghost glides into
  it and they merge; then everyone sees "**X has been reanimated**" across their screen (X whoever the body was). They
  walk about in it exactly as its owner looked (in Daphne's body, as Daphne, with none of her abilities), moving and
  sprinting as a survivor does, with a survivor's sight, no inventory and no health bar, seen by everyone. They're told
  **you cannot use items or fight, but you can help your team repair generators**: they can't pick anything up, talk
  to an NPC, use an item or a weapon or hurt anyone; the Soundcloud Burst and Penjamin's or 50 Nic's gas still reach
  them, hurting nothing. All they can do is hold E at a generator or a gate's lever a living survivor is already
  working, adding two thirds of the speed-up a second living survivor would (never a skill check; they let go when
  the survivor does). Any blow of Zach's (a machete hit at any charge, a lunge, one tick of the Hemp Beam, a single
  golden pump pellet) drops the body where it stands and throws the ghost out of it, as a 0.50 cal round throws a body
  back. They're a ghost again, sprinting forever, and never take another body; instead they **see everything**: the
  whole screen lit, through walls, the map revealed, the wheel zooming the view as in testing mode. The **Ghost Pulse
  is gone**. A disconnect or the end game's Entity never makes a ghost.
- **Zach's bar**: each time he's put down 10 comes off the top of his bar for good (his speed still goes by his health
  out of his original 100, so he's slower too), and he gets back up with half of what's left; each survivor he stakes
  adds 5 to the bar (and to his health), which makes him faster (past 100, faster than at the start). Worn away to
  nothing he's killed, and with every Zach dead the survivors win ("Zach was killed"). His head bar shows the part worn
  away, dark; his HP line reads out of what's left.
- **Zach's swing**: uncharged it takes 10 of a survivor's 100, growing evenly over the charge to two thirds fully
  charged (pulling someone out of hiding still takes a third). A rock or a boulder rings under the machete with the
  Half-Life metal clink.
- **The building's lights**: each of the building's generators lights its own half of the building (every room by the
  generator nearest it; a long hallway split light by light), a lone one all of it, at night vision's shaded brightness:
  they show the rooms and halls, never who's in them (players only ever see who's in their own light).
- **Night vision** widens the beam as it comes on, never snapping to it: the angle opens out as the see-through light
  lengthens, all the way by the time the light passes the side of the screen.
- **Numbers**: the Hemp Battery recharges over 50 s (40 at first); Penjamin's strongest slow is 40% less; the Hemp Beam
  has five charges; golden pumps hold twelve shells; the 0.50 cal has five rounds, its slot showing 5/5, 4/5... (another
  0.50 cal picked up adds its rounds and five to the count: 10/10), and every gun shows its rounds that way; a gun out of
  rounds is gone. Five P250s lie out on the map from the start, spread about the playable circle as far from each other
  as they can be; a golden pump lies on Miga Sushi's counter and another somewhere on the map.
- **Notes**: one beside every generator, the rest along the paths (or in the building when the paths are full), so
  every photo lies somewhere, the user's own included (`World/NotePictures`: `Resources/Images/local_note-7` and on,
  kept out of git).
- **Wading**: anyone moving through the lake, player or NPC, leaves rings rippling out over the water round their legs,
  a small wake behind them and now and then a splash (`MatchEffects.Water`).
- **The lake dock** is solid underfoot (`SandboxWorld.OnDock`): walk along its boards (they sound like wood); step off
  its side or end and you're straight down in the water at its depth, no fall; walk back to it from the water and
  you're snapped straight up onto its deck and walking on it again. Everyone else (players, NPCs, bodies) stands on it
  too.
- **Sounds** (local files, kept out of git): a jingle for everyone when a generator starts or someone escapes; and, in
  place of the old machine whirr, a small diesel generator coming to life where it stands, played through once, falling
  off linearly to a fifth of its volume over its first seven seconds and heard (faintly) only within ten feet of it; the
  gas explosion (from 1.8 s into its file) when a galaxy gas trap goes off; the gamer smash as Plasma.TTV goes into his
  rage; a clip from Chacko's couch when anyone attacks him. The jingle stops a second before its file ends (something
  else is in its last second). The machete's slash into someone is a quarter as loud as it was at first (halved twice).
  Sounds can start part way into their file, decay over time, fall away at the end or be cut short
  (`SoundBank.Cue`'s Start, DecayTime and DecayTo, TailFade, CutEnd).
- **Zach and golden pumps**: he can pick up any golden pump lying on the map (or one a survivor dropped): twelve shots
  (the lobby's golden pump shells) that replace his machete until they run out.
- **Notes**: reading one, E puts it down again (as Esc or a click does).

### Look panel (F4)

Live sliders for finding the palette. Each slider shows its current value, **Copy values** puts all of
them on the clipboard (and in the log) as one line of text, and **Reset** restores the defaults. Values
persist between runs. The defaults are the user's chosen look (a look saved before them is replaced by them once).

| Slider | Range | Default | Effect |
|---|---|---|---|
| Contrast | 0-2 | 1.81 | Global contrast, pivoting on mid-grey |
| Saturation | 0-2 | 1.22 | Global saturation of every colour (0 = greyscale) |
| Lit brightness | 0-2 | 1.21 | Multiplier on everything lit (flashlight, campfires, lanterns) |
| Unlit brightness | 0-5 | 0.00 | Multiplier on the unlit ground and objects |
| Beam intensity | 0.5-2 | 1.89 | Strength of the flashlight beam only |
| Beam edge falloff | 1-6 | 2.50 | Exponent of the beam's fade toward the screen edge |
| Blur start / end (m) | 0-30 | 11.21 / 30.01 | Distance from the player where the distance blur begins and is full |
| Blur max (px) | 0-8 | 8.00 | Full distance-blur radius in screen pixels |
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
   - Cone: fixed half-angle; every ray of it runs off the screen at any facing, resolution or view turn: its reach is
     the farthest any ray of the cone has to go to leave the visible ground (its edges, rays across it, and any
     corner of the view inside it, since a cone aimed near a corner must reach the corner, further than where its
     axis leaves), plus 6%, plus room for hills and hollows; checked at every 5 degrees of facing, from points all
     over the screen, with the view turned six ways.
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
- **Fittings**: lockers along the corridors (up to 8), wardrobes, beds and barrels to hide in; breaker panels,
  posters, clocks, stains, cobwebs and dead plants (no air ducts, overhead pipe runs or vent grilles: from above they
  hid the windows, doorways and exits). Furniture keeps
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

`PathNetwork` joins the points of interest as the original does (a minimum spanning tree and up to three extra loops)
and routes each link with A* over a grid whose cost rises steeply with slope; a seeded noise field tugs the routes
aside and a gentle meander bends them, so they wind through the woods. Over them grows a web of narrower trails:
junctions out in the woods tied into the network (some by two trails), and cross-links where two paths pass near each
other. Each path has its own width, swelling and narrowing along it, and its own look drifting into a second one along
its length (packed dirt, gravel, leaf litter, moss, clay, rutted mud), worn lighter down the middle, with ruts on the
main paths, damp patches and puddles, and litter by look (pebbles, leaves, twigs, moss, puddles, stones by the edges). The land changes gradually: slow
noise fields choose between evergreens (seven forms, including ragged spiky ones bristling with sharp needle-spikes and
smaller spikes off them) and thirteen dead-tree designs (the newest a tall, nearly black bare spar with short jagged
branches in whorls, each kinked down at an elbow and hooked at the tip, and a broken, forked top),
and colour the ground from earth and clay to moss and straw, with rock grey on steep slopes, mud in the
ditches and packed dirt on the paths.

## Characters

- **Models** (`CharacterSpec`, `CharacterBuilder`): every character is built on the same 51-bone rig, flat-shaded and
  vertex-coloured, at most **1000 triangles**: a fourteen-sided torso in seven rings (hips to shoulders), a ten-sided
  head with jaw, brow, nose and ears, eight-sided limbs with deltoid, bicep, elbow, forearm, thigh, knee and calf rings,
  mitten hands with a thumb, and shaped feet (a rounded heel, the instep rising to the ankle, the arch, the broad ball
  and a toe box tapering to the big toe, on a flat darker sole). The **survivor** stays grey and identical for everyone
  (tonal greys: darker trousers, gloves and soles). **Zach** is Jason Voorhees as Mortal Kombat X draws him, in up to
  1500 triangles (500 more than anyone else): 2.2 m, big and heavy-limbed with a slight hunch; bald, his scalp blotched
  grey and raw red-brown; the ivory hockey mask standing off his face with its rim turned back (so it has thickness),
  rows of round vent holes, dark eye holes, red chevrons over the brows and on the cheeks, and black straps round the
  back of his head and over the crown; a worn olive field jacket (stained and faded in patches) that is a separate shell
  of cloth standing off his body, open down the front over a dark slate shirt, with a stand-up collar, pocket flaps and
  long sleeves to the wrist, its thickness showing in a band of darker lining at the hem, both front edges, the collar's
  top and inside, and the cuffs; black gloves, dark trousers and boots. His machete is a long traditional one: a wrapped
  grip with a steel pommel, a flat oval guard and a blade over 0.65 m long widening toward its belly, the spine running
  straight until it clips down to the point, bevelled to the edge, with blood smeared and spattered over it. The **NPCs** use the original's look table (skin, hair, shirt,
  jacket, trousers, shoes) with features to fine-tune later: hair styles, a backwards cap, glasses, a headset, hoods,
  hi-vis bands, a star of life, a checked flannel, a jersey band.
- **Props** (`PropModels`): separate meshes of at most 130 triangles (machete, pump-action shotgun, golden pump (both
  pumps' forends their own pieces, sliding back and forward as the pump is worked),
  P250, 0.50 cal, flashlight, magnifier, tablet, controller, bottle, book, jar, can, bar, the mini shield's flask, the
  galaxy gas tank, night vision goggles) held on hand sockets, or worn on the face; guns sit in the fist as in a real
  hand, the barrel along the forearm.
- **Facing**: the body faces the cursor at once. The legs walk the way the player moves (backwards when that is behind
  the aim) and the spine and chest take the whole turn toward the aim, the legs dragged round when the aim swings past
  what the waist can turn; actions turn on top of it, so a swing, a shot or a throw always goes where you aim.
- **Animation**: the procedural walk and sprint (`GaitSolver`, `HumanoidAnimator`) adapts to slopes and, at the
  original's speeds, strides like a sprinter. On top of it, `ActionLayer` plays keyframed clips (`ActionClips`,
  Catmull-Rom through the keys, eased blends, events such as "release", "hit" and "kick") on the bones each clip owns,
  so the legs keep walking under an upper-body action; hits add a damped flinch from the blow's direction.
  `CharacterView` picks the model, the held prop and the clip from the match: repairing, the lever, healing and
  reviving, picking up, hiding, drinking, eating, talking, planting a trap, a quick throw (whipped back behind the head,
  snapped over the shoulder), each gun held as it is in real life (the pistol in a two-handed Weaver grip, the support
  arm bent and its hand cupping the grip; the shotgun shouldered with the trigger elbow out and the support hand under
  the forend, the body bladed; the 0.50 cal carried at the hip, its grip low by the right hip and the support hand
  under the forend, until right click brings it up: the stock into the shoulder, the cheek down on it and the eye to the
  scope, the support hand far out, the upper body steady over the walking legs, eased in over a quarter of a second)
  and its kick (the pistol's muzzle flip; the shotgun's shove, then the pump worked as a real one is: the support
  hand pulls the forend straight back along the magazine, 9 cm, a spent red shell kicking out of the port on the right
  to bounce and lie on the ground a while, then pushes it home, at a real pump's speed, the arm solved so the hand stays
  on the forend all the way (scratchpad pump_solve.py), the gun held on aim for the rest of the reload, 1.2 seconds for
  the shotgun and one for the Legendary pump (the 0.50 cal takes 2 seconds), stopped if it's put away; Zach working the golden pump the same way; the
  0.50 cal simply held on after it fires, both hands on it, no movement at all; every kick ends in the gun's hold and
  hands straight back to it at full weight, so the support hand never lets go and the body never drops back to the
  walk in between),
  JARVIS,
  stunned, coughing in Penjamin's gas, the scare, staked, carried, crawling; Chacko flinching on the couch; Zach
  stunned (reeling on the spot: his knees buckling in turn as his weight rolls round in a slow circle, the torso
  slumped and swaying, the masked head lolling a beat behind, the machete arm hanging limp and swinging, the free hand
  pressed to the side of the mask); Zach's charge, light and heavy swings,
  lunge, carry, lift, stake, search, kicks, burst, Penjamin, Hemp Battery and beam. One-shot clips (a swing, a throw, a
  gun's kick, a swig) come from the match's events; the player's own character hears them from the moment the match
  host exists (it is enabled before the level hands it its world, so listening only in `OnEnable` used to leave the
  local model deaf to them, while other players' models played them).
- **Animation lab** (`CharacterTests`): every clip is played through at 240 Hz and checked for pops (bone speeds under
  1000 deg/s, 2600 for strikes, 3200 for the throw and the slashes), single-firing events and a clean end; key poses
  are checked for where the hands go (in front for the lever, at the mouth when drinking, overhead on a stake, both
  hands on the gun and the barrel straight ahead, the slashes level from one side to the other) and the chest is
  checked to face a new aim on the very next frame.
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
| `Assets/Vision/Prefabs/` | One prefab per prop variant (12 dead-tree designs and 7 evergreen forms in three sizes, rocks from pebbles to boulders (in the level each stands behind an upright round wall instead of its own tilted collider, from under the ground to above its top, so nobody walks up a rock's curve onto it and stands inside its outline, seeing nothing), crates, car wrecks, a generator, a burning barrel and a campfire with animated flames, a lantern post, crows, wanderer, player), each with its collider, occluder or light, plus `PropLibrary.asset` listing them. |
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
Runs the 263 EditMode tests (`ReferenceCoverageTests` among them: every constant copied from the original's
`balance.ts` must be used by the game, or listed with the reason it isn't, so a behaviour of the original can't be
copied and then forgotten): visibility polygons, doors, triangle winding and normals, model sizes and
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
`Captures/`, then quits. `-visionGasLab <folder>` instead runs only the gases (galaxy gas and Penjamin as they grow,
hang and fade, and Zach's scent trail), about a minute, for tuning their look.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindowsDev
./Builds/WindowsDev/VisionSandbox.exe -visionPerf Logs/perf.txt -screen-fullscreen 1 -screen-width 2560 -screen-height 1600
```
A frame-time check under play: from the title screen into testing mode, it walks and sprints the player
from spawn through the building for 30 s, then writes frame-time percentiles, the costliest profiler
markers (development build) and the slowest frames, screenshots the minimap and full map, and quits.
It also runs on the release build without the markers.

```bash
unity run . --editor-version 6000.6.3f1 -- -executeMethod Vision.EditorTools.VisionSetup.BuildWindowsNext
./Builds/Next/VisionSandbox.exe -visionNetHost 1 -visionNetPort 7791 -visionNetOut Captures/net -screen-width 1280 -screen-height 720 -screen-fullscreen 0
./Builds/Next/VisionSandbox.exe -visionNetJoin 127.0.0.1:7791 -visionNetOut Captures/net -screen-width 1280 -screen-height 720 -screen-fullscreen 0
```
Two copies of the game playing each other on this machine (`NetSmoke`; `Builds/Next` is a build beside
`Builds/Check` for when that one is open): the host moves two balance sliders and the guest sees them,
both build the same three-generator level, the host (Zach) starts the night and both walk about (frame
times and the guest's ping logged), then the guest sets JARVIS going with its own key, the host powers
both gates, opens Miga Sushi's with the end game and opens the hatch, puts Zach down (his bar worn on the
guest's screen), and sacrifices the guest, who comes back a ghost in Miga Sushi or the Diddy Chamber, walks with
endless sprint, takes its own body with its own E (the rise, then the "has been reanimated" banner on both screens)
and is knocked out of it by the host's own machete swing, to see everything; then everyone back to the lobby. Each side writes
what it saw (`net_host.txt`, `net_client.txt`) and screenshots. The EditMode tests play the same over
TCP within one process (`NewMechanicsOnlineTests`, `MultiplayerScenarioTests`).

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

