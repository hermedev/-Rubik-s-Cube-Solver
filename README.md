# 3D Rubik's Cube Solver (Unity)

An interactive 3D Rubik's Cube simulator and solver implemented in **Unity 6** using the **Universal Render Pipeline (URP)**.

---

## 🚀 Features

- **Interactive 3D Rubik's Cube**: Full 3x3 cube simulation composed of modular piece prefabs.
- **Dynamic Face Rotation & Animation**: Smooth rotation of faces around animated pivots.
- **Cube State & Mapping**: Real-time reading of facelet states using raycasts and 2D UI mapping.
- **Universal Render Pipeline (URP)**: Upgraded with modern Lit shaders and render pipeline asset configuration for optimized multi-platform rendering.

---

## 🛠️ Requirements & Environment

- **Unity Version**: Unity 6 (`6000.6.3f1` or later recommended)
- **Render Pipeline**: Universal Render Pipeline (URP v17.x)
- **Platform**: PC / Mac Standalone, WebGL, Mobile

---

## 📁 Project Structure

```text
Assets/
├── Materials/
│   ├── CubeColors/          # URP Lit materials for each cube face (Red, Blue, Green, Yellow, Orange, White, Black)
│   └── Table/               # Background table textures and URP material
├── Prefabs/
│   ├── Piece.prefab         # Individual cube cubie/piece prefab
│   └── RayStart.prefab      # Raycast emitter prefab used to read cube facelet states
├── Scenes/
│   └── Rubics Cube.unity    # Main gameplay and solver scene
├── Scripts/
│   ├── CubeMap.cs           # Manages 2D representation/UI of the cube state
│   ├── CubeState.cs         # Tracks facelet states, face arrays, and active sides
│   ├── PivotRotation.cs     # Controls face rotation parenting, angle lerping, and completion events
│   ├── ReadCube.cs          # Uses raycasts to read colors off the cube pieces
│   ├── RotateCube.cs        # Handles whole-cube rotation and user camera/cube orbit
│   └── SelectFace.cs        # Raycast picking for user face selection and slice rotations
└── Settings/
    ├── UniversalRenderer.asset             # Forward URP Renderer configuration
    └── UniversalRenderPipelineAsset.asset   # Active URP pipeline asset
```

---

## 🎮 How to Run

1. Open the project in **Unity 6 (`6000.6.3f1` or newer)**.
2. In the Project window, navigate to `Assets/Scenes/`.
3. Open **`Rubics Cube.unity`**.
4. Press the **Play** button in the Unity Editor toolbar.

---

## 🕹️ Controls & Interaction

- **Left Mouse Click / Drag**: Select and rotate a face or rotate the entire cube view.
- **Raycast Face Reading**: Automatically identifies color alignments on each face of the cube.

---

## ⚙️ Render Pipeline Configuration

The project is configured with the **Universal Render Pipeline**:
- Pipeline asset: `Assets/Settings/UniversalRenderPipelineAsset.asset`
- All materials are upgraded to the `Universal Render Pipeline/Lit` shader.
- Target scene `Rubics Cube.unity` is registered in **Editor Build Settings**.

