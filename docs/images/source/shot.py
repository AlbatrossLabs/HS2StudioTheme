# Renders banner.png (1600x400), social-preview.png (1280x640) and social-post-3x2.png (1200x800) at 2x from banner.html.
import pathlib, sys
from playwright.sync_api import sync_playwright
here = pathlib.Path(__file__).parent.resolve()
out = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else here.parent
sizes = {'banner': (1600, 400, 'banner.png'), 'social': (1280, 640, 'social-preview.png'), 'social32': (1200, 800, 'social-post-3x2.png')}
with sync_playwright() as p:
    chrome = pathlib.Path('/usr/bin/google-chrome')
    b = p.chromium.launch(executable_path=str(chrome)) if chrome.exists() else p.chromium.launch()
    for name, (w, h, file) in sizes.items():
        pg = b.new_page(viewport={'width': w, 'height': h}, device_scale_factor=2)
        pg.goto(f'file://{here}/banner.html?layout={name}')
        pg.wait_for_timeout(500)
        pg.locator('.stage').screenshot(path=str(out / file))
    b.close()
