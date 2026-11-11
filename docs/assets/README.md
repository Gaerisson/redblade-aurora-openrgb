# Shortcut maps

The maps show the 109-key Red Blade Full Size ISO layout and firmware Full shortcuts (unchanged in 0.11). Colors identify available roles; they do not imply that every state is active simultaneously. Letter labels use French AZERTY positions.

Regenerate the editable SVG files from the pinned SonixQMK tree:

```sh
python tools/make-shortcut-map.py --qmk-tree /path/to/qmk_firmware
python tools/make-feature-visual.py --qmk-tree /path/to/qmk_firmware
```

The generator reads the pinned board geometry and this project's actual base keymap, checks all matrix positions against `led-map.json`, and checks that Fn is the base-layer `MO(1)` key. Board/layout sources retain the SonixQMK GPL notices included in the complete source bundle. The project logo is documented separately in [assets/LOGO.md](../../assets/LOGO.md).

PNG exports were rendered from the earlier 0.10 SVG files with Sharp 0.35.5 and retain their 0.10 labels; regenerate them from the current SVGs before publishing 0.11. No AI-generated key positions or labels are used.

The separate feature showcase uses the same actual 109-key geometry, with four media keys. It illustrates roles rather than simultaneous states. Core has no added gameplay controls; see [editions](../EDITIONS.md).
