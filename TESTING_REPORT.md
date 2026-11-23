# Testing Report - MVP 0.1

| Test | Status |
|---|---|
| Source compiles on Windows | UNTESTED by Claude (no Windows/.NET here). GitHub build will verify. |
| Data tables load correctly (Z towns=66, soil spectrum) | Automated in GitHub workflow (self-test) |
| Installer builds | Automated in GitHub workflow |
| Silent install + launch | Automated in GitHub workflow |
| ETABS 22 connection | UNTESTED - needs user's ETABS 22 |
| Load pattern creation / duplicate check | UNTESTED - needs ETABS 22 |
| Table values vs BNBC PDF | Extracted by script from PDF p.618-619; user spot-check recommended |
| Load combinations, wind, wall loads, checker, reactions, PDF/Excel export | NOT IMPLEMENTED yet |
