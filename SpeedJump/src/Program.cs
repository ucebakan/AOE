namespace SpeedJump;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if(args.Length>0 && args[0]=="--self-test")return SelfTests.Run(args.Length>1?args[1]:"self-test.json");
            if(args.Length>0 && args[0]=="--resolve")
            {using var b=new Binary(args[1]);var p=Profiles.Load(b);Files.Save(Path.Combine(Files.Root,"resolver-result.json"),new{status="VALIDATED",p.Sha256,Profiles.ScanCount,p});return 0;}
            if(args.Length>0 && args[0]=="--probe")
            {using var s=Session.Connect(readOnly:true);Files.Save(args[1],new{status="READ_ONLY_READY",s.Pid,s.Created,s.Base,s.Owner,s.Player,writes=0});return 0;}
            using var mutex=new Mutex(true,@"Local\4UnitySpeedJump",out bool first);
            if(!first)return 1;
            ApplicationConfiguration.Initialize();
            if(args.Contains("--ui-preview"))
            {
                using var form=new MainForm(true);form.Show();Application.DoEvents();
                using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[^1]);
                return form.Controls.OfType<Button>().Count()==2?0:1;
            }
            Application.Run(new MainForm());return 0;
        }
        catch(Exception ex)
        {
            try{Files.Log(ex.ToString());if(args.Length>0)Files.Save(Path.Combine(Files.Root,"last-error.json"),new{error=ex.ToString()});}catch{}
            if(args.Length==0)MessageBox.Show(ex.Message,"4Unity Speed / Jump");return 2;
        }
    }
}

sealed class MainForm : Form
{
    readonly Button speed=new StateButton(),jump=new StateButton();
    readonly Label status=new();
    readonly Engine engine=new();
    readonly System.Windows.Forms.Timer timer=new(){Interval=1000};
    bool busy,closing,allowClose;
    public MainForm(bool preview=false)
    {
        Text="4Unity · Speed / Jump";ClientSize=new(420,192);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(23,26,34);ForeColor=Color.WhiteSmoke;Font=new("Segoe UI",10);
        var title=new Label{Text="4UNITY",Location=new(22,16),AutoSize=true,Font=new("Segoe UI",13,FontStyle.Bold)};
        speed.SetBounds(22,58,180,54);jump.SetBounds(218,58,180,54);
        foreach(var b in new[]{speed,jump}){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.Font=new("Segoe UI",11,FontStyle.Bold);b.Cursor=Cursors.Hand;}
        status.SetBounds(22,128,376,54);status.ForeColor=Color.FromArgb(190,197,211);status.AutoEllipsis=true;
        Controls.AddRange([title,speed,jump,status]);speed.Click+=async(_,_)=>await Work(()=>engine.Toggle(false));jump.Click+=async(_,_)=>await Work(()=>engine.Toggle(true));
        timer.Tick+=async(_,_)=>await Work(engine.Poll);
        if(!preview){Shown+=async(_,_)=>{await Work(engine.Poll);timer.Start();};FormClosing+=ClosingAsync;}
        PaintState();
    }
    async Task Work(Action action)
    {
        if(busy||closing)return;busy=true;PaintState();
        try{await Task.Run(action);}catch(Exception ex){status.Text=ex.Message;}
        finally{busy=false;if(!IsDisposed)PaintState();}
    }
    void PaintState()
    {
        var v=engine.View;speed.Text=v.Speed?"SPEED · AÇIK":"SPEED";jump.Text=v.Jump?"JUMP · AÇIK":"JUMP";
        speed.BackColor=v.Speed?Color.FromArgb(106,79,200):Color.FromArgb(48,53,68);
        jump.BackColor=v.Jump?Color.FromArgb(32,135,116):Color.FromArgb(48,53,68);
        speed.Enabled=jump.Enabled=!busy&&!closing&&v.Ready;status.Text=busy?"Bağlantı kontrol ediliyor…":v.Message;
    }
    async void ClosingAsync(object? sender,FormClosingEventArgs e)
    {
        if(allowClose)return;e.Cancel=true;if(closing)return;closing=true;timer.Stop();speed.Enabled=jump.Enabled=false;
        while(busy)await Task.Delay(50);
        try{status.Text="Özellikler kapatılıyor…";await Task.Run(engine.Dispose);allowClose=true;Close();}
        catch(Exception ex){closing=false;status.Text="Kapatılamadı: "+ex.Message;timer.Start();}
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}

sealed class StateButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?Color.WhiteSmoke:Color.FromArgb(173,181,197),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
    }
}
