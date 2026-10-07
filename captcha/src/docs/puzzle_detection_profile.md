# Read-only puzzle visibility profile

Analyzed client: C:/Games/4Unity/TClient.exe, 16,549,888 bytes.

SHA-256: 4dc9c526a895a10113c4cf23f2d199bff283d2c7ce75f949dc49cef15d27d622.

Static inspection found the puzzle strings and references in the dialog update routine. RTTI identifies CTSecuritySystemDlg. Addresses below are RVAs or object-relative offsets, not fixed runtime addresses.

| Item | Location | Evidence/interpretation |
|---|---|---|
| Class vtable | RVA 0xDC94B8 | RTTI COL RVA 0xE540C0 |
| Show/hide override | Vtable slot 0x30 → RVA 0x384080 | Tests mode and visibility/challenge state |
| Render override | Vtable slot 0x160 → RVA 0x3839D0 | Security dialog rendering |
| Matching display update | RVA 0x3840A0 | References title, target, stage and attempts |
| Puzzle mode | Object byte +0x328 | Display update sets 0xFA for matching puzzle |
| Visibility | Object DWORD +0x19C | Base UI methods write/read 0 or 1 |
| Title control pointer | Object pointer +0x288 | Additional candidate validation |

The observer requires visibility 1 and mode 0xFA for an active matching puzzle. Other modes are not treated as the puzzle. It opens the selected process with 0x410 (query information and VM read); it requests no write, VM operation, debug or thread creation access. Runtime addresses use the loaded module base.

Only committed readable private allocations are scanned, with a 256 MiB / six-second bound and a 12,000-region cap. Multiple candidates found within that budget are rejected; uniqueness outside that range is not established. Loaded vtable method addresses and a title child object's image-resident vtable are validated. Snapshot reads revalidate identity. Unsupported or invalid states fall back to visual detection. Answer selection remains verified through OCR.

Static evidence: workspace logs/puzzle_static/strings.json, refs.json and vtable.json, produced by tools/puzzle_static_probe.py, puzzle_string_refs.py and puzzle_vtable_probe.py. Packaged verification includes these reports.

Live limitation: the read-only probe of the elevated client (PID 900 at probe time) returned Win32 5 access denied. No live UI object or open/closed transition was verified. The packaged EXE requests administrator approval. Synthetic byte parser tests and automatic four-stage interaction with the helper's fixture are not live memory validation.
