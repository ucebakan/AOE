using System.Text.Json;

namespace UnityMonsterList;

static class CaptureFiles
{
    // Share WRITE too: the original recorder may still own the JSONL handle during recovery.
    static StreamReader OpenRead(string path)=>new(new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite));
    public static void FinalizeCapture(string directory,string status)
    {
        using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"metadata.json")));
        int homeCount=metadata.RootElement.GetProperty("home_candidate_offsets").GetArrayLength();
        string timelineTemp=Path.Combine(directory,"timeline.pending.json"),reportTemp=Path.Combine(directory,"report.pending.json");
        long count=0,invalid=0;DateTimeOffset? first=null,last=null;JsonElement? lastCenter=null,lastHome=null;
        using(var input=OpenRead(Path.Combine(directory,"timeline.jsonl")))
        using(var output=new FileStream(timelineTemp,FileMode.Create,FileAccess.Write,FileShare.Read))
        using(var writer=new Utf8JsonWriter(output))
        {
            writer.WriteStartArray();string? line;
            while((line=input.ReadLine()) is not null)
            {
                if(string.IsNullOrWhiteSpace(line))throw new IOException($"Boş örnek satırı: {count+1}");
                using var item=JsonDocument.Parse(line);
                var row=item.RootElement;
                if(row.GetProperty("Index").GetInt64()!=count)throw new IOException($"Örnek sırası bozuk: {count}");
                var timestamp=row.GetProperty("Timestamp").GetDateTimeOffset();first??=timestamp;last=timestamp;
                if(!row.GetProperty("Valid").GetBoolean())invalid++;
                if(row.TryGetProperty("LearnedCenter",out var learned))lastCenter=learned.Clone();
                if(row.TryGetProperty("Home",out var home))lastHome=home.Clone();
                row.WriteTo(writer);count++;
            }
            writer.WriteEndArray();writer.Flush();output.Flush(true);
        }
        int events=0;
        using(var input=OpenRead(Path.Combine(directory,"events.jsonl")))
        {string? line;while((line=input.ReadLine()) is not null){using var item=JsonDocument.Parse(line);events++;}}
        File.WriteAllText(reportTemp,JsonSerializer.Serialize(new
        {
            status,samples=count,
            DIRECT_OBSERVATION=new{timeline="timeline.json",csv="timeline.csv",events="events.jsonl",first_timestamp=first,last_timestamp=last,invalid_samples=invalid,manual_event_count=events,home_candidates=homeCount,event_source="Manual user observation only; no inferred labels",invalid_sample_policy="Retained for diagnostics; excluded from evidence"},
            LEARNED_RETURN_CENTER=lastCenter,
            OPERATIONAL_HOME=lastHome,
            INFERENCE=Array.Empty<string>(),
            UNTESTED=new[]{"A/B semantic roles","Home role and return cycles","Teleport","Server-authoritative effect"}
        },new JsonSerializerOptions{WriteIndented=true}));
        // Raw sources are never modified. Incomplete output does not replace a good final file.
        File.Move(timelineTemp,Path.Combine(directory,"timeline.json"),true);
        File.Move(reportTemp,Path.Combine(directory,"report.json"),true);
    }
}
