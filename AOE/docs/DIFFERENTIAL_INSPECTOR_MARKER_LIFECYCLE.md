# Differential Inspector marker lifecycle — 1.1.8-research

The read-only Differential Inspector maintains four independent capture slots: A, B, C, and D. Each slot is `EMPTY`, `ARMED FOR NEXT 0209`, or `CAPTURED`.

Starting the Inspector leaves A ready. Pressing **MARK A** changes only A to armed and disables MARK until the first valid initial `0x0209` is stored. That store changes A to captured and makes **MARK B** available immediately. Periodic `0x020A` events are retained in their separate stream and do not change slot readiness, so the operator does not have to wait for the periodic sequence before marking the next slot.

Only one slot may be armed. **CANCEL CURRENT MARK** removes evidence associated with that still-pending marker, returns its slot to empty, and preserves all previously captured slots. Capturing B, C, or D appends independent marker, register, operation-record, decoded-candidate, and resolved-actor evidence; it does not replace earlier capture data. Comparisons refresh after every stored initial event, and JSON export includes the state and evidence for every completed slot.

Use sequential normal casts. Marking the next slot may happen as soon as the previous initial event is captured, but two simultaneous casts can make ownership of the diagnostic periodic `0x020A` stream ambiguous. Initial `0x0209` capture ownership remains defined by the single armed slot, and periodic events are excluded from A/B/C operation-record comparisons.

Workflow:

1. Start **DIFFERENTIAL INSPECTOR**.
2. Select Mob A, press **MARK A**, and cast one normal AOE.
3. Wait for `A: CAPTURED` and `B: READY`.
4. Repeat for B and C; D is optional.
5. If a marker was pressed without a cast, use **CANCEL CURRENT MARK** and mark that slot again.
6. Press **STOP**, then **EXPORT JSON**.

Initial Nx remains mutually excluded for the whole active Inspector session. The saved Auto Arm preference is unchanged and can resume eligibility only after Inspector stop under the existing same-session validation rules.
