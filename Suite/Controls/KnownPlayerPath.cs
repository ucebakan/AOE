namespace UnityTools.Controls;

// This path is proven only for this exact executable. All live addresses are
// recomputed from the current module base; nothing from an earlier PID is used.
public static class KnownPlayerPath
{
    public const string Sha = "9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
    public static (long Owner, long Player) Resolve(long moduleBase, Func<long, int, byte[]> read,
        int ownerVtable, int playerVtable, int playerOffset)
    {
        long Pointer(long address) => BitConverter.ToInt64(read(address, 8));
        bool Valid(long p) => p >= 0x10000 && p < 0x7FFFFFFF0000 && (p & 7) == 0;
        if (!read(moduleBase + 0x7C1A20, 8).SequenceEqual(Convert.FromHexString("488B0599CF6A00C3")))
            throw new IOException("Oyuncu pointer yolu canlı kodla uyuşmuyor.");
        long owner = Pointer(moduleBase + 0xE6E9C0);
        if (!Valid(owner) || Pointer(owner) != moduleBase + ownerVtable) throw new IOException("Oyuncu/harita bekleniyor; kayıtlı profil hazır.");
        long player = Pointer(owner + playerOffset);
        if (!Valid(player) || Pointer(player) != moduleBase + playerVtable || Pointer(moduleBase + 0xE6E9C0) != owner)
            throw new IOException("Oyuncu pointer yolu henüz hazır değil.");
        return (owner, player);
    }
}
