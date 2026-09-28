# 4Unity Speed / Jump

Run 4UnitySpeedJump.exe and accept the Windows administrator prompt.
Only SPEED and JUMP feature toggles are shown. Both start OFF.

SPEED pins int32 23452 every 25ms. OFF only stops the writer.
JUMP validates and NOPs the resolved writer, pins float20.0, and restores the original instruction on OFF/normal close. The writer is process-wide; its patch is not limited to the local character even though the float pin is local.

profiles/<SHA256>.json contains build data only. Session owner/P are resolved anew and cached. Known-profile buttons perform no code or heap scan. Multiple valid owner/player chains, unknown code changes, inaccessible memory and ambiguous signatures fail closed.

logs/movement.log contains connection, validation and feature changes. recovery.json is a separate crash-recovery ownership journal: PID + creation time + SHA + code RVA + original protection. It contains no owner/P heap pointers. Recovery restores only the same session's journaled patch. An unrelated eight-NOP modification is rejected.

Unknown builds use four contextual signatures plus RTTI and structural validation. A structural match is not proof that a future build has identical gameplay behavior. Compiler/register/layout changes can cause rejection. No future-build compatibility guarantee.

Validation: current binary verified; Release x64 standalone publish; 20 self-tests passed; UI preview inspected. Live read-only probe in the agent's unelevated session returned OpenProcess error5. This build has not been toggled against the running game by the agent.

If another utility/CE currently NOPs the jump writer, restore its original instruction before connecting; this application will not claim that third-party patch.
