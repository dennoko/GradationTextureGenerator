# Usage

## Basic Usage

### 1. Open the Tool

Select `Tools > Gradation Baker` from the menu to open the window.

### 2. Add Meshes
 
 Drag and drop SkinnedMeshRenderer or MeshRenderer from the Hierarchy into the drop area in the Target Meshes section. You can add multiple meshes.
 
 Expand the "▶" icon to the left of each mesh to access detailed settings:
 
 - **UV Channel**: Target UV channel for baking (UV0-UV3)
 - **Mask Texture**: R channel of texture used as mask (optional)
 - **Use Vertex Color**: R channel of vertex color used as mask
 - **Invert Mask**: Invert mask application
 
 ### 3. Set the Gradient
 
 Configure colors in the "Gradient" section. Click on the gradient editor to modify colors.
 
 ### 4. Adjust Gradient Range
 
 Use the box handles in the Scene View to adjust the gradient range and direction:
 
 - **Rotation Handle**: Change gradient direction
 - **Position Handle**: Move the entire box
 - **Red Cone (bottom)**: Gradient start position (0%)
 - **Green Cone (top)**: Gradient end position (100%)
 
 Click "Fit to Mesh Bounds" to automatically fit to the mesh's bounding box.
 
 ### 5. Bake & Save
 
 Click the "Bake & Save" button to generate and save the texture.
 The UV channel and mask settings configured for each mesh will be applied.
 
 ---
 
 ## Advanced Features
 
 ### Work Mesh
 
 When using non-destructive scale components like ModularAvatar, coordinate mismatches can occur. Create a work mesh copy to avoid this issue.
 
 - Click "Create Work Mesh" to create working copies of all meshes
 - "Fit to Mesh Bounds" is automatically executed when work meshes are created
 
 ### Gradient Save/Load
 
 Click "Save" to save the gradient as a texture, and "Load" to restore it. Default save location is `Assets/GeneratedGradation/gradation/`.
 
 ### Mirror
 
 Select an axis in the "Mirror" section to apply the gradient symmetrically along that axis. A cyan box indicates the mirror position.
 
 ### Output Settings
 
 | Setting | Description |
 |---------|-------------|
 | Resolution | Output texture resolution |
 | Output to material's texture folder | Save to the same folder as the mesh's material textures |
 | Save Path | Save destination when the above is OFF |
 | Background | Choose from Transparent/White/Black |

### Language Toggle

Use the "英語表記を有効化" checkbox at the top right of the window to switch between English and Japanese.
 
 ## Notes
 
 - The preview overlay is simplified. The generated texture colors will correspond to an opacity of 1 (the preview opacity slider value does not affect the output).
