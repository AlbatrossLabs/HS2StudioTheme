# Studio Theme (HS2 Studio)

BepInEx plugin for Honey Select 2 Studio (StudioNEOV2) that restyles Studio's UI in an Apple-style look. Version 0.11.0 covers the main menu (add / anim / sound / system), the first list each one opens, everything Add and Anim open, the panels System opens (Config, Screen Effect, Option, Light, Logo), the sound players, the workspace (object list, resizable), Studio's tool buttons and gizmo panel at the bottom left, MoarCamz's camera controls, and the top of the screen: the show/hide switches and the camera bar.

Almost nothing moves: buttons keep their order, names and behaviour, so there's little to relearn. The two exceptions are at the top of the screen: the show/hide switches sit tighter in the top-right corner, and the camera bar is centred.

## What changes

- **Main menu column:** a tinted glass panel with line icons (Tabler Icons, MIT) and a label under each one. The open menu gets an accent-coloured tile.
- **Lists (Girls / Boys / Item..., Current State / Kinematics..., Game BGM..., Config / Save / Load...):** glass panels with rounded corners and a hairline border, left-aligned rows in the Windows system font, a soft highlight on hover, the accent colour on the selected row, and a thin scroll thumb.
- The four lists end where the main menu column's glass ends, so the first two levels line up top and bottom (the scroll bar keeps Studio's margins).
- Rows that other plugins add (*Search...*, *fixed system*) are picked up when the list opens, including rows a plugin makes by copying an existing, already-themed row.
- **System panels:** glass backgrounds, section headers as soft bands with a disclosure chevron (down when open, right when folded), Apple-style check boxes and radio buttons, sliders with a thin track, accent fill and round knob, rounded text fields and dropdowns (popup list included), soft capsule buttons, a reset icon on Studio's *def* buttons, rounded colour swatches, and every label in the system font.

- **Show/hide switches** (main menu, workspace, move gizmos, color picker, map and object controls, camera bar): one glass capsule in the top-right corner with a line icon each. A tinted switch means its panel is showing, a dim one means it's hidden. Hovering shows the name and state, e.g. *Workspace: shown*.
- **Camera bar:** one glass capsule centred at the top. Numbered pills load a view; the thin tab above each saves to it (tooltip *Save view to 3*). With MoarCamz, the pills follow its slot colours: dimmed when empty, accent when it's the current view, accent tint for the last one loaded. The camera selector (a themed dropdown) sits at the right end. The capsule is the same height as the show/hide switches' and the same 8 units from the top of the screen: each slot is a 6-unit save strip over a 22-unit load pill, and MoarCamz's strip is trimmed to that row. The bar re-centres itself as MoarCamz slots are added or removed.

- **Anim panels:** the character panels (State, Kinematics with FK / IK / Gaze / Neck / Face / Hands / Save pose, Animations, Voice, Outfit, Joint), the item, light, folder, route and camera panels, and the floating animation controller, external image and route windows. Panels are glass; section headers fold with a chevron; buttons whose text Studio paints into the picture (On / Half / Off, None / Some / Lots, Front / Camera / Away / Fixed / Control / Anim, Save / Load / Import / Delete, Apply pose to IK...) are drawn as capsules with that text in the system font; icon buttons (restart, copy, paste, play, stop, repeat...) get line icons with tooltips; panel titles and section labels Studio paints in (Face: Eyebrows / Eyes / Mouth, Voice: Character / Type / Line, Light controller...) are written out at the same place.
- **Kinematics and Joint Adjustment:** Kinematics' FK / IK / Gaze / Neck / Face / Hands / Save pose panels are themed like the rest (they sit next to Kinematics' list, which had made the theme mistake them for hidden list templates).
- HS2PE's extra joint rows (Crotch, Ankles), which it hangs under Joint Adjustment's bottom edge on a grey strip, sit on the panel's glass inside it.
- Labels that started right at the panel edge (Studio's old pictures had a frame) start 10 units in; the per-part "個別参照" buttons read *From anim*; Kinematics' panels and Joint Adjustment grow at the bottom if their last row would touch or pass the edge (`PanelFit`).
- **Add panels:** the Girls / Boys character lists (sort by Name / Date with a chevron showing the direction, card preview on glass, Load / Change character as capsules), the item group → category → item picker as a column view (same as the animation picker below), and the map, light, background and frame lists.
- **Animation picker** (Anim > Animations): a Finder-style column view. The group, category and animation columns are narrower (104 units, 6 apart: Studio's 90-wide rows plus a thin scroll bar), each has a title, rows that open another column have a chevron, and only the current pick is accent: the path you took shows grey in the earlier columns (including *Animations* in Anim's own list). A breadcrumb under the columns spells out the path, e.g. *Animations › Character › Standing › Idle 2*.
- **Workspace** (the object list): one glass window with the title and an object count (*12 objects*). Rows read like a Finder or Xcode sidebar: a chevron to fold (only on rows with children), an eye that turns into a crossed-out eye when the object is hidden (its name dims too), a line icon for the kind of object (character, item, light, folder, route, route point, camera) tinted with Studio's colour for that kind, and the name in the system font. The selection is a full-width accent bar, multi-select included; hovering a row shows a faint bar. The toolbar buttons keep their places and get line icons with tooltips: Delete (red), New folder, New camera, New route, Unparent, Parent, Copy, Duplicate. Thin scroll bars.
- **Workspace placement and resize:** the window starts right under the show/hide switches, exactly as wide as them and lined up with their right edge (it can still be dragged by its title bar). Its top-right corner stays put; drag the grip in the bottom-left corner to resize (it snaps back to the switches' width when you get close). The size is remembered (setting *Workspace custom size*; 0, 0 = the switches' width, Studio's height).
- **Workspace toolbar:** 30-unit square buttons in rows along the bottom, right-aligned, as many per row as fit (6 at the default width), in Studio's order with plugin buttons slotted in where their plugin put them; Delete sits on its own at the bottom left. The horizontal scroll bar sits just above the rows and the list fills the rest.
- **Long text scrolls instead of shrinking:** one-line labels (list rows, buttons, panel labels like *Right Forearm Group*) keep their size. Text that doesn't fit ends in "…"; point at it and the whole text scrolls across, pauses, and scrolls back.
- **Gizmo panel (bottom left):** one glass panel with *Position* / *Rotation* / *Scale* rows, X / Y / Z in Apple's red, green and blue, rounded fields, and the *def* buttons as reset icons. Move / Rotate / Scale are one segmented control (a glass capsule around three icon buttons) with the current mode in the accent colour. With HSUS's *Improved Transform Operations*, its Copy / Paste / Reset Transform box becomes glass with capsule buttons, Reset in red.
- **MoarCamz controls:** its add slot / remove slot / center / settings buttons become icon buttons in one row at the left end of the camera bar (the settings button turns accent while the settings show; remove only appears with extra slots, as before). The camera settings become one glass card: rounded fields, the themed FOV slider, *Pos:* / *Rot:* as capsules with the active one in the accent colour, the lock and target buttons as icons, and the drag pads (XZ, X, Z, Y, distance) as soft rounded tiles with line icons. The pads are still where you drag.
- **Scene cameras:** Studio's camera dropdown (switch between camera objects added to the scene) is replaced by a button at the right end of the camera bar showing the active one (*Free camera* when none). It opens a glass menu: *Free camera* and each scene camera with a tick on the active one, then *Add camera to the scene* (the workspace's new-camera button). Studio's dropdown is still there underneath, hidden, and does the switching.
- **Camera slots:** three show by default, plus every slot with a view saved in it. MoarCamz's **+** shows the next hidden slot (and only adds a new MoarCamz slot once all are showing); **-** hides an empty last slot first. The strip and the camera selector shrink to fit, so the bar stays compact.
- **Right-click character menu:** right-click a character in the scene (a click, not a camera drag) for a glass context menu with the character's name on top:
  - Workspace utilities: *Select in workspace*, *Focus camera* (Studio's F), *Hide* / *Show*, *Duplicate* (Ctrl+D), *Delete* (red, Studio's own confirmation).
  - Shortcuts into the character's panels: *Current state…*, *Kinematics…*, *Animations…*, *Outfit…* select the character, open Anim and the matching page (with Studio Browser installed, Outfit brings up the outfit browser as usual).
  - *FK* / *IK* switches with a tick when on.
  - *Clothing ›* opens a flyout beside the menu: each part they're wearing (Top, Bottom, Bra, Underwear, Gloves, Pantyhose, Socks, Shoes) and All, each with On / Half / Off; the current state is accent, states a piece doesn't have are dimmed, and the flyout stays open for several changes.
  - Esc, a click elsewhere or another right-click closes it. F1 › *Right-click character menu* turns it off.
- **Animations with FK / IK on:** in Anim › Animations, when the character (or a selected one) has FK or IK on, a yellow note under the breadcrumb says the animation won't move the posed parts, with a **Turn off** button that switches FK and IK off for them.
- **Colour-coded FK nodes:** while Studio's character FK colours are still its factory white, each part gets its own colour on start: left hand blue, right hand red, body yellow, head and neck orange, hair purple, chest pink, skirt cyan (the usual rigging left-blue / right-red convention). F1 › *Apply FK colours now* sets them any time; F1 › *Colour-coded FK nodes* off leaves Studio's alone.
- **On / Half / Off as sliders:** Studio's rows of tiny state buttons (clothes, accessories, All Clothes, and None / Some / Lots) are drawn as a small three-stop slider: a thin track through the buttons, a dot per stop (dimmed if that state isn't available), the current state as an accent knob, and its name to the right when the row has room. Hovering a stop shows a faint knob there and its name. Studio's buttons stay where they were as invisible click targets.
- **Fields:** TextMeshPro fields (Config numbers, the gizmo panel, MoarCamz) get rounded backgrounds and their own text recoloured in place, so typing works as before. Numbers keep shrinking to fit their box; only words scroll.
- **Tool buttons (bottom left):** object and map controls, camera target, gizmo axes, camera free / locked, gimmicks, move handle, center point, undo and redo are small glass squares with line icons and tooltips, in Studio's places. On/off states show in the accent colour; the camera button's icon switches between a camera (free) and a lock (locked, hold Alt to move the camera); Undo / Redo dim when there's nothing to undo / redo.
- **Toolbar folders (bottom left):** iPhone-style. Drag a tool button onto another to put both in a folder (named *Tools*, *Plugins* or *Folder* by what's in it; click the name in the open folder to rename it). Drag more buttons onto the folder to add them. Click the folder (a glass square showing up to four of its icons) to open it: a glass panel beside it with its buttons, which work as usual; a click elsewhere or Esc closes it. Drag a button out of the open folder, or any button or folder to another spot, to move it there. Buttons fill the toolbar's own grid from its first cell, so the gaps close up. A folder left with one button dissolves. Works for Studio's buttons and every plugin's (KKAPI) button; the layout is saved in F1 › *Toolbar folders* (empty = Studio's layout).
- **Tooltips everywhere:** every tool button shows one tooltip (the theme's; KKAPI's own is switched off for those buttons so they don't double up), with Studio's shortcut where it has one (Undo Ctrl+Z, Redo Ctrl+Shift+Z, gizmo axes Q, move handle J, center point K, Move W / Rotate E / Scale R) and, for plugin buttons, the plugin's name. Plugin buttons the theme has no icon for keep their own picture on a glass square.
- **Plugin buttons:** known plugins' 32-pixel toolbar pictures are replaced with line icons. Left toolbar (glass squares, KKAPI's tooltips kept): QuickAccessBox (search), Timeline (yellow icon when the scene has Timeline data, accent while playing), PoseEditor, VideoExport (red record dot), NodesConstraints, Hooah, ObjectMap, extra gizmos, transform orientation, Graphics. Workspace toolbar (with tooltips): Material Editor, Custom Masking, the accessory controller. Unknown plugins' buttons keep their own pictures.
- **Sound players** (BGM, environment sound, external sound): glass panel with its title written in the system font (Studio's titles are painted into the panel picture), transport buttons with line icons (Play turns accent while something is playing, Repeat while it's on), a chevron for the track list, and the track list as themed rows with the playing track in the accent colour. Long track names keep Studio's scrolling effect.

## Settings (F1, *Studio Theme*)

| Setting | Default | |
|---|---|---|
| Enabled | on | Off puts Studio's original look back straight away, without a restart. |
| Accent colour | `0A84FF` (Apple blue) | Selected tile and row. `6EE7B7` matches Camera Sweep's mint. Light accents get dark text automatically. |
| Glass opacity | 0.86 | Lower shows more of the scene through the panels. Higher reads better over bright scenes. |
| Labels under menu icons | on | |
| Workspace custom size | 0, 0 | Set by dragging the workspace's corner grip. 0, 0 is as wide as the show/hide switches, Studio's 600 high. |
| Font | Segoe UI | Any font installed in Windows. Nothing is bundled. Japanese text falls back to Yu Gothic UI or Meiryo. |

## How it works

- Studio marks the open menu or row by colouring that button's image green, and white when it closes. A Harmony prefix on `Graphic.color` catches that write for themed buttons, records it as "selected", and swaps in the theme's colour. Studio's own selection logic is untouched.
- Buttons keep their own `Image` (still the click target). It gets a generated rounded sprite. The game's text (TextMeshPro) stays in place, made transparent, so XUnity AutoTranslator and other plugins keep working on it. A label in the system font mirrors whatever that text says.
- The glass behind the menu column is drawn by the column object itself (`00_Command`). No objects are added between Studio's own, so plugins that look things up by name or position find what they expect.
- All sprites are generated at start-up at 2 pixels per UI unit and 9-sliced, so the corners stay crisp at any size. The only image files are the four icons, embedded in the DLL.
- Settings panels are restyled control by control (`ControlStyler`) and every change is written to an undo log, so turning the theme off puts each one back in reverse order.
- Check boxes, radio buttons and sliders keep Studio's own objects as the click targets. The tick and radio dot are generated sprites with the accent colour baked in. The slider knob and fill are two added children: the knob is a fixed-size circle centred on Studio's narrow handle, and the fill runs from the track's left end to the knob.
- Copies: themed buttons keep their state in serialised fields, so when a plugin copies one (`Instantiate`), the copy takes over with Studio's original look and its own text. Hidden objects a list keeps for the game to copy (Logo's `node`) are left untouched, so their copies come out plain and are themed as rows.
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

## Not covered yet

- Picture buttons other than *def* and colour swatches keep Studio's pictures.
- Real background blur. The glass is a tint.

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
src/ColorHooks.cs          which images' colour writes the Graphic.color hook redirects, and to whom
```

Where Studio keeps each list (found from the game's scene): `01_Add/Scroll View Add Group`, `02_Manipulate/00_Chara/00_Root`, `03_Sound/Scroll View`, `04_System` (the list itself), all under `Canvas Main Menu`.
