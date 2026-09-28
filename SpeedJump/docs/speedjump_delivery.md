# SpeedJump delivery

Dedicated companion source: tools/SpeedJump/SpeedJump.csproj. UI style and guarded thread-patch technique adapted from InvisibleAggro; independent feature engine and SHA profiles implemented for Speed/Jump.

Output: dist/4UnitySpeedJump/4UnitySpeedJump.exe (self-contained Windows x64, requireAdministrator).
Profile: dist/4UnitySpeedJump/profiles/9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28.json.
Four current-build unique signatures: dist/4UnitySpeedJump/signatures.json.
Jump context RVA845B75 (patch+26=845B8F), pair compare86F585, pair store86F690, owner read79FB31. Offsets extracted from encoded disp32 operands, compare/store coherence checked, class vtables derived from RTTI. Owner call target must match CTClientChar slot4B8; setter sites must be near slotB0 target. This is a narrow structural resolver, not a general decompiler or future-build causal proof.

Profile validation on same SHA performs fixed reads/matches, RTTI and PE metadata checks, no AOB scan. Heap owner scan occurs only for a new connection; validated changing local-P pointer can be refreshed without rescanning. Feature clicks use cached session data. Live writes each revalidate owner/P. Normal close stops and joins both writers before restoring journal-owned code. Unknown code modifications fail closed. Recovery journal also preserves original memory protection.

Self-tests: logs/speedjump_selftest.json. PASS,20 checks; actual profile/RTTI/signature validation plus simulated concurrent pins, cancellation, process exit and rollback. No game writes during tests. UI: logs/speedjump_ui.png, exactly two feature buttons. Read-only live probe: OpenProcess error5; live execution remains unverified in this session.

The new PROVEN gameplay status in speed_checkpoint.md is explicitly attributed to user-reported repeated CE tests, not a new agent runtime observation. Historical helper mapping remains unresolved.
