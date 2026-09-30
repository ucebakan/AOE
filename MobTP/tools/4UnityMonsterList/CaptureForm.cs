namespace UnityMonsterList;

sealed class CaptureForm : Form
{
    readonly TextBox offsets=new(){Width=420,PlaceholderText="Home aday offsetleri (hex, virgülle); bilinmiyorsa boş"};
    readonly NumericUpDown radius=new(){Minimum=1,Maximum=1000,Value=50,DecimalPlaces=1,Width=75};
    readonly TextBox notes=new(){Width=400,PlaceholderText="Gözlem notu (isteğe bağlı)",MaxLength=2000};
    readonly ComboBox events=new(){Width=245,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Button start=new(){Text="Kaydı başlat",AutoSize=true},stop=new(){Text="Kaydı bitir",Enabled=false,AutoSize=true},mark=new(){Text="Gözlenen olayı işaretle",Enabled=false,AutoSize=true};
    readonly Label status=new(){AutoSize=false,Dock=DockStyle.Fill,Padding=new Padding(12),Font=new Font("Consolas",10)};
    readonly System.Windows.Forms.Timer timer=new(){Interval=100};
    readonly Snapshot snapshot;
    readonly Monster monster;
    MobCapture? capture;
    bool busy,closing;
    public CaptureForm(Snapshot snapshot,Monster monster)
    {
        this.snapshot=snapshot;this.monster=monster;
        Text=$"Tek mob gözlemi — key {monster.EntityId} — 0x{monster.ActorPtr:X}";Width=1120;Height=590;StartPosition=FormStartPosition.CenterParent;
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=110,Padding=new Padding(8)};
        top.Controls.AddRange([offsets,new Label{Text="Home radius XZ",AutoSize=true},radius,start,stop,events,notes,mark]);Controls.Add(status);Controls.Add(top);
        events.Items.AddRange(MobCapture.Events);events.SelectedIndex=-1;
        status.Text="HOME: Actor+12D8/12DC/12E0, radius varsayılan 50. Güncel alan değeri kullanılır.\r\nÖĞRENİLMİŞ MERKEZ — salt okunur. İlk tamamlanan dönüşte öğrenilir, ikinciyle teyit edilir.\r\n+12D8 hedef XYZ ve +12FC dönüş bayrağı adayları otomatik kaydedilir.\r\nTam Actor (0x1320 byte) ve tek seviyeli pointer-adayı bölgeleri kaydedilir.\r\nHome kaynağı seçildi; manuel ek aday kutusu boş kalabilir.\r\nOlayları gerçekten gözlemledikten sonra İşaretle'ye basın. Kimlik koparsa kayıt biter.";
        start.Click+=async(_,_)=>await Start();stop.Click+=(_,_)=>Finish();mark.Click+=(_,_)=>Mark();timer.Tick+=async(_,_)=>await Tick();
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;closing=true;timer.Stop();}else{if(!Finish()){e.Cancel=true;closing=false;return;}capture?.Dispose();capture=null;}};
    }
    async Task Start()
    {
        start.Enabled=false;offsets.Enabled=false;radius.Enabled=false;busy=true;
        try
        {
            int[] values=MobCapture.ParseOffsets(offsets.Text);
            double selectedRadius=(double)radius.Value;
            capture=await Task.Run(()=>new MobCapture(snapshot,monster,values,radius:selectedRadius));
            stop.Enabled=true;mark.Enabled=true;status.Text="Kayıt başladı.\r\n"+capture.DirectoryPath;timer.Start();
        }
        catch(Exception ex){status.Text="Kayıt başlatılamadı: "+ex.Message;start.Enabled=true;offsets.Enabled=true;radius.Enabled=true;}
        finally{busy=false;if(closing)Close();}
    }
    async Task Tick()
    {
        if(busy || capture is null)return;busy=true;
        try
        {
            var sample=await Task.Run(capture.Sample);
            if(sample is not null)status.Text=HomeText(sample.Home)+CenterText(sample.LearnedCenter)+Environment.NewLine+$"{capture.Status}\r\nSample: {sample.Index}   Geçen: {sample.ElapsedMs:F0} ms   Okuma: {sample.ReadDurationMs:F2} ms\r\nRTTI: {sample.Before.Identity?.Rtti ?? sample.Before.Status}\r\nA: {sample.A}\r\nB: {sample.B}\r\nA↔B: {sample.DistanceAB3D:F4}   Bağlı bölge: {sample.LinkedMemory?.Blocks.Count(x=>x.BytesBase64 is not null)}   Sorgu: {sample.LinkedMemory?.Queries}/{sample.LinkedMemory?.EligibleSlots}   Eksik: {sample.LinkedMemory?.Truncated}\r\n{capture.DirectoryPath}";
            if(capture.Status!="CAPTURING"){timer.Stop();stop.Enabled=false;mark.Enabled=false;}
        }
        catch(Exception ex){Finish("CAPTURE TERMINATED: "+ex.Message);}
        finally{busy=false;if(closing)Close();}
    }
    void Mark()
    {
        if(capture is null || events.SelectedItem is not string label)return;
        try{capture.Mark(label,notes.Text);notes.Clear();events.SelectedIndex=-1;}
        catch(Exception ex){Finish("CAPTURE TERMINATED: "+ex.Message);}
    }
    bool Finish(string reason="CAPTURE STOPPED: user")
    {
        timer.Stop();stop.Enabled=false;mark.Enabled=false;
        try{capture?.Stop(reason);if(capture is not null){status.Text=HomeText(capture.LastHome)+CenterText(capture.LastCenter)+"\r\n"+capture.Status+"\r\n"+capture.DirectoryPath;try{HomeAnalysis.Analyze(capture.DirectoryPath);status.Text+="\r\nHome aday incelemesi tamamlandı; alan rolleri henüz kanıtlanmadı.";}catch(Exception analysisError){status.Text+="\r\nKayıt tamamlandı; aday analizi hatası: "+analysisError.Message;}}return true;}
        catch(Exception ex){status.Text="Dosya sonlandırma hatası: "+ex.Message+"\r\nHam kayıt korundu: "+capture?.DirectoryPath+"\r\nYeniden sonlandır düğmesiyle tekrar deneyebilirsiniz.";stop.Text="Dosyaları yeniden sonlandır";stop.Enabled=true;return false;}
    }
    internal static string HomeText(HomeState? h)=>h?.XYZ is not Position p?"HOME: okunamıyor\r\n":$"HOME XYZ: {p.X:F3} / {p.Y:F3} / {p.Z:F3} (Actor+12D8)\r\nOyuncu → Home XZ: {h.PlayerDistanceXZ?.ToString("F2")??"—"} · Radius: {h.RadiusXZ:F1} · {(h.PlayerWithinRadius is null?"BİLİNMİYOR":h.PlayerWithinRadius.Value?"İÇERİDE":"DIŞARIDA")}\r\n\r\n";
    internal static string CenterText(LearnedCenterInfo? center)
    {
        if(center?.Center is not Position p)return "ÖĞRENİLMİŞ MERKEZ HENÜZ YOK — "+(center?.Detail??"Tam bir dönüş bekleniyor");
        string state=center.CompletedReturns>=2?"TEKRAR TEYİT EDİLDİ":"İLK DÖNÜŞTEN ÖĞRENİLDİ";
        return $"{state} · {center.CompletedReturns} tamamlanmış dönüş\r\nÖğrenilmiş merkez XYZ: {p.X:F3} / {p.Y:F3} / {p.Z:F3}\r\nMob → merkez XZ: {center.MobDistanceXZ?.ToString("F2")??"—"}    Oyuncu → merkez XZ: {center.PlayerDistanceXZ?.ToString("F2")??"—"}\r\nKalıcı Home offseti değildir. {center.Detail}\r\n";
    }
    internal void PreviewCenter(LearnedCenterInfo center)=>status.Text=HomeText(HomeState.Read(true,center.Center,center.Center,center.Center,50))+CenterText(center)+"\r\nSYNTHETIC UI PREVIEW — canlı oyun verisi değildir.";
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
