# Evidence Run

A small 3D stealth-and-retrieval game prototype built in Unity for WebGL.

The objective is simple: find the evidence, avoid the guards, and return to the extraction point without being caught.

The project was built with a stronger focus on the underlying gameplay architecture and custom C# implementation than on final visual polish.

## Gameplay

The player starts with a short mission briefing showing the evidence they need to find and the controls required to play.

During the mission:

1. Find the evidence.
2. Avoid the guards' vision cones.
3. Use walking, running, and crouching to stay hidden.
4. Throw objects to create noise and distract guards.
5. Pick up the evidence.
6. Return to the extraction point.
7. Get caught by a guard and the mission is lost.

When the evidence is picked up, the evidence object disappears and the objective changes to returning to extraction.

## Controls

| Input | Action |
|---|---|
| `W A S D` / Arrow Keys | Move |
| Mouse | Look / Aim |
| `Shift` | Run |
| `Left Ctrl` | Crouch |
| `G` | Throw |

## Core Technical Implementation

### Custom Player Movement

Player movement is implemented using a `Rigidbody` and custom movement logic rather than Unity's `CharacterController`.

The movement system includes:

- Acceleration
- Deceleration
- Walk speed
- Run speed
- Crouch speed
- Rigidbody interpolation
- Continuous collision detection

Input is collected separately through `PlayerInputReader` and converted into an `ActorIntent`.

```text
PlayerInputReader
        ↓
    ActorIntent
        ↓
     ActorMotor
        ↓
     Rigidbody
```

This keeps input handling separate from the actual movement implementation.

## Guard AI

Guards use a custom state machine written specifically for this project.

The main states are:

```text
Patrol
   ↓
Suspicious / Investigate
   ↓
Chase
```

### Patrol

Guards follow their configured patrol route.

### Suspicious / Investigate

Guards investigate suspicious activity such as:

- Seeing the player
- Hearing thrown objects
- Moving toward the player's last known position

### Chase

When the player's awareness reaches the detection threshold, the guard enters Chase and moves toward the player.

The state machine is implemented as separate state classes rather than a large conditional or switch statement. This keeps each behavior isolated and easier to extend.

## Vision Cone & Line of Sight

The guard vision system was implemented from scratch.

Detection is performed in a cheap-to-expensive sequence:

```text
1. Distance check
       ↓
2. Field-of-view angle
       ↓
3. Raycast for wall occlusion
```

### Distance

Squared distance is used where possible:

```csharp
float sqrDistance = offset.sqrMagnitude;
```

This avoids an unnecessary square-root calculation.

### Field of View

The direction to the player is compared with the guard's forward direction using `Vector3.Dot`.

The guard configuration precomputes the cosine of half the field of view, so trigonometric calculations are not required in the normal detection path.

Conceptually:

```text
dot(forward, directionToPlayer) >= cosHalfFov
```

### Wall Occlusion

After the range and FOV checks pass, a raycast determines whether blocking geometry exists between the guard and player.

This prevents guards from seeing the player through walls.

## Vision Cone Rendering

The guard's vision cone is a separate presentation component.

The renderer uses:

- Preallocated vertex data
- Preallocated triangle data
- A reusable dynamic mesh
- Raycasts to determine the visible boundary
- Awareness-based visual feedback

The visual cone is kept separate from the actual perception logic so the gameplay system does not depend on its presentation.

## Noise System

Thrown objects generate noise that guards can hear.

A custom `NoiseBus` is used instead of having every noise source search for guards directly.

```text
Throwable
    ↓
NoiseBus
    ↓
Guards
```

This leaves room for future noise sources such as footsteps or gunshots without changing the core guard-hearing architecture.

## Projectile & Object Pooling

Projectiles and temporary impact effects use a custom fixed-capacity object pool.

The pool is created and prewarmed during initialization and reused during gameplay.

```text
Bootstrap
   ↓
Prewarm Pool
   ↓
ProjectileSystem
   ↓
Acquire → Use → Release
```

The projectile system is code-simulated and uses a raycast sweep between the previous and new projectile positions.

The same architecture can later be reused for bullets and other temporary effects.

## Architecture

The project is structured around clear responsibilities and explicit dependency direction.

```text
Bootstrap
    ↓
Presentation
    ↓
Gameplay
    ↓
Core
```

### Core

Contains reusable and testable systems such as:

- State machine
- Object pooling
- Event structures
- Shared interfaces
- Math utilities

Core does not depend on scene objects.

### Gameplay

Contains:

- Player logic
- Guard AI
- Perception
- Noise
- Projectiles
- Objectives
- Combat interfaces

Gameplay owns the rules of the game.

### Presentation

Contains:

- Main menu
- HUD
- Vision cone
- Character animation
- Visual effects
- Camera presentation

Presentation reads gameplay information but does not own gameplay rules.

### Bootstrap

`GameBootstrap` acts as the composition root.

It creates and wires the runtime systems and dependencies instead of relying on global singletons or scene-wide searches.

`GameLoop` provides an explicit update order for the important gameplay systems.

## Player & Guard Animation

Generic humanoid characters were added after the initial gameplay systems were completed.

A shared `ActorBase` Animator Controller is used as the base for both player and guards.

### Player animations

- Idle
- Walk
- Run
- Crouch
- Throw

### Guard animations

- Idle
- Walk
- Run

Animation is deliberately separated from movement and AI logic.

```text
Guard / Player gameplay
        ↓
     ActorIntent
        ↓
Animation Controller
        ↓
      Animator
```

The animation system is functional, but the final visual transitions and alignment still need polish.

## Main Menu

The project includes a mission-briefing style main menu containing:

- Game title
- Mission description
- Evidence reference image
- Controls
- Start button

The main menu is separated from the gameplay scene so gameplay systems are initialized only when the mission starts.

## Objective & Game Flow

The game uses a small session state:

```text
Boot
 ↓
Playing
 ├── Evidence Picked
 │       ↓
 │   Extraction
 │       ↓
 │      Won
 │
 └── Player Caught
         ↓
        Lost
```

The objective system uses simple distance checks for the evidence and extraction points.

Evidence pickup raises an `EvidencePicked` event, which updates the game session and hides the evidence from the scene.

## WebGL Optimization & Performance

The project was designed with WebGL performance in mind.

The implementation avoids unnecessary allocations in gameplay code through:

- Struct-based events
- Preallocated arrays
- Fixed-capacity object pools
- Cached Animator parameter hashes
- No LINQ in gameplay paths
- No third-party character controller
- Custom perception math
- Explicit update order

### Final performance verification

The final WebGL performance targets were verified successfully:

- **60 FPS target: PASSED**
- **Under 20 MB target: PASSED**
- **Final performance verification: COMPLETE**

## Known Limitations / Areas for Improvement

This version is a functional prototype with a stronger engineering foundation than final visual polish.

### Guard navigation

NavMesh movement was not used for this project, so guard movement and obstacle handling were implemented from scratch around the existing `IMover` and `ActorMotor` architecture.

There is still a known limitation:

> Guards can become stuck against walls or certain obstacles during a chase.

The current direct movement approach works in open areas, but more complex geometry can create situations where a guard cannot determine the correct route around an obstacle.

This would need a more advanced custom navigation solution in a future iteration.

### Animation polish

The player and guard animation systems are functional, but the final visual presentation still needs refinement.

Areas for improvement include:

- Transition timing
- Blend quality
- Root/bone alignment
- Capture/interaction animation
- Better animation matching to movement speed

The animation architecture was kept separate so these improvements can be made without rewriting gameplay systems.

### UI polish

The UI is functional, but some visual elements do not yet sit as cleanly as a final production UI.

Areas for improvement include:

- Spacing
- Typography
- Panel alignment
- Transitions
- Overall visual consistency

### Audio

The project does not yet have a complete sound-effect layer.

The following would be suitable additions in a future polish pass:

- Footsteps
- Guard alert sounds
- Throw sounds
- Pickup sounds
- UI feedback
- Capture audio

### Visual polish

The environment and characters use generic/free assets. The main focus of this version was gameplay functionality and architecture rather than final art direction.

## What I Would Improve Next

The next development pass would focus on:

1. Improving custom guard obstacle navigation.
2. Adding the guard catch / aim / surrender animation.
3. Improving player and guard animation transitions.
4. Refining the UI layout and visual hierarchy.
5. Adding a proper audio system and sound effects.
6. Additional visual polish and consistency.

## Assets & Credits

The project uses the following asset packs.

### Low Poly Shooter Pack by BD Studios

Unity Asset Store:  
https://assetstore.unity.com/packages/3d/environments/low-poly-shooter-pack-by-bd-studios-254467

Used for environment and supporting 3D assets.

### Cartoon FX Remaster Free

Unity Asset Store:  
https://assetstore.unity.com/packages/vfx/particles/cartoon-fx-remaster-free-109565

Used for particle and impact effects.

Character models and animation assets are generic assets used for prototyping and presentation.

## Requirements Coverage

| Requirement | Status |
|---|---|
| Custom Rigidbody movement | ✅ |
| Mouse aiming | ✅ |
| Custom FOV / vision math | ✅ |
| `Vector3.Dot` based detection | ✅ |
| Raycast wall occlusion | ✅ |
| Rendered vision cone | ✅ |
| Guard Patrol state | ✅ |
| Guard Suspicious / Investigate state | ✅ |
| Guard Chase state | ✅ |
| Custom object pool | ✅ |
| Custom projectile simulation | ✅ |
| Noise / distraction system | ✅ |
| Evidence objective | ✅ |
| Extraction objective | ✅ |
| Win / Lose flow | ✅ |
| Main menu | ✅ |
| Player animations | ✅ |
| Guard animations | ✅ |
| WebGL target | ✅ |
| 60 FPS verification | ✅ |
| Under 20 MB verification | ✅ |
| Complete audio layer | ⬜ |
| Final visual polish | ⬜ |
| Advanced guard navigation | ⬜ |

## Project Structure

```text
Assets/_Project/
│
├── Scripts/
│   ├── Core/
│   ├── Gameplay/
│   │   ├── Player/
│   │   ├── Guards/
│   │   ├── Noise/
│   │   ├── Projectiles/
│   │   ├── Objectives/
│   │   └── Combat/
│   │
│   ├── Presentation/
│   │   ├── Characters/
│   │   ├── HUD/
│   │   ├── Effects/
│   │   └── MainMenu/
│   │
│   └── Bootstrap/
│
├── Data/
├── Prefabs/
├── Scenes/
├── Art/
└── Tests/
```

## Submission

### WebGL Build

`<[LIVE_WEBGL_LINK](https://play.unity.com/en/games/a0be4d2a-6248-4f84-be08-2657ad724b1e/evidence-run)>`

## Final Note

This project prioritizes a clean gameplay foundation, custom implementation, and a modular architecture over final production polish.

The core gameplay systems, WebGL performance targets, and required custom implementations are complete and verified. The main remaining technical limitation is custom guard navigation around obstacles, while the main presentation gaps are animation polish, UI refinement, and a complete audio layer.
