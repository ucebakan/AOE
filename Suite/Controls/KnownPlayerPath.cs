namespace UnityTools.Controls;

// Getter/root operands come from a validated SHA profile. Live addresses are
// recomputed from the current module base; nothing from an earlier PID is used.
public static class KnownPlayerPath
{
    public const string Sha = "9CD77CD0C305359A3048D60C3C2FD837B907E2E61BBF8B828BFCC8E5270E5C28";
    public static (long Owner, long Player) Resolve(long moduleBase, Func<long, int, byte[]> read,
        int ownerVtable, int playerVtable, int playerOffset, int getterRva=0x7C1A20, int rootRva=0xE6E9C0)
    {
        long Pointer(long address) => BitConverter.ToInt64(read(address, 8));
        bool Valid(long p) => p >= 0x10000 && p < 0x7FFFFFFF0000 && (p & 7) == 0;
        byte[] getter=read(moduleBase+getterRva,8);
        if (getter.Length!=8 || getter[0]!=0x48 || getter[1]!=0x8B || getter[2]!=0x05 || getter[7]!=0xC3 ||
            getterRva+7L+BitConverter.ToInt32(getter,3)!=rootRva)
            throw new IOException("Oyuncu pointer yolu canlı kodla uyuşmuyor.");
        long owner = Pointer(moduleBase + rootRva);
        if (!Valid(owner) || Pointer(owner) != moduleBase + ownerVtable) throw new IOException("Oyuncu/harita bekleniyor; kayıtlı profil hazır.");
        long player = Pointer(owner + playerOffset);
        if (!Valid(player) || Pointer(player) != moduleBase + playerVtable || Pointer(moduleBase + rootRva) != owner)
            throw new IOException("Oyuncu pointer yolu henüz hazır değil.");
        return (owner, player);
    }
}
