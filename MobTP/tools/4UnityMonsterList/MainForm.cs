using System.Text.Json;

namespace UnityMonsterList;

sealed class BufferedGrid : DataGridView
{
    public BufferedGrid(){DoubleBuffered=true;}
}

sealed class MainForm : Form
{
    readonly TextBox info=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=false,ScrollBars=ScrollBars.Both,Font=new("Consolas",10),BorderStyle=BorderStyle.FixedSingle};
    internal readonly BufferedGrid Grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false};
    readonly Button pause=new(){Text="Listeyi duraklat",AutoSize=true},capture=new(){Text="Seçili mob — merkez öğren / kaydet",AutoSize=true};
    readonly Label clock=new(){AutoSize=true,Margin=new Padding(12,9,3,3)};
    readonly System.Windows.Forms.Timer timer=new(){Interval=750};
    readonly Dictionary<string,DataGridViewRow> rows=[];
    Snapshot latest=Snapshot.Disconnected("Başlatılıyor");
    bool refreshing,paused,closing;
    string? session;
    public MainForm(bool preview=false)
    {
        Text="4Unity Mob Observer 1.7 — salt okunur";ClientSize=new(1240,720);MinimumSize=new(900,550);StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8)};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,175));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(info,0,0);layout.Controls.Add(Grid,0,1);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Padding=new Padding(0,6,0,0)};
        var refresh=new Button{Text="Bir kez yenile",AutoSize=true};var save=new Button{Text="Tanı snapshot kaydet",AutoSize=true};
        buttons.Controls.AddRange([pause,refresh,capture,save,clock]);layout.Controls.Add(buttons,0,2);
        Add("Id","Mob ID",95);Add("Actor","Actor adresi",145);Add("AX","A X",85);Add("AY","A Y",75);Add("AZ","A Z",85);Add("BX","B X",85);Add("BY","B Y",75);Add("BZ","B Z",85);Add("DA","A↔A XZ (ham)",135);Add("DB","B↔B XZ (ham)",135);Add("AB","Mob A↔B 3D",120);
        pause.Click+=(_,_)=>{paused=!paused;pause.Text=paused?"Listeyi sürdür":"Listeyi duraklat";};
        refresh.Click+=async(_,_)=>await RefreshAsync(true);capture.Click+=OpenCapture;save.Click+=Save;
        timer.Tick+=async(_,_)=>await RefreshAsync();
        if(!preview)Shown+=async(_,_)=>{await RefreshAsync();timer.Start();};
        FormClosing+=(_,_)=>{closing=true;timer.Stop();};
    }
    void Add(string name,string label,int width){Grid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,HeaderText=label,Width=width,SortMode=DataGridViewColumnSortMode.NotSortable});}
    static string Key(Monster m)=>$"{m.EntityId}:{m.ActorPtr:X}:{m.NodePtr:X}";
    static string F(double? x)=>x?.ToString("F2")??"—";
    async Task RefreshAsync(bool force=false)
    {
        if(refreshing||closing||paused&&!force)return;refreshing=true;capture.Enabled=false;
        try{var s=await Task.Run(Reader.Capture);if(!closing)Render(s);}
        finally{refreshing=false;if(!closing)capture.Enabled=true;}
    }
    void OpenCapture(object? sender,EventArgs e)
    {
        if(refreshing||Grid.CurrentRow?.Tag is not Monster m)return;
        timer.Stop();try{using var form=new CaptureForm(latest,m);form.ShowDialog(this);}finally{if(!closing)timer.Start();}
    }
    internal void Render(Snapshot s)
    {
        latest=s;
        string nextSession=$"{s.Pid}:{s.ModuleBase}:{s.Context}";
        if(session!=nextSession){Grid.Rows.Clear();rows.Clear();session=nextSession;}
        string? selected=Grid.CurrentRow?.Tag is Monster selectedMob?Key(selectedMob):null;
        int top=Grid.FirstDisplayedScrollingRowIndex,horizontal=Grid.HorizontalScrollingOffset;
        string? topKey=top>=0 && Grid.Rows[top].Tag is Monster topMob?Key(topMob):null;
        var incoming=s.Monsters.ToDictionary(Key);
        Grid.SuspendLayout();
        try
        {
            foreach(string removed in rows.Keys.Where(k=>!incoming.ContainsKey(k)).ToArray()){Grid.Rows.Remove(rows[removed]);rows.Remove(removed);}
            foreach(var m in s.Monsters)
            {
                string key=Key(m);
                if(!rows.TryGetValue(key,out var row)){row=Grid.Rows[Grid.Rows.Add()];rows.Add(key,row);}
                row.Tag=m;
                object[] values=[m.EntityId,$"0x{m.ActorPtr:X}",m.PositionValid?F(m.X):"—",m.PositionValid?F(m.Y):"—",m.PositionValid?F(m.Z):"—",F(m.B?.X),F(m.B?.Y),F(m.B?.Z),F(m.DistanceXZ),F(Reader.DiagnosticDistance(m.B,s.Player?.B)),F(MobCapture.Distance(m.PositionValid?new(m.X,m.Y,m.Z):null,m.B))];
                for(int i=0;i<values.Length;i++)if(!Equals(row.Cells[i].Value,values[i]))row.Cells[i].Value=values[i];
            }
            if(selected is not null)
            {
                if(rows.TryGetValue(selected,out var chosen)){if(Grid.CurrentRow!=chosen)Grid.CurrentCell=chosen.Cells[0];chosen.Selected=true;}
                else{Grid.CurrentCell=null;Grid.ClearSelection();}
            }
            if(topKey is not null && rows.TryGetValue(topKey,out var anchor))Grid.FirstDisplayedScrollingRowIndex=anchor.Index;
            else if(top>=0 && Grid.Rows.Count>0)Grid.FirstDisplayedScrollingRowIndex=Math.Min(top,Grid.Rows.Count-1);
            Grid.HorizontalScrollingOffset=horizontal;
        }
        finally{Grid.ResumeLayout();}
        string player=s.Player is null?"Oyuncu doğrulanamadı; mesafe gösterilmiyor.":$"Oyuncu {s.Player.EntityId} @ 0x{s.Player.ActorPtr:X} | A: {F(s.Player.X)}, {F(s.Player.Y)}, {F(s.Player.Z)} | B: {F(s.Player.B?.X)}, {F(s.Player.B?.Y)}, {F(s.Player.B?.Z)}";
        string text=$"Durum: {s.Status} | PID: {s.Pid} | Mob adayları: {s.Monsters.Count}\r\n{player}\r\nMesafeler: mob A ↔ oyuncu A ve mob B ↔ oyuncu B, XZ düzlemi. Ham birim; sıfır koordinat kaynağında mesafe boş. Metre olduğu doğrulanmadı.\r\nKoordinatlar adaydır. A/B uyuşmazlığını ve oyuncu koordinatlarını tanı snapshot'ında karşılaştırın.\r\nSHA: {s.Sha256}\r\nContext: {s.Context} | Registry: {s.Tree} | {s.Message}";
        if(info.Text!=text){int start=info.SelectionStart,length=info.SelectionLength;info.Text=text;info.Select(Math.Min(start,text.Length),Math.Min(length,Math.Max(0,text.Length-start)));}
        clock.Text=$"Son okuma {s.Timestamp:HH:mm:ss} · {s.Monsters.Count} mob";
    }
    void Save(object? sender,EventArgs e)
    {
        using var dialog=new SaveFileDialog{Filter="JSON (*.json)|*.json",FileName=$"4unity-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.json"};
        if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new{distance_source="A-A XZ; B-B XZ; raw units",a_offset="0x70",b_offset="0xB0",snapshot=latest},new JsonSerializerOptions{WriteIndented=true}));
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
