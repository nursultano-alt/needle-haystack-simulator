# 🌾 Needle in a Haystack Simulator (Unity 6 URP)

A high-performance tactile physical granular simulator featuring **10,000+ interactive hay straws**, real-time fiber interlocking physics on the GPU, tactile needle search gameplay, and an interactive **Hay Pourer Studio** tool for artists and technical designers.

[![Unity 6](https://img.shields.io/badge/Unity-6000.3.26f1%20URP-black?style=flat&logo=unity)](https://unity.com/)
[![Physics](https://img.shields.io/badge/Physics-GPU%20Compute%20Shader%20XPBD-gold)](#)
[![GitHub](https://img.shields.io/badge/License-MIT-blue)](#)

---

## 🎮 Gameplay Overview

Inspired by tactile granular physics and the classic idiom *"Searching for a needle in a haystack"*:
- A wooden barn crate filled with **10,000 to 20,000 physically simulated hay straws**.
- **Tactile Straw Extraction:** Click and drag individual straws out of the haystack. Experience dynamic tension and watch neighboring straws get dragged along due to interlocking friction.
- **Sifting & Digging:** Use your hands or pitchfork prongs to push through and part the hay with radial physical impulses.
- **Golden Needle Discovery:** Search deep inside the pile to uncover the gleaming metallic needle! Pulling it out triggers victory.

---

## 🕹️ Controls

| Key / Mouse | Action |
| :--- | :--- |
| **`W, A, S, D`** | First-Person Locomotion around the barn crate |
| **`Mouse Look`** | Look around & aim reticle |
| **`[LMB]` (Hold)** | **Grab & Drag** specific straw under reticle out of the stack |
| **`[LMB]` (Release)** | Toss / Drop held straw with release velocity |
| **`[RMB]` (Hold)** | **Sift / Dig** through hay (radial push force with pitchfork) |
| **`[Space]` (Hold)**| **Stream Pour** hay directly from hands into the stack |
| **`[R]`** | **Reset Haystack** (re-emits 10,000 fresh straws and hides needle) |
| **`[Esc]`** | Toggle mouse cursor lock |

---

## 🖌️ Artist & Tech Tool: Hay Pourer Studio

Designed for level designers, tech artists, and gameplay engineers:
- **Editor Window:** Open via `Tools -> Haystack -> Hay Pourer Studio`.
- **Live SceneView Pour Brush:** Hold `[LMB]` in the SceneView over any surface to pour continuous streams of physical hay in real-time.
- **Physics-Driven Settling:** Straws tumble, collide with container walls, bounce with restitution, and naturally interlock into an organic mound rather than falling through or clipping statically.
- **Configurable Parameters:**
  - Pour rate (straws/second)
  - Nozzle spray cone & velocity
  - Straw length, radius, and rotational tumble
  - Color presets: *Golden Wheat, Sun-Bleached, Fresh Green Hay, Autumn Amber*
  - Instant burst buttons: *+500 straws*, *+2,500 straws*, *Clear Stack*, *Re-hide Needle*.

---

## ⚙️ Technical Architecture

### 1. GPU Compute Shader Simulation (`HaySimulation.compute`)
- **Kernels:**
  - `CS_InitStraws`: Buffer initialization and zero-allocation cleanup.
  - `CS_EmitStraw`: Physical stream emitter with cone spread, tumble velocity, and color variation.
  - `CS_Integrate`: Semi-implicit Euler integration for gravity, linear drag, and angular damping.
  - `CS_SolveCollisions`: Segment-to-segment distance constraints, container boundary collisions, and **anisotropic fiber interlocking friction**.
  - `CS_SolveInteraction`: Spring-damper constraint for grabbed straws + radial sifting raycast.
  - `CS_BuildMatrices`: Generates `float4x4` transformation matrices directly on GPU memory.
- **Rendering:** `Graphics.DrawMeshInstancedIndirect` with zero GC allocations per frame.

### 2. Custom URP Shaders
- `Assets/Shaders/HayStrawGPUInstanced.shader`:
  - Anisotropic specular sheen mimicking natural dry wheat fibers.
  - Sub-surface light translucency for warm sunlight penetration.
- `Assets/Shaders/GoldenNeedleURP.shader`:
  - Mirror-metallic gold reflections with pulsing sparkle glints upon player discovery.

### 3. Blender 5.0 Headless 3D Models
Generated via Python `bpy` with zero primitive placeholders:
- `Straw_LowPoly.fbx`: Segmented curved wheat stem with node bulges (8 radial segments, 32 vertices).
- `Needle_Golden.fbx`: Sharp tapered needle with pierced eyelet loop.
- `Hay_Barn_Crate.fbx`: $4.0 \times 4.0 \times 1.2\text{m}$ rustic wooden pen with plank walls.
- `Hay_Pitchfork_Tool.fbx`: 3-tine farm pitchfork with curved steel prongs and wooden handle.

---

## 🚀 Quick Start
1. Clone the repository:
   ```bash
   git clone https://github.com/nursultano-alt/needle-haystack-simulator.git
   ```
2. Open in **Unity 6 (6000.3.26f1)** via Unity Hub.
3. Open scene `Assets/Scenes/HaystackPlayground.unity` and press **Play**!
