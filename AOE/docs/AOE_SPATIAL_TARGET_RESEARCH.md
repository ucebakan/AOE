# AOE Spatial Target Research

## Scope and build

Version 1.1.10-research added read-only spatial capture to the existing A/B/C/D Target Differential Inspector for TClient SHA-256 `FB13C1257A401BB4E866247921A79D7C044AFC69CDAED9869C945940DD60B8D7`. The later gameplay clarification establishes that the same aggroed Wolf followed the moving player, so the old target-versus-caster proximity experiment is confounded. It does not change Initial Nx, packets, actors, coordinates, or game state.

## Local-player source

The initial `0x0209` shared-call contract supplies the owner/local actor in RDX. Its identity is checked at actor `+0x768` and category `+0x7E1`; the expected local player category is 1. The current build independently establishes the object path `context+0x2710 -> local actor` and coordinate storage `actor+0xB0/+0xB4/+0xB8`:

- `TClient.exe+79FA19` loads `[context+0x2710]`; `+79FA20` writes X to `[actor+0xB0]` and `+79FA4F` writes Z to `[actor+0xB8]`.
- `TClient.exe+8076C7` loads `[context+0x2710]`; `+8076CE`, `+8076E4`, and `+8076F3` write X/Y/Z to `[actor+0xB0/+0xB4/+0xB8]`.
- Existing PlayerXYZ research had already runtime-verified the same float32 XYZ representation and homogeneous W at `+0xBC` on prior exact builds. This Manager validates finite values, a bounded world range, and W within 0.05 of 1.0 before accepting a position.

At the exact initial `0x0209` breakpoint, the Inspector reads the operation record, RDX owner identity, and RDX owner XYZ together. This initial-event snapshot is authoritative. MARK-time player XYZ is best effort from the most recent verified owner pointer (or the same-session live-validation initial-call owner); it may be unavailable for the first marker.

## Selected-target source

No profile-verified global current-selection pointer was found. `owner+0x660` remains closed as caster/reference evidence and `owner+0x688` remains an effect/configuration descriptor.

Operation-record ID/type probes are retained for legacy export only. They cannot promote a selected-target conclusion, even when three A/B/C values resolve to category-2 actors. Selected target and selected-target XYZ remain `UNKNOWN` in this Inspector; selection provenance is now collected separately from the `CTClientGame+0x2318` static candidate.

A promoted category-2 actor does not assume the player `+0xB0` layout. Current-build accessors `TClient.exe+781D80/+781DB0/+781DE0` resolve an optional movement link at `actor+0x1390`, accept it only when `link+0x1380` points back to the actor, otherwise retain the actor as the base, and read X/Y/Z at resolved base `+0x70/+0x74/+0x78`. The Inspector reproduces that accessor logic with reads only and applies finite/range validation. Confidence is high from static accessor semantics, while a new live monster-motion correlation remains pending. The operation-record ID/type field meaning is established only by the A/B/C differential rule. If no candidate passes, no target coordinates are synthesized.

## Operation spatial candidate and comparisons

Operation record dwords at `+0x00`, `+0x04`, and `+0x08` are preserved bit-for-bit and decoded with `memcpy` as IEEE-754 float32 X/Y/Z. They remain named `operationSpatialCandidate`. Non-finite values or absolute components above 1,000,000 are invalid.

For each initial event, the Inspector computes 3D Euclidean distance to the player. Operation-record actor candidates are not accepted as selected-target provenance. Version 1.1.12 can additionally snapshot the independent profiled `context+0x2318` candidate at MARK and initial-`0x0209` time. Manual live validation returned NULL in all three tested states, so it remains an UNPROVEN target-state candidate; category-2 and typed-tree pointer validation can identify a captured actor but cannot by itself establish current-selection semantics. It also reports X/Z plane distance because prior coordinate research established Y as height; exported axes remain explicitly X/Y/Z. The documented match tolerance is 0.75 world units, but distance is never used to establish actor identity.

Classification remains `INSUFFICIENT_DATA` without an independent selected-target source. The Wolf-following captures cannot distinguish caster position, selected-target position, a point between them, cast origin, or cast destination. A single capture can never produce a provenance conclusion. Player distance is retained as measurement only and is not identity evidence.

## Candidate +0x24

Operation `+0x24` is exported only as `candidateField24`. Its observed values `0x91`, `0x9D`, and `0xA9` changed while the same Wolf remained selected, so it is not supported as a stable selected-monster actor ID.

## JSON and UI

JSON keeps all previous fields and adds `operationSpatialCandidate`, `playerSpatial`, spatial data on actor probes, grouped `spatialCaptures` with separate `markerTime` and `initial0209Time`, distances, `candidateField24`, and `spatialConclusion` including source, tolerance, and reason.

The Research page adds compact A/B/C rows for operation XYZ, event-time player XYZ, conditionally proven monster XYZ, distances, and the cross-capture conclusion. The existing A/B/C/D marker lifecycle is unchanged.

## Remaining unknowns

A direct profile-verified UI/current-selection pointer is still unknown. MARK-time selected-target capture therefore remains unavailable. Player and monster storage offsets are not treated as shared. Monster position confidence is tied to the current-build accessor semantics and a typed, category-2 actor that passes the A/B/C differential proof; this build has not yet supplied a new live monster-motion validation capture. The next live export should be treated as evidence gathering, not as multikill behavior.

