# StoryLab PointCloud Renderer for Unity 6

An optimized point cloud renderer for Unity 6 with special support for Meta Quest 3 and other VR platforms.

## Features

- Import and render PLY point cloud files in Unity 6
- VR-optimized rendering for Meta Quest 3 and other mobile VR headsets
- Dynamic LOD system with automatic performance optimization
- Flexible chunking system for efficient rendering
- Multiple rendering quality presets for VR

## Requirements

- Unity 6.0.0 or later
- Universal Render Pipeline (URP) package
- For VR: XR Interaction Toolkit and OpenXR packages

## Installation

### Option 1: Install via Unity Package Manager (UPM)

1. Open your Unity project
2. Go to Window > Package Manager
3. Click the "+" button and select "Add package from git URL..."
4. Enter the following URL:
   ```
   https://github.com/Dataroomxyz/pointcloudrenderer.git
   ```
5. Click "Add"

### Option 2: Clone the Repository

1. Clone the repository to your local machine:
   ```
   git clone https://github.com/Dataroomxyz/pointcloudrenderer.git
   ```
2. In Unity, go to Window > Package Manager
3. Click the "+" button and select "Add package from disk..."
4. Navigate to the cloned repository and select the package.json file

## Quick Start

### Importing Point Clouds

1. Copy your .ply files into your Unity project
2. Select a .ply file in the Project window
3. In the Inspector, adjust the import settings:
   - For VR projects, enable "Optimize for VR" and select an appropriate optimization level
   - Adjust Chunk Size, LOD settings, and other parameters as needed
4. Click "Apply" to import the point cloud

### VR Optimization Settings

For Meta Quest 3, we recommend the following settings:

- **VR Optimization Level**: Medium
- **Chunk Size**: 3-5 units
- **CrossFade LODs**: Enabled
- **Add VR Optimizer Component**: Enabled

For more complex point clouds or performance-sensitive applications:

- **VR Optimization Level**: High or Extreme
- **Chunk Size**: 2-3 units
- **Initial Subsample Mode**: SpatialFast with minimum distance 0.01-0.05

## Performance Tips for Meta Quest 3

1. **Use the VR Point Cloud Optimizer component**
   - This component automatically adjusts point cloud rendering based on current performance
   - Use the "Balanced" preset as a starting point

2. **Adjust chunk size carefully**
   - Smaller chunks (2-5 units) work better for VR as they allow better culling
   - Too many small chunks can increase draw calls, so find the right balance

3. **Use LOD system effectively**
   - Enable CrossFade for smoother transitions
   - Configure more aggressive LOD transitions for distant objects

4. **Use the mobile shader**
   - The URP_Mobile shader is automatically used when "Optimize for VR" is enabled
   - This shader avoids using geometry shaders which perform poorly on mobile GPUs

5. **Batch point clouds by material**
   - Use "Share Default" or "Custom" material mode for multiple point clouds to reduce draw calls

## Known Issues

- HDRP support is limited and not recommended for VR projects
- Very large point clouds (>1 million points) may require aggressive optimization for Quest devices
- Dynamic point clouds and runtime modifications are not fully optimized for VR performance

## License

This package uses the MIT License. See LICENSE.md for details.

## Credits

- Original PointCloudRenderer by StoryLab Research Institute
- Updated for Unity 6 with VR optimization by Dataroomxyz
- Based on PCX point cloud importer by Keijiro Takahashi
