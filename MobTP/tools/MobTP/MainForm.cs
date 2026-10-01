using UnityMonsterList;

namespace MobTP;

sealed class MobGrid : DataGridView { public MobGrid(){DoubleBuffered=true;} }
sealed partial class MainForm : Form
{
    readonly Label state=new(){Dock=DockStyle.Fill,Text="Oyuncu ve moblar bekleniyor…",Font=new("Segoe UI",11),AutoEllipsis=true};
    readonly Label player=new(){Dock=DockStyle.Fill,Font=new("Consolas",11),Text="Oyuncu XYZ: —"};
    readonly Label count=new(){Dock=DockStyle.Fill,Font=new("Segoe UI",12,FontStyle.Bold)};
    readonly TextBox result=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text="Düğme, Home sınırına uyan mobları çevrene taşır."};
    readonly Button tp=new(){Text="UYGUN MOBLARI YANIMA GETİR",AutoSize=true,Height=45,Padding=new(12),Enabled=false};
    readonly NumericUpDown spread=new(){Minimum=1,Maximum=10,DecimalPlaces=1,Increment=0.5m,Value=2,Width=75};
    readonly CheckBox show=new(){Text="Arka plandaki mob listesini göster",AutoSize=true};
    readonly ComboBox distanceOrder=new UnityTools.Controls.ReadableComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=170};
    readonly TextBox keyBox=new(){ReadOnly=true,Width=180,Text="Tıklayıp tuşa basın"};
    readonly Label keyStatus=new(){AutoSize=true,Text="Kısayol: atanmamış",Margin=new(12,8,3,0)};
    KeyBinding? pendingKey;
    HotkeyRegistration? hotkey;
    UserSettings settings=new();
    readonly MobGrid grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,Visible=false};
    readonly System.Windows.Forms.Timer timer=new(){Interval=750};
    readonly SemaphoreSlim gate=new(1,1);
    readonly CancellationTokenSource cancellation=new();
    readonly Dictionary<ulong,DataGridViewRow> rows=[];
    bool closing,moving,allowClose;
    World? last;
    public MainForm(bool preview=false)
    {
        Text="MobTP 2.1 · 4Unity";ClientSize=new(1030,700);MinimumSize=new(1000,650);StartPosition=FormStartPosition.CenterScreen;DoubleBuffered=true;
        if(!preview){settings=UserSettings.Load();spread.Value=settings.Spread;}
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new(16)};
        foreach(float height in new[]{36f,32f,60f,65f,45f,75f,30f})layout.RowStyles.Add(new(SizeType.Absolute,height));
        layout.RowStyles.Add(new(SizeType.Percent,100));Controls.Add(layout);
        layout.Controls.Add(state,0,0);layout.Controls.Add(player,0,1);layout.Controls.Add(count,0,2);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};
        buttons.Controls.AddRange([tp,new Label{Text="Çevre mesafesi",AutoSize=true,Margin=new(20,20,3,0)},spread,new Label{Text="Home sınırı: 50 XZ",AutoSize=true,Margin=new(16,20,3,0)}]);
        var keys=new FlowLayoutPanel{Dock=DockStyle.Fill};var assign=new Button{Text="Tuşu ata",AutoSize=true};var clear=new Button{Text="Kaldır",AutoSize=true};var retry=new Button{Text="Profili yeniden kontrol et",AutoSize=true};
        keys.Controls.AddRange([keyBox,assign,clear,keyStatus,retry]);
        layout.Controls.Add(buttons,0,3);layout.Controls.Add(keys,0,4);layout.Controls.Add(result,0,5);var listOptions=new FlowLayoutPanel{Dock=DockStyle.Fill};
        distanceOrder.Items.AddRange(["Yakından uzağa","Uzaktan yakına"]);distanceOrder.SelectedIndex=settings.DistanceDescending?1:0;
        listOptions.Controls.AddRange([show,new Label{Text="Oyuncuya mesafe (XZ)",AutoSize=true,Margin=new(12,5,3,0)},distanceOrder]);
        layout.Controls.Add(listOptions,0,6);layout.Controls.Add(grid,0,7);
        foreach(string title in new[]{"Mob ID","Mob XYZ","Home XYZ","Oyuncu → Home","Oyuncu → Mob (XZ)","Durum"})grid.Columns.Add(title,title);
        grid.Columns[1].FillWeight=170;grid.Columns[2].FillWeight=170;
        foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
        grid.Columns[4].SortMode=DataGridViewColumnSortMode.Programmatic;
        grid.Columns[4].ValueType=typeof(double);grid.Columns[4].DefaultCellStyle.Format="F2";grid.Columns[4].DefaultCellStyle.NullValue="—";
        distanceOrder.SelectedIndexChanged+=(_,_)=>{settings=settings with{DistanceDescending=distanceOrder.SelectedIndex==1};if(last is not null)Render(last);if(!preview)try{settings.Save();}catch(Exception ex){result.Text=ex.Message;}};
        grid.ColumnHeaderMouseClick+=(_,e)=>{if(e.ColumnIndex==4)distanceOrder.SelectedIndex=1-distanceOrder.SelectedIndex;};
        show.CheckedChanged+=(_,_)=>grid.Visible=show.Checked;
        tp.Click+=async(_,_)=>await Teleport();timer.Tick+=async(_,_)=>await RefreshWorld();
        keyBox.KeyDown+=(_,e)=>{e.SuppressKeyPress=true;e.Handled=true;var candidate=new KeyBinding((int)e.KeyCode,(e.Control?2u:0)|(e.Alt?1u:0)|(e.Shift?4u:0));if(candidate.Valid){pendingKey=candidate;keyBox.Text=candidate.ToString();}};
        assign.Click+=(_,_)=>{if(pendingKey is not null)AssignKey(pendingKey);};
        clear.Click+=(_,_)=>{hotkey?.Dispose();hotkey=null;settings=settings with{Hotkey=null};keyStatus.Text="Kısayol: atanmamış";try{settings.Save();}catch(Exception ex){result.Text=ex.Message;}};
        retry.Click+=async(_,_)=>{ProfileStore.Retry();await RefreshWorld();};
        spread.ValueChanged+=(_,_)=>{settings=settings with{Spread=spread.Value};if(last is not null)Render(last);if(!preview)try{settings.Save();}catch(Exception ex){result.Text=ex.Message;}};
        if(!preview)Shown+=async(_,_)=>{if(settings.Hotkey is not null)AssignKey(settings.Hotkey);await RefreshWorld();timer.Start();};
        FormClosing+=async(_,e)=>{
            if(allowClose)return;e.Cancel=true;if(closing)return;
            closing=true;timer.Stop();cancellation.Cancel();hotkey?.Dispose();
            await gate.WaitAsync();gate.Release();allowClose=true;Close();
        };
    }
    void AssignKey(KeyBinding binding)
    {
        try{hotkey??=new(Handle);hotkey.Assign(binding);settings=settings with{Hotkey=binding};keyBox.Text=binding.ToString();keyStatus.Text="Oyun içi: "+binding;settings.Save();}
        catch(Exception ex){result.Text=ex.Message;}
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x0312&&hotkey?.Owns(message.WParam)==true)
        {
            int? pid=last?.Source?.Pid;
            if(!closing&&!moving&&HotkeyRegistration.GameForeground(pid))_ = Teleport(pid);
        }
        base.WndProc(ref message);
    }
    async Task RefreshWorld()
    {
        if(closing||moving||!await gate.WaitAsync(0))return;
        try{var w=await Task.Run(Engine.Capture);if(!closing)Render(w);}
        finally{gate.Release();}
    }
    async Task Teleport(int? hotkeyPid=null)
    {
        if (UnityTools.Controls.OperationGate.Blocked) return;
        if(moving||closing)return;moving=true;tp.Enabled=false;spread.Enabled=false;
        double radius=(double)spread.Value;
        await gate.WaitAsync();
        try
        {
            if(closing)return;
            result.Text="Güncel moblar doğrulanıyor ve uygun olanlar taşınıyor…";
            var outcome=await Task.Run(()=>Engine.Teleport(radius,cancellation.Token,hotkeyPid));
            if(!closing)result.Text=outcome.Message+Environment.NewLine+"Kayıt: "+outcome.Log;
        }
        catch(Exception ex){if(!closing)result.Text=ex.Message;}
        finally{gate.Release();moving=false;if(!closing){spread.Enabled=true;await RefreshWorld();}}
    }
    internal void Render(World w)
    {
        last=w;state.Text=w.Status+" · "+w.Time.ToString("HH:mm:ss");
        player.Text=w.Player is null?"Oyuncu XYZ: —":$"Oyuncu XYZ: {w.Player.X:F3} / {w.Player.Y:F3} / {w.Player.Z:F3}";
        var plan=Placement.Plan(w,(double)spread.Value);int eligible=plan.Count(p=>p.Eligible);
        count.Text=$"Yüklenen mob: {w.Mobs.Count}     Home ≤ 50: {plan.Length}\r\nTeleport koşullarını sağlayan: {eligible}";
        tp.Enabled=!moving&&w.Player is not null&&eligible>0;
        int top=grid.FirstDisplayedScrollingRowIndex;
        var ids=w.Mobs.Select(m=>m.Mob.ActorPtr).ToHashSet();
        grid.SuspendLayout();
        try
        {
            foreach(var id in rows.Keys.Where(k=>!ids.Contains(k)).ToArray()){grid.Rows.Remove(rows[id]);rows.Remove(id);}
            foreach(var m in w.Mobs)
            {
                if(!rows.TryGetValue(m.Mob.ActorPtr,out var row)){row=grid.Rows[grid.Rows.Add()];rows[m.Mob.ActorPtr]=row;}
                string XYZ(Position? p)=>p is null?"—":$"{p.X:F1} / {p.Y:F1} / {p.Z:F1}";
                var placement=plan.FirstOrDefault(p=>p.Mob.Mob.ActorPtr==m.Mob.ActorPtr);
                object[] values=[m.Mob.EntityId,XYZ(m.A),XYZ(m.Home),m.PlayerHomeXZ?.ToString("F2")??"—",w.Player is null?null!:MobCapture.Distance(Placement.Player(w.Player),m.A,true)!,m.PlayerHomeXZ is null?"Home yok":placement?.Eligible==true?"TP uygun":placement is not null?"Hedef sınır dışında":"Home sınırı dışında"];
                for(int i=0;i<values.Length;i++)if(!Equals(row.Cells[i].Value,values[i]))row.Cells[i].Value=values[i];
            }
            grid.Sort(new DistanceRowComparer(settings.DistanceDescending));
            grid.Columns[4].HeaderCell.SortGlyphDirection=settings.DistanceDescending?SortOrder.Descending:SortOrder.Ascending;
            if(top>=0&&grid.Rows.Count>0)grid.FirstDisplayedScrollingRowIndex=Math.Min(top,grid.Rows.Count-1);
        }
        finally{grid.ResumeLayout();}
    }
    sealed class DistanceRowComparer(bool descending) : System.Collections.IComparer
    {
        public int Compare(object? x,object? y)
        {
            var a=(DataGridViewRow)x!;var b=(DataGridViewRow)y!;
            double? da=a.Cells[4].Value as double?,db=b.Cells[4].Value as double?;
            bool va=da.HasValue&&double.IsFinite(da.Value),vb=db.HasValue&&double.IsFinite(db.Value);
            if(va!=vb)return va?-1:1;
            int c=va?da!.Value.CompareTo(db!.Value):0;
            if(c!=0)return descending?-c:c;
            return Convert.ToUInt32(a.Cells[0].Value).CompareTo(Convert.ToUInt32(b.Cells[0].Value));
        }
    }
    internal void VerifyDistanceSorting()
    {
        foreach(int order in new[]{0,1})
        {
            distanceOrder.SelectedIndex=order;
            var distances=grid.Rows.Cast<DataGridViewRow>().Select(r=>Convert.ToDouble(r.Cells[4].Value)).ToArray();
            if(!distances.SequenceEqual(order==0?distances.OrderBy(x=>x):distances.OrderByDescending(x=>x)))throw new InvalidOperationException("Distance sort failed");
        }
        distanceOrder.SelectedIndex=0;
    }
    internal void ShowList()=>show.Checked=true;
    protected override void Dispose(bool disposing){if(disposing){timer.Dispose();cancellation.Cancel();}base.Dispose(disposing);}
}
