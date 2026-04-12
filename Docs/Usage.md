# Usage

## Basic Usage

### 1. Open the Tool

Select `dennokoworks > Gradation Baker` from the menu to open the window.

Check the "Enable Tool" checkbox at the top right of the window to activate the UI. When unchecked, all operations are locked.

### 2. Add Meshes

Drag and drop SkinnedMeshRenderer, MeshRenderer, or any GameObject with a Renderer component from the Hierarchy into the drop area in the Target Meshes section. You can add multiple meshes.

Expand the "▶" icon to the left of each mesh to access detailed settings:

- **UV Channel**: Target UV channel for baking (UV0–UV3)
- **Mask Texture**: R channel of texture used as mask (optional)
- **Use Vertex Color**: R channel of vertex color used as mask
- **Invert Mask**: Invert mask application
- **Split by Material**: Outputs separate textures for each sub-mesh (material), useful when UVs overlap.

Click "Clear All" to remove all meshes from the list.

### 3. Set the Gradient

Configure colors in the "Gradient" section. Click on the gradient editor to modify colors.

### 4. Adjust Gradient Range

Use the box handles in the Scene View to adjust the gradient range and direction.

You can also enter values directly in the window fields:

- **Shape**: Choose between `Linear` (directional gradient) and `Spherical` (radial gradient from center).
- **Center**: Center position of the box (use the "Reset" button to automatically fit to mesh bounds)
- **Rotation**: Gradient direction (use the "Reset" button to reset to default)
- **Height** (Linear) / **Size** (Spherical): Scale of the gradient range
- **Rotation Handle**: Change gradient direction
- **Position Handle**: Move the entire box or sphere center
- **Red/Green Cones**: Adjust dimensions (Height for Linear, Width/Height/Depth for Spherical). The gradient starts at 0% in the center/bottom and ends at 100% at the surface/top.


### 5. Bake & Save

Click the "Bake & Save" button to generate and save the texture.
The UV channel and mask settings configured for each mesh will be applied.

---

## Advanced Features

### Work Mesh

When using non-destructive scale components like ModularAvatar, coordinate mismatches can occur. Create a work mesh copy to avoid this issue.

- When no work mesh exists, a "**Create Work Mesh**" button is shown. Click it to create working copies of all meshes.
- When work meshes exist, the button changes to "**Delete Work Mesh**" to remove them.
- Gradient range is automatically adjusted to fit the mesh when work meshes are created.

### Gradient Save/Load

Click "Save" to save the gradient as a texture, and "Load" to restore it. Default save location is `Assets/GeneratedGradation/gradation/`.

### Mirror

Enable the toggle in the "Mirror" section to access the following settings:

- **Mirror Axis**: Mirror the gradient symmetrically along X, Y, or Z axis. A cyan box indicates the mirror position.
- **Overlap Blend**: Choose how overlapping mirrored areas are blended.
  - **Brighter (Max)**: Blend by taking the brighter color.
  - **Darker (Min)**: Blend by taking the darker color.

### Preview

Use the "Blend Mode" dropdown in the "Preview" section to switch how the gradient overlay is composited in the Scene View.

> **Note**: The preview overlay is simplified. The generated texture colors correspond to an opacity of 1 (fully opaque).

### Output Settings

| Setting | Description |
|---------|-------------|
| Resolution | Output texture resolution |
| Output to material's texture folder | Save to the same folder as the mesh's material textures (ON by default) |
| Save Path | Save destination when the above is OFF (use "..." button to browse) |
| Background | Choose from Transparent / White / Black |
| Edge Padding | Expands UV island edges outwards (0–16px) to prevent seam artifacts during sampling |

> **Tip**: If you notice "seams" due to MIP mapping or texture sampling, setting Edge Padding to 4px or higher is recommended.

> **Note**: After saving, the first saved texture will automatically be selected and revealed in the Project tab.

### Language Toggle

Use the "Enable English Mode" checkbox at the top right of the window to switch between English and Japanese.
