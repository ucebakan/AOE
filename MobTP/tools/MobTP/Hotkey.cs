using System.Runtime.InteropServices;
using System.Text.Json;

namespace MobTP;

record KeyBinding(int Key,uint Modifiers)
{
    public bool Valid=>Key is >=8 and <=254 && !new[]{16,17,18,91,92,160,161,162,163,164,165}.Contains(Key)&&(Modifiers&~7u)==0;
    public override string ToString()=>(Modifiers.HasFlag(2)?"Ctrl+":"")+(Modifiers.HasFlag(1)?"Alt+":"")+(Modifiers.HasFlag(4)?"Shift+":"")+((Keys)Key).ToString();
}
static class ModBits { public static bool HasFlag(this uint value,uint mask)=>(value&mask)!=0; }
record UserSettings(KeyBinding? Hotkey=null,decimal Spread=2,bool DistanceDescending=false)
{
    static string PathName=>Path.Combine(ProfileStore.Root,"settings.json");
    public static UserSettings Load()
    {
        try{var s=JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(PathName));return s is not null&&s.Spread>=1&&s.Spread<=10&&(s.Hotkey is null||s.Hotkey.Valid)?s:new();}catch{return new();}
    }
    public void Save(){Directory.CreateDirectory(ProfileStore.Root);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(this));File.Move(PathName+".tmp",PathName,true);}
}
sealed class HotkeyRegistration(IntPtr window) : IDisposable
{
    int id;
    public KeyBinding? Binding {get;private set;}
    public bool Owns(nint messageId)=>id!=0&&messageId==(nint)id;
    public void Assign(KeyBinding binding)
    {
        if(!binding.Valid)throw new ArgumentException("Geçerli bir tuş veya Ctrl/Alt/Shift kombinasyonu seçin.");
        if(binding==Binding)return;
        int next=id==0x4D01?0x4D02:0x4D01;
        if(!RegisterHotKey(window,next,binding.Modifiers|0x4000,(uint)binding.Key))throw new IOException("Bu kısayol başka uygulamada kullanımda veya Windows tarafından ayrılmış. Başka tuş seçin.");
        if(id!=0)UnregisterHotKey(window,id);id=next;Binding=binding;
    }
    public void Dispose(){if(id!=0)UnregisterHotKey(window,id);id=0;Binding=null;}
    public static bool GameForeground(int? pid)
    {
        if(pid is null or <=0)return false;
        GetWindowThreadProcessId(GetForegroundWindow(),out uint foreground);
        return foreground==pid;
    }
    [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}
