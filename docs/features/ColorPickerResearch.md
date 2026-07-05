This describes UX/UI research and should be a basis not the plan

ColorPickerReference.png should be a visual reference of what I want the color picker to look like


Designing the Next-Generation Digital Color Picker: An HCI and Color Science Framework for Creative Applications
Core UX Pain Points in Modern Color Pickers
Designing an intuitive and high-performance digital color picker requires a systematic analysis of the interaction barriers, physical strains, and cognitive friction points that disrupt the creative workflow of digital artists. Traditional design paradigms in mainstream creative suites frequently prioritize technical screen values over human-centered ergonomics and artistic intuition, resulting in severe disruptions to creative flow.

Spatial Disruption and Ergonomic Strain
The physical layout of standard digital workspaces introduces substantial physical travel for artists using digitizer tablets and styluses. In legacy applications like Adobe Photoshop, the primary color selection tool often resides as a static pop-up dialog box or a docked panel at the periphery of a high-resolution display. When an artist needs to alter a hue or adjust a shade, they are forced to break their visual focus, move their pen stylus across the screen, select the color, and travel back to the active canvas area. This repetitive journey over long drawing sessions causes muscle fatigue and breaks the creative momentum.   

To bypass this, developers have attempted to introduce on-canvas heads-up displays (HUDs) or floating color companions, but these solutions introduce their own ergonomic challenges.   

Floating palettes, such as Procreate's "Color Companion," are frequently too small or strip out critical tools—such as secondary color indicators—forcing artists back into deep, screen-obscuring popups when configuring complex, pressure-sensitive color dynamics.   

Furthermore, pop-up menus often lack intelligent context awareness, appearing over the active stroke area and obscuring the painting canvas.   

Keyboard Modifiers and Operating System Conflicts
The integration of rapid color-sampling shortcuts often triggers conflicts with system-level interface mechanics, particularly in desktop environments. In programs like Affinity Photo, the default on-canvas color picker is mapped to the "Alt" key, which is simultaneously reserved by the Windows operating system to activate the top menu bar.   

When an artist uses rapid, successive taps of the "Alt" modifier to sample colors, a minor timing delay can cause the operating system to intercept the input, transferring the application's active focus to the menu bar. Consequently, the stylus cursor transforms into a standard pointer, locking the artist out of the canvas and forcing a manual click to regain brush control.   

This inconsistent behavior, paired with a lack of persistent visual feedback indicating whether the eyedropper is active, degrades user trust.   

Driver Instability and Shortcut Overload
Advanced canvas-based shortcut triggers are highly vulnerable to hardware driver failures. Photoshop's HUD color picker, triggered on Windows systems via the complex key-mouse combination of Shift + Alt + Right-Click, frequently fails when mapped to digitizer stylus buttons, such as those on Wacom or Huion devices.   

Due to differences in how tablet drivers translate stylus taps versus standard mouse clicks, the shortcut regularly defaults back to the standard eyedropper tool or fails to display the wheel altogether. Artists are then forced to perform clean driver reinstalls or repeatedly reset their master preferences, adding frustration to their workflow.   

Sampling Failures on Complex and Layered Canvases
The classic digital eyedropper tool presents severe limitations when working on layered or composited digital paintings. On canvases utilizing multiple translucent layers, blending modes, or complex textures, standard sampling tools often capture only the raw pixel values of the actively selected layer rather than the visually composited color on the screen.   

In Adobe Illustrator, for example, sampling colors from grouped vector elements with variable opacities frequently pulls the base color of the topmost shape, rather than the visual color resulting from the stack.   

Similarly, mobile and tablet artists experience issues where color-sampling engines extract values that are darker or shifted in hue compared to reference images. This issue is exacerbated when working across different canvas color spaces (such as sRGB and CMYK), where profile mismatches trigger unexpected color shifts during active painting.   

Cognitive Load and UI Noise in Hue Selection
Standard digital color selectors struggle with visual noise and a lack of real-time comparative feedback. Many design suites present multiple, inconsistent color picking interfaces within the same application, forcing the user to mentally switch between numeric inputs, swatch selectors, and raw sliders.   

Furthermore, standard RGB hue tracks often feature highly saturated color bands (such as neon greens and yellows) that act as visual distractions, distorting the artist's value judgment.   

When a color is sampled, artists often lack a direct, real-time comparison between the previously active foreground color and the newly targeted shade. Without this immediate split-screen feedback, artists struggle to evaluate relative shifts in temperature, chroma, and value before committing a stroke.   

Core Pain Point Category	Specific UX Failure Mechanism	Operational Workarounds (Legacy)	HCI Outcome
Spatial Travel

[cite: 1]

Color selectors are locked to fixed peripheral docks, forcing long physical travels with the stylus.

Artists drag panels over the canvas, obscuring active artwork areas.

Wrist fatigue; fractured creative focus.

Modality Conflicts

[cite: 5]

Alt-key modifier conflicts with OS window menu activation, shifting focus away from canvas.

Double-clicking to toggle tools, or mapping secondary buttons.

Unintentional cursor shifts; painting flow is repeatedly blocked.

Driver Failures

[cite: 6]

HUD multi-key shortcuts (e.g., Shift+Alt+Right-Click) fail to register via tablet driver configurations.

Reinstalling Wacom/Huion drivers; resetting application preferences.

Tool defaults back to standard pointer; HUD fails to render.

Composited Sampling

[cite: 8]

Eyedropper extracts single-layer values or top shape properties rather than visual blending result.

Manually duplicating and merging layers to sample, then deleting the merge.

Inaccurate values; workflow pauses to perform canvas maintenance.

Lack of Comparative View

[cite: 11]

No direct spatial comparison of the newly targeted color against the active foreground paint.

Squinting at the canvas or repeatedly painting test strokes.

Color value mismatches; muddy transitions on the canvas.

  
Geometric Configurations and Spatial Cognitive Mapping
The spatial geometry of a digital color picker is a critical factor in how intuitively an artist can navigate color relationships and build muscle memory. The visual layout of saturation, brightness, and hue defines the speed and accuracy with which an artist can target a specific shade.   

The HSV/HSB Square
The orthogonal HSV (Hue, Saturation, Value) Square is the most common color picking layout in modern design software. It maps color saturation along the horizontal x-axis and value (or brightness) along the vertical y-axis, with hue isolated to an external slider or a circular ring surrounding the square.   

The primary advantage of the square is its axial independence. Because the saturation and value controllers are perfectly perpendicular, an artist can execute precise, isolated adjustments to a single property without affecting the other.   

For instance, when rendering a form under consistent lighting, an artist must repeatedly adjust the saturation of a flesh tone while keeping its value stable. The perpendicular axes of the square align with this workflow, making it easy to build muscle memory.   

However, the square possesses a severe geometric flaw: absolute white is represented by a single pixel at the top-left corner, while the entire bottom horizontal edge of the square maps to absolute black. This design creates massive visual redundancy. Because human eyes cannot distinguish between different desaturated dark tones near black, this layout can bias users toward choosing overly dark colors and makes targeting pure white difficult, as any slight diagonal slip drops the selection into a gray tint or a darker shade.   

The Traditional Equilateral Triangle (John Derry Model)
First introduced by John Derry for Corel Painter, this model places an equilateral triangle within an outer hue ring. The three vertices of the triangle represent absolute white, absolute black, and the pure, fully saturated color of the selected hue.   

The primary advantage of the triangle is its logical representation of traditional color mixing. As an artist moves horizontally across the triangle, they navigate a linear progression of equal value, keeping the perceived lightness consistent. Tints (hue mixed with white) are mapped cleanly along the top-left to right edge, while shades (hue mixed with black) lie along the bottom-left to right edge.   

Crucially, the triangle features only a single black point at the bottom-left vertex. This eliminates the visual redundancy of the square's bottom edge.   

However, the spatial area of a triangle is mathematically smaller than a square, resulting in compressed midtone zones that make fine adjustments to low-saturation colors highly sensitive to tiny physical stylus movements.   

The Polar Disc
The polar disc configuration displays saturation radially from a completely desaturated gray center to a fully saturated outer edge, with the angle around the circle defining the hue. Value is controlled via an independent vertical or horizontal slider.   

While visually elegant, the disc lacks physical corners. In UI design, corners act as natural boundaries that allow artists to quickly throw their pen to an extreme to select pure white, absolute black, or maximum saturation. Without these physical constraints, selecting extreme parameters on a disc requires high-precision visual hunting, which slows down the workflow.   

RYB (Itten), YURMBY, and RGB Color Wheels
The underlying color system used to arrange hues around the primary ring shapes how artists construct color harmonies.   

Standard RGB/CMY Wheels: These wheels are based on the physics of digital screens, placing green opposite magenta, and blue opposite yellow. While technically accurate for light emitting devices, this mapping does not align with the artistic relationships taught in traditional painting, where red is opposite green, and yellow is opposite violet.   

The Traditional RYB (Red, Yellow, Blue) Itten Wheel: Used in traditional art education, this model arranges hues based on physical paint mixing. This alignment allows digital artists to easily apply classical color schemes, such as complementary, triadic, and analogous harmonies.   

The YURMBY Color Wheel: Developed by Tobey Sanford, this wheel combines RGB and CMY models to create a truer representation of human color perception. The acronym represents Yellow, Red, Magenta, Blue, Cyan, and Green. By integrating these color models, the YURMBY wheel ensures that the center of the wheel aligns with the human eye's perception of neutral gray, making it highly effective for digital gamut masking.   

Selection Interface Geometry	Axis Mechanics	Ergonomic Corner Targeting	Visual Redundancy	Artistic Usability
HSV Square

[cite: 14]

Orthogonal (Saturation x-axis, Value y-axis).

High; extreme points are placed in distinct corners.

High; entire bottom edge maps to redundant black pixels.

Excellent for building muscle memory and making isolated value/saturation shifts.

Derry Triangle

[cite: 14, 18]

Equilateral (Linear progression of equal value).

High; absolute black and white are isolated to clear vertices.

Low; features a single, consolidated black point.

Highly intuitive for traditional painters accustomed to mixing tints and shades.

Polar Disc

[cite: 15, 19, 20]

Cylindrical (Radial Saturation, Angular Hue).

Low; lacks physical corners for quick targeting.

Low; desaturated colors are consolidated at the center.

Useful for visualizing overall color relationships, but difficult for rapid, precise value adjustments.

  
Perceptually Uniform Modeling and the Shift to OKLCH
Traditional digital art applications default to RGB or HSV spaces, which are computationally convenient but fail to align with the non-linear way the human eye perceives color. This misalignment represents a major barrier for digital artists, particularly when managing value relationships.   

The Perceptual Flaws of HSL and HSV
The primary issue with HSL (Hue, Saturation, Lightness) and HSV (Hue, Saturation, Value) models is their lack of perceptual uniformity. Under these systems, color dimensions are treated as mathematically uniform, but human visual receptors are highly non-linear.   

For example, if an artist selects a pure HSL yellow and a pure HSL blue at the exact same 50% lightness value, the yellow will appear dramatically brighter than the blue.   

This is because HSL does not account for the intrinsic luminance differences of distinct wavelengths. Furthermore, when an artist attempts to generate a scale of desaturated shades or lighter tints in HSL, the colors suffer from severe hue and saturation drift. Light blue shades often drift toward purple, and dark values muddy out toward a flat, lifeless gray.   

This structural failure forces digital painters to constantly compensate. To maintain a cohesive value structure, they cannot simply change the hue slider. Instead, they must manually adjust both saturation and value diagonally in a complex, non-intuitive dance to keep the perceived brightness consistent.   

Traditional Munsell Value Calibration
Traditional art education relies on systems like the Munsell Color Tree, which maps color along three coordinates: Hue, Value, and Chroma. Unlike RGB, Munsell value is calibrated to human perception.   

In traditional painting, desaturated colors naturally have appropriate values (e.g., yellow has a light value, red a medium value, and blue a dark value).   

When digital artists transition from traditional pigments to standard digital color pickers, they are often misled by the uniform dimensions of HSL, which mask these natural variations.   

HSY' (Hue, Saturation, Luma) as an Intermediate Bridge
To resolve these perceptual inaccuracies without leaving the RGB color space, applications like Krita include the HSY' color model.   

Unlike HSL, HSY' replaces standard lightness with Luma (Y 
′
 ), which is an approximation of true physical luminosity calculated by applying weighted coefficients to the RGB channels.   

The coefficients represent the human eye's heightened sensitivity to green light compared to blue light. The standard ITU-R BT.709 Luma equation is calculated as follows:   

Y 
709
′
​
 =0.2126R 
′
 +0.7152G 
′
 +0.0722B 
′
 
[cite: 33]

By calibrating the color picker to these luma coefficients, HSY' provides a far more intuitive selection tool for digital painters.   

When an artist shifts the hue slider in HSY', the picker dynamically adjusts the saturation and value sliders to maintain a constant perceived brightness, preventing unexpected jumps in value.   

OKLCH: The Perceptually Uniform Standard
Developed by Björn Ottosson in 2020, the OKLCH color space represents a major advancement in digital color science. OKLCH is a polar coordinate representation of the OKLab color space, which is modeled directly on human vision principles. OKLCH defines color using three channels:   

Lightness (L): Ranges from 0 (absolute black) to 1 (absolute white), matching human perceived brightness in a perfectly linear fashion.   

Chroma (C): Represents color purity or intensity, ranging from 0 (pure grayscale) to approximately 0.4 or more for highly saturated wide-gamut colors.   

Hue (h): Represents the color wavelength as an angle from 0 
∘
  to 360 
∘
 .   

OKLab conversions begin by mapping linear RGB coordinates into a LMS cone-excitation space, applying a non-linear cube root compression to match human neural signal processing, and calculating polar opponent axes a (red/green) and b (yellow/blue). The conversion from OKLab to polar OKLCH coordinates is calculated as follows:   

C= 
a 
2
 +b 
2
 

​
 
[cite: 28]

h=atan2(b,a)⋅ 
π
180
​
 
[cite: 28]

if h<0 then h=h+360
[cite: 28]

Because OKLCH is perceptually uniform, changing hue (h) while keeping Lightness (L) and Chroma (C) constant preserves perceived brightness.   

If an artist creates a series of paint strokes across different hues using a fixed L and C, the strokes will have the exact same perceived lightness and visual weight, eliminating value skew.   

Additionally, OKLCH supports wide-gamut color spaces (such as Display-P3 and Rec2020) used by modern displays, allowing artists to paint with highly vibrant colors that lie outside the standard sRGB gamut.   

Perceptual Consistency Comparison:
[ HSL / HSV Space ] ---> Hue shift ---> Lightness fluctuates wildly (unstable values)
[ OKLCH Space ]     ---> Hue shift ---> Lightness remains perfectly flat (stable values)
Subtractive Pigment Simulation and Spectral Color Mixing
Traditional digital art software operates on an additive RGB light-blending model, which treats pixels as light sources that combine to form white. This approach fails to mimic physical paint mixing, where pigments subtract light wavelengths.   

The Muddy Failures of Additive Blending
Under additive RGB rules, blending blue and yellow paint pixels does not yield a vibrant green. Instead, because blue and yellow are opposite vectors in the RGB color cube, their mathematical midpoint passes directly through desaturated gray.   

This causes blended brush strokes to look muddy, dead, and desaturated. Traditional physical pigments, however, behave subtractively: paint particles absorb specific light wavelengths and reflect others.   

When real blue and yellow pigments mix, they absorb red and orange wavelengths, leaving only green wavelengths to reflect back to the viewer's eye.   

Additionally, combining physical pigments with white titanium paint typically increases their perceived saturation, whereas linear RGB blending simply washes out and desaturates colors.   

Kubelka-Munk Spectral Equations
To replicate physical paint behavior, digital painting apps can implement a subtractive spectral mixing model. This model is based on the Kubelka-Munk theory (formulated in 1931), which mathematically predicts how light scatter and absorption behave within layered, pigmented materials.   

In a simplified single-constant Kubelka-Munk model, the scattering coefficient (S) is treated as a constant, allowing the Kubelka-Munk function (F(R)) to directly define the relationship between reflectance (R) and absorption (K):   

S
K
​
 = 
2R
(1−R) 
2
 
​
 
[cite: 42]

When mixing colors, the software converts the input RGB values into spectral reflectance curves across the visible spectrum (approximately 380 to 750 nm).   

It calculates the concentrations of each pigment based on luminance (L), tinting strength (T), and mixing factors (f):   

C 
pigment
​
 =g(L,T,f)
[cite: 42]

These concentrations are used to compute the weighted sum of each color's absorption-to-scattering ratio (K/S), resulting in a combined reflectance curve (R 
mix
​
 ). Finally, the system converts this curve back into RGB space, producing a vibrant, natural blend.   

Additive sRGB Mixing (Mathematical):
[ Saturated Blue ]  +  [ Saturated Yellow ]  -->  [ Muddy Neutral Gray ]

Subtractive Pigment Mixing (Physical):
[ Saturated Blue ]  +  [ Saturated Yellow ]  -->  [ Saturated Green ]
Integration of Painting Libraries: Mixbox and Spectral.js
Developing a custom spectral rendering engine can be challenging due to high computational complexity.   

Fortunately, production-ready libraries like Mixbox and Spectral.js solve this by packaging physical color models into a simple interface.   

Mixbox: This engine treats colors internally as four-component physical pigments close to CMYK. To achieve the high speeds required for real-time digital painting, Mixbox uses large pre-calculated look-up tables (LUTs) to instantly translate RGB inputs into pigment concentrations, perform the Kubelka-Munk blend, and output the resulting RGB value. This enables saturated gradients, natural hue shifts (e.g., phthalo blue shifting to turquoise when spread thin), and vibrant brushstroke falloffs without performance lag. Mixbox is currently used by professional applications like Rebelle to achieve hyper-realistic fluid and paint simulations.   

Spectral.js: A lightweight, zero-dependency JavaScript library designed for web-based digital painting apps and color pickers. It uses a seven-primary spectral reflectance curve system (White, Cyan, Magenta, Yellow, Red, Green, and Blue) to calculate Kubelka-Munk values. It supports multi-color mixing, custom palettes, and real-time GPU-accelerated blending via GLSL shaders. It also allows developers to configure the "tinting strength" of specific colors, preventing dominant pigments from visually overwhelming a mixture.   

Advanced Creative Workflows and User-Requested Features
To design a truly professional digital color picker, developers must look beyond simple color selection and integrate tools that actively assist with color theory, composition, and palette organization.   

Gamut Masking and Spatial Limitations
Gamut masking is a technique popularized by illustrator James Gurney in his book Color and Light. It translates traditional color planning and limited-palette mixing into the digital realm.   

In a digital app, this feature allows artists to overlay custom geometric vector shapes (such as triangles, diamonds, or custom polygons) directly onto the color wheel.   

Once applied, the color picker restricts selection exclusively to the colors enclosed within the mask boundaries. This restriction forces a highly disciplined, cohesive relationship across the painting.   

Gamut Masking Workflow:
1. Select Color Wheel
2. Overlay Geometric Vector Polygon (e.g., Triad)
3. Restrict/Lock Color Picker to Polygon Interior
4. Center of Gravity of Polygon defines the Perceived Neutral Gray
Crucially, the geometry and placement of the mask determine the visual structure of the artwork. The geometric center of gravity of the mask represents the perceived neutral color of the composition.   

By shifting the mask away from true gray (for example, centering a triangular mask over the orange-yellow spectrum), the artist establishes a warm, unified color grade.   

Anything inside the mask will harmonize, and desaturated tones near the mask's center will appear to the viewer's brain as a natural neutral gray, even if they are technically tinted warm orange.   

Software like Krita provides a dedicated Gamut Masks Docker that supports rotating, editing, and saving custom vector shapes.   

Intermediate and Approximate Color Palettes
When painting complex subjects like human skin or organic environments, artists need to quickly generate and select transition tones.   

Intermediate Color Panels: As implemented in Clip Studio Paint, this tool features a grid (typically configurable up to 30×30 subdivisions) with four user-defined key colors in the corners. The engine automatically interpolates clean step transitions between these four poles. This allows artists to quickly generate custom, cohesive blending scales.   

Approximate Color Panels: This utility displays a matrix of colors similar to the active foreground selection. Artists can configure which color dimensions the panel alters. For instance, the grid can display variations of the active color by shifting value along the vertical axis and saturation/chroma along the horizontal axis while holding hue perfectly static. This provides a highly controlled environment for choosing subtle highlights and shadows without introducing unwanted hue drift.   

Luminosity, Tone, and Chroma Locking
Value structure is the single most critical factor in creating depth, contrast, and realism in digital art. When value relationships are broken, compositions quickly fall apart. To protect an artwork's value structure, professional artists demand "Luminosity Lock" (or Tone Lock) capabilities.   

When this feature is enabled, the color picker anchors the perceived lightness of the active color. As the artist slides the cursor across different hues or saturation levels on the wheel, the picker automatically calculates and shifts the RGB/HSV coordinates to ensure the perceived luminance remains completely unchanged.   

This allows digital artists to easily introduce rich, vibrant hue-shifting into highlights and shadows (e.g., shifting warm shadows toward cool blues or purples) with absolute confidence that they are not altering the underlying value structure.   

Dynamic Luma-Based Palettes
To reduce manual color tweaking, advanced interfaces can include Luma-Based Palettes. This mode displays a dynamically updating set of colors that match the brightness of the active selection. It features two display modes:   

Focused Palette: Shows only nearby hues that have a similar perceived brightness. The lightness slider restricts its range, allowing artists to quickly explore subtle value relationships.   

Full Palette: Shows the entire spectrum of colors at the selected brightness level, providing complete freedom to choose harmonious accents.   

Color-Lock and Custom Swatches Compatibility
Professional creative pipelines require seamless palette synchronization across different applications.   

To support this, digital color pickers must import and export standard palette formats, including Adobe Swatch Exchange (.ase), Photoshop binary (.aco), Krita Palette (.kpl), Gimp Palette (.gpl), and Microsoft RIFF (.riff).   

Furthermore, integrating a color history palette that tracks previously used colors ensures that artists can easily retrieve exact values without having to sample from the canvas.   

Implementation Guidelines and Architecture for Digital Painting Apps
To build a professional-grade digital color picker that addresses the needs of digital artists, developers should integrate spatial convenience, perceptual science, subtractive pigment blending, and advanced harmonization utilities into a single, cohesive toolset.

1. Spatial and Interaction Architecture
The primary interface must be built on a modal, context-aware pop-up system, modeled after the "Follow Mode" found in advanced tablet controllers.   

The Trigger: The color picker should be bound to a single, customizable stylus button or short-tap modifier that does not trigger operating-system menu actions.   

The Popup: Upon activation, the color panel must appear immediately adjacent to the pen tip to eliminate hand movement.   

The Interaction: The panel must support two display modes:

Follow Mode: The panel tracks the stylus movement, staying close to the active stroke.   

Pin Mode: The panel can be torn off and docked anywhere on the screen.   

The Preview: The selection cursor must feature an immediate visual feedback mechanism, displaying a split-ring preview that shows the previously selected color in one half and the newly targeted color in the other.   

2. Double-Engine Color Pipeline
The application’s back-end must employ a dual-engine architecture to ensure both perceptual consistency and realistic color blending:

Engine A (Perceptual Selection Core): Built entirely on the OKLCH color space. The primary interface sliders and selection geometry (triangle or square) must map directly to Lightness (L), Chroma (C), and Hue (h). This ensures that visual steps in the color picker correspond perfectly to human vision, preventing hue drift and unexpected brightness jumps during palette building.   

Engine B (Subtractive Mixing Core): Integrated directly into the color mixing panels (such as the Intermediate Color grid and brush-blending canvas) using Mixbox or Spectral.js. When colors are mixed, the system must process them as physical pigments. This ensures that yellow and blue blend into a rich green, and paints retain their saturation when combined with white, mirroring traditional media.   

3. Advanced Theoretical Suitability
The color picker must include built-in utilities for gamut masking, intermediate mixing, and value protection:

Gamut Masking Integration: The main color wheel must support custom vector overlays. Users can choose from preset schemes (e.g., Complementary, Triadic, Atmospheric Triad) or draw custom polygons. The picker must provide a toggle to "Enforce Gamut Mask," which clips any selection pointer to the interior of the mask, making color harmony automatic.   

Intermediate Grid Panel: A secondary tab must provide an n×n grid where artists can drop up to four distinct colors into the corners. The spectral mixing engine then generates smooth, pigment-realistic intermediate transitions across the grid, allowing users to pick cohesive blending tones.   

Luminosity / Tone Lock Toggle: A prominent toggle on the interface must allow the artist to lock the perceived Lightness (L) or Chroma (C). When locked, any adjustments to the hue slider will keep the brightness or saturation completely stable.   

Color Picker Feature	Target Interface Modality	Underpinning Engine & Math Model	Artistic Workflow Advantage
Contextual Heads-Up Picker

[cite: 1, 3]

On-canvas popover containing a combined wheel and triangle or square selector.

Spatial follow matrix tracking stylus pointer coordinates in real-time.

Eliminates hand movement and travel fatigue.

Perceptual LCH Coordinates

[cite: 27, 57]

Primary sliders and coordinate fields displaying Lightness (L), Chroma (C), and Hue (h).

OKLab conversion matrices with non-linear cube root compression.

Prevents hue and saturation drift, ensuring value consistency.

Subtractive Palette Mixer

[cite: 37, 50, 51]

An n×n transition grid with user-assignable key-color slots in each corner.

Kubelka-Munk theory calculations integrated via Mixbox or Spectral.js.

Automatically generates physically accurate color transitions.

Gamut Mask Overlay

[cite: 48, 49]

Geometric vector overlays (such as triangles or custom polygons) on the color wheel.

Clip-path boundary checking that limits color selection to the interior of the mask.

Simplifies color scheme planning and ensures canvas-wide harmony.

Luminosity & Chroma Lock

[cite: 11, 12, 25]

Interface toggles next to the primary H, S, and V/L sliders.

Code that holds L or C values static in the OKLCH engine, calculating and updating HSB/RGB outputs in real-time.

Protects the underlying value structure of the painting, allowing artists to shift hues without altering perceived brightness.

Wide-Gamut Proofing

[cite: 58]

Toggle indicator on the main interface to check if colors fall within specific spaces.

Real-time out-of-gamut detection mapping Display-P3 or Rec2020 to sRGB fallbacks.

Ensures that highly vibrant colors map accurately across different output devices.

Multi-Format Swatch Support

[cite: 54, 55]

Import and export menus in the swatch panel configuration.

File parsing engines reading and writing standard formats (.ase, .aco, .kpl, .gpl, .riff).

Supports seamless integration and color consistenc