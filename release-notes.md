<!--
Release notes for the next version of Studio Theme, filled in as the code changes.
Optional: a sentence or two up here. Then one line per change under its heading ("- ..."); empty headings are left out.
release.bat opens this in Notepad before publishing, uploads it as a draft GitHub release, saves a copy to
docs\releases\<version>.md, adds it to CHANGELOG.md and empties this file for the next version.
-->

First public release: Studio's menus restyled as clean glass, with every button still in its place.

### ADD
- Glass look for the main menu, every list and panel that Add, Anim, Sound and System open, in the Windows system font with line icons. Every System entry, End included, fits without scrolling.
- A cleaner Exit dialog: glass card, system font, capsule buttons.
- Apple-style check boxes, radio buttons, sliders, fields and dropdowns; section headers fold with a chevron.
- Finder-style workspace: object count, type icons, eye and fold chevrons, accent selection, resizable from its corner.
- Column-view pickers for animations and items, with a breadcrumb of the path you took.
- Show/hide switches as one capsule at the top right, and a centred camera bar (works with MoarCamz) with a scene-camera menu.
- A Graphics preset per camera: *Set camera graphics preset* at the bottom right of the camera settings card, the camera menu's *Graphics preset*, or a view number's right-click menu picks one of the Graphics plugin's presets, and switching to that camera or view loads it, so a scene can have a day camera and a night camera. Cameras without one are on *Default*, Graphics' own default look for Studio (only once a camera in the scene has a preset, so scenes that don't use presets keep their own settings). Saved with the scene; Camera Sweep 0.9.7 shoots each camera with its look. Works with Graphics 3.x and the older 0.5.
- Culling distance per camera (Clip: under FOV in the camera settings): hide anything too close to the camera to see past walls and props, saved with the scene and with each saved camera view.
- Camera slots: click a number to view, click the thin strip above it to save, click an empty slot to save there; + and − sit right after the numbers, and the camera menu clears a view.
- Right-click a character in the scene, or any object in the workspace, for a menu: focus, hide, rename, duplicate, delete, jump to its panels, FK / IK, and clothing On / Half / Off. Renamed characters, items and lights keep their names with the scene. Rename: Enter or a click elsewhere keeps the name, Esc cancels, and Studio's shortcuts stay quiet while you type.
- iPhone-style folders for the tool buttons: drag one onto another to combine, click to open. Buttons keep the places they already had (KKAPI's, including ones you set) and can be dragged anywhere on screen; F1 › *Toolbar grid at the left* tidies them into a grid instead.
- On / Half / Off button rows drawn as small sliders; empty accessory slots fold away in Current State (extra slots from plugins included).
- Tooltips show above a button when another button is right under it, so they don't cover the next row.
- Colour-coded FK nodes (left hand blue, right hand red...) while Studio's FK colours are still the defaults.
- A note in Animations when FK / IK is on, with a button to turn it off.
- Other plugins' panels (FK & IK, HS2PE, MoarCamz, HSUS, toolbar buttons) get the same look.
- Loading a pose pauses its animation at the pose's moment, so it doesn't play on and move the character (Studio's Pose list, Studio Browser, Pose Tools; can be turned off in the settings).

### CHANGE
- Tidier camera settings card: Dis, FOV and Clip boxes are the same width as the Pos boxes, the drag pads start level with the Pos row, and the XZ switch sits level with the Y and Dis labels.
- Hovering a list row whose name ends in "…" shows the whole name in a tooltip beside the list, so it never covers the other names. Tooltips are lighter and always drawn on top.
- Right-click a character › *Swap character…* selects them and opens Add › Girls / Boys, where *Change character* puts a different card in their place with the same pose and position.
- The workspace's right-click menu now includes KKAPI's items (Select children, and anything other plugins add there) instead of opening KKAPI's menu on top of it.
- Much less work per frame: labels check their text a few times a second instead of every frame, rows scrolled out of view are checked rarely, long lists are themed a little each frame instead of in one go, other plugins' types are looked up once, the toolbar only lays itself out when something changed, and fewer canvas rebuilds in the workspace and sliders.
- F1 › Debug › *Log performance* writes the theme's heaviest per-frame work to the log every 5 seconds, for bug reports.
- Anim › Animations columns stay visible when another plugin has resized or re-anchored Studio's animation lists (the Group column could come up empty).

### REMOVE

### FIX
- The scene-camera menu works: choosing a camera switches to it (clicks used to fall through to the backdrop and just close the menu), it scrolls when a map has many cameras, and Studio's own camera dropdown can no longer pop up underneath it.
- The glass no longer turns flat white on some setups when Studio starts (the theme repairs its pictures if another plugin removes them).
- Studio's own "Exit Studio Neo V2?" box and its confirmations get a glass card with real text. Reset and Delete confirmations say what they do ("Delete everything and reset the scene?") with a red Reset / Delete button and Cancel.
- Right-clicking between two characters standing close picks the one under the pointer, and a right-click on the other character while the menu is open switches to it straight away.
- Field text (numbers, dropdowns) sits centred in its box at the same size as the rest; oversized labels (Character Light) are back to the normal size.
- Row labels at the left of a panel (Config's BGM, SFX...) are left-aligned instead of centred.
- Long panel labels shrink a little before ending in "…", so Character Light and Joint Adjustment fit whole. List rows keep one size and show the whole name in a tooltip instead.
- Screen Effect's fold chevrons sit at the right end of their headers.
- Anim's list starts as close to the top as the other lists.
- Accessory On / Off toggles work again when a plugin shows long accessory names: labels no longer catch the clicks, and long names end before the slider.
- On / Half / Off sliders sit at the right end of their rows, giving names more room.
- The theme no longer resets toolbar buttons you had placed with KKAPI.
- Dropping a toolbar button next to another no longer pushes the whole column down: the others only move if they had already made room while you were dragging (otherwise the button goes back), and a spot that looks empty never pushes anything.
