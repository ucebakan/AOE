using System.Runtime.InteropServices;
using System.Text;

namespace UnityTools;
static class AoeProfileRecovery
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl,CharSet=CharSet.Unicode)]
    delegate int Recover([MarshalAs(UnmanagedType.LPWStr)] string directory,[Out] byte[] message,int capacity);
    internal static void Ensure()
    {
        byte[] message=new byte[2048];
        if(NativeModules.Function<Recover>("UnityAoe.dll","RecoverProfile")(Path.Combine(Program.DataRoot,"AOE","profiles"),message,message.Length)==0)
            throw new IOException(Encoding.UTF8.GetString(message).TrimEnd('\0'));
    }
}
