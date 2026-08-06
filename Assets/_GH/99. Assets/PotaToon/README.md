# POTA-TOON

![PotaToon](./Documentation/PotaToonImage_256.png)

POTA-Toon is an animation-style toon rendering asset designed for URP Forward(+) environments and PC/Console platforms. It supports lighting from multiple sources and detailed character shadows, ensuring natural illumination even in low directional light settings, such as concerts, by accurately detecting spotlights on characters.

Beyond lighting, POTA-Toon provides advanced rendering for complex transparent meshes, including realistic shadow support. Transparent shadows are available in all environments, while the OIT feature—designed for rendering intricate transparent meshes like dresses—is officially supported only on Windows devices (specifically DirectX 11 and 12).

Additionally, POTA-Toon includes dedicated post-processing for characters, allowing for localized color adjustments and bloom effects to enhance artistic expression.

- Supported Versions: 2021 LTS, 2022 LTS, 6.0 or later (RenderGraph-compatible from 6.0).

![Thumbnail](./Documentation/PotaToonThumbnail.png)


## How To Use
Please import the PotaToonURP asset via double-click.
You can check out the details in the below guideline documents. 

### Please check out https://potatoon.dev for more information!

## Examples
You can find the sample projects in the `Examples` folder.


## Open Sources
- lilToon by lilxyzw (MIT License)
  - https://github.com/lilxyzw/lilToon
  - Used Glitter feature from lilToon
- Tony McMapface by Tomasz Stachowiak (MIT License)
  - https://github.com/h3r2tic/tony-mc-mapface
- OIT by Till Davin (Apache License 2.0)
  - https://github.com/happy-turtle/oit-unity
  - Code Changes
    1. Make it compatible with RenderGraph in Unity 6.0 or above.
    2. Separate the buffer initialization logic to PreRenderPass.
    3. Create a Depth buffer for OIT objects.
    4. Remove the interface and move the OITLinkedList class to the pass class.
    5. Add ‘Additive’ blending mode.
    6. Change to use _ScreenSize.
    7. Expand OIT packed color from RGBA8 to RGBA16F.
- GLSL Tonemappings by Damien Seguin (MIT License)
  - https://github.com/dmnsgn/glsl-tone-map
- GT Tonemapping by yaoling1997 (MIT License)
  - https://github.com/yaoling1997/GT-ToneMapping

## Used Avatars
1. 早川葵, https://booth.pm/ko/items/5405703
2. マヌカ, https://booth.pm/ko/items/5058077
