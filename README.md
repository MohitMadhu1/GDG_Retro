# 🕶️✨ NEON ARENA — IMMERSIVE CYBER VR ✨🕶️

> **Virtual Reality Project**  
> *Retro-Cyber World built in Unity — glowing grids, neon glyphs, and a hidden secret key...*  

---

## 🎯 Objective
Create a **virtual reality environment** in Unity featuring **retro-cyber aesthetics**:  
🟣 Neon grids  
💠 Glowing glyphs  
💿 Cyberpunk ambience  
👾 A hidden **secret key** revealed through interaction  

---

## 🛠 Features
- ⚡ **Unity XR Toolkit** → VR hand-tracking & poke gestures  
- 🌐 **Retro-Cyber Visuals** → neon grids, scrolling code walls, black-body + neon outline shader  
- 🔮 **Immersion** → Bloom, Vignette, Chromatic Aberration, Cyberpunk soundtrack  
- 👁 **Interactive Glyphs** → gaze at glowing glyphs for 3 seconds to destroy  
- 🗝 **Secret Key** → discover the glitch glyph to unlock: **“GOBLIN MACHINE!!!”**  

---

## 🖼 Visual Elements
- 🟩 **Neon Grid Floor/Ceiling** → pulsing emissive shader  
- 🟪 **Matrix Data-Rain Walls** → UV-scrolled glyph curtains  
- 🟥 **Black Body + Neon Outline** → glowing drones/objects  
- 🟦 **Retro UI** → TextMeshPro start/teaser/win screens  
- 🟨 **Secret Glyph** → pulsing green cube hidden on walls  

---

## 📜 Core Scripts

| Script Name                | Purpose                                                                 |
|-----------------------------|-------------------------------------------------------------------------|
| **RoomBuilder_TronShinyMatrix.cs** | Main controller: builds arena, handles start → teaser → game → win flow |
| **GlyphWallSpawner.cs**     | Spawns glowing glyphs on walls, manages timing & placement              |
| **GazeDestroyGlyph.cs**     | Detects gaze on glyphs (3 sec) → destroys & triggers win event          |
| **PulseEmission.cs**        | Pulses emissive color over time for glowing neon effect                 |
| **UVScroller.cs**           | Scrolls UVs to create animated **Matrix code wall** effect              |
| **AutoDestroyUnscaled.cs**  | Cleans up temporary glyph objects after lifetime expires                 |
| **Shaders**                 | `BlackBodyNeonOutlineURP.shader`, `NeonTileGridPulseURP.shader`, `ScrollTextCodeURP.shader` |

---

## 📦 Technologies
- Unity **2022 LTS** + **URP (Universal Render Pipeline)**  
- **XR Interaction Toolkit** (VR hands, poke gesture start)  
- **TextMeshPro** (retro styled text)  
- **Custom HLSL Shaders** (neon outlines, pulsing grids, scrolling code)  
- **PostFX Volume** → Bloom, Vignette, Chromatic Aberration  

---

## 🚀 How to Play
1. Clone/download project  
2. Open in **Unity 2022.3+ (URP enabled, XR plugins on)**  
3. Connect VR headset (tested: **Meta Quest 2**)  
4. Open scene: `NeonArena.unity`  
5. Experience flow:  
   - 👉 **Poke gesture** or `Space` → Start teaser countdown  
   - 👁 **Look at glitch glyph** for 3 sec → reveal secret  
   - 🎉 Win screen: **“GOBLIN MACHINE!!!”**  

---

## 🔑 Secret Key
```ansi
█▀▀ █▀▀ █▀▀ █▀▀ █▄░█   █▀▄ █▀▀ █▀▄
█▀▀ ▀▀█ ▀▀█ █▀▀ █░▀█   █░█ █▀▀ █░█
▀▀▀ ▀▀▀ ▀▀▀ ▀▀▀ ▀░░▀   ▀▀░ ▀▀▀ ▀▀░
