# Cozy Cafe Game: Build Notes

A record of everything added to the project so far: what each piece is, where it lives, and how it was built. Positions are world coordinates in Unity units, as of 2026-11-16.

## Project at a glance

| | |
|---|---|
| Unity version | 6000.6.0f1 |
| Render pipeline | URP, Forward+ rendering (`Assets/Settings/PC_RPAsset.asset`) |
| Input | Active Input Handling = **Both**. The scripts use the classic `Input` class |
| Main scene | `Assets/Scenes/Main Scene.unity` (the only scene in Build Settings) |
| Git branch | `feature/barista-player` (nothing committed yet) |
| Editor tooling | MCP for Unity package (`com.coplaydev.unity-mcp` v10.2.0), which lets Claude read and edit the open Editor |

### Game idea

You play the barista in a cozy cafe, in first person. You stand behind the counters while customers come in through the door, walk to the front counter and place an order. You confirm the order, and the customer goes and sits at a free chair. Making the order is the next step and isn't built yet.

## Map of the room

```text
            +Z (north)
   Wall_right (1)  z = 9.9
  +--------------------------------------------+
  |  Trash      table 1        small tables    |
  |  [back  ][front ]   table 1 (1)            |  Wall_front  x = 5
  |  [counter][counter]  table 1 (2)    Window |  (window + door)
  |  Barista lane                         Door |
  +--------------------------------------------+
   Wall_right  z = -4.9
 Wall_back x = -10.9                     +X (east)
```

- **Floor:** top surface at y = 0.1.
- **Walls:** 5.5 high (y 0.07 to 5.57).
- **Ceiling:** underside at y = 5.57.
- **Barista lane:** between the back counters (x ≈ -10.8 to -9.4) and the front counter (x ≈ -7.0 to -5.6), from z ≈ -0.85 to 6.66.
- **Customer area:** everything east of the front counter.
- **Door:** on the front wall, centered at z ≈ -1.65.
- **Window:** on the front wall, centered at z ≈ 5.1.

## Scene hierarchy

```text
Directional Light          sun, 4200K, intensity 2 (blocked by the ceiling now)
Global Volume              post-processing (SampleSceneProfile)
Room
├── Floor, Wall_right, Wall_right (1), Wall_back, Wall_front   cubes with BoxColliders
├── Ceiling                copy of a wall cube
├── Window                 your Blender window (window3.fbx)
├── Door_White             white door model, swings open for customers
├── Doorway_Outside        bright panel behind the door
├── table 1, table 1 (1), table 1 (2)            table + 4 chairs each
├── small table 1, (1), (2)                      side table + armchair each
├── Trash_Can
├── Wall back items        counters, coffee machines, menu board, props
└── Wall front items
BaristaBounds              invisible walls that keep the barista behind the counters
Barista                    the player
└── CameraPivot
    └── Main Camera
Lighting                   3 hanging lamps + 3 warm point lights
Customer System
├── CustomerSpawner        SpawnPoint, OrderSpot, DoorHinge
├── OrderCanvas            the caption bar and OK button
├── EventSystem            lets the OK button receive clicks
└── NavMesh                walkable area for customers
```

## Layers and tags

| Object | Layer / tag | Why |
|---|---|---|
| `BaristaBounds` and its 4 children | Layer **Ignore Raycast** | They block the player's body, but clicks and the E-to-interact ray pass through them. Without this, the display-case wall would stop you clicking `Cake_1` inside the case. |
| `Barista` | Tag **Player** | Standard tag for the player. |
| `Main Camera` | Tag **MainCamera** | `Interactor.cs` uses `Camera.main`, which looks for this tag. |

No custom layers were added. Everything else is on Default.

---

## 1. The barista (first-person player)

### GameObjects

| Object | Components | Key settings |
|---|---|---|
| `Barista` | CharacterController, PlayerController, Interactor, DragController | Spawn (-8.2, 0.12, 2.2), facing +X toward customers. Controller height 2.3, radius 0.35, center Y 1.15, step offset 0.3 |
| `Barista/CameraPivot` | Transform only | Local (0, 2.41, -1.33): eye height, tilted up and down by the script. You changed this; Z = 0 would put the eyes inside the body |
| `Barista/CameraPivot/Main Camera` | Camera, AudioListener, URP camera data | Near clip 0.1, **Post Processing on** |

### How it was built

1. Created an empty `Barista`, added a CharacterController, and sized it to the large-scale furniture. The chairs were 1.5 tall at first, so a normal 1.8 player looked tiny.
2. Added `CameraPivot` as a child at eye height with the camera under it. The body turns left and right, and the pivot tilts up and down, so the controller capsule never tilts.
3. Linked `PlayerController.cameraPivot` to the pivot.
4. Added your existing `Interactor` and `DragController`. `CameraSwitcher` was left off because it needs overview and close-up cameras that don't exist yet.

### Controls

| Input | Action |
|---|---|
| W A S D / arrow keys | Move |
| Mouse | Look (always on, cursor locked) |
| Space | Jump |
| E | Interact with what you look at (3 units) |
| Hold / release left mouse | Pick up / drop a `Draggable` item at screen center (4 units) |
| Esc | Free the cursor (click to lock again) |

### Keeping the barista behind the counters: `BaristaBounds`

Four invisible BoxColliders, with no mesh, form a box around the lane:

| Child | Center | Size | Blocks |
|---|---|---|---|
| `Block_BackCounter` | (-10.12, 2.1, 2.15) | 1.4 × 4 × 9.03 | back counters |
| `Block_FrontCounter` | (-6.29, 2.1, 2.91) | 1.44 × 4 × 7.51 | front counter + display case |
| `Block_NorthEnd` | (-8.22, 2.1, 6.76) | 2.41 × 4 × 0.2 | north end of the lane |
| `Block_SouthEnd` | (-8.22, 2.1, -0.95) | 2.41 × 4 × 0.2 | south end of the lane |

They are 4 tall, so a jump can't get over them or land on a counter. Their layer is explained under **Layers and tags** above.

---

## 2. Room changes

| Change | How |
|---|---|
| **Window** (`Room/Window`) | Your Blender model `Assets/Models/window3.fbx`. Scale 0.9 (about 3.4 wide × 2.3 tall), rotation 0, on the front wall with its top level with the door. The 4 glass panes use `Window_Glass.mat`, so they look like daylight without a hole in the wall. Its import setting **Normals = Calculate** is kept. |
| **Door** (`Room/Door_White`) | `Door White Interior_fbx_Scene.fbx` replaced the old `PortalDoor`. Import settings **Import Lights** and **Import Cameras** are off, because the file carried 4 area lights URP can't use. Their names remain as empty child objects, which do nothing. |
| **Doorway panel** (`Room/Doorway_Outside`) | A Quad with `Window_Glass.mat` hidden inside the closed door. When the door swings open you see "outside" instead of the wall. |
| **Ceiling** (`Room/Ceiling`) | A copy of `Wall_right`, with the same cube, Wall material and collider, scaled 16.3 × 0.2 × 15.2 at y 5.67. |
| **Walls shortened** | From 8 to 5.5 high. `Wall back items` and `Wall front items` were moved out from under the walls first, because the counters were children of `Wall_back` and would have been squashed. Their sizes were checked and unchanged. |
| **Tables and chairs** | Scaled to 80% (chairs ~1.2 tall) around each set's own center, so the layout stays the same. |
| **Wall color** | `Wall.mat` is now `#B0FF88`. You set this, not Claude. |

### Materials created

| File | What it is |
|---|---|
| `Assets/Materials/Window_Glass.mat` | URP Lit, sky blue `#9ED1FA`, smoothness 0.9, gentle emission, rendered on both sides. Used by the window panes and the doorway panel. |
| `Assets/Materials/Window_Arc.mat` | Workaround material for the earlier window version's arc. **No longer used.** Safe to delete. |

---

## 3. Lighting

| Setting | Value | Where |
|---|---|---|
| Ambient light | **Trilight**: sky (0.62, 0.54, 0.44), equator (0.50, 0.42, 0.34), ground (0.30, 0.24, 0.19) | Window > Rendering > Lighting > Environment |
| Sun | 4200K. The ceiling blocks it now, so it no longer lights the inside | `Directional Light` |
| Lamps | 3 × `Lamp_20.fbx`, scale 200, hanging from the ceiling, shadows off | `Lighting/Lamp_*` |
| Lamp lights | 3 Point lights, color (1, 0.78, 0.55), intensity 8, range 14, no shadows | `Lighting/Lamp_*_Light` |
| Color boost | **Color Adjustments: Saturation +12** added to the volume profile | `Assets/Settings/SampleSceneProfile.asset` |
| Camera post-processing | Turned **on** for the barista camera | `Main Camera` > Rendering > Post Processing |

Lamp positions: two over the customer tables at (-0.4, z 1.0) and (-0.4, z 8.0), and one over the back counters at (-10.1, z 2.2). The counter lamp was moved there because it hung right in the barista's view.

Notes:
- The camera originally had no post-processing, so the Global Volume (tonemapping, bloom, vignette) wasn't reaching the game view at all.
- `SampleSceneProfile` is shared, so any other scene using it also gets the saturation boost.
- The rattan lamps were replaced because each cluster had about 600,000 triangles. `Lamp_20` has about 4,400.

---

## 4. Customers

### What happens

1. Two seconds after Play, `Door_White` swings open about its hinge and a customer appears in the doorway.
2. The customer walks around the furniture, using the NavMesh, to the front counter and turns to face the barista.
3. The caption bar appears with **"Esra: I want a cappuccino and a chocolate cake"** and an **OK** button. The barista freezes and the cursor is freed.
4. Clicking **OK** hides the bar and re-locks the cursor. The customer picks a random free chair, walks beside it, then slides into the seated pose.
5. That chair is marked taken, so later customers won't pick it.

### GameObjects (`Customer System`)

| Object | Components | Key settings |
|---|---|---|
| `CustomerSpawner` | CustomerSpawner | Prefab `Customer_Girl`, name "Esra", the order text, start delay 2s, door swing -80° in 0.5s, door stays open 1.5s |
| `CustomerSpawner/SpawnPoint` | Transform | (4.2, 0.1, -1.65), facing -X (into the room) |
| `CustomerSpawner/OrderSpot` | Transform | (-5.0, 0.1, 4.2), facing -X. Moved off the cash register line so the register doesn't hide the customer |
| `CustomerSpawner/DoorHinge` | Transform | (4.85, 0.1, -2.56), the door's hinge edge. **If you resize or move the door, move this too.** |
| `OrderCanvas` | Canvas (Screen Space Overlay), CanvasScaler (1920×1080, match 0.5), GraphicRaycaster, OrderCaption | Holds `CaptionBar` |
| `OrderCanvas/CaptionBar` | Image (dark brown, 92% opaque) | 1300 × 130, bottom-center, 60 px up. Children: `Caption` (Text, size 36) and `OkButton` (Button + `Label` "OK") |
| `EventSystem` | EventSystem, StandaloneInputModule | Needed for UI clicks |
| `NavMesh` | NavMeshSurface | Collect: All, from Render Meshes. Data saved at `Assets/Scenes/Main Scene/NavMesh.asset` |

Every chair (12 `Chair18`, 3 `Chair9`) has a **CustomerSeat** component. Seat Height is 0.504 for the dining chairs and 0.56 for the armchairs, which is the seat top above the chair's pivot after the 80% scale.

### Assets created

| File | What it is |
|---|---|
| `Assets/Prefabs/Customer_Girl.prefab` | Your `CasualGirlBlink` prefab plus: Animator using `Customer.controller`, root motion off; NavMeshAgent (radius 0.35, height 2, speed 1.7, stopping 0.1, no avoidance); Customer script |
| `Assets/Animations/Customer.controller` | States **Walk** (default), **Idle**, **Sit**. Bool params `Walking` and `Sitting`. Transitions are 0.2s, with no exit time |
| `Assets/Animations/Sit_Pose.anim` | A seated pose built from humanoid muscle values, because the models had no sit animation. Upper Leg Front-Back -0.6, Lower Leg Stretch -0.1, arms lowered |
| Walk / Idle clips | Reused from Starter Assets: `Locomotion--Walk_N.anim.fbx` and `Stand--Idle.anim.fbx`. Both characters are Humanoid rigs, so these clips fit them |

### Project setting changed

**Navigation agent radius** is 0.35 (was 0.5) and height is 2, in Project Settings > Navigation > Agents > Humanoid. At 0.5 the gaps between the counter and side tables were too narrow to walk through.

---

## 5. Code

All scripts are in `Assets/Scripts/`. They use the classic `Input` class and no namespace, the same style as your existing scripts.

### `PlayerController.cs` (edited)

Moves the barista with the CharacterController and handles looking around.
- **Mouse look:** the mouse turns the body left and right and tilts `cameraPivot` up and down, clamped to ±80°. It only works while the cursor is locked.
- **Cursor:** Esc unlocks it and a left click re-locks it.
- **Movement:** WASD, clamped so diagonal movement isn't faster.
- **Jump:** Space, with jump speed `sqrt(2 · g · jumpHeight)` and normal gravity while in the air.
- **Order UI hook:** `public static bool UIOpen` and `public static void SetUIOpen(bool open)`. While a UI panel is open, the player can't move, look or jump, and the cursor is free.
- **Fields:** `moveSpeed` 3, `jumpHeight` 1, `mouseSensitivity` 2, `cameraPivot`.

### `DragController.cs` (edited)

- **Aiming:** it now aims from the center of the screen (`ViewportPointToRay(0.5, 0.5)`) instead of the mouse position, because the cursor is locked.
- **UI guard:** it ignores clicks while `PlayerController.UIOpen` is true, so clicking OK doesn't grab a cake.

### `Customer.cs` (new)

One customer's visit, run as a coroutine:

```text
Begin(name, order, orderSpot, faceDirection)
  └─ Visit()
       WalkTo(orderSpot)   NavMeshAgent + Animator "Walking"
       Turn(faceDirection)
       OrderCaption.Show("Name: order")   wait until OK
       seat = CustomerSeat.FindFree()     mark taken
       WalkTo(FindSpotBeside(seat))       tries left / right / behind the chair, shortest path
       SitDown()                          agent off, "Sitting" on, slide into the seat over 0.6s
```

- **Sitting position:** `sittingRootOffset` (-0.82) and `sittingForwardOffset` (0.15) place the character so her hips sit on the seat.
- **`OnFootstep()`:** an empty method. The Walk_N clip sends footstep events, and without a receiver Unity logs a warning on every step. It's also the place to add footstep sounds later.

### `CustomerSeat.cs` (new)

Goes on each chair.
- **Fields:** `seatHeight` and `taken`.
- **Seat list:** every enabled seat registers itself in a static list.
- **`FindFree()`:** returns a random seat that isn't taken, or null when all are full.

### `OrderCaption.cs` (new)

The caption bar.
- **`OrderCaption.Instance`:** a single shared instance.
- **`Show(text, onConfirmed)`:** fills in the text, shows the bar and calls `PlayerController.SetUIOpen(true)`.
- **`Confirm()`:** wired to the OK button. It hides the bar, gives control back and runs the callback.

### `CustomerSpawner.cs` (new)

- **Sequence:** on Start it waits `startDelay`, swings the door open, spawns the customer at `SpawnPoint` and calls `Begin(...)`, then closes the door.
- **Door swing:** `Transform.RotateAround(doorHinge, up, angle)` over `doorSwingTime`.
- **Edit the order:** change `customerName` and `order` in the Inspector.

### Unchanged scripts you already had

`Interactor.cs` (E key raycast to `Interactable`), `Interactable.cs` (UnityEvent), `Draggable.cs`, `CameraSwitcher.cs` (not in the scene).

---

## How changes were made

- **Unity Editor:** Claude used the MCP for Unity bridge to run small C# snippets inside the Editor. These created objects, set fields, built the animator and prefab, baked the NavMesh and took screenshots. Scripts were written as normal `.cs` files.
- **Testing:** after each change the scene was played and checked with screenshots from the barista camera and positioned cameras, plus console checks for errors. For the customer, the OK button was pressed from code, so a real mouse click should still be tested by you.
- **Saving:** the scene was saved after every step.

## Known issues and next steps

- **Order making isn't built.** After OK, the customer sits and nothing else happens. `Customer` keeps `customerName` and `order` for when you build it.
- **One customer only**, and the order is one fixed line. The boy model (`CasualBoyBlink`) isn't set up yet. It needs the same prefab setup as `Customer_Girl`.
- **Seated pose:** customers snap into a simple pose instead of a sit-down animation. A Mixamo "Sitting" clip could replace `Sit_Pose.anim`.
- **Door:** it has no collider, and the bottom of the door is about 0.06 above the floor. Set its Position Y to about 2.09 to close the gap.
- **Window:** it doesn't let in real light, because the glass is a bright surface and the wall is solid.
- **Customers and the barista don't collide.** The barista can't leave the lane anyway.
- **Play in the background:** if Play mode seems frozen, click inside the Game view. Unity pauses Play mode when it isn't the focused window.
- **Rebake the NavMesh** (select `Customer System/NavMesh` > **Bake**) whenever you move furniture, counters, walls or the door.
- **Unused assets:** `rattan lamp.obj`, `PortalDoor.fbx`, `window2.fbx` and `Window_Arc.mat` are no longer used in this scene.
