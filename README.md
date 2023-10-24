# StoryLab Point Cloud Renderer
Importer, chunker and LOD generator with PCX-based importing and rendering mechanisms for PLY pointcloud files. Compatible with instanced rendering for VR applications.

The point clouds can optionally be broken into cubic chunks to significantly improve rendering performance. The importer can also optionally auto-generate LODs for the point clouds, which improves performance by an order of magnitude when a large portion of the point cloud is far enough from the camera that individual points cannot be distinguished. To maintain the same visual appearance, the size of the points is increased to compensate for the loss of area as points are removed at far distances. A set of recommended LOD settings is included by default, but this can be user modified.

The following graph shows performance across a test sample of six point clouds, ranging from 0.5 million points to 57.8 million points. In the most intensive test scenario, the chunking and LOD system with the default settings included in the package were found to improve GPU performance from 81ms/frame (12FPS) to 5ms/frame (200fps) on a cloud of 57.8 million points. These tests were carried out on URP, Multipass rendering, Ryzen 7 5800H, 40GB DDR4-3200, RTX 3060 Mobile, Oculus Quest 2 via Quest Link at native resolution.

![Screenshot 2023-10-24 123909](https://github.com/StoryLab-Research-Institute/pointcloudrenderer/assets/114744494/6d14a3c9-7b94-4101-8873-89031e4de160)

The point shaders are integrated into the Unity LOD cross-fade system, using resizing of the points according to the cross-fade factor to provide a very cheap means of cross-fading with no need for transparency or dithering. The transition between LOD stages can still be noticable for significant differences in point density, but for slow movement and dense clouds it was found to become almost indistinguishable. The point sizes in the clip below have been exaggerated to make the transition more visible.

https://github.com/StoryLab-Research-Institute/pointcloudrenderer/assets/114744494/0347edfb-56c6-4f8c-aaa6-b7d6f8ff79f8

The points are rendered as diamonds on platforms supporting geometry shaders, as these were found to be more visually pleasing than vertically aligned squares.

# Recommendations
If you have point clouds with a lot of overdraw, for example you are using large points for sylistic reasons, you may wish to turn on forced depth texture priming in URP. This setting can be found in your Universal Renderer Data as "Depth Priming Mode" (under "rendering"), and allows the renderer to use depth information to decide whether to draw a given pixel of an object or not. On the test system, this improved performance significantly with large point sizes.

# Limitations
Only supports URP for now. There is no technical reason why BRP cannot be supported (in fact I already wrote a version of the shaders for BRP), I simply haven't updated the BRP shaders to match the URP ones yet for a consistent experience.

Point Clouds are rendered using geometry shaders on platforms which support them, or will otherwise fall back on the rendering API's default points mechanism, which may or may not respect the point size (DX11 will never render more than one pixel, OpenGL will always render the point as a sized quad).

# Installation
Install via the package manager.

![Picture1](https://github.com/StoryLab-Research-Institute/pointcloudrenderer/assets/114744494/3322c55e-da6a-4249-aeb7-ef8037faa1fc)

Enter the GitHub URL.

![Picture2](https://github.com/StoryLab-Research-Institute/pointcloudrenderer/assets/114744494/d4fdb8c1-b7a8-494f-aed0-21d54c080749)

Installation is complete, drag and drop your .PLY files into the project window.

![Picture3](https://github.com/StoryLab-Research-Institute/pointcloudrenderer/assets/114744494/6603104c-aaf1-489d-9046-187e78fe2d05)
