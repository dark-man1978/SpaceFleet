# Space Fleet 0.3 demo

The MP4 is stored in seven ordered binary parts to allow reliable uploads through the repository connector. The release workflow joins them and verifies the SHA-256 digest before publishing the video.

Download the complete playable MP4 from the [v0.3.0 release](https://github.com/dark-man1978/SpaceFleet/releases/tag/v0.3.0).

To reconstruct locally from the repository root:

```bash
cat docs/videos/SpaceFleet-0.3-Demo.mp4.part* > SpaceFleet-0.3-Demo.mp4
```

Expected SHA-256: `da2d110d7ead5db84887796b87400060eb7b6340cb5f6681de2d78223d867169`.

Real Godot engine captures, scripted demo flight and scene cuts. Original game audio. 56.5 seconds, 1280 × 800, H.264/AAC. MIT project.
