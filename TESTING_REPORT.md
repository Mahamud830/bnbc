# Testing Report

## Verified on the user's PC (ETABS 22.5.0, MVP 0.1)
- Installer installs and the app launches
- ETABS 22 auto-detect and connect
- Reading the ETABS load pattern list
- Creating a load pattern (WALL) and duplicate skip
- Project save / load
- Z lookup for Dhaka (0.20) and soil SC parameters

## Version 0.2 - Load Combinations (NOT yet tested in ETABS)
| Item | Status |
|---|---|
| Combination formulas vs BNBC 2020 Sec 2.7.2.1 (p.687) and 2.7.3.1 (p.688) | Read from PDF; strength page checked visually |
| Generator logic (13 strength / 15 allowable-stress for the test mapping) | Automatic test in GitHub build |
| Creating combinations in ETABS (RespCombo API) | UNTESTED - needs user ETABS 22 |
| Missing-case check and duplicate-name skip | UNTESTED |

## Not generated / not implemented
- Flood and tidal-surge combinations (Fa)
- Ev inside E (user supplies E)
- Wind, wall loads, checker, reactions, PDF/Excel
