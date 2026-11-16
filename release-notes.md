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
- Culling distance per camera (Clip: under FOV in the camera settings): hide anything too close to the camera to see past walls and props, saved with the scene.
- Camera slots: click a number to view, click the thin strip above it to save, click an empty slot to save there; + and − sit right after the numbers, and the camera menu clears a view.
- Right-click a character in the scene, or any object in the workspace, for a menu: focus, hide, rename, duplicate, delete, jump to its panels, FK / IK, and clothing On / Half / Off. Renamed characters, items and lights keep their names with the scene.
- iPhone-style folders for the bottom-left tool buttons: drag one onto another to combine, drag to any cell to move it (gaps are kept), click to open.
- On / Half / Off button rows drawn as small sliders; empty accessory slots fold away in Current State (extra slots from plugins included).
- Tooltips show above a button when another button is right under it, so they don't cover the next row.
- Colour-coded FK nodes (left hand blue, right hand red...) while Studio's FK colours are still the defaults.
- A note in Animations when FK / IK is on, with a button to turn it off.
- Other plugins' panels (FK & IK, HS2PE, MoarCamz, HSUS, toolbar buttons) get the same look.
- Loading a pose pauses its animation at the pose's moment, so it doesn't play on and move the character (Studio's Pose list, Studio Browser, Pose Tools; can be turned off in the settings).

### CHANGE
- Anim › Animations columns stay visible when another plugin has resized or re-anchored Studio's animation lists (the Group column could come up empty).

### REMOVE

### FIX
