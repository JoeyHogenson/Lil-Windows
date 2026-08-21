===========================================================
Draw On Screen – Painting System (Unity Asset)
===========================================================

Version: 1.0.7
Unity Compatibility: Unity 2022 LTS – Unity 6.0+
Author: Ahmad Naser

-----------------------------------------------------------
1. Overview
-----------------------------------------------------------

Drawing Board is a complete 2D painting system for Unity.

It allows users to draw freely using a brush, erase strokes,
choose colors, adjust brush size, and navigate between multiple
drawing boards.

This asset is ideal for creative apps, kids drawing games,
and educational painting tools.

-----------------------------------------------------------
2. Key Features
-----------------------------------------------------------

- Brush tool with adjustable size
- Eraser tool (erase mode + clear board)
- Color picker and palette buttons
- Smooth stroke drawing (mobile and PC support)
- Multi-canvas navigation (Next / Previous boards)
- Optional Save/Load system per board
- Clean prefabs and modular script structure
- Fully compatible with Unity 6.0+

-----------------------------------------------------------
3. Package Folder Structure
-----------------------------------------------------------

DrawingBoard/
   Animations/
   Atlases/
   Scripts/
   Prefabs/
   Scenes/
   Sprites/
   Materials/
   Plugins/
   Resources/
   Sounds/
   Documentation/

-----------------------------------------------------------
4. Quick Start
-----------------------------------------------------------

1. Import the package into your Unity project.

2. Open the demo scene:

   Assets/DrawingBoard/Scenes/DemoScene.unity

3. Press Play.

4. Start drawing using touch input (mobile) or mouse (PC).

5. Change color, brush size, or switch to eraser mode.

6. Use Next / Previous buttons to move between boards.

-----------------------------------------------------------
5. Core Scripts
-----------------------------------------------------------

DrawingManager.cs
- Main controller that handles drawing strokes, tool selection,
  and board state.

StrokeRenderer.cs
- Creates and updates strokes using LineRenderer or sprite-based
  drawing methods.

ToolController.cs
- Manages brush/eraser switching, brush size, and color selection.

BoardManager.cs
- Handles multiple boards and navigation between them.

UIEvents.cs
- UI button events (Brush, Eraser, Clear, Next, Previous, Save).

-----------------------------------------------------------
6. Customization
-----------------------------------------------------------

Brush Size:
- Adjust min/max brush thickness in ToolController.

Colors:
- Edit the palette list or enable a custom color picker.

Board Count:
- Change the number of available boards inside BoardManager.

Stroke Style:
- Replace LineRenderer material or brush texture for different
  drawing styles.

-----------------------------------------------------------
7. Requirements & Notes
-----------------------------------------------------------

- Unity 2020.3 LTS or newer (Unity 6 supported)
- Best used with an Orthographic 2D camera
- Ensure UI buttons are linked correctly in the Inspector
- Recommended screen orientation: Landscape

-----------------------------------------------------------
8. Support
-----------------------------------------------------------

Author: Ahmad Naser
Email:  info@ahmadnaser.com 
WhatsApp: 00970599042502
Website : https://ahmadnaser.com
Facebook : https://www.facebook.com/dev.ahmadnaser
See online demo : https://tinyurl.com/yry73kct
Playstore Demo : https://tinyurl.com/p8txu3k6

===========================================================
Thank you for using Alphabet Board Asset!
===========================================================
