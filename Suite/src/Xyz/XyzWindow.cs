using System.Globalization;
using UnityTools.Controls;

namespace UnityTools.Xyz;

sealed class XyzWindow : Form
{
    readonly IXyzClient client;
    readonly string cache;
    readonly Label connection = Responsive.Text("Ana uygulamaya bağlanılıyor…"), notice = Responsive.Text("Noktalar ekledikçe otomatik saklanır. Liste kaydet ile ayrı bir dosya oluşturabilirsin.");
    internal readonly TextBox[] Inputs = Enumerable.Range(0, 3).Select(_ => (TextBox)new PlayerXYZ.CoordinateTextBox()).ToArray();
    internal readonly TextBox[] LiveInputs = Enumerable.Range(0, 3).Select(_ => new TextBox { ReadOnly = true, TabStop = false, Text = "—" }).ToArray();
    internal readonly TextBox PointName = new() { MaxLength = 80 };
    internal readonly DataGridView Grid = new();
    internal readonly Button CopyLive = MakeButton("Konumumu hedefe kopyala"), Teleport = MakeButton("Işınlan"), AddPoint = MakeButton("Listeye ekle");
    readonly Button remove = MakeButton("Seçileni sil"), save = MakeButton("Liste kaydet…"), load = MakeButton("Liste yükle…"), close = MakeButton("Kapat");
    readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    readonly List<SavedPoint> points = new();
    readonly bool polling, reuseOnClose;
    readonly System.Windows.Forms.Timer parentWatch = new() { Interval = 1000 };
    readonly System.Diagnostics.Process? parent;
    bool busy, reading, ready, blocked, closing, dirty, allowExit;
    string listName = "Son noktalar";
    internal IReadOnlyList<SavedPoint> Points => points;

    internal XyzWindow(IXyzClient client, string directory, bool polling = true, bool reuseOnClose = false, int parentPid = 0)
    {
        this.client = client; this.polling = polling; this.reuseOnClose = reuseOnClose; cache = Path.Combine(directory, "last-points.json");
        if (parentPid > 0) parent = System.Diagnostics.Process.GetProcessById(parentPid);
        Text = "4UnityTools · Player XYZ"; Font = new("Segoe UI", 10);
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(380, 760); MinimumSize = new(320, 420); TopMost = true;
        StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var stack = new ContentStack(); scroll.Controls.Add(stack); Controls.Add(scroll);
        stack.AddRow(Responsive.Text("PLAYER XYZ", 18, true)); stack.AddRow(connection);
        stack.AddRow(Responsive.Text("Kendi konumum · canlı", 11, true));
        var liveCoordinates = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        for (int i = 0; i < 3; i++)
        {
            liveCoordinates.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            liveCoordinates.Controls.Add(Responsive.Text(new[] { "X", "Y · yükseklik", "Z · düzlem" }[i]), i, 0);
            LiveInputs[i].Dock = DockStyle.Fill; LiveInputs[i].Margin = new(3, 6, 10, 10);
            liveCoordinates.Controls.Add(LiveInputs[i], i, 1);
        }
        stack.AddRow(liveCoordinates); stack.AddRow(Responsive.Text("Hedef konum · gideceğim yer", 11, true));
        var coordinates = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };
        for (int i = 0; i < 3; i++)
        {
            coordinates.ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            coordinates.Controls.Add(Responsive.Text(new[] { "X", "Y · yükseklik", "Z · düzlem" }[i]), i, 0);
            Inputs[i].Dock = DockStyle.Fill; Inputs[i].Margin = new(3, 6, 10, 10);
            coordinates.Controls.Add(Inputs[i], i, 1);
        }
        stack.AddRow(coordinates); stack.AddRow(Responsive.Flow(CopyLive, Teleport));
        stack.AddRow(Responsive.Text("Boş hedef kutularına X, Y, Z gir; Işınlan veya Listeye ekle düğmesini kullan. Canlı konum hedefini değiştirmez.", 9));
        stack.AddRow(Responsive.Text("Nokta adı", 11, true)); PointName.Dock = DockStyle.Fill; stack.AddRow(PointName);
        stack.AddRow(Responsive.Flow(AddPoint, remove));
        stack.AddRow(Responsive.Flow(save, load));
        Grid.AllowUserToAddRows = Grid.AllowUserToDeleteRows = Grid.AllowUserToResizeRows = false;
        Grid.ReadOnly = true; Grid.MultiSelect = false; Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Grid.RowHeadersVisible = false; Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        Grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        Grid.Height = 230; Grid.Margin = new(3, 8, 3, 8); Grid.Dock = DockStyle.Fill;
        Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Kayıtlı noktalar", FillWeight = 135, MinimumWidth = 130,
            DefaultCellStyle = new() { WrapMode = DataGridViewTriState.True, Padding = new(5, 5, 5, 5) } });
        foreach (string axis in new[] { "X", "Y", "Z" })
            Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = axis, HeaderText = axis, Visible = false, FillWeight = 95, MinimumWidth = 75,
                DefaultCellStyle = new() { Format = "G9", FormatProvider = CultureInfo.InvariantCulture } });
        Grid.Columns.Add(new DataGridViewButtonColumn { Name = "Go", HeaderText = "Git", Text = "↗", UseColumnTextForButtonValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 48, MinimumWidth = 48, FlatStyle = FlatStyle.Flat });
        stack.AddRow(Grid); stack.AddRow(notice); stack.AddRow(Responsive.Flow(close));
        Palette.Apply(this); connection.ForeColor = Palette.Blue; notice.ForeColor = Palette.Muted;
        foreach (var input in Inputs) input.TextChanged += (_, _) => UpdateControls();
        CopyLive.Click += async (_, _) => await CopyLiveAsync();
        Teleport.Click += async (_, _) => await TeleportAsync();
        AddPoint.Click += (_, _) => AddCurrentPoint(); remove.Click += (_, _) => RemoveSelected();
        save.Click += (_, _) => SaveList(); load.Click += (_, _) => OpenList(); close.Click += (_, _) => Close();
        Grid.CellContentClick += async (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 4) await TeleportPointAsync(e.RowIndex); };
        Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex != 4) SetTarget(points[e.RowIndex].Values); };
        timer.Tick += async (_, _) => await RefreshAsync();
        Shown += async (_, _) =>
        {
            BeginInvoke(() => WindowLayer.PlaceTopmost(this)); FitScreen();
            if (reuseOnClose && client is PipeClient pipe)
                try { await pipe.RegisterWindowAsync(Handle); } catch(Exception ex) { notice.Text="Pencere bağlantısı: "+ex.Message; }
            if (parent is not null) parentWatch.Start();
            if (polling) { await RefreshAsync(); if (!closing && Visible) timer.Start(); }
        };
        parentWatch.Tick += (_, _) => { if (parent?.HasExited == true) ClosePermanently(); };
        DpiChanged += (_, _) => BeginInvoke(FitScreen);
        FormClosing += (_, e) =>
        {
            if (busy) { e.Cancel = true; notice.Text = "Süren işlemin tamamlanmasını bekle."; return; }
            if (dirty && MessageBox.Show(this, "Son değişiklikler kaydedilemedi. Yine de kapatılsın mı?", "Player XYZ", MessageBoxButtons.YesNo) != DialogResult.Yes)
            { e.Cancel = true; return; }
            if (reuseOnClose && !allowExit) { e.Cancel = true; timer.Stop(); Hide(); return; }
            closing = true; timer.Stop();
        };
        try { if (File.Exists(cache)) points.AddRange(PointStore.Load(cache)); }
        catch (Exception ex) { notice.Text = "Son liste okunamadı: " + ex.Message; }
        RebuildGrid(); UpdateControls();
    }
    static Button MakeButton(string text) => new() { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new(10, 7, 10, 7), MinimumSize = new(90, 36) };
    void FitScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        if (WindowState == FormWindowState.Normal) Size = new(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
    }
    void UpdateControls()
    {
        bool valid = Inputs.All(x => PlayerXYZ.CoordinateTextBox.TryValue(x.Text, out _));
        CopyLive.Enabled = !busy && ready; Teleport.Enabled = !busy && ready && !blocked && valid;
        AddPoint.Enabled = !busy && valid; remove.Enabled = !busy && points.Count > 0;
        save.Enabled = load.Enabled = !busy;
        foreach (var input in Inputs) input.ReadOnly = busy;
        PointName.ReadOnly = busy; Grid.Enabled = !busy;
    }
    void ShowReply(Reply reply)
    {
        ready = reply.Ready; blocked = reply.Blocked; connection.Text = reply.Message;
        for (int i = 0; i < 3; i++) LiveInputs[i].Text = reply.Ready && reply.Live is { Length: 3 }
            ? ((double)reply.Live[i]).ToString("0.##############################", CultureInfo.InvariantCulture) : "—";
    }
    internal async Task RefreshAsync()
    {
        if (busy || reading || closing) return; reading = true;
        try { var reply = await client.SendAsync("snapshot"); if (!closing && !busy) ShowReply(reply); }
        catch (Exception ex) { if (!closing) { ready = false; connection.Text = "Ana uygulama bağlantısı yok · " + ex.Message; } }
        finally { reading = false; if (!closing) UpdateControls(); }
    }
    internal async Task CopyLiveAsync()
    {
        if (busy || closing) return; busy = true; UpdateControls();
        try
        {
            var reply = await client.SendAsync("snapshot"); ShowReply(reply);
            if (!reply.Ready || reply.Live is not { Length: 3 }) throw new IOException("Canlı koordinatlar doğrulanamadı.");
            SetTarget(reply.Live); notice.Text = "Canlı konum hedef kutularına kopyalandı; ışınlanma yapılmadı.";
        }
        catch (Exception ex) { notice.Text = ex.Message; }
        finally { busy = false; UpdateControls(); if(allowExit)Close(); }
    }
    internal void SetTarget(float[] values)
    {
        string[] text = values.Select(v => ((double)v).ToString("0.##############################", CultureInfo.InvariantCulture)).ToArray();
        if (text.Where((t, i) => !PlayerXYZ.CoordinateTextBox.TryValue(t, out float parsed) || parsed != values[i]).Any())
            throw new IOException("Koordinat giriş alanı için değer çok uzun veya çok küçük.");
        for (int i = 0; i < 3; i++) Inputs[i].Text = text[i];
    }
    float[] ReadTarget()
    {
        var values = new float[3];
        for (int i = 0; i < 3; i++) if (!PlayerXYZ.CoordinateTextBox.TryValue(Inputs[i].Text, out values[i])) throw new IOException("X, Y ve Z için geçerli sayılar gir.");
        return values;
    }
    internal async Task TeleportPointAsync(int index)
    {
        if (busy || closing || index < 0 || index >= points.Count) return;
        try { SetTarget(points[index].Values); await TeleportAsync(); }
        catch (Exception ex) { notice.Text = ex.Message; }
    }
    internal async Task TeleportAsync()
    {
        if (busy || closing) return;
        // A list click can arrive while disconnected. Revalidate on the parent instead of trusting cached UI state.
        try
        {
            var target = ReadTarget(); busy = true; UpdateControls();
            var reply = await client.SendAsync("teleport", target); ShowReply(reply); notice.Text = reply.Message;
        }
        catch (Exception ex) { notice.Text = "Işınlanma uygulanamadı: " + ex.Message; }
        finally { busy = false; UpdateControls(); if(allowExit)Close(); }
    }
    internal void AddCurrentPoint()
    {
        if (busy) return;
        try
        {
            var values = ReadTarget(); string name = PointName.Text.Trim(); if (name.Length == 0) name = "Nokta " + (points.Count + 1);
            var next = new SavedPoint(name, values[0], values[1], values[2]); PointStore.Validate(points.Append(next).ToArray());
            points.Add(next); PointName.Clear(); RebuildGrid(); Persist();
        }
        catch (Exception ex) { notice.Text = ex.Message; }
    }
    void RemoveSelected()
    {
        if (busy || Grid.SelectedRows.Count == 0) return;
        points.RemoveAt(Grid.SelectedRows[0].Index); RebuildGrid(); Persist();
    }
    void RebuildGrid()
    {
        Grid.Rows.Clear(); foreach (var point in points)
            Grid.Rows.Add(point.Name + "\n" + string.Join("\n", point.Values.Select((v, i) => new[] { "X", "Y", "Z" }[i] + " = " + v.ToString("G9", CultureInfo.InvariantCulture))), point.X, point.Y, point.Z);
        Grid.ClearSelection(); Text = "4UnityTools · Player XYZ · " + listName; UpdateControls();
    }
    void Persist()
    {
        dirty = true;
        try { PointStore.Save(cache, points); dirty = false; notice.Text = $"{points.Count} nokta · otomatik saklandı."; }
        catch (Exception ex) { notice.Text = "Otomatik kayıt başarısız. Liste kaydet ile başka konuma kaydedebilirsin: " + ex.Message; }
    }
    internal void Export(string path)
    { PointStore.Save(path, points); listName = Path.GetFileNameWithoutExtension(path); dirty = false; RebuildGrid(); notice.Text = "Liste kaydedildi: " + Path.GetFileName(path); }
    internal void Import(string path)
    {
        var loaded = PointStore.Load(path); // Parse and validate fully before replacing the visible list.
        points.Clear(); points.AddRange(loaded); listName = Path.GetFileNameWithoutExtension(path); RebuildGrid(); Persist();
    }
    void SaveList()
    {
        using var dialog = new SaveFileDialog { Filter = "XYZ nokta listesi (*.json)|*.json", DefaultExt = "json", AddExtension = true,
            FileName = listName + ".json", InitialDirectory = Path.GetDirectoryName(cache) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Export(dialog.FileName); } catch (Exception ex) { notice.Text = "Kaydedilemedi: " + ex.Message; }
    }
    void OpenList()
    {
        using var dialog = new OpenFileDialog { Filter = "XYZ nokta listesi (*.json)|*.json", InitialDirectory = Path.GetDirectoryName(cache) };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { Import(dialog.FileName); } catch (Exception ex) { notice.Text = "Liste açılamadı: " + ex.Message; }
    }
    internal void ClosePermanently() { allowExit = true; if (!busy) Close(); }
    internal void Reopen()
    {
        if (closing) return; Show(); WindowState = FormWindowState.Normal; Activate(); WindowLayer.PlaceTopmost(this);
        if (polling) { _ = RefreshAsync(); timer.Start(); }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WindowBridge.ReopenMessage) { Reopen(); return; }
        if (message.Msg == WindowBridge.ExitMessage) { ClosePermanently(); return; }
        base.WndProc(ref message);
    }
    protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); parentWatch.Dispose(); parent?.Dispose(); } base.Dispose(disposing); }
}
