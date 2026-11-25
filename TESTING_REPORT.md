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
| Creating combinations in ETABS (RespCombo API) | VERIFIED by user screenshot: 13 combinations ULS1..ULS7_2 exist; ULS5_1 = 1.2 Dead + 1.2 SDL + 1.2 WALL + 1 EX + 1 Live (matches Eq.5) |
| Status text after a second Create click said FAILED (code 1) | Cause: existing-name check was unreliable, Add rejected the duplicate name. Fixed in v0.2.1 (checks by reading the combo's case list) |
| Missing-case check | UNTESTED |
| Duplicate-name skip (v0.2.1 fix) | UNTESTED |
| Remaining combos (sign of -EX, W combos, ULS6/7) factor check in ETABS | Only ULS5_1 checked so far |

## Version 0.3 - Floor live loads and Wall loads (NOT yet tested in ETABS)
| Item | Status |
|---|---|
| Live load table (64 rows, BNBC Table 6.2.3, PDF p.536-541) | Typed from PDF text extraction; automatic count/value test in GitHub build; user spot-check recommended |
| Wall unit weights (Table 6.2.1) and line load formula | Automatic formula test in GitHub build |
| Read stories from ETABS | UNTESTED |
| Assign live load to selected slabs (AreaObj.SetLoadUniform) | UNTESTED |
| Assign wall load to selected beams (FrameObj.SetLoadDistributed) | UNTESTED |
| Live load reduction (Sec 2.3.13), concentrated loads, SDL table 6.2.2 | NOT implemented |

## Version 0.4 (NOT yet tested in ETABS)
| Item | Status |
|---|---|
| Occupancy search box (residential rows renamed so "residential" / "apartment" find them) | Automatic row-count test in GitHub build; UI untested |
| Foundation Reaction Extractor (Results.JointReact for selected joints, CSV export) | UNTESTED - needs analysed model in user ETABS 22 |
| Sign convention note (F3 positive = upward) | From ETABS convention, confirm on a simple model |

## Version 0.5 - Seismic Static Analysis (NOT yet tested on user PC)
| Item | Status |
|---|---|
| Formulas Eq 6.2.34, 6.2.35a-d, 6.2.36, 6.2.37, 6.2.38, 6.2.41, 6.2.42 | Read from PDF page images (p.615-616, 629-632) |
| Tables 6.2.18, 6.2.19 (28 systems), 6.2.20 | Typed from PDF text; counts and sample values tested in GitHub build |
| Numeric test: Dhaka, SC soil, R=5, hn=18 m, 6 stories of 1075 kN: V = 472.27 kN, T = 0.628 s | Automatic in GitHub build (matches independent Python calculation) |
| Reading story elevations from ETABS | UNTESTED |
| Transfer of Sa / k into ETABS | NOT implemented (user enters it by hand) |
| Accidental torsion, drift, shear-wall period (Eq 6.2.39), vertical seismic component | NOT implemented |

## Not generated / not implemented
- Flood and tidal-surge combinations (Fa)
- Ev inside E (user supplies E)
- Wind, wall loads, checker, reactions, PDF/Excel
