# Initial Nx experiment lifecycle — 1.1.7-research

Live validation and replay experiments have different lifetimes. Live validation belongs to the verified TClient process identity. An Initial Nx experiment belongs to one configured cast.

The `Initial Calls` control now stores `configuredInitialCalls`, which controls the next experiment. Once an experiment is armed, its `targetInitialCalls` is frozen. Editing the control never mutates that active value.

After a completed or aborted experiment, changing `Initial Calls` archives the terminal result, resets only the experiment state, restores the same session's validation breakpoints, and prepares the new target. With Auto Arm enabled and same-session validation still passed, the new experiment arms automatically for the next cast. With Auto Arm disabled, it remains `READY / NOT ARMED`.

An armed experiment that has not seen its first `0x0209` can be safely replaced immediately. If replay has started, the new value is stored as the pending next target. The running experiment finishes with its original frozen target; breakpoint reset and creation of the pending experiment happen only after completion.

The Play page and diagnostics report three distinct views:

- **CONFIGURATION:** the current `Initial Calls` value.
- **LAST Nx EXPERIMENT:** target, observed calls, redirects, and terminal result.
- **NEXT Nx EXPERIMENT:** next target and readiness/arming state.

Changing this configuration does not alter the PID, process creation time, SHA/profile identity, live validation evidence, Inspector safety gates, or the replay implementation in `initial_2x.cpp`.
