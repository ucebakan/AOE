using System.Text.Json;

namespace UnityTools;

// An explicit research-only CLI snapshot. No UI feature, item use, hook or write.
static class CollectionResearch
{
    internal static object Read()
    {
        using var session = Salesman.Session.Connect(); var world = session.Snapshot();
        using var binary = new PlayerXYZ.Binary(Multikill.Profile.GamePath);
        if (binary.Sha != "4DC9C526A895A10113C4CF23F2D199BFF283D2C7CE75F949DC49CEF15D27D622") throw new IOException("Collection araştırması bu SHA ile sınırlı.");
        int table = binary.FindVtable(".?AVCTClientMaintain@@");
        int finder = binary.RelativeCall(0x1b1d94);
        // The skill finder has its two returns in the opposite block order.
        const string skillTree = "4C 8B 05 ?? ?? ?? ?? 49 8B D0 49 8B 40 08 80 78 19 00 75 18 66 39 48 20 73 06 48 83 C0 10 EB 03 48 8B D0 48 8B 00 80 78 19 00 74 E8 80 7A 19 00 75 06 66 3B 4A 20 73 03 49 8B D0 49 3B D0 74 05 48 8B 42 28 C3 33 C0 C3";
        var signature = PlayerXYZ.Binary.Pattern(skillTree);
        var finderCode = binary.At(finder, signature.Length);
        if (!PlayerXYZ.Binary.Match(finderCode, signature) || !session.Read(session.Base + finder, signature.Length).SequenceEqual(finderCode)) throw new IOException("Collection skill lookup fingerprint değişmiş.");
        long root = session.Base + finder + 7 + binary.I32(finder + 3);
        long headSkill = session.Pointer(root), skillNode = session.Pointer(headSkill + 8), selected = headSkill;
        int steps = 0;
        while (session.Read(skillNode + 0x19, 1)[0] == 0)
        {
            if (++steps > 256) throw new IOException("Skill tree sınırı.");
            if (BitConverter.ToUInt16(session.Read(skillNode + 0x20, 2)) >= 9909) { selected = skillNode; skillNode = session.Pointer(skillNode); }
            else skillNode = session.Pointer(skillNode + 0x10);
        }
        if (selected == headSkill || BitConverter.ToUInt16(session.Read(selected + 0x20, 2)) != 9909) throw new IOException("Collection 9909 resource yok.");
        long collectionSkill = session.Pointer(selected + 0x28);
        if (BitConverter.ToUInt16(session.Read(collectionSkill, 2)) != 9909) throw new IOException("Collection resource identity uyuşmuyor.");
        var maps = new List<object>();
        var body = new byte[0x3000];
        for (int p = 0; p < body.Length; p += 4096) session.Read(world.Player + p, 4096).CopyTo(body, p);
        bool Pointer(long value) => value > 0x10000 && value < 0x0000800000000000 && (value & 7) == 0;
        for (int offset = 8; offset + 16 <= body.Length; offset += 8)
        {
            long head = BitConverter.ToInt64(body, offset), count = BitConverter.ToInt64(body, offset + 8);
            if (!Pointer(head) || count is < 1 or > 64) continue;
            try
            {
                var sentinel = session.Read(head, 0x30); if (sentinel[0x19] != 1) continue;
                var seen = new HashSet<long>(); var pending = new Stack<long>(); pending.Push(BitConverter.ToInt64(sentinel, 8));
                var entries = new List<object>();
                while (pending.Count > 0)
                {
                    long node = pending.Pop(); if (node == head) continue;
                    if (!Pointer(node) || !seen.Add(node) || seen.Count > count) throw new IOException("Bounded tree mismatch");
                    var n = session.Read(node, 0x30); if (n[0x19] != 0) throw new IOException("Tree nil mismatch");
                    pending.Push(BitConverter.ToInt64(n, 0)); pending.Push(BitConverter.ToInt64(n, 0x10));
                    long value = BitConverter.ToInt64(n, 0x28);
                    if (!Pointer(value) || session.Pointer(value) != session.Base + table) continue;
                    var record = session.Read(value, 0xa0); long skill = BitConverter.ToInt64(record, 0x20);
                    int? candidateId = Pointer(skill) ? BitConverter.ToUInt16(session.Read(skill, 2)) : null;
                    entries.Add(new { node_key = BitConverter.ToUInt32(n, 0x20), maintain = $"0x{value:x}", resource_pointer = $"0x{skill:x}", resource_skill_id = candidateId,
                        collection_resource_identity_match = skill == collectionSkill, record_skill_word = BitConverter.ToUInt16(record, 0x50), raw = Convert.ToHexString(record) });
                }
                if (seen.Count == count && entries.Count > 0) maps.Add(new { player_offset = $"0x{offset:x}", count, entries });
            }
            catch (IOException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        if (session.Snapshot() != world) throw new IOException("Collection okuması sırasında dünya veya shop değişti.");
        return new { status = "PASS", session.Pid, session.Profile.Sha, maintain_vtable = $"0x{table:x}", maps, game_memory_writes = 0, game_function_calls = 0,
            limitation = "RTTI-bound active-maintain candidates matched against independently resolved Collection 9909 resource pointer. This does not prove Collection causes loot or establish current server checks." };
    }
}
