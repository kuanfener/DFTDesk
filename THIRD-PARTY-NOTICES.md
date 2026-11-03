# Third-party licenses and acknowledgements

DFTDesk is licensed under MIT; see [LICENSE](LICENSE).

## Filee — upstream MIT-licensed components

DFTDesk incorporates source code adapted from [Filee by KnifeLemon](https://github.com/KnifeLemon/Filee), including parts of its Avalonia interface, donut menu, gesture handling and Windows-platform utilities.

**Original copyright notice preserved:**
Copyright (c) 2026 Myeonggu Kim (KnifeLemon) and Filee contributors.

The original MIT license is included within this repository's `LICENSE`; the corresponding inherited source is separated into `third_party/Filee/`. The inherited code continues to use Filee namespace identifiers for provenance and compatibility.

## Runtime dependencies

- Avalonia UI — MIT — https://github.com/AvaloniaUI/Avalonia
- SharpHook — MIT; its native libuiohook has LGPL-3.0 and relevant linking provisions — https://github.com/TolikPylypchuk/SharpHook
- Microsoft.Extensions.Logging.Abstractions — MIT — https://github.com/dotnet/runtime
- .NET runtime — Microsoft licenses — https://dotnet.microsoft.com/

## Optional external integration

- ASE (Atomic Simulation Environment) — LGPL-2.1-or-later — https://ase-lib.org/
- Python, NumPy and ASE are **not redistributed with DFTDesk**. Users choose their independently installed Python environment.

See the respective project licenses for complete rights, conditions and notices.
