# Selection corner sprites

Four transparent, 6×6 pixel PNGs form the shared menu-selection highlight:

- `corner-top-left.png`
- `corner-top-right.png`
- `corner-bottom-right.png`
- `corner-bottom-left.png`

Place each sprite at the corresponding corner of the selected item's or slot's
on-screen bounds. Use point filtering; ordinary menus keep the native corner size.
The gap between corners expands with the target rectangle. The same logical
rectangle should drive controller selection and mouse-hover highlighting.
These assets are embedded in ControllerSupport.dll and used by the shared
selection renderer. Active Item Trasher uses red, Emblem Forge blue and Prism
Forge purple. All use the same sprites and shared 1.5× corner scale. Only corner artwork changes; the selection
rectangle, hitbox, held icon and quantity geometry retain their normal placement.
Inactive modes and ordinary menus retain the original tint and size.

`selection-corner-master.png` is the generated transparent source image. The
four production sprites were mechanically cropped, nearest-neighbor reduced,
and rotated from that source. The source was created with the built-in imagegen
tool in `stylized-concept` mode using this prompt:

> Use case: stylized-concept
> Asset type: pixel-art game UI sprite master, genuinely transparent PNG
> Primary request: ONE compact TOP-LEFT L-shaped selection corner for a retro 2D inventory slot. The visual should be a very simple orthogonal bracket, not a frame.
> Style: strict 16x16 pixel-art grid aesthetic, nearest-neighbor hard square pixels, no gradients or antialiasing. Use only four solid colors: dark charcoal outline (#211b19), muted brown (#6a4a31), warm brass (#b88a4c), pale ivory highlight (#f2dfac). This matches a tiny dark-brown/charcoal inventory UI with warm beige numerals.
> Geometry: bracket starts near the upper-left of a tight transparent square; horizontal arm to the right and vertical arm down, each about 10 grid pixels long and 2 grid pixels thick. One-pixel darker outer edge, one-pixel brighter inner edge. Orthogonal arms with a crisp square intersection. Bracket occupies most of the canvas, with only 1–2 grid pixels of transparent padding around it. A 90-degree rotation must yield an equally good corner.
> Constraints: truly transparent everywhere except the bracket; no stray pixels, haze, shadows outside the bracket, textures, bevel gradients, rounded edges, extra corners, background, letters, icons, or watermark. Treat this as a shippable small UI sprite, not a presentation mockup.
