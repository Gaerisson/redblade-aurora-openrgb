# Modifier background dimming

Holding Ctrl, Shift or Alt should make existing useful-key highlights easier to
see by temporarily dimming the remaining lighting. Studio provides a bilingual
enable control and a 0–100% dimming amount. Enable selects 70%; zero disables it.
Existing profiles remain unchanged until edited. HighlightMode continues to
decide whether this applies only locally or also over host RGB. Fn guidance,
RGB Off, USB sleep and active helper/status indicators retain priority.

Use the existing reserved profile byte 14 for HighlightDimPercent, 0–100. Keep
the 1696-byte payload, schema and four-profile layout. JSON missing the property
means zero. Nonzero values require Full 0.14.0; reject downgrade/save to older
firmware across all profiles rather than stripping settings. Studio 0.14.0
includes Full 0.14.0 and unchanged Core 0.12.1.

Approach: dim the existing active SN32 output buffer after copying the raw
render buffer at flush. This handles Direct/Aurora and native effects without
repeatedly dimming stored colors or allocating another RGB buffer. Cache two
bytes of modifier/dim state at the ordinary render boundary and request a flush
when they change. No scan/PWM/USB timer or interrupt changes. The alternative of
scaling the render buffer would compound brightness on static effects; scaling
inside the scan ISR would add input-path work. Neither is used.

Validation: production C tests for modifier/layout unions, exact percentages,
release restoration, noncompounding flushes, feature/mode/Off/suspend gates and
Turbo/Recorder/Windows priorities. Native schema/journal and Studio tests cover
range, persistence, old JSON, all-profile downgrade and actual UI controls.
Build both editions, keep at least 64 usable SRAM bytes, review, then publish
the normal stable release with BINs, Studio and matching sources. Physical
acceptance is reported separately; no automatic flashing is part of this task.
