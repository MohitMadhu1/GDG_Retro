# 🌐🕶 NEON ARENA VR — RETRO-CYBER IMMERSION 🕶🌐
*Step into a glowing cyber world where neon grids, glyphs, and secrets collide...*  

---

## 🎯 Experience
Enter a **virtual reality arena** built in Unity, inspired by retro cyberpunk aesthetics. Explore an atmospheric grid-world where interactive glyphs and glowing drones guide you to uncover a **hidden key** that unlocks the final surprise.  

---

## ✨ Key Highlights
- 🖐 **VR Interactions** → Hand-tracking + poke gestures via Unity XR Toolkit  
- 🌌 **Stylized Cyber Ambience** → Neon grids, scrolling glyph curtains, emissive shaders  
- 🎧 **Immersion Effects** → Bloom, vignette, chromatic aberration, synthwave ambience  
- 🔮 **Glyph Gameplay** → Stare at glyphs for 3 seconds to destroy them  
- 🗝 **Hidden Mechanic** → Discover the glitch glyph to reveal **“GOBLIN MACHINE!!!”**  

---

## 🎨 Visual Design
- 🟦 **Arena Grid** → Pulsing emissive shader floor & ceiling  
- 🟩 **Matrix-Style Walls** → Animated UV-scrolling glyph rain  
- 🟥 **Glowing Drones & Objects** → Black-body shader with neon outlines  
- 🟨 **Retro Interface** → TextMeshPro screens for intro, teaser, and win state  
- 🟪 **Glitch Glyph** → A secret cube glowing green among the walls  

---
## 🎮 Demo Gameplay
[![Watch Demo](https://img.youtube.com/vi/VdULRo8oNRg/hqdefault.jpg)](https://youtu.be/VdULRo8oNRg)


---

## 🧩 Core Components

| Script | Role |
|--------|------|
| `RoomBuilder_TronShinyMatrix.cs` | Handles arena build, game state flow (intro → teaser → gameplay → win) |
| `GlyphWallSpawner.cs` | Spawns glyphs dynamically on walls with timing control |
| `GazeDestroyGlyph.cs` | Detects 3s gaze input → destroys glyphs / triggers events |
| `PulseEmission.cs` | Drives glowing emission pulse over time |
| `UVScroller.cs` | Creates continuous Matrix-style wall scroll effect |
| `AutoDestroyUnscaled.cs` | Cleans up temporary glyphs / objects |
| **Custom Shaders** | Neon grid pulse, glowing outlines, scrolling text shaders |

---

## 🛠 Tech Stack
- **Unity 2022.3 LTS** with **URP**  
- **XR Interaction Toolkit** → poke, gaze, hand-tracking  
- **TextMeshPro** → retro UI elements  
- **Custom URP Shaders** → HLSL neon grids, outlines, glyph scroll  
- **Post-Processing** → Bloom, chromatic aberration, vignette  

---

## 🎮 How to Play
1. Clone/download this repository  
2. Open in **Unity 2022.3+** (ensure URP + XR plugins installed)  
3. Connect your **VR headset** (tested on Meta Quest 2)  
4. Open the scene: `NeonArena.unity`  
5. Gameplay loop:  
   - 👉 Perform poke gesture (or press `Space`) to start teaser  
   - 👁 Focus gaze on glyphs to destroy them (3s hold)  
   - 🟩 Find the pulsing green glitch glyph  
   - 🎉 Unlock the **secret ending: “GOBLIN MACHINE!!!”**  

---

## 🗝 Secret Tip
Look for the **green pulsing glyph** hidden among the walls... it holds the key.  
