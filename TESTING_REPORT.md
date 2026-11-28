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

## Version 0.6 - Wind Load (NOT yet tested on user PC)
| Item | Status |
|---|---|
| Eq 6.2.17 (qz), 6.2.19 (p = q G Cp), Cp of Figure 6.2.6, GCpi of Figure 6.2.5 | Read from PDF page images (p.559-561, 573-575) |
| Table 6.2.8 (74 locations), 6.2.9, 6.2.10, 6.2.11 (formula), 6.2.12 | Typed/parsed from PDF text; counts and sample values tested in GitHub build |
| Kz formula vs table values (A, B, C) | Automatic test in GitHub build |
| Reference case (Dhaka V=65.7, Exposure B, 6 stories to 18 m, 20 x 30 m plan): base shear X = 783.5 kN | Automatic test in GitHub build (matches independent Python calculation) |
| Story forces sent to ETABS | NOT implemented (CSV / report export only) |
| Roof pressures, torsional wind cases (Fig 6.2.9), parapets, flexible buildings (Eq 6.2.8-6.2.16), partially enclosed buildings, topographic factor Kzt | NOT implemented |

## Version 0.7 - Model Checker (NOT yet tested on user PC)
| Item | Status |
|---|---|
| Project data checks (name, location, soil, occupancy, stories) | Logic simple; UI untested |
| ETABS checks: analysis status, object counts, stories, diaphragms, support restraints, load patterns named in this tool, BNBC combinations | UNTESTED - needs user ETABS 22 |
| Result words | PASS / WARN / FAIL / INCOMPLETE / NOT CHECKED. A check that cannot be read from ETABS is never PASS |
| Compile note | ModelChecker.cs (CheckRow class) was found in the working folder, not written by the assistant; it is used as is |

## Version 0.8 - Report Generator (NOT yet tested on user PC)
| Item | Status |
|---|---|
| HTML report: text escaping, "Not calculated" / "MISSING" for absent data, no PASS without data | Automatic test in GitHub build |
| Excel (.xlsx) file creation: package opens, 8 worksheets present | Automatic test in GitHub build (structure only) |
| Excel file opens correctly in Microsoft Excel 2010 on the user PC | UNTESTED |
| HTML opens in browser and prints to PDF | UNTESTED |
| Direct PDF file creation | NOT implemented (use browser Print > Save as PDF) |

## Version 0.8 - Report Generator (NOT yet tested on user PC)
| Item | Status |
|---|---|
| Excel (.xlsx) writer, HTML writer | Automatic test in GitHub build: workbook structure, number cells, text escaping |
| Opening the .xlsx in Excel 2010 on the user PC | UNTESTED (hand-written minimal workbook) |
| Browser print to PDF | Uses the browser, UNTESTED |
| Sections not calculated | Shown as empty / NOT ENTERED, never filled in |

## Version 0.9 - Compliance Checker (NOT yet tested on user PC)
| Item | Status |
|---|---|
| Status classification rules (FAIL / INCOMPLETE / WARN / PASS) | Automatic test in GitHub build |
| Collection of seismic, wind, combination, live load and model-check results | UI untested |
| What it checks | Only the tool's own results and the limits it knows. It is NOT a full BNBC compliance review and not a design approval |

## Version 0.9.1 - build fix
| Item | Status |
|---|---|
| GitHub Build #6 and #7 failed: Model Checker and Report pages/handlers were defined twice and a type `ReportData` was missing | Cause: a second, unrelated copy of those two modules was present in the assistant's working folder and was not noticed before adding the new code. The duplicate copy was removed; the version tested in the GitHub build is the one described above |
| Compile status | NOT confirmed until the next GitHub Build turns green. More errors may appear that the compiler could not report while the duplicates existed |

## Version 0.9.2 - ETABS API Check
| Item | Status |
|---|---|
| Lists the functions that the user's own ETABSv1.dll has (cAutoSeismic, cAutoWind, cLoadPatterns, point-load functions, cDiaphragm) | UNTESTED - needs user ETABS 22 |
| Why it exists | A public developer test report (Nov 2026) says ETABS 23's cAutoSeismic has no SetUserCoefficient. Whether ETABS 22.5 has it is unknown, so automatic transfer of the seismic coefficient is NOT built until this list is read |

## Version 0.10 - Apply story forces as joint forces
| Item | Status |
|---|---|
| ETABS 22.5.0 function list (read from the user's own ETABS) | VERIFIED: cAutoSeismic has only ASCE 7-16 and IBC 2006 setters, NO SetUserCoefficient; cAutoWind is empty; cPointObj.SetLoadForce exists |
| Consequence | A BNBC coefficient cannot be set in ETABS automatically. Story forces are applied as joint forces instead |
| Assigning a story force to one selected joint (cPointObj.SetLoadForce) | UNTESTED - needs user ETABS 22 |
| Story forces of the seismic run are exposed correctly (sum equals base shear) | Automatic test in GitHub build |
| Accidental torsion moments, wind torsional cases | NOT implemented |

## Not generated / not implemented
- Flood and tidal-surge combinations (Fa)
- Ev inside E (user supplies E)
- Wind, wall loads, checker, reactions, PDF/Excel
