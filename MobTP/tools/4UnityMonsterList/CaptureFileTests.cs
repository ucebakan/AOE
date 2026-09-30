using System.Text.Json;

namespace UnityMonsterList;

static class CaptureFileTests
{
    public static void Run()
    {
        string dir=Path.Combine(Path.GetTempPath(),"4UnityCaptureFiles-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"metadata.json"),"{\"home_candidate_offsets\":[]}");
        File.WriteAllText(Path.Combine(dir,"events.jsonl"),"");
        string raw=Path.Combine(dir,"timeline.jsonl");
        using var source=new StreamWriter(raw){AutoFlush=true};
        source.WriteLine(JsonSerializer.Serialize(new{Index=0,Timestamp=DateTimeOffset.UtcNow,Valid=true}));
        bool reproduced=false;
        try{File.ReadAllLines(raw);}catch(IOException){reproduced=true;}
        if(!reproduced)throw new Exception("Expected Windows sharing failure was not reproduced");
        CaptureFiles.FinalizeCapture(dir,"TEST");
        string final=Path.Combine(dir,"timeline.json"),report=Path.Combine(dir,"report.json");
        using(var doc=JsonDocument.Parse(File.ReadAllText(final)))if(doc.RootElement.GetArrayLength()!=1)throw new Exception("Lost sample");
        string saved=File.ReadAllText(final);
        bool rejected=false;
        using(var locked=new FileStream(final,FileMode.Open,FileAccess.Read,FileShare.None))
        {try{CaptureFiles.FinalizeCapture(dir,"TEST_RETRY");}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){rejected=true;}}
        if(!rejected)throw new Exception("Expected locked destination failure");
        CaptureFiles.FinalizeCapture(dir,"TEST_RETRY");
        if(File.ReadAllText(final)!=saved)throw new Exception("Retry changed raw data");
        source.WriteLine("{incomplete");
        rejected=false;try{CaptureFiles.FinalizeCapture(dir,"TEST_CORRUPT");}catch(JsonException){rejected=true;}
        if(!rejected || File.ReadAllText(final)!=saved || !File.ReadAllText(report).Contains("TEST_RETRY"))throw new Exception("Corrupt source replaced complete output");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"capture-file-test.json"),JsonSerializer.Serialize(new{passed=5,old_sharing_failure_reproduced=true,open_writer_recovery=true,locked_output_failure=true,retry_succeeds=true,corrupt_source_preserves_final=true,fixture=dir}));
    }
}
