# SPDX-License-Identifier: GPL-2.0-or-later
VIA_ENABLE = yes
CONSOLE_ENABLE = no
COMMAND_ENABLE = no
LTO_ENABLE = yes
# Minimal mouse reports for Turbo buttons and bounded anti-AFK movement;
# no mouse-key acceleration engine or extra task.
MOUSE_ENABLE = yes
MOUSE_SHARED_EP = yes
SRC += redblade_rgb.c redblade_controls.c redblade_recorder.c redblade_config.c redblade_store.c redblade_store_flash.c
override LDSCRIPT = $(KEYMAP_PATH)/redblade_store.ld
# Group aligned LTO sections to avoid wasting the small remaining SRAM on padding.
EXTRALDFLAGS += -Wl,--sort-section=alignment

