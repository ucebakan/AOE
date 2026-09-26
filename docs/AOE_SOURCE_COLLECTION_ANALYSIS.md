# AOE Source Collection Static Analysis

This report covers `TClient.exe` SHA-256 `CD772F3F7AE7496A674783341A63E257A4902606F8A4E8FE76879DDC0E28970E`. Absolute addresses use the reference module base `0x7FF69D570000`.

The main result is negative but conclusive: the collection consumed by `TClient.exe+865AD0 | 0x7FF69DDD5AD0` is a persistent ordered tree of configuration/effect records. It is not a collection of candidate actors. Its proven producer copies key/payload pairs from a configuration record into the owner object. No candidate-actor enumeration, distance test, or actor-category test exists in this provenance chain.

## Effective `TClient.exe+865AD0 | 0x7FF69DDD5AD0` ABI

The prologue at `TClient.exe+865AD0 | 0x7FF69DDD5AD0` preserves the register arguments as follows:

```text
TClient.exe+865ADF | 0x7FF69DDD5ADF  mov [rsp+20h],r9d
TClient.exe+865AF1 | 0x7FF69DDD5AF1  mov esi,r9d
TClient.exe+865AF4 | 0x7FF69DDD5AF4  mov r15d,r8d
TClient.exe+865AF7 | 0x7FF69DDD5AF7  mov rbp,rdx
TClient.exe+865AFA | 0x7FF69DDD5AFA  mov r14,rcx
```

After the five pushes and 0x50-byte local allocation, the original R9D value is at `[rsp+0x98]` and the first stack argument is at `[rsp+0xA0]`. They are consumed at:

```text
TClient.exe+865D1B | 0x7FF69DDD5D1B  mov eax,[rsp+98h]
TClient.exe+865D22 | 0x7FF69DDD5D22  mov [runtime+210h],al
TClient.exe+865D28 | 0x7FF69DDD5D28  mov eax,[rsp+A0h]
TClient.exe+865D2F | 0x7FF69DDD5D2F  mov [runtime+214h],eax
```

The effective signature is therefore:

```cpp
void BuildDescriptors(
    OwnerObject* owner,             // RCX
    PointerVector* destination,     // RDX; begin/end/capacity
    uint32_t requestedId,           // R8D
    uint32_t stateFlag,             // R9D; low byte stored at runtime+0x210
    uint32_t contextValue);         // [entry RSP+0x28]; stored at runtime+0x214
```

This is an effective data-flow signature, not a recovered source symbol. There is no source-collection argument: the function loads the source tree from `owner+0x258` itself.

## Initial-path arguments and provenance

At `TClient.exe+7D0C30 | 0x7FF69DD40C30`, incoming RCX is preserved as a controller/context pointer in RBP, incoming RDX is preserved as the owner object in RBX, and incoming R8D chooses an owner-relative record:

```text
TClient.exe+7D0C4A | 0x7FF69DD40C4A  mov rbp,rcx
TClient.exe+7D0C60 | 0x7FF69DD40C60  add rsi,rdx       ; owner+0x818 or owner+0x858
TClient.exe+7D0C63 | 0x7FF69DD40C63  mov rbx,rdx       ; owner
TClient.exe+7D0C66 | 0x7FF69DD40C66  movzx ecx,word [rsi+0Ch]
TClient.exe+7D0C6A | 0x7FF69DD40C6A  call TClient.exe+A61090 | 0x7FF69DFD1090
```

The call at `TClient.exe+7D0CDB | 0x7FF69DD40CDB` receives:

| Argument | Value at the call | Provenance and role |
|---|---|---|
| RCX | RBX | owner/entity-state object, restored at `TClient.exe+7D0CD8 | 0x7FF69DD40CD8` |
| RDX | `RBX+0x688` | destination pointer vector, formed at `TClient.exe+7D0CBC | 0x7FF69DD40CBC` |
| R8D | `[RDI+0x88]` | requested ID from the record returned by `TClient.exe+A61090 | 0x7FF69DFD1090`, loaded at `TClient.exe+7D0CC3 | 0x7FF69DD40CC3` |
| R9D | 1 | state flag, set at `TClient.exe+7D0CCD | 0x7FF69DD40CCD` |
| stack argument 5 | 0 | context value, stored at `TClient.exe+7D0CD3 | 0x7FF69DD40CD3` |

The incoming R8D to `TClient.exe+7D0C30 | 0x7FF69DD40C30` is only the selector for `owner+0x818` versus `owner+0x858`; it is not passed through as a range or count.

## Source collection layout

`owner+0x258` is a node-based ordered tree:

| Owner field | Meaning |
|---|---|
| `owner+0x258` | pointer to the header/sentinel node |
| `owner+0x260` | number of tree elements |

The constructor `TClient.exe+A01D10 | 0x7FF69DF71D10` creates it. `TClient.exe+A01E57 | 0x7FF69DF71E57` forms `owner+0x258`, `TClient.exe+A01E60 | 0x7FF69DF71E60` and `TClient.exe+A01E64 | 0x7FF69DF71E64` zero the header pointer and count, and `TClient.exe+A01E68 | 0x7FF69DF71E68` allocates the 0x30-byte header. The header self-links are written at `TClient.exe+A01E72 | 0x7FF69DF71E72`, its sentinel flags at `TClient.exe+A01E7D | 0x7FF69DF71E7D`, and its pointer is installed at `TClient.exe+A01E83 | 0x7FF69DF71E83`.

Each allocated 0x30-byte node has this proven shape:

| Node field | Proven use |
|---|---|
| `node+0x00` | left/next tree link |
| `node+0x08` | parent tree link |
| `node+0x10` | right tree link |
| `node+0x18/+0x19` | color/sentinel state bytes |
| `node+0x20` | dword key |
| `node+0x28` | source-record pointer S |

There is no contiguous source stride. Source begin is `*header`, loaded at `TClient.exe+865BCE | 0x7FF69DDD5BCE`; source end is the header/sentinel pointer loaded from `owner+0x258` at `TClient.exe+865BC7 | 0x7FF69DDD5BC7`. Empty detection compares those two pointers at `TClient.exe+865BDE | 0x7FF69DDD5BDE`. Raw source count is `[owner+0x260]`, although `TClient.exe+865AD0 | 0x7FF69DDD5AD0` traverses until the sentinel rather than reading the count.

Tree successor traversal runs from `TClient.exe+865D6B | 0x7FF69DDD5D6B` through `TClient.exe+865DBF | 0x7FF69DDD5DBF`. It follows `node+0x10`, `node+0x08`, and `node+0x00`, using byte `node+0x19` to recognize the sentinel, then compares the next node with `[owner+0x258]`.

Destruction confirms ownership and node size. The destructor path loads the header at `TClient.exe+A02C2E | 0x7FF69DF72C2E`, destroys 0x30-byte nodes in the loop beginning at `TClient.exe+A02C40 | 0x7FF69DF72C40`, and frees the header at `TClient.exe+A02C72 | 0x7FF69DF72C72`.

## Source-record fields consumed by `TClient.exe+865AD0 | 0x7FF69DDD5AD0`

For each node, `TClient.exe+865BF0 | 0x7FF69DDD5BF0` loads `S=[node+0x28]`. The normal admission predicate is:

```text
TClient.exe+865BF4 | 0x7FF69DDD5BF4  mov eax,[lookupResult+4]
TClient.exe+865BF8 | 0x7FF69DDD5BF8  cmp [S+24h],eax
```

The lookup result comes from `TClient.exe+A61040 | 0x7FF69DFD1040`, called at `TClient.exe+865BA4 | 0x7FF69DDD5BA4` with the requested ID. Requested ID `0x6F5E` selects a special loop and compares `[S+0x24]` with `0x6F5E` at `TClient.exe+865DE6 | 0x7FF69DDD5DE6`.

On a normal match the builder copies these fields from S into newly allocated 0x48-byte metadata:

| Source field | Destination field |
|---|---|
| `S+0x0C` qword and `S+0x14` dword | `metadata+0x0C/+0x14` |
| `S+0x18` qword and `S+0x20` dword | `metadata+0x18/+0x20` |
| `S+0x28` dword | `metadata+0x28` |
| `S+0x2C` dword | `metadata+0x2C` |
| `S+0x30` dword | `metadata+0x30` |
| `S+0x34` byte | `metadata+0x34` |

It also assigns `[lookupResult+0x08]` to `metadata+0x00` and the requested ID to `metadata+0x24`. A new 0x238-byte runtime object is constructed through `TClient.exe+871630 | 0x7FF69DDE1630`. The descriptor points to the metadata at `+0x00`, stores the generated dword from `TClient.exe+A22510 | 0x7FF69DF92510` at `+0x08`, sets ownership/state bytes at `+0x0C/+0x0D`, and points to the runtime object at `+0x10`.

The special `0x6F5E` branch borrows S directly as descriptor field `+0x00` at `TClient.exe+865E5A | 0x7FF69DDD5E5A`; it does not make the normal metadata copy. The fallback at `TClient.exe+865F31 | 0x7FF69DDD5F31` synthesizes one metadata/descriptor pair and does not iterate source records.

## Proven source producer

`TClient.exe+A08CE0 | 0x7FF69DF78CE0` is an owner-configuration application routine with effective leading arguments `owner` in RCX and `configRecord` in RDX. It preserves them at `TClient.exe+A08CFA | 0x7FF69DF78CFA` and `TClient.exe+A08CF7 | 0x7FF69DF78CF7`.

The routine copies configuration bytes into owner state. In particular, `TClient.exe+A08DE9 | 0x7FF69DF78DE9` reads `configRecord+0x06` and `TClient.exe+A08DEE | 0x7FF69DF78DEE` stores it at `owner+0x1EE`. At `TClient.exe+A0922C | 0x7FF69DF7922C`, that byte controls whether the following source tree is copied.

When enabled:

```text
TClient.exe+A09235 | 0x7FF69DF79235  mov rax,[configRecord+58h] ; source header
TClient.exe+A09239 | 0x7FF69DF79239  mov rbx,[rax]              ; first source node
TClient.exe+A09245 | 0x7FF69DF79245  lea rsi,[owner+258h]      ; destination tree
TClient.exe+A09250 | 0x7FF69DF79250  mov edx,[rbx+20h]         ; key
TClient.exe+A09256 | 0x7FF69DF79256  mov rax,[rbx+28h]         ; payload S
```

The function searches the destination tree for the key. If absent, `TClient.exe+A092D0 | 0x7FF69DF792D0` allocates a 0x30-byte node, `TClient.exe+A092DB | 0x7FF69DF792DB` copies the key/payload pair into `newNode+0x20`, and `TClient.exe+A0930A | 0x7FF69DF7930A` calls `TClient.exe+22360 | 0x7FF69D592360` to insert it. The insertion helper's first instruction increments `[RCX+0x08]`, the tree count. Existing keys branch to `TClient.exe+A0930F | 0x7FF69DF7930F` without replacement. The source-tree successor loop ends by comparing the next node with `[configRecord+0x58]` at `TClient.exe+A0935F | 0x7FF69DF7935F` and loops at `TClient.exe+A09363 | 0x7FF69DF79363`.

This is a shallow keyed copy/union: the destination gets a new tree node, but its `+0x28` value is the same source-record pointer held by the configuration tree. No direct clear of `owner+0x258` occurs before this loop, so duplicate keys are preserved rather than reinserted. For the concrete owner vtable used by this path, the three reset calls near the start resolve to `TClient.exe+A06420 | 0x7FF69DF76420`, `TClient.exe+859390 | 0x7FF69DDC9390`, and `TClient.exe+8594C0 | 0x7FF69DDC94C0`; none accesses `owner+0x258`. The only proven full lifecycle reset is construction/destruction.

Because this source is a tree, it has no vector end-pointer increment or capacity-growth operation. Each unique insertion allocates one 0x30-byte node, links/rebalances it through `TClient.exe+22360 | 0x7FF69D592360`, and increments `owner+0x260`. Duplicate keys allocate nothing and do not change the count.

A representative caller proves configuration-record provenance:

```text
TClient.exe+872820 | 0x7FF69DDE2820  mov edx,[rdi+C4h]          ; registry key
TClient.exe+872826 | 0x7FF69DDE2826  mov rcx,[rbp-70h]          ; registry/manager
TClient.exe+87282A | 0x7FF69DDE282A  call TClient.exe+9F4CA0 | 0x7FF69DF64CA0
TClient.exe+87282F | 0x7FF69DDE282F  mov rdx,rax                ; config record
TClient.exe+872832 | 0x7FF69DDE2832  mov rcx,r15                ; owner
TClient.exe+872835 | 0x7FF69DDE2835  call TClient.exe+A08CE0 | 0x7FF69DF78CE0
```

`TClient.exe+9F4CA0 | 0x7FF69DF64CA0` is a dword-key ordered-tree lookup over the manager's tree at `manager+0xF0`; on an exact key it returns `[node+0x28]` at `TClient.exe+9F4CDB | 0x7FF69DF64CDB`. This makes the value passed to `TClient.exe+A08CE0 | 0x7FF69DF78CE0` a registry/configuration payload, not an actor selected by distance.

The other append-capable builder `TClient.exe+A07020 | 0x7FF69DF77020` confirms the shared format. It accepts this same tree through RDX, checks its count at `TClient.exe+A0705D | 0x7FF69DF7705D`, loads the header and first node at `TClient.exe+A070AD | 0x7FF69DF770AD`, then reads node key and payload at `TClient.exe+A070D0 | 0x7FF69DF770D0` and `TClient.exe+A070D3 | 0x7FF69DF770D3`. Its comparisons of `S+0x28` and `S+0x24` at `TClient.exe+A070E0 | 0x7FF69DF770E0` and `TClient.exe+A070EA | 0x7FF69DF770EA` are record scheduling/key conditions, not spatial actor tests.

## Count transformation and the observed 1/3/4

`TClient.exe+865AD0 | 0x7FF69DDD5AD0` appends to its destination and does not clear it. The normal store and end increment are at `TClient.exe+865D54 | 0x7FF69DDD5D54` and `TClient.exe+865D57 | 0x7FF69DDD5D57`. The special stores at `TClient.exe+865EB4 | 0x7FF69DDD5EB4` and advances at `TClient.exe+865EB7 | 0x7FF69DDD5EB7`. The fallback stores at `TClient.exe+866053 | 0x7FF69DDD6053` and advances at `TClient.exe+866056 | 0x7FF69DDD6056`.

The exact static relationships are:

```text
normal emitted count  = number of source records with S+0x24 == [lookup(requestedId)+0x04]
special emitted count = number of source records with S+0x24 == 0x6F5E
fallback emitted count = 1
final +0x688 count    = count present on entry + emitted count
```

Thus 1/3/4 is not automatically the raw `[owner+0x260]` count. Records can be skipped by the S+0x24 predicate, and existing descriptors remain in the destination. If the destination happened to be empty on a capture, the final count would equal the accepted record count for that branch, but emptiness at these calls is not established statically. Captures greater than one cannot arise from the single-descriptor fallback alone when the destination begins empty.

## Actor provenance, range, and actor-type predicates

There is no candidate actor in the proven chain. The owner object is an entity/gameplay-state context, but it is not an iterated candidate target. Each S pointer comes from a configuration record's tree through `TClient.exe+9F4CA0 | 0x7FF69DF64CA0` and `TClient.exe+A08CE0 | 0x7FF69DF78CE0`. The first requested “candidate actor pointer -> selection -> S” stage therefore does not exist for this source collection.

No instruction in the proven chain

```text
manager+0xF0 tree -> +9F4CA0 -> configRecord -> +A08CE0
-> owner+0x258 -> +865AD0 -> descriptor -> owner+0x688
```

reads candidate positions, subtracts coordinates, computes a squared distance, obtains a radius, or compares distance with range. It likewise does not test a candidate vtable/type, faction/team, alive state, targetable state, self identity, player/NPC/pet class, or attackability relationship. The proven predicates are configuration-enable byte `configRecord+0x06`, duplicate dword key suppression, and record-field matching through S+0x24.

`TClient.exe+86B620 | 0x7FF69DDDB620` contains separate coordinate/distance-like math elsewhere in the binary, but no static data-flow edge connects it to this source producer. It is therefore not identified as the AOE range predicate.

The source collection is best classified as option D: it is initialized with the owner, populated/unioned when skill/effect configuration is applied, and later consumed by descriptor builders. It is not proven to be continuously maintained nearby actors or built by a cast-time spatial query.

## Relationship to `owner+0x660` and outgoing flow

The verified data flow is:

```text
registry/manager ordered tree at +0xF0
  -> TClient.exe+9F4CA0 | 0x7FF69DF64CA0
  -> configuration record
  -> configuration tree at configRecord+0x58
  -> TClient.exe+A08CE0 | 0x7FF69DF78CE0
  -> ordered source tree at owner+0x258
  -> S+0x24 record-key admission in TClient.exe+865AD0 | 0x7FF69DDD5AD0
  -> allocated 0x18 descriptor
  -> pointer appended to owner+0x688
```

The same initial-path function later forms `owner+0x660` at `TClient.exe+7D0D6C | 0x7FF69DD40D6C`, passes it to `TClient.exe+7F06E0 | 0x7FF69DD606E0` at `TClient.exe+7D0D84 | 0x7FF69DD40D84`, and reaches:

```text
owner+0x660
  -> TClient.exe+7F06E0 | 0x7FF69DD606E0
  -> TClient.exe+7F091D | 0x7FF69DD6091D
  -> TClient.exe+7BB160 | 0x7FF69DD2B160
  -> TClient.exe+99240 | 0x7FF69D609240
```

This is a verified control path, but no verified instruction copies or transforms an `owner+0x688` descriptor into an `owner+0x660` element. The collections have different formats and remain independent. The common containing path is not evidence of a target-list conversion.

## Single preferred future research point

The one preferred future research point is `TClient.exe+A0930A | 0x7FF69DF7930A`, whose exact instruction is:

```text
call TClient.exe+22360 | 0x7FF69D592360
```

This is the proven insertion of a newly allocated node into `owner+0x258`. At this point RCX is `&owner+0x258`, R8 is the new 0x30-byte node, `[R8+0x20]` is the key, and `[R8+0x28]` is S. It is preferable to changing `owner+0x688` because it observes provenance before descriptor allocation and can establish which configuration application created each later input. It is a research anchor only: it is not an actor-selection predicate and should not be patched as a Multikill control.

Static analysis uniquely identified the source collection and producer, so no AOE Builder Input Inspector or source-writer watch mode was added.
