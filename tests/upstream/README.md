# Pinned QMK color implementation

The four C/header files in this directory are unmodified copies from `quantum/` in SonixQMK commit `62973ee48a976c6f7cc6d4f7b198bd48a2fdfe60`.

`sn32f2xx.c` is the unmodified `drivers/led/sn32f2xx.c` from the same commit.
The interrupt-visibility regression test applies the production driver patch
and compiles its actual flush function with the real Full rendering code.
Firmware preparation verifies that this fixture matches the pinned Git source.

They retain their upstream GPL-2.0-or-later notices. The native benchmark compiles this actual HSV → RGB implementation with `USE_CIE1931_CURVE`, matching the firmware build. The small `tests/stubs` headers replace host-incompatible platform attributes only. These files are used by host tests/benchmarks, not as a second firmware implementation.
