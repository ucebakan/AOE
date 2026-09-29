using System.Globalization;

namespace PlayerXYZ;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if(args.Contains("--resolve-profile"))
        {using var b=new Binary(args[1]);Profiles.Load(b);return 0;}
        if(args.Contains("--resolver-test"))return ResolverTests.Run(args[^1]);
        if(args.Contains("--self-test"))
        {
            var values=new[]{123.25f,-4.5f,987.125f};var bytes=Session.Encode(values[0],values[1],values[2]);
            if(bytes.Length!=12||!Session.Decode(bytes).SequenceEqual(values))return 1;
            bool rejected=false;try{Session.Encode(float.NaN,0,0);}catch(ArgumentException){rejected=true;}
            if(!rejected||!MainForm.TryNumber("12,5",out float f)||f!=12.5f)return 1;
            Files.Save(args[^1],new{status="PASS",mapping="X:70/B0, Y(height):74/B4, Z:78/B8",writes=0,checks=new[]{"float32 coordinate encoding","12 bytes only (W preserved)","NaN rejected","decimal comma accepted"}});return 0;
        }
        using var mutex=new Mutex(true,@"Local\PlayerXYZ",out bool first);if(!first)return 1;
        ApplicationConfiguration.Initialize();
        if(args.Contains("--ui-preview"))
        {
            using var form=new MainForm(true);form.Show();Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);
            form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[^1]);return 0;
        }
        Application.Run(new MainForm());return 0;
    }
}

sealed class MainForm:Form
{
    readonly Label player=new(),status=new();
    readonly Label[] fieldLabels=Enumerable.Range(0,6).Select(_=>new Label()).ToArray();
    readonly Label[] cells=Enumerable.Range(0,6).Select(_=>new Label()).ToArray();
    readonly TextBox[] inputs=Enumerable.Range(0,3).Select(_=>new TextBox()).ToArray();
    readonly Button write=new();
    readonly System.Windows.Forms.Timer timer=new(){Interval=100};
    Session? session;bool busy,closing,allowClose;DateTime retry;
    public MainForm(bool preview=false)
    {
        Text="PlayerXYZ";ClientSize=new(670,480);BackColor=Color.FromArgb(23,26,34);ForeColor=Color.WhiteSmoke;
        Font=new("Segoe UI",10);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        AddLabel("PLAYER XYZ",22,16,620,30,13,true);player.SetBounds(22,53,625,25);Controls.Add(player);player.Text="P = bekleniyor…";
        AddLabel("Alan / eksen",22,93,150,25);AddLabel("Canlı adres",182,93,220,25);AddLabel("Float32 değer",424,93,205,25);
        string[] fields={"P+70 · X","P+74 · Y / H","P+78 · Z","P+B0 · X","P+B4 · Y / H","P+B8 · Z"};
        for(int i=0;i<6;i++)
        {
            int y=123+i*28;fieldLabels[i].Text=fields[i];fieldLabels[i].SetBounds(22,y,150,25);Controls.Add(fieldLabels[i]);
            cells[i].SetBounds(182,y,465,25);cells[i].Font=new("Consolas",10);cells[i].Text="—";Controls.Add(cells[i]);
        }
        AddLabel("Hedef koordinatlar · tek seferlik yazım",22,310,620,25);
        string[] names={"X","Y (yükseklik / H)","Z (oyun Y)"};
        for(int i=0;i<3;i++)
        {
            AddLabel(names[i],22+i*210,341,200,23);inputs[i].SetBounds(22+i*210,366,195,28);
            inputs[i].BackColor=Color.FromArgb(48,53,68);inputs[i].ForeColor=Color.White;inputs[i].BorderStyle=BorderStyle.FixedSingle;
            Controls.Add(inputs[i]);
        }
        write.SetBounds(22,408,195,40);write.Text="KOORDİNATLARI YAZ";write.BackColor=Color.FromArgb(106,79,200);write.ForeColor=Color.White;
        write.FlatStyle=FlatStyle.Flat;write.FlatAppearance.BorderSize=0;write.Enabled=false;Controls.Add(write);
        status.SetBounds(233,405,410,65);status.AutoEllipsis=true;status.ForeColor=Color.FromArgb(190,197,211);status.Text="4Unity bekleniyor…";Controls.Add(status);
        write.Click+=async(_,_)=>await WriteAsync();timer.Tick+=async(_,_)=>await Poll();
        if(!preview){Shown+=async(_,_)=>{await Poll();timer.Start();};FormClosing+=CloseAsync;}
    }
    void AddLabel(string text,int x,int y,int w,int h,int size=10,bool bold=false)
    {Controls.Add(new Label{Text=text,Bounds=new(x,y,w,h),Font=new("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)});}
    public static bool TryNumber(string text,out float number)=>float.TryParse(text.Trim().Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&float.IsFinite(number);
    async Task Poll()
    {
        if(busy||closing||session is null&&DateTime.UtcNow<retry)return;busy=true;write.Enabled=false;
        try
        {
            if(session is null){status.Text="Güncel local P aranıyor…";session=await Task.Run(()=>Session.Connect());status.Text="Bağlı · canlı okuma (100 ms)";}
            var snapshot=await Task.Run(session.Snapshot);player.Text=$"PID {session.Pid}    P = 0x{snapshot.Player:X}";
            int[] offsets=session.Profile.CoordinateA.Concat(session.Profile.CoordinateB).ToArray();
            for(int i=0;i<6;i++)fieldLabels[i].Text=$"P+{offsets[i]:X} · {new[]{"X","Y / H","Z"}[i%3]}";
            for(int i=0;i<6;i++)cells[i].Text=("0x"+(snapshot.Player+offsets[i]).ToString("X")).PadRight(22)+(i<3?snapshot.A[i]:snapshot.B[i-3]).ToString("G9",CultureInfo.InvariantCulture);
        }
        catch(Exception ex)
        {
            session?.Dispose();session=null;player.Text="P = doğrulanamadı";foreach(var c in cells)c.Text="—";
            status.Text=ex.Message;Files.Log(ex.Message);retry=DateTime.UtcNow.AddSeconds(10);
        }
        finally{busy=false;write.Enabled=!closing&&session is not null;}
    }
    async Task WriteAsync()
    {
        if(busy||closing||session is null)return;
        var v=new float[3];for(int i=0;i<3;i++)if(!TryNumber(inputs[i].Text,out v[i])){status.Text="X, Y ve Z için geçerli sayılar gir.";inputs[i].Focus();return;}
        busy=true;write.Enabled=false;
        try{await Task.Run(()=>session.SetCoordinates(v[0],v[1],v[2]));status.Text="Altı adres yazıldı ve doğrulandı.";}
        catch(Exception ex){status.Text="Yazılamadı: "+ex.Message;Files.Log(ex.ToString());}
        finally{busy=false;write.Enabled=!closing&&session is not null;}
        await Poll();
    }
    async void CloseAsync(object? sender,FormClosingEventArgs e)
    {
        if(allowClose)return;e.Cancel=true;if(closing)return;closing=true;timer.Stop();write.Enabled=false;
        while(busy)await Task.Delay(50);session?.Dispose();session=null;allowClose=true;Close();
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
