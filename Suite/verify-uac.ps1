param([Parameter(Mandatory = $true)][string]$Path)
$ErrorActionPreference = 'Stop'
if (-not ('UnityTools.ManifestReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
namespace UnityTools {
    public static class ManifestReader {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", EntryPoint="FindResourceW", SetLastError=true)]
        static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
        [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module, IntPtr resource);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        public static string Read(string path) {
            // Map only as data: never execute the application or prompt for elevation.
            var module=LoadLibraryEx(path,IntPtr.Zero,0x22);
            if(module==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                var resource=FindResource(module,new IntPtr(1),new IntPtr(24));
                if(resource==IntPtr.Zero) throw new InvalidOperationException("EXE manifest resource is missing.");
                int size=checked((int)SizeofResource(module,resource));
                var pointer=LockResource(LoadResource(module,resource));
                if(pointer==IntPtr.Zero || size==0) throw new InvalidOperationException("EXE manifest resource is empty.");
                var bytes=new byte[size];Marshal.Copy(pointer,bytes,0,size);
                return Encoding.UTF8.GetString(bytes).Trim('\uFEFF','\0');
            } finally { FreeLibrary(module); }
        }
    }
}
'@
}
$exePath = (Resolve-Path -LiteralPath $Path).Path
[xml]$manifest = [UnityTools.ManifestReader]::Read($exePath)
$execution = $manifest.SelectSingleNode("//*[local-name()='requestedExecutionLevel']")
if ($null -eq $execution -or $execution.GetAttribute('level') -ne 'requireAdministrator' -or $execution.GetAttribute('uiAccess') -ne 'false') {
    throw "UAC verification failed: $exePath must embed requireAdministrator with uiAccess=false."
}
Write-Output "UAC verified: requireAdministrator, uiAccess=false ($exePath)"
