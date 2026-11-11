# RGB brightness: verified limits and remaining uncertainty

This audit concerns the pinned SonixQMK driver used by RedBlade+ Full/Core 0.12. It is a source/documentation audit, not an electrical or optical measurement of the keyboard.

## What the software already supports

- Per-key HSV value and the global master range from 0 to 255. Master 255 preserves each pixel's chosen value; it does not turn a dark color into a brighter one. Studio's maximum-intensity command raises selected non-black colors to value 255 without changing hue/saturation.
- Local default colors use value 64. A valid saved custom palette can already supply 255; 64 is not a fixed cap on those custom colors. Reactive peak, media fallback, host pixels and PC effect brightness have their own color/intensity settings.
- Direct RGB uses `hsv_to_rgb_nocie`. There is no second CIE dimming curve to remove.
- Driver color multipliers default to 1 for R/G/B. Values are ultimately stored as 8-bit channels and sent to hardware PWM with period 255. Raising the multiplier cannot produce a channel above 255 in this representation; unchecked multiplication could wrap and make colors worse.

Relevant project sources: [renderer](../firmware/aurora_via/redblade_rgb.c), [configuration](../firmware/aurora_via/redblade_config.c), [maximum-intensity editor](../app/KeyboardEditing.cs). The upstream driver is pinned to [SonixQMK commit 62973ee](https://github.com/SonixQMK/qmk_firmware/blob/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60/drivers/led/sn32f2xx.c), with its [configuration header](https://github.com/SonixQMK/qmk_firmware/blob/62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60/drivers/led/sn32f2xx.h). Complete matching sources are in the release bundles.

## Could a different driver make it brighter?

Possibly, but the current evidence cannot establish a safe gain. The board configuration multiplexes six rows and three color channels: 18 successive slots. An individual channel therefore has a theoretical slot share near 1/18 before scan/interrupt overhead; that is not a measured duty cycle. Outputs are blanked around transitions and the key matrix is scanned by the same path. Changing blanking, slot timing or row activation could change average LED current, key scanning and ghosting. RedBlade+ keeps the existing 720,896 Hz shared clock and scan behavior.

The [manufacturer](https://designedby.gg/product/red-blade-01/) identifies the SN32F248B and SonixQMK compatibility, but does not supply the LED circuit/current limits on that page. The [official Sonix SN32F248B v2.4 manual](https://www.sonix.com.tw/webapi/fl219873/SN32F248B_V2.4_EN.pdf), GPIO sections 6.2–6.3, documents direction/configuration registers; no programmable GPIO brightness/current selector was identified there or in the pinned GPIO header/PAL implementation. Chip drive specifications are not measured LED current or permission to increase board current.

Before claiming an electrical boost, measure lit-slot and blanking durations, key-scan timing, LED/row current, USB voltage/current and temperature, and identify the LED/resistor/driver topology. No percentage gain can be supported without those measurements. This is a remaining engineering question, not proof that every possible driver optimization is impossible.

## Why lights can change during Apply

Studio pauses RGB clients while writing onboard settings, then restores them. The driver also skips its usual matrix/PWM channel update while flash programming is active. These facts can explain transitions or temporary artifacts; no waveform was measured to identify the exact cause of a particular visible flash. A transient brighter state does not establish that it is safe or sustainable.

No overdrive, GPIO-current change, PWM-clock change or scan bypass is included in 0.12. Higher contrast, darker background colors and longer colored reactions can improve visibility within the existing output range.
