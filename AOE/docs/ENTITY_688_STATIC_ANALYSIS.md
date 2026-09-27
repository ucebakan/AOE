# Entity +0x688 Static Analysis

This report covers the known `TClient.exe` SHA-256 `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`. Absolute addresses use the reference base `0x7FF69D570000`.

## Vector layout proof

The entity constructor initializes three adjacent qwords to zero:

```text
TClient.exe+8507C4 | 0x7FF69DDC07C4  mov [rdi+688h],rsi
TClient.exe+8507CB | 0x7FF69DDC07CB  mov [rdi+690h],rsi
TClient.exe+8507D2 | 0x7FF69DDC07D2  mov [rdi+698h],rsi
```

The reset path reads begin, compares it with end, and assigns end=begin:

```text
TClient.exe+850C38 | 0x7FF69DDC0C38  mov rax,[rdi+688h]
TClient.exe+850C3F | 0x7FF69DDC0C3F  cmp rax,[rdi+690h]
TClient.exe+850C48 | 0x7FF69DDC0C48  mov [rdi+690h],rax
```

Append sites compare `[vector+8]` with `[vector+0x10]`, store an eight-byte element at the old end, and advance end by eight. This proves:

- `entity+0x688`: begin
- `entity+0x690`: end
- `entity+0x698`: capacity
- element stride: 8 bytes

`TClient.exe+851D3E | 0x7FF69DDC1D3E` forms `&entity[0x688]`; `TClient.exe+851D45 | 0x7FF69DDC1D45` calls storage destructor `TClient.exe+1CBE0 | 0x7FF69D58CBE0`. Clear/destruction also passes the vector to the element cleanup routine at `TClient.exe+85193E | 0x7FF69DDC193E` and `TClient.exe+859418 | 0x7FF69DDC9418`, both reaching `TClient.exe+A0A440 | 0x7FF69DF7A440`. No whole-vector swap was identified. Removal paths call tail-move helper `TClient.exe+C824D0 | 0x7FF69E1F24D0` and decrement end.

## Builder 1: requested-template expansion

`TClient.exe+865AD0 | 0x7FF69DDD5AD0` receives:

- RCX: owner entity
- RDX: destination vector address
- R8D: requested ID
- R9D and stack argument 5: mode/state inputs

The initial `0x0209` path supplies `&entity[0x688]` at `TClient.exe+7D0CBC | 0x7FF69DD40CBC` and calls this builder at `TClient.exe+7D0CDB | 0x7FF69DD40CDB`. R8D comes from the looked-up operation/template record at `+0x88`.

The normal branch calls `TClient.exe+A61040 | 0x7FF69DFD1040` for the requested ID and iterates the owner’s tree rooted at `entity+0x258`. Its local admission condition is:

```text
TClient.exe+865BF0 | 0x7FF69DDD5BF0  mov rcx,[node+28h]
TClient.exe+865BF4 | 0x7FF69DDD5BF4  mov eax,[lookupResult+4]
TClient.exe+865BF8 | 0x7FF69DDD5BF8  cmp [rcx+24h],eax
```

Only equal IDs take the descriptor-construction branch. Requested ID `0x6F5E` has a special branch that requires `[nodePayload+0x24] == 0x6F5E`. A fallback branch is used when `[lookupResult+4] == 0` or a builder gate selects it; it synthesizes one descriptor rather than accepting an actor pointer.

For each accepted source record, the function allocates and constructs a 0x238-byte runtime object through `TClient.exe+871630 | 0x7FF69DDE1630`, allocates a 0x18-byte descriptor, prepares metadata, generates descriptor dword `+0x08` through `TClient.exe+A22510 | 0x7FF69DF92510`, and appends the descriptor pointer.

The three direct end-pointer increments are:

- normal match: store at `TClient.exe+865D54 | 0x7FF69DDD5D54`, advance end at `TClient.exe+865D57 | 0x7FF69DDD5D57`;
- special `0x6F5E` match: store at `TClient.exe+865EB4 | 0x7FF69DDD5EB4`, advance at `TClient.exe+865EB7 | 0x7FF69DDD5EB7`;
- synthesized fallback: store at `TClient.exe+866053 | 0x7FF69DDD6053`, advance at `TClient.exe+866056 | 0x7FF69DDD6056`.

When end equals capacity, all three paths call the vector-growth helper `TClient.exe+1B750 | 0x7FF69D58B750` from `TClient.exe+865D66 | 0x7FF69DDD5D66`, `TClient.exe+865EC6 | 0x7FF69DDD5EC6`, or `TClient.exe+866065 | 0x7FF69DDD6065`. The helper installs new begin/end/capacity values.

The same shared builder is used by many general entity/effect update callers. One additional representative is `TClient.exe+864669 | 0x7FF69DDD4669`, which forms `&entity[0x688]` and calls the builder at `TClient.exe+864688 | 0x7FF69DDD4688`. `TClient.exe+7820C2 | 0x7FF69DCF20C2` first scans the existing vector for a duplicate metadata ID; if absent, `TClient.exe+7820F8 | 0x7FF69DCF20F8` passes the vector to the builder at `TClient.exe+78210D | 0x7FF69DCF210D`. These callers show that the routine is general state construction rather than an AOE-only target collector.

## Builder 2: keyed descriptor construction

`TClient.exe+865460 | 0x7FF69DDD5460` looks up R8D in a global keyed tree. If the key is found, it constructs a 0x238-byte runtime object, a 0x18-byte descriptor, and 0x48-byte metadata. It copies two caller-provided 12-byte records into the metadata but does not subtract, square, normalize, or compare their components.

It stores the descriptor pointer at `TClient.exe+865614 | 0x7FF69DDD5614` and advances destination end at `TClient.exe+865617 | 0x7FF69DDD5617`; the full-capacity path calls `TClient.exe+1B750 | 0x7FF69D58B750` at `TClient.exe+865626 | 0x7FF69DDD5626`.

Two proven callers pass `&entity[0x688]`:

- `TClient.exe+872A82 | 0x7FF69DDE2A82`, call at `TClient.exe+872AAD | 0x7FF69DDE2AAD`;
- `TClient.exe+8731B0 | 0x7FF69DDE31B0`, call at `TClient.exe+8731DF | 0x7FF69DDE31DF`.

Both use key `0x1128B245` and R9B=1. These are general entity-update paths, not the proven initial `0x0209` preparation path.

## Builder 3: source-record scheduling/deduplication

`TClient.exe+873F33 | 0x7FF69DDE3F33` supplies `&entity[0x688]` as R8, `&entity[0x258]` as RDX, and `&entity[0x390]` as R9, then calls `TClient.exe+A07020 | 0x7FF69DF77020` at `TClient.exe+873F4F | 0x7FF69DDE3F4F`.

For each source record, the locally visible conditions are:

- source, destination, and deduplication tree are non-null;
- source collection count is nonzero;
- `[sourceRecord+0x28]` is not greater than a converted caller input at `TClient.exe+A070E0 | 0x7FF69DF770E0`;
- if `[sourceRecord+0x24]` is nonzero, it equals a caller-supplied dword at `TClient.exe+A070EA | 0x7FF69DF770EA`;
- source key `[node+0x20]` is absent from the R9 tree, checked from `TClient.exe+A07103 | 0x7FF69DF77103` through `TClient.exe+A07138 | 0x7FF69DF77138`.

The builder then allocates a 0x230-byte runtime object and a 0x18-byte descriptor. `TClient.exe+A07191 | 0x7FF69DF77191` stores the source-record pointer at descriptor `+0x00`. `TClient.exe+A07205 | 0x7FF69DF77205` calls generic pointer-vector insertion helper `TClient.exe+A5190 | 0x7FF69D615190`. Its non-growing path copies the descriptor pointer at `TClient.exe+A51AC | 0x7FF69D6151AC` and advances end at `TClient.exe+A51B2 | 0x7FF69D6151B2`.

## Removal, clear, and consumers

`TClient.exe+858BC0 | 0x7FF69DDC8BC0` resolves a word ID, iterates the entity vector, and compares descriptor metadata `+0x24` with lookup fields `+0x84/+0x88` at `TClient.exe+858C49 | 0x7FF69DDC8C49`. A second condition tests a nested metadata field at `+0x60`. Matching descriptors are cleaned up, the tail is moved, and `entity+0x690` is decremented at `TClient.exe+858D01 | 0x7FF69DDC8D01`.

`TClient.exe+859280 | 0x7FF69DDC9280` removes descriptors by a supplied dword ID. It compares `[[E]+0x24]` at `TClient.exe+8592D0 | 0x7FF69DDC92D0`, frees the descriptor and owned metadata where required, moves the tail, and decrements end at `TClient.exe+8592FF | 0x7FF69DDC92FF` or `TClient.exe+859365 | 0x7FF69DDC9365`. `TClient.exe+780DBF | 0x7FF69DCF0DBF` is a proven caller passing `&entity[0x688]`.

Two consumers expose the entry shape. At `TClient.exe+85EBF8 | 0x7FF69DDCEBF8`, the loop loads slot value E at `TClient.exe+85EC10 | 0x7FF69DDCEC10`, then loads `[E+0x10]` at `TClient.exe+85EC13 | 0x7FF69DDCEC13` and clears byte `[object+0x210]` at `TClient.exe+85EC1C | 0x7FF69DDCEC1C`. The paired loop starts at `TClient.exe+86ACDC | 0x7FF69DDDACDC`, loads E at `TClient.exe+86ACF0 | 0x7FF69DDDACF0`, loads `[E+0x10]` at `TClient.exe+86ACF3 | 0x7FF69DDDACF3`, and sets `[object+0x210]` at `TClient.exe+86ACFC | 0x7FF69DDDACFC`.

`TClient.exe+854308 | 0x7FF69DDC4308` and `TClient.exe+86753D | 0x7FF69DDD753D` pass the nonempty vector to the entity’s virtual method at vtable offset `+0x98`; they do not append locally.

## Entry semantics

For a vector slot value E, the statically proven descriptor layout is:

| Descriptor field | Proven use |
|---|---|
| `E+0x00` | metadata/source-record pointer; some branches allocate/copy 0x48 bytes, while another branch borrows the source record |
| `E+0x08` | generated dword from `TClient.exe+A22510 | 0x7FF69DF92510` |
| `E+0x0C/+0x0D` | state/ownership bytes; `+0x0D` controls whether metadata is freed |
| `E+0x10` | separately allocated 0x230/0x238-byte runtime object |

No consumer in the verified paths converts E or `[E+0x10]` into a known actor pointer. Static construction disproves the hypothesis that E is directly a `CClientMonster` pointer. The narrow evidence supports only “descriptor plus runtime object”; stronger names such as target, monster, actor, or damage recipient are not justified.

## Range and entity-type predicates

No `entity+0x688` insertion path contains coordinate subtraction, squared distance, square root, radius comparison, horizontal-distance math, a virtual position getter, or a call whose inputs form an identifiable candidate-entity/radius test.

No insertion path checks a candidate actor’s vtable/type, faction, alive/dead state, attackable flag, self identity, player class, NPC class, or pet class. The builders do not receive candidate actor pointers. Their proven admission predicates are template/key equality, optional record-field equality, record-age/order comparison, and deduplication.

Therefore normal AOE range selection and loaded-monster type selection do not occur at `entity+0x688` insertion. If the counts are indirectly caused by target selection, that decision is upstream of the source collections consumed by these builders. This analysis does not assign an unproven range predicate to that upstream code.

## Relationship to entity+0x660

The same constructor independently zeros the `entity+0x660/+0x668/+0x670` triple at `TClient.exe+85077F | 0x7FF69DDC077F` through `TClient.exe+85078D | 0x7FF69DDC078D`. `TClient.exe+8595C0 | 0x7FF69DDC95C0` iterates that vector with eight-byte slots, frees each pointed-to eight-byte allocation, and resets end=begin at `TClient.exe+85963E | 0x7FF69DDC963E`.

On the initial path, `TClient.exe+7D0CDB | 0x7FF69DD40CDB` builds `entity+0x688`; later, `TClient.exe+7D0D6C | 0x7FF69DD40D6C` independently forms `entity+0x660` and passes it through `TClient.exe+7F06E0 | 0x7FF69DD606E0` to `TClient.exe+7BB160 | 0x7FF69DD2B160`. The shared worker treats each `+0x660` element as an eight-byte record with dword `[entry]` and byte `[entry+4]`, then calls `TClient.exe+7B9BD0 | 0x7FF69DD29BD0` to resolve it.

No verified path copies or transforms `entity+0x688` into `entity+0x660`, and the shared worker has no fixed-offset access to the owner’s `+0x688` vector. The two vectors have different element ownership and different consumers. This explains why `+0x660` can remain at one element while `+0x688` changes 1/3/4: they are independent collections created for different stages. Static evidence does not justify calling `+0x660` a primary target or `+0x688` secondary targets.

## Runtime writer trace decision

A hardware write-watch mode was not needed. Static analysis identified every local end-pointer increment used by the three proven builders, both removal implementations, reset, cleanup, and vector growth. Manager source and executable behavior remain unchanged at version `1.1.2-research`.
