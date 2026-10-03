using System.Reflection.PortableExecutable;

namespace PlayerXYZ;
static class ResolverTests
{
    public static int Run(string output)
    {
        string saved=Files.Root;Files.Root=Path.Combine(Path.GetTempPath(),"PlayerXYZ-tests-"+Guid.NewGuid().ToString("N"));
        var checks=new List<string>();
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
        void Reject(Action test,string name){try{test();}catch{checks.Add(name);return;}throw new Exception("Accepted invalid data: "+name);}
        try
        {
            using var b=new Binary(@"C:\Games\4Unity\TClient.exe");var p=Profiles.Load(b);
            Check(p.CoordinateA.SequenceEqual(new[]{0x70,0x74,0x78})&&p.CoordinateB.SequenceEqual(new[]{0xB0,0xB4,0xB8}),"current XYZ offsets derived");
            Check(p.Signatures.Count==4,"four unique signatures");
            int n=Profiles.ScanCount;Profiles.Load(b);Check(Profiles.ScanCount==n,"known SHA no AOB scan");
            p.CoordinateA[0]++;Reject(()=>Profiles.Validate(b,p),"corrupt profile rejected");p.CoordinateA[0]--;
            var data=b.Data.ToArray();
            int Raw(int rva){var s=b.Pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.SizeOfRawData);return s.PointerToRawData+rva-s.VirtualAddress;}
            int a=p.Signatures["coords_a"].Rva,c=p.Signatures["coords_b"].Rva,o=p.Signatures["owner"].Rva;
            foreach(int offset in new[]{4,10,16})data[Raw(a+offset)]+=4;
            foreach(int offset in new[]{10,30,57})BitConverter.GetBytes(b.I32(c+offset)+16).CopyTo(data,Raw(c+offset));
            foreach(int offset in new[]{21,37})BitConverter.GetBytes(b.I32(o+offset)+8).CopyTo(data,Raw(o+offset));
            using(var changed=new Binary(data))
            {
                var q=Profiles.Resolve(changed);
                Check(q.Sha256!=b.Sha&&q.CoordinateA[0]==0x74&&q.CoordinateB[0]==0xC0&&q.OwnerToPlayerOffset==p.OwnerToPlayerOffset+8,"synthetic new SHA extracts changed offsets");
            }
            data[Raw(a+10)]++;
            using(var broken=new Binary(data))Reject(()=>Profiles.Resolve(broken),"inconsistent XYZ triplet rejected");
            Reject(()=>b.FindUnique("CC CC CC"),"ambiguous signature rejected");
            p.Version=1;Reject(()=>Profiles.Validate(b,p),"old profile schema rejected");
            Files.Save(output,new{status="PASS",checks,live_writes=0,scope="Synthetic relocation test is not future-build compatibility proof."});return 0;
        }
        catch(Exception ex){Files.Save(output,new{status="FAIL",checks,error=ex.ToString()});return 1;}
        finally{Files.Root=saved;}
    }
}
