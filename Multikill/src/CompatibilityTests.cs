using System.Text.Json;
using UnityTools.Controls;

namespace Multikill;

static class CompatibilityTests
{
    internal static object Run(Func<OwnedCodePatchQuery,bool>? nativeProof=null)
    {
        var build=Profile.Resolve(File.ReadAllBytes(Profile.GamePath));
        var memory=new Memory(build);
        string directory=Path.Combine(Path.GetTempPath(),"4UnityMultikill-compatibility",Guid.NewGuid().ToString("N"));
        using var engine=new PatchEngine(directory,()=>memory);
        int checks=0;
        void Check(bool ok,string message){if(!ok)throw new IOException(message);checks++;}
        var query=new OwnedCodePatchQuery(memory.Pid,memory.Created,memory.Base,build.Sha256,build.PatchRva,Convert.FromHexString(build.Original),Convert.FromHexString(build.Patched));
        bool Proof(OwnedCodePatchQuery? q=null){var candidate=q??query;bool managed=OwnedCodePatchRegistry.Validate(candidate);if(nativeProof is not null&&nativeProof(candidate)!=managed)throw new IOException("Native/managed ownership proof mismatch");return managed;}
        engine.Validate();Check(!Proof(),"Inactive module authorized JE");
        engine.Toggle();Check(Proof(),"Verified active owner rejected");
        int writes=memory.Writes;
        Check(!Proof(query with{Pid=query.Pid+1}),"Other PID accepted");
        Check(!Proof(query with{Created=query.Created+1}),"Reused PID accepted");
        Check(!Proof(query with{ModuleBase=query.ModuleBase+0x10000}),"Old ASLR base accepted");
        Check(!Proof(query with{Sha=new string('A',64)}),"Other build accepted");
        Check(!Proof(query with{Rva=query.Rva+1}),"Other patch site accepted");
        var changed=query.Patched.ToArray();changed[2]^=1;Check(!Proof(query with{Patched=changed}),"Changed Jcc displacement accepted");
        memory.Corrupt=true;Check(!Proof(),"Changed neighboring code accepted");memory.Corrupt=false;
        string journal=Path.Combine(directory,"patch-recovery.json"),saved=File.ReadAllText(journal);
        File.WriteAllText(journal,"{}");Check(!Proof(),"Corrupt journal accepted");File.WriteAllText(journal,saved);
        object duplicate=new();OwnedCodePatchRegistry.Register(duplicate,_=>true);
        try{Check(!Proof(),"Ambiguous owners accepted");}finally{OwnedCodePatchRegistry.Remove(duplicate);}
        Check(memory.Writes==writes,"Compatibility checks wrote memory");
        engine.Toggle();Check(!Proof()&&memory.Opcode==0x86,"OFF kept patch authorization");
        engine.Toggle();engine.Dispose();Check(!Proof()&&memory.Opcode==0x86,"Shutdown kept patch authorization");
        return new{checks,game_memory_writes=0,scope="Owned fake session; no game attach, arm or patch"};
    }
    sealed class Memory(Signature build):IPatchSession
    {
        public Signature Build=>build;
        public int Pid=>123;public long Created=>134356269284984972;public long Base=>0x180000000;public bool Exited=>false;
        public byte Opcode=0x86;public bool Corrupt;public int Writes;
        public byte[] ReadSite(){var data=build.Pattern;data[build.PatchOffset+1]=Opcode;if(Corrupt)data[0]^=1;return data;}
        public uint Protection()=>0x20;public void ValidateIdentity(){}
        public void WriteOpcode(byte value,uint protection){Opcode=value;Writes++;}
        public void RestoreProtection(uint protection){}public void Dispose(){}
    }
}
