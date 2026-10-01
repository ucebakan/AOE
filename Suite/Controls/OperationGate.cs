namespace UnityTools.Controls;

// Shared by embedded tools, including their hotkey paths. Standalone tools default to unblocked.
public static class OperationGate
{
    static int blocked;
    static int stopContinuous;
    public static bool Blocked { get => Volatile.Read(ref blocked) != 0; set { Volatile.Write(ref blocked, value ? 1 : 0); if (!value) StopContinuous = false; } }
    public static bool StopContinuous { get => Volatile.Read(ref stopContinuous) != 0; set => Volatile.Write(ref stopContinuous, value ? 1 : 0); }
    public static void Check() { if (Blocked) throw new InvalidOperationException("SafeMode: oyuncu sayacı güvenli durumda değil; işlem engellendi."); }
    public static void CheckContinuous() { if (StopContinuous) throw new InvalidOperationException("SafeMode: sürekli işlem durduruldu."); }
}
