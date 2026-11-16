# Studio Theme: developer notes

How the theme works, how to build it, and where everything lives. For what it does, see the [README](../README.md).

## How it works

- Studio marks the open menu or row by colouring that button's image green, and white when it closes. A Harmony prefix on `Graphic.color` catches that write for themed buttons, records it as "selected", and swaps in the theme's colour. Studio's own selection logic is untouched.
- Buttons keep their own `Image` (still the click target). It gets a generated rounded sprite. The game's text (TextMeshPro) stays in place, made transparent, so XUnity AutoTranslator and other plugins keep working on it. A label in the system font mirrors whatever that text says.
- The glass behind the menu column is drawn by the column object itself (`00_Command`). No objects are added between Studio's own, so plugins that look things up by name or position find what they expect.
- All sprites are generated at start-up at 2 pixels per UI unit and 9-sliced, so the corners stay crisp at any size. The only image files are the four icons, embedded in the DLL.
- Settings panels are restyled control by control (`ControlStyler`) and every change is written to an undo log, so turning the theme off puts each one back in reverse order.
- Check boxes, radio buttons and sliders keep Studio's own objects as the click targets. The tick and radio dot are generated sprites with the accent colour baked in. The slider knob and fill are two added children: the knob is a fixed-size circle centred on Studio's narrow handle, and the fill runs from the track's left end to the knob.
- Copies: themed buttons keep their state in serialised fields, so when a plugin copies one (`Instantiate`), the copy takes over with Studio's original look and its own text. Hidden objects a list keeps for the game to copy (Logo's `node`) are left untouched, so their copies come out plain and are themed as rows.
- Copies of sliders and labels work the same way, and the theme's own sprites are named `theme_…` so a copied control that's already themed isn't themed twice. Copies that took over are tracked (`Clones`) and put back when the theme is turned off.
- New plugin UI: `PluginAdopter` watches the panels the theme styled. Once a second it compares the scene's object count (`Transform.hierarchyCount`, cheap) with the last one; when it changed, it looks for objects it hasn't seen under those panels and runs `ControlStyler` on each new one. Objects inside a list's content and the theme's own (`Theme …`) are skipped.
- Only labels created while the game runs are wrapped for "…" / scrolling. Studio's own labels (loaded with the scene) are left where they are, because plugins copy them and position the copy relative to the original.
- Studio swaps the switch pictures between a bright and a dark version to show on/off. The theme puts its own sprite back whenever that happens and reads on/off from Studio's own `visibleFlags`, so the switches always match what's really showing.
- The camera bar works with Studio's ten slots or with MoarCamz's scrolling strip (MoarCamz moves the slots into its own scroll view and adds buttons). Twice a second the bar themes any new slots and measures what's on its top row, then moves the whole bar so that row is centred. MoarCamz's own layout inside the bar is untouched.
- Legacy text inside a themed button (Studio's list nodes) is restyled where it is (font and colour), so effects that move it, like the sound list's scrolling names, keep working. TextMeshPro labels are mirrored as before.
- Arrow pictures Studio swaps to show a state (sort direction) become line chevrons that follow every swap (`IconFollower`).
- `BakedArt.cs` lists what Studio paints into its pictures, by sprite name: which pictures are panel backgrounds (drawn as glass), which titles and labels they carry (with their position in the picture, so the theme can write them at the same place), and which button pictures stand for which label or icon. A button that Studio switches between pictures (expand / collapse arrows, repeat on / off) follows the switch.
- States Studio shows with a picture rather than a colour (the sound players' playing indicator, the repeat on/off pictures) are read from that picture and drawn as the accent colour.
- Workspace rows: Studio colours a row's select button green to select it (a different green for multi-select). The same `Graphic.color` hook catches that and the row draws its accent bar instead (`IColorHook`, shared with themed buttons). Rows are picked up as Studio creates them, including rows inside folded folders. The chevron and eye pictures Studio swaps are replaced on every swap, and the name moves 16 units right to make room for the type icon (re-applied if Studio repositions it), so renaming, sorting and jump-to-selection plugins keep working on Studio's own objects.
- Workspace resizing changes the sizes and positions Studio's layout already uses (the title bar's `PreferredSizeFitter` width, the scroll view, scroll bars and buttons) by the same offset; turning the theme off puts Studio's numbers back.
- Plugin buttons are recognised by the plugin that owns them: KKAPI's toolbar list gives the owning plugin's GUID and button ID; a workspace button is matched by the type that handles its click.
- Turning the theme off restores every original sprite, colour, button transition and position.

## Build

Same setup as Camera Sweep: .NET SDK 6+, references from the IllusionMods/BepInEx NuGet feeds in `nuget.config`. TextMeshPro comes from the game's own `StudioNEOV2_Data\Managed` folder under `HS2Dir`.

```
dotnet build -c Release
```

Output: `bin\Release\HS2StudioTheme.dll`, also copied to `X:\HS2 RX15\BepInEx\plugins\HS2StudioTheme\`. Change the path with `-p:HS2Dir="D:\Other\HS2"`.

Icons: `res/icon_*.png`, rendered from Tabler Icons: menu icons (`square-rounded-plus`, `run`, `music`, `settings`) at 96 px with a 1.75 stroke; `chevron-down`, `arrow-back-up` (reset), `check`, the switch icons (`menu-2`, `list-tree`, `arrows-move`, `palette`, `adjustments-horizontal`, `video`) the transport icons (`player-play`, `player-stop`, `player-pause` filled, `repeat`), and `chevron-right`, `chevron-left`, `player-skip-back`, `refresh`, `copy`, `clipboard`, `player-track-next`, `x`, `device-floppy`, `download`, `chevron-up`, for plugin buttons `search`, `timeline`, `stretching`, `player-record` (filled), `affiliate`, `components`, `map-2`, `axis-x`, `compass`, `aperture`, `brush`, `mask`, `hanger`, for the tool buttons `edit`, `focus-2`, `gizmo`, `video`, `lock`, `bone`, `puzzle`, `arrows-move`, `circle-dot`, `arrow-back-up`, `arrow-forward-up`, for the gizmo modes `arrows-move`, `rotate-clockwise`, `arrows-maximize`, for MoarCamz `plus`, `minus`, `arrows-vertical`, `arrows-horizontal`, `arrows-diagonal`, `zoom-in`, and for the workspace `trash`, `folder-plus`, `video-plus`, `route`, `link-off`, `link`, `squares`, `eye`, `eye-off`, `user`, `box`, `bulb`, `folder`, `map-pin`, `video` at 64 px. Licence in `res/LICENSE-tabler-icons.txt`.

## Source map

```
src/StudioThemePlugin.cs   entry point, settings, applies the theme once Studio's menu starts
src/Hooks.cs               Harmony: RootButtonCtrl.Start, Graphic.color (selected state)
src/MainMenuTheme.cs       finds the four menu buttons and their lists
src/ThemedButton.cs        one restyled button: rounded image, hover/press/selected, mirrored label, icon
src/ThemedPanel.cs         one restyled list: glass background, scroll thumb, rows (rescans for added rows)
src/GlassRect.cs           padded 9-slice graphic behind the menu column
src/Sprites.cs             generated sprites (glass, rows, tiles, scroll thumb) and the embedded icons
src/Fonts.cs               Windows system font with Japanese fallbacks
src/Palette.cs             colours, radii, opacities
src/ControlStyler.cs       settings panels: backgrounds, headers, toggles, sliders, fields, dropdowns, buttons, labels
src/ThemedLabel.cs         a TextMeshPro label shown in the system font (mirrors the hidden original)
src/ThemedSlider.cs        thin track, accent fill, round knob
src/DisclosureChevron.cs   section header fold button
src/Labels.cs              text helpers (alignment mapping, tag stripping, sizes)
src/Undo.cs                restore log for the settings panels
src/TopBar.cs              show/hide switches (top-right capsule) and the centred camera bar
src/TooltipView.cs         the hover tooltip
src/SoundTheme.cs          BGM / environment / external sound players
src/BakedArt.cs            text and icons painted into Studio's pictures, by sprite name
src/ColumnView.cs          column views for the animation and item pickers (narrow columns, titles, chevrons, grey path, breadcrumb)
src/IconFollower.cs        state arrows (sort direction) as chevrons
src/PanelFit.cs            grows a panel's glass so its last row has room at the bottom
src/WorkspaceTheme.cs      the workspace window (glass, count, toolbar icons, resize grip) and its rows
src/PluginButtons.cs       line icons for known plugins' toolbar buttons (left toolbar and workspace)
src/LeftTools.cs           Studio's tool buttons at the bottom left (icons from BakedArt, glass squares)
src/GuideTheme.cs          the gizmo panel (Guide Input): glass, captions, mode segment, HSUS's transform buttons
src/MoarCamzTheme.cs       MoarCamz's camera buttons (one row in the camera bar) and settings card
src/CharacterMenu.cs       right-click character context menu (workspace utilities, panels, FK / IK, clothing flyout)
src/KinematicsNotice.cs    "FK / IK is on" note with a Turn off button under the animation columns
src/FkColours.cs           colour-coded FK node preset
src/ToolbarFolders.cs      drag-to-combine folders, reordering and compaction for the bottom-left toolbar
src/GlassMenu.cs           small glass pop-up menu (ticks, separators, closes on outside click / Esc)
src/StateSlider.cs         On / Half / Off (None / Some / Lots) button rows drawn as three-stop sliders
src/Marquee.cs             one-line labels that end in "…" and scroll on hover instead of shrinking
src/PluginAdopter.cs       themes controls other plugins add to Studio's panels later
src/Clones.cs              themed controls in plugin-made copies, so turning the theme off restores them too
src/CameraClip.cs          per-camera culling (near clip) distance; ClipSceneData.cs saves it in the scene via KKAPI
src/SlotClicks.cs          camera slot clicks: view, save on an empty slot, right-click menu
src/ExitDialogTheme.cs     the "exit the game?" dialog
src/SceneNames.cs          names given from the right-click menu (saved with the scene via ClipSceneData)
src/CameraSlots.cs         clear / remove a camera slot (MoarCamz, by reflection)
src/EmptyRows.cs           folds away Current State's empty accessory slots
src/ColorHooks.cs          which images' colour writes the Graphic.color hook redirects, and to whom
```

Where Studio keeps each list (found from the game's scene): `01_Add/Scroll View Add Group`, `02_Manipulate/00_Chara/00_Root`, `03_Sound/Scroll View`, `04_System` (the list itself), all under `Canvas Main Menu`.

## Releasing

`release.bat` (wrapper for `scripts/release.ps1`) asks for the version, builds with `-p:SkipDeploy=true` (the DLL installed in HS2 is left alone), zips `BepInEx/plugins/HS2StudioTheme/` into `releases/` and uploads a draft GitHub release with the zip and its SHA-256. Release notes come from `release-notes.md`, filled in as changes are made. `release.bat -NoBuild` packages the DLL that's already installed.
