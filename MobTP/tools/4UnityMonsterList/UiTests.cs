using System.Text.Json;

namespace UnityMonsterList;

static class UiTests
{
    public static void Run()
    {
        using var form=new MainForm(true);form.Show();Application.DoEvents();
        var monsters=Enumerable.Range(1,100).Select(i=>new Monster((uint)i,2,(ulong)(0x200000+i*0x1000),(ulong)(0x400000+i*0x100),5330+i,80,4760+i,true,i,new(5330+i,80,4760+i))).ToList();
        var snapshot=new Snapshot(DateTimeOffset.Now,"SYNTHETIC_UI_TEST","SYNTHETIC",123,"SYNTHETIC","0x100000","0x500000","0x600000",monsters,"CANDIDATE",new(1,0x700000,5340,80,4770,"SYNTHETIC",new(5340,80,4770)),"Sentetik UI testi; canlı oyun bulgusu değildir.");
        form.Render(snapshot);Application.DoEvents();
        var selected=form.Grid.Rows[40];form.Grid.CurrentCell=selected.Cells[0];form.Grid.FirstDisplayedScrollingRowIndex=35;
        int top=form.Grid.FirstDisplayedScrollingRowIndex;
        for(int i=0;i<12;i++)form.Render(snapshot with{Timestamp=DateTimeOffset.Now,Monsters=monsters.AsEnumerable().Reverse().Select(m=>m with{X=m.X+0.01f*i}).ToList()});
        if(form.Grid.CurrentRow!=selected || form.Grid.FirstDisplayedScrollingRowIndex!=top || form.Grid.Rows[40]!=selected)throw new Exception("Scroll/selection/row continuity regression");
        form.Render(snapshot with{Monsters=monsters.Where(m=>m.EntityId!=41).ToList()});
        if(form.Grid.CurrentRow is not null)throw new Exception("Removed selection rebound");
        if(Reader.DiagnosticDistance(new(5334.352f,79,4767.18f),new(0,0,0)) is not null)throw new Exception("Zero-player distance accepted");
        if(Reader.DiagnosticDistance(new(3,9,4),new(0,1,0))!=5)throw new Exception("XZ distance regression");
        Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(AppContext.BaseDirectory,"ui-preview.png"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-test.json"),JsonSerializer.Serialize(new{passed=6,synthetic=true,scroll_preserved=true,selection_preserved=true,rows_reused=true,no_selection_rebind=true,zero_player_rejected=true,xz_distance=true}));
        using(var capture=new CaptureForm(snapshot,monsters[0]))
        {
            capture.Show();capture.PreviewCenter(new("RECONFIRMED",new(5459.36f,77.16f,4714.1f),2,0.28,32.5,"Aynı merkez 2 tamamlanmış dönüşte gözlendi"));Application.DoEvents();
            using var centerBitmap=new Bitmap(capture.Width,capture.Height);capture.DrawToBitmap(centerBitmap,new(Point.Empty,centerBitmap.Size));centerBitmap.Save(Path.Combine(AppContext.BaseDirectory,"center-ui-preview.png"));capture.Close();
        }
        form.Close();
    }
}
