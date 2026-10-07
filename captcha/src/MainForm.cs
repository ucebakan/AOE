using System.Diagnostics;
using System.Text.Json;

namespace UnityPuzzleTest;

sealed class MainForm : Form
{
    readonly ComboBox windows = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox automatic = new() { Text = "Kutuyu otomatik bul", Checked = true, AutoSize = true };
    readonly Label status = new(), area = new();
    readonly PictureBox preview = new() { SizeMode = PictureBoxSizeMode.Zoom };
    readonly Button refresh, select, start, stop, demo;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 600 };
    readonly PuzzleFlow flow = new();
    WindowChoice? selected;
    DemoForm? fixture;
    OcrReader? reader;
    MemoryDetector? memory;
    MemoryState? memoryState;
    CancellationTokenSource? connecting;
    string memoryDetail = "Bellek izleme henüz başlamadı";
    Rectangle crop;
    Size clientSize;
    bool running, busy, hotkey;
    string stopKey = "F9", phase = "idle", finderDetail = "";
    int frameCount, clickCount;
    double suspendedAt, suspendedSeconds;
    int generation;
    string lastLog = "";
    readonly string logRoot = Path.Combine(AppContext.BaseDirectory, "logs");
    public MainForm(bool render = false)
    {
        Text = "4Unity Puzzle Test · v1.1"; ClientSize = new(920, 690); MinimumSize = MaximumSize = Size;
        StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 10);
        BackColor = Color.FromArgb(20, 25, 32); ForeColor = Color.FromArgb(235, 240, 245);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        LabelText("Offline test · dört adımlı eşleştirme", 22, 17, 860, 37, 18, true);
        LabelText("Client seç → Bağlan ve başlat. Kutu geldiğinde otomatik aranır.", 24, 65, 870, 28);
        windows.SetBounds(24, 106, 716, 32); Controls.Add(windows);
        refresh = Button("Yenile", 755, 104, 140, 36, (_, _) => RefreshWindows());
        area.SetBounds(24, 151, 870, 30); area.ForeColor = Color.LightSteelBlue; Controls.Add(area);
        select = Button("Alanı elle seç (isteğe bağlı)", 24, 194, 270, 40, async (_, _) => await SelectAreaAsync());
        demo = Button("Yerel deneme aç", 24, 245, 270, 40, (_, _) => OpenDemo());
        automatic.SetBounds(24, 292, 270, 28); Controls.Add(automatic);
        LabelText("Çalışma şekli", 24, 326, 270, 26);
        mode.Items.AddRange(["Yalnız oku · tıklama yok", "Eşleştir · offline test"]); mode.SelectedIndex = 1;
        mode.SetBounds(24, 359, 270, 32); Controls.Add(mode);
        start = Button("Bağlan ve başlat", 24, 405, 154, 44, async (_, _) => await StartRun());
        stop = Button("Durdur · F9", 186, 405, 108, 44, (_, _) => StopRun("Kullanıcı durdurdu")); stop.Enabled = false;
        LabelText("Oyun önde ve görünür kalmalı. Bekleme veya hata nedeni altta gösterilir.", 24, 472, 272, 69);
        LabelText("Ayrı sürüm mevcut farm EXE’sini durdurmaz. Farm bağlantısı birleştirme aşamasında eklenecek.", 24, 544, 272, 80);
        preview.SetBounds(331, 194, 564, 413); preview.BackColor = Color.FromArgb(11, 15, 20); preview.BorderStyle = BorderStyle.FixedSingle; Controls.Add(preview);
        status.SetBounds(24, 631, 872, 44); status.ForeColor = Color.LightCyan; Controls.Add(status);
        status.Text = "Client seçildiğinde Bağlan ve başlat kullan"; area.Text = "Henüz client seçilmedi";
        windows.SelectedIndexChanged += (_, _) =>
        {
            if (running) return;
            selected = windows.SelectedItem as WindowChoice; crop = Rectangle.Empty;
            area.Text = selected == null ? "Henüz client seçilmedi" : $"Seçildi · PID {selected.Pid} · {selected.Path}";
            phase = "selected"; status.Text = "Client seçildi · bağlantıyı başlatmak için Bağlan ve başlat";
        };
        timer.Tick += async (_, _) => await TickAsync();
        if (!render) RefreshWindows();
        else
        {
            windows.Items.Add("Örnek · offline test penceresi"); windows.SelectedIndex = 0;
            area.Text = "Seçildi · PID 1234 · C:\\Games\\4Unity\\TClient.exe";
            using var example = new DemoForm(); using var snapshot = example.Snapshot(); ShowPreview(snapshot);
        }
    }
    void LabelText(string text, int x, int y, int width, int height, int size = 10, bool bold = false)
    {
        Controls.Add(new Label { Text = text, Bounds = new(x, y, width, height), Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ForeColor });
    }
    Button Button(string text, int x, int y, int width, int height, EventHandler action)
    {
        var b = new Button { Text = text, Bounds = new(x, y, width, height), BackColor = Color.FromArgb(42, 52, 65), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderColor = Color.FromArgb(75, 94, 110); b.Click += action; Controls.Add(b); return b;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e); hotkey = Native.RegisterHotKey(Handle, 9, 0x4000, 0x78);
        if (!hotkey) { hotkey = Native.RegisterHotKey(Handle, 9, 0x4006, 0x78); stopKey = "Ctrl+Shift+F9"; }
        stop.Text = "Durdur" + (stopKey == "F9" ? " · F9" : "");
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x312 && m.WParam.ToInt32() == 9) StopRun(stopKey + " ile durduruldu");
        base.WndProc(ref m);
    }
    void RefreshWindows()
    {
        string? previous = selected?.Path;
        windows.Items.Clear(); windows.Items.AddRange(WindowChoice.List());
        if (windows.Items.Count > 0)
        {
            int index = windows.Items.Cast<WindowChoice>().ToList().FindIndex(w => string.Equals(w.Path, previous, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = windows.Items.Cast<WindowChoice>().ToList().FindIndex(w => string.Equals(Path.GetFileName(w.Path), "TClient.exe", StringComparison.OrdinalIgnoreCase));
            windows.SelectedIndex = Math.Max(0, index);
        }
        else status.Text = "Açık test penceresi bulunamadı; yerel denemeyi açabilirsin";
    }
    void OpenDemo()
    {
        fixture?.Close(); fixture?.Dispose(); fixture = new DemoForm(); fixture.Show();
        using var process = Process.GetCurrentProcess();
        var choice = new WindowChoice(fixture.Handle, Environment.ProcessId, fixture.Text, process.MainModule!.FileName!, process.StartTime.ToUniversalTime().Ticks);
        windows.Items.Add(choice); windows.SelectedItem = choice;
        selected = choice; crop = fixture.PuzzleBounds; clientSize = fixture.ClientSize;
        automatic.Checked = true;
        area.Text = "Yerel simülasyon alanı hazır · oyuna bağlanmaz";
        using var sample = fixture.Snapshot(); ShowPreview(sample);
    }
    async Task SelectAreaAsync()
    {
        if (selected == null) { status.Text = "Önce bir test penceresi seç"; return; }
        SetEnabled(false); var window = selected;
        try
        {
            status.Text = "Test penceresinin görüntüsü alınıyor"; Hide(); Native.SetForegroundWindow(window.Handle);
            await Task.Delay(700);
            using var image = window.Capture(null); Show(); Activate();
            using var picker = new CropForm(image);
            if (picker.ShowDialog(this) != DialogResult.OK) { status.Text = "Alan seçimi iptal edildi"; return; }
            crop = picker.Selected; clientSize = image.Size;
            automatic.Checked = false;
            using var panel = image.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb); ShowPreview(panel);
            area.Text = $"Alan hazır · {crop.Width} × {crop.Height} · pencere {clientSize.Width} × {clientSize.Height}";
            status.Text = "Elle seçilen alan hazır · Bağlan ve başlat kullan"; phase = "manual_area_ready"; WriteState(status.Text, null);
        }
        catch (Exception e) { status.Text = e.Message; phase = "calibration_failed"; WriteState(status.Text, null); SaveError(e); }
        finally { Show(); SetEnabled(true); }
    }
    void SetEnabled(bool enabled)
    {
        windows.Enabled = mode.Enabled = automatic.Enabled = refresh.Enabled = select.Enabled = demo.Enabled = start.Enabled = enabled;
        stop.Enabled = !enabled && running;
    }
    async Task StartRun()
    {
        try
        {
            if (busy) throw new IOException("Önceki okumanın bitmesini bekleyin");
            if (selected == null) throw new IOException("Önce bir client seçin");
            if (!automatic.Checked && crop.IsEmpty) throw new IOException("Otomatik kutu bulmayı açın veya alanı elle seçin");
            if (!hotkey && mode.SelectedIndex == 1) throw new IOException("F9 ve Ctrl+Shift+F9 kullanımda; Yalnız oku modunu kullanın veya çakışan uygulamayı kapatın");
            reader ??= new OcrReader(); flow.Reset(); generation++; running = true;
            frameCount = clickCount = 0; suspendedAt = suspendedSeconds = 0;
            SetEnabled(false); Native.SetForegroundWindow(selected.Handle);
            phase = "connecting"; status.Text = $"Başladı · PID {selected.Pid} · durdurma: {stopKey}"; WriteState(status.Text, null, true);
            connecting?.Dispose(); connecting = new CancellationTokenSource();
            var token = connecting.Token; var window = selected; int ticket = generation;
            phase = "connecting_memory"; status.Text = $"PID {window.Pid} · bellek bağlantısı kontrol ediliyor"; WriteState(status.Text, null, true);
            var linked = await Task.Run(() => MemoryDetector.Connect(window, token));
            if (!running || ticket != generation) { linked.Detector?.Dispose(); return; }
            memory = linked.Detector; memoryDetail = linked.Detail; memoryState = memory?.Snapshot();
            phase = "connected"; status.Text = memoryDetail + $" · durdurma: {stopKey}"; WriteState(status.Text, null, true);
            timer.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (IsDisposed || Disposing) return;
            running = false; timer.Stop(); SetEnabled(true); status.Text = "Başlatılamadı: " + e.Message;
            phase = "start_failed"; WriteState(status.Text, null, true); SaveError(e);
            MessageBox.Show(this, status.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    void StopRun(string reason)
    {
        running = false; generation++; timer.Stop(); flow.Fail(reason);
        connecting?.Cancel(); memory?.Dispose(); memory = null;
        status.Text = reason; phase = "stopped"; SetEnabled(true); WriteState(reason, null);
    }
    async Task TickAsync()
    {
        if (!running || busy || selected == null || reader == null) return;
        busy = true; int ticket = generation; var window = selected;
        try
        {
            if (memory != null)
            {
                memoryState = memory.Snapshot(); memoryDetail = memoryState.Detail;
                if (!memoryState.Valid) { memory.Dispose(); memory = null; }
            }
            if (!window.IsReady(out string reason, automatic.Checked ? null : clientSize, automatic.Checked ? null : crop))
            {
                status.Text = memoryState?.Active == true ? "Bellek: doğrulama açık · " + reason : reason; phase = "waiting_for_window";
                if (suspendedAt == 0) suspendedAt = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                // Focus changes are pauses. Geometry/session failures require explicit recalibration.
                if (!reason.StartsWith("Bekliyor:") && !reason.StartsWith("Test alanının üstünde")) StopRun(reason);
                else WriteState(status.Text, null, true);
                return;
            }
            if (suspendedAt > 0) { suspendedSeconds += Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - suspendedAt; suspendedAt = 0; }
            Bitmap? captured = null;
            Reading reading;
            if (automatic.Checked)
            {
                using var full = window.Capture(null);
                if (clientSize != Size.Empty && full.Size != clientSize && flow.Blocked) { StopRun("Oyun boyutu aktif adım sırasında değişti; tekrar başlatın"); return; }
                clientSize = full.Size;
                phase = "locating"; LocateResult located = await reader.LocateAsync(full);
                if (!running || generation != ticket) return;
                finderDetail = located.Detail;
                if (located.HeaderSeen && located.Bounds.IsEmpty)
                {
                    Directory.CreateDirectory(logRoot);
                    File.WriteAllText(Path.Combine(logRoot, "finder-reading.json"), JsonSerializer.Serialize(new { located,
                        lines = reader.LastLines.Where(l => l.Text.Contains("puzzle", StringComparison.OrdinalIgnoreCase) || l.Text.Contains("Tar", StringComparison.OrdinalIgnoreCase) || l.Text.Contains("Step", StringComparison.OrdinalIgnoreCase)) }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (located.Bounds.IsEmpty)
                {
                    reading = located.HeaderSeen ? Reading.Unknown(located.Detail) : memoryState?.Active == true ?
                        Reading.Unknown("Bellek doğrulamanın açık olduğunu gösteriyor; görüntü henüz okunamadı") : Reading.Absent;
                    crop = Rectangle.Empty; ShowPreview(full);
                }
                else
                {
                    crop = located.Bounds;
                    captured = full.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try { reading = await reader.ReadAsync(captured); } catch { captured.Dispose(); throw; }
                }
            }
            else
            {
                captured = window.Capture(crop, clientSize);
                try { reading = await reader.ReadAsync(captured); } catch { captured.Dispose(); throw; }
            }
            using var frame = captured;
            if (!running || generation != ticket) return;
            frameCount++; phase = reading.Valid ? "reading_ready" : reading.Kind == ReadingKind.Absent ? "waiting_for_puzzle" : "reading_uncertain";
            area.Text = $"Bağlı · PID {window.Pid} · okuma {frameCount} · seçim {clickCount} · {memoryDetail}";
            if (frame != null) ShowPreview(frame);
            if (mode.SelectedIndex == 0)
            {
                string detail = reading.Valid ? $"Okundu · {reading.Step}/4 · {reading.Target} · tıklama yok" : reading.Detail;
                status.Text = detail; WriteState(detail, reading, reading.Kind != ReadingKind.Absent); return;
            }
            Option? choice = flow.Observe(reading, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - suspendedSeconds);
            if (choice != null)
            {
                // Recheck the same screen after asynchronous OCR and before posting any input.
                using var fresh = window.Capture(crop, clientSize);
                var confirm = await reader.ReadAsync(fresh);
                if (!running || generation != ticket) return;
                if (!confirm.Valid || confirm.Key != reading.Key) flow.Fail("Ekran seçimden önce değişti; tıklama yapılmadı");
                else
                {
                    phase = "click_posted"; WriteState($"{reading.Step}/4 · {reading.Target} için tıklama gönderiliyor", reading);
                    window.Click(new(crop.X + choice.Center.X, crop.Y + choice.Center.Y), crop, clientSize); clickCount++;
                    Directory.CreateDirectory(logRoot);
                    frame?.Save(Path.Combine(logRoot, $"step-{reading.Step}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png"));
                }
            }
            status.Text = flow.Status + $" · PID {window.Pid} · durdurma: {stopKey}";
            if (flow.Complete) phase = "complete";
            if (flow.Faulted) phase = "faulted";
            WriteState(flow.Status, reading);
            if (flow.Faulted) { running = false; generation++; timer.Stop(); SetEnabled(true); }
        }
        catch (Exception e) { if (running && generation == ticket) { StopRun("Durdu: " + e.Message); SaveError(e); } }
        finally { busy = false; }
    }
    void ShowPreview(Bitmap bitmap)
    {
        Image? old = preview.Image; preview.Image = new Bitmap(bitmap); old?.Dispose();
    }
    void WriteState(string detail, Reading? reading, bool? blocked = null)
    {
        try
        {
            Directory.CreateDirectory(logRoot);
            var state = new { utc = DateTimeOffset.UtcNow, version = "1.1", standalone = true, farm_connected = false,
                running, mode = mode.SelectedIndex == 0 ? "read_only" : "match", blocked = blocked ?? flow.Blocked, complete = flow.Complete, faulted = flow.Faulted,
                last_clicked_step = flow.LastClicked, detail, selected_pid = selected?.Pid,
                selected_path = selected?.Path, step = reading?.Step, target = reading?.Target, options = reading?.Options.Select(o => o.Word),
                phase, automatic = automatic.Checked, frame_count = frameCount, click_count = clickCount, stop_key = stopKey,
                roi = new { crop.X, crop.Y, crop.Width, crop.Height }, client = new { clientSize.Width, clientSize.Height },
                ocr_language = reader?.Language, raw_puzzle_text = reader?.LastText, finder_detail = finderDetail, backend = "window_messages",
                memory_detail = memoryDetail, memory_state = memoryState, memory_access = "0x410 (query + read only)" };
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            string pending = Path.Combine(logRoot, "integration-state.tmp"); File.WriteAllText(pending, json);
            File.Move(pending, Path.Combine(logRoot, "integration-state.json"), true);
            string change = phase + ":" + detail;
            if (change != lastLog) { File.AppendAllText(Path.Combine(logRoot, "events.jsonl"), JsonSerializer.Serialize(state) + Environment.NewLine); lastLog = change; }
        }
        catch { status.Text = detail + " · günlük yazılamadı"; }
    }
    void SaveError(Exception error)
    {
        try { Directory.CreateDirectory(logRoot); File.WriteAllText(Path.Combine(logRoot, "last-error.txt"), error.ToString()); } catch { }
    }
    internal async Task VerifyAutomaticSession(string output)
    {
        OpenDemo(); fixture!.PuzzleVisible = false; fixture.Invalidate(); fixture.Update();
        if (!automatic.Checked || mode.SelectedIndex != 1) throw new IOException("Automatic defaults are not ready");
        crop = Rectangle.Empty;
        await StartRun();
        if (!running) throw new IOException("UI could not connect: " + status.Text);
        var watch = Stopwatch.StartNew();
        while (frameCount < 2 && watch.Elapsed.TotalSeconds < 12) await Task.Delay(100);
        if (frameCount < 2 || flow.Blocked || clickCount != 0) throw new IOException("Idle scan did not wait without clicking: " + status.Text);
        fixture.PuzzleVisible = true; fixture.Invalidate(); fixture.Update();
        while (!flow.Complete && !flow.Faulted && watch.Elapsed.TotalSeconds < 35) await Task.Delay(100);
        if (!flow.Complete || clickCount != 4 || fixture.Accepted != 4 || fixture.Inputs != 4 || fixture.Attempts != 3)
            throw new IOException("Automatic arrival did not complete: " + status.Text + " / clicks " + clickCount);
        File.WriteAllText(Path.Combine(output, "automatic-session.json"), JsonSerializer.Serialize(new { pass = true,
            manual_area_selected = false, manually_notified_arrival = false, idle_frames = 2, frames = frameCount,
            clicks = clickCount, accepted = fixture.Accepted, attempts = fixture.Attempts, complete = flow.Complete, target_pid = Environment.ProcessId, game_accessed = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopRun("Uygulama kapandı"); fixture?.Close(); base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            running = false; generation++; timer.Dispose(); if (hotkey && IsHandleCreated) Native.UnregisterHotKey(Handle, 9);
            connecting?.Cancel(); connecting?.Dispose(); connecting = null; memory?.Dispose(); memory = null;
            fixture?.Dispose(); preview.Image?.Dispose(); preview.Image = null;
        }
        base.Dispose(disposing);
    }
}
