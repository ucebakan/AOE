using UnityTools.Controls;

namespace UnityTools;

sealed class FeatureCard : UserControl
{
    internal readonly Dictionary<Feature, Button> Actions = new();
    internal readonly Button Settings;
    readonly Label state;
    internal readonly int Id;
    internal readonly Button DragHandle;
    internal readonly CoordinateEntry? Coordinates;
    bool busy;
    public FeatureCard(int number, string title, string description, Feature[] features, Action<Feature> execute, Action settings, CoordinateDraft? draft = null)
    {
        Id = number;
        DoubleBuffered = true; BackColor = Palette.Surface; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new(8); Padding = new(1); Dock = DockStyle.Fill;
        var content = new ContentStack { Padding = new(14), BackColor = Palette.Surface };
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2 };
        header.ColumnStyles.Add(new(SizeType.Percent, 100)); header.ColumnStyles.Add(new(SizeType.AutoSize));
        header.Controls.Add(Responsive.Text(title, 16, true), 0, 0);
        DragHandle = new Button { Text = "↕ " + number.ToString("00"), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ForeColor = Palette.Accent, FlatStyle = FlatStyle.Flat, Cursor = Cursors.SizeAll, AccessibleName = title + " kartını taşı", Padding = new(4), Margin = new(3, 5, 3, 3) };
        DragHandle.FlatAppearance.BorderSize = 0;
        header.Controls.Add(DragHandle, 1, 0); content.AddRow(header);
        var descriptionLabel = Responsive.Text(description); descriptionLabel.ForeColor = Palette.Muted; descriptionLabel.Margin = new(3, 3, 3, 6); content.AddRow(descriptionLabel);
        if (draft is not null) { Coordinates = new CoordinateEntry(draft); content.AddRow(Coordinates); }
        var actions = new List<Control>();
        foreach (var feature in features)
        {
            var button = new Button { Name = feature.ToString(), Text = FeatureActions.Name(feature), FlatStyle = FlatStyle.Flat, BackColor = Palette.Elevated, ForeColor = Palette.Ink, Cursor = Cursors.Hand };
            Palette.Apply(button);
            Responsive.Button(button); button.MinimumSize = new(100, 38); button.Padding = new(10, 6, 10, 6); button.FlatAppearance.BorderColor = Palette.Line;
            button.Click += (_, _) => execute(feature); Actions[feature] = button; actions.Add(button);
        }
        var actionFlow = Responsive.Flow(actions.ToArray()); actionFlow.Margin = Padding.Empty; content.AddRow(actionFlow);
        state = Responsive.Text("Kapalı", 9); state.Margin = new(3, 4, 3, 2); state.ForeColor = Palette.Blue; content.AddRow(state);
        Settings = new Button { Text = number == 6 ? "Sayaç kullanımı" : "Ayarlar ve doğrulama ↗", AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = Palette.Muted, Cursor = Cursors.Hand, Padding = Padding.Empty };
        Settings.FlatAppearance.BorderSize = 0; Settings.Click += (_, _) => settings(); var settingsFlow = Responsive.Flow(Settings); settingsFlow.Margin = Padding.Empty; content.AddRow(settingsFlow);
        Controls.Add(content);
        if (Coordinates is not null) foreach (var input in Coordinates.Inputs)
            input.TextChanged += (_, _) => Actions[Feature.Coordinates].Enabled = !busy && Coordinates.Valid;
    }
    internal void UpdateState(FeatureActions actions)
    {
        busy = actions.Busy;
        if (Coordinates is not null) foreach (var input in Coordinates.Inputs) input.ReadOnly = busy;
        var messages = new List<string>();
        foreach (var item in Actions)
        {
            var value = actions.Read(item.Key);
            string label = FeatureActions.Name(item.Key);
            string text = FeatureActions.OneShot(item.Key) ? label : label + (value.Active ? " · KAPAT" : " · AÇ");
            if (item.Value.Text != text) item.Value.Text = text;
            item.Value.BackColor = value.Active ? Color.FromArgb(80, 65, 137) : Palette.Elevated;
            item.Value.Enabled = !actions.Busy && (Coordinates is null || Coordinates.Valid);
            if (item.Key == Feature.SafeMode) { messages.Add(value.Message); continue; }
            if (item.Key == Feature.Salesman) { messages.Add(value.Active ? "Satış penceresi açık" : value.Ready ? "Salesman hazır" : "Profil / bağlantı gerekli"); continue; }
            messages.Add(FeatureActions.OneShot(item.Key) ? (value.Ready ? "Tek seferlik işlem hazır" : "Ayar / bağlantı gerekli") : $"{label}: {(value.Active ? "Açık" : "Kapalı")}");
        }
        string summary = string.Join("  ·  ", messages); if (state.Text != summary) state.Text = summary;
    }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(Palette.Line); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
}

sealed class OverviewPanel : Panel
{
    readonly TableLayoutPanel grid = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, BackColor = Palette.Background };
    internal readonly List<FeatureCard> Cards = new();
    readonly CardOrder order;
    readonly ToolTip movementTip = new();
    readonly string dragToken = Guid.NewGuid().ToString("N");
    const string CardFormat = "4UnityTools.Card";
    internal event Action<string>? LayoutError;
    int columns;
    internal OverviewPanel(Action<Feature> execute, Action<int> settings, CoordinateDraft draft, string orderPath)
    {
        order = new CardOrder(orderPath);
        Dock = DockStyle.Fill; AutoScroll = true; BackColor = Palette.Background;
        string[] descriptions = ["Hazırladığın koordinatları tek seferde uygula.", "Hız ve zıplamayı bağımsız aç veya kapat.", "Invisible ve Aggro modları arasında geçiş yap.", "Uygun mobları ayarladığın mesafeye bir kez taşı.", "Doğrulanmış oturumda continuous arm aç veya kapat.", "Eski görünümüyle küçük, taşınabilir oyun üstü sayaç."];
        Feature[][] groups = [[Feature.Coordinates], [Feature.Speed, Feature.Jump], [Feature.Invisible, Feature.Aggro], [Feature.MobTP], [Feature.Aoe], [Feature.Counter]];
        for (int i = 0; i < 6; i++) { int index = i + 1; Cards.Add(new FeatureCard(index, SuiteForm.Names[index], descriptions[i], groups[i], execute, () => settings(index), index == 1 ? draft : null)); }
        Cards.Add(new FeatureCard(7, "SafeMode", "Oyuncu sayısı 1 veya üzerine çıkınca etkin işlemleri geri al.", [Feature.SafeMode], execute, () => settings(7)));
        Cards.Add(new FeatureCard(8, "Multikill", "Doğrulanmış JBE → JE işlevini aç veya kapat.", [Feature.Multikill], execute, () => settings(8)));
        Cards.Add(new FeatureCard(9, "Salesman", "NPC'ye gitmeden satış penceresini tek tıkla aç.", [Feature.Salesman], execute, () => settings(9)));
        Cards.Add(new FeatureCard(10, "Collection", "Hedef seçmeden yeni ölen mobların loot'unu otomatik topla.", [Feature.Collection], execute, () => settings(10)));
        var saved = order.Load(Cards.Select(c => c.Id)); var byId = Cards.ToDictionary(c => c.Id); Cards.Clear(); Cards.AddRange(saved.Select(id => byId[id]));
        foreach (var card in Cards) ConfigureMovement(card);
        Disposed += (_, _) => movementTip.Dispose();
        Controls.Add(grid); Resize += (_, _) => Reflow(); DpiChangedAfterParent += (_, _) => Reflow(); Reflow();
    }
    internal void Reflow(bool force = false)
    {
        int min = (int)Math.Ceiling(350 * DeviceDpi / 96f);
        int count = Math.Clamp((ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / Math.Max(1, min), 1, 3);
        if (count == columns && !force) return;
        columns = count; grid.SuspendLayout(); grid.Controls.Clear(); grid.ColumnStyles.Clear(); grid.RowStyles.Clear();
        grid.ColumnCount = columns; grid.RowCount = (Cards.Count + columns - 1) / columns;
        for (int i = 0; i < columns; i++) grid.ColumnStyles.Add(new(SizeType.Percent, 100f / columns));
        for (int i = 0; i < grid.RowCount; i++) grid.RowStyles.Add(new(SizeType.AutoSize));
        for (int i = 0; i < Cards.Count; i++) grid.Controls.Add(Cards[i], i % columns, i / columns);
        grid.ResumeLayout(true);
    }
    internal bool MoveCard(int id, int destination)
    {
        int index = Cards.FindIndex(c => c.Id == id);
        if (index < 0 || destination < 0 || destination >= Cards.Count || index == destination) return false;
        var next = Cards.ToList(); var card = next[index]; next.RemoveAt(index); next.Insert(destination, card);
        try { order.Save(next.Select(c => c.Id)); }
        catch (Exception ex) { LayoutError?.Invoke("Kart düzeni kaydedilemedi: " + ex.Message); return false; }
        Cards.Clear(); Cards.AddRange(next); Reflow(true); ScrollControlIntoView(card); return true;
    }
    internal DataObject DragData(int id) => new(CardFormat, dragToken + ":" + id);
    internal int? DraggedCard(IDataObject? data)
    {
        if (data?.GetData(CardFormat) is not string text || !text.StartsWith(dragToken + ":", StringComparison.Ordinal)) return null;
        return int.TryParse(text[(dragToken.Length + 1)..], out int id) && Cards.Any(c => c.Id == id) ? id : null;
    }
    void ConfigureMovement(FeatureCard card)
    {
        movementTip.SetToolTip(card.DragHandle, "Sürükleyerek taşı. Sağ tık: sıralama seçenekleri.");
        Point? start = null;
        card.DragHandle.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) start = e.Location; };
        card.DragHandle.MouseUp += (_, _) => start = null;
        card.DragHandle.MouseMove += (_, e) =>
        {
            if (start is not Point origin || e.Button != MouseButtons.Left) return;
            var threshold = new Rectangle(origin.X - SystemInformation.DragSize.Width / 2, origin.Y - SystemInformation.DragSize.Height / 2, SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
            if (threshold.Contains(e.Location)) return;
            start = null; card.DragHandle.DoDragDrop(DragData(card.Id), DragDropEffects.Move);
        };
        var menu = new ContextMenuStrip();
        foreach (var entry in new[] { ("Öne taşı", -1), ("Arkaya taşı", 1), ("En başa taşı", -100), ("En sona taşı", 100) })
            menu.Items.Add(entry.Item1, null, (_, _) => MoveCard(card.Id, Math.Clamp(Cards.IndexOf(card) + entry.Item2, 0, Cards.Count - 1)));
        card.DragHandle.ContextMenuStrip = menu; card.Disposed += (_, _) => menu.Dispose();
        void Bind(Control control)
        {
            control.AllowDrop = true;
            control.DragEnter += (_, e) => e.Effect = DraggedCard(e.Data).HasValue ? DragDropEffects.Move : DragDropEffects.None;
            control.DragOver += (_, e) =>
            {
                if (!DraggedCard(e.Data).HasValue) return;
                var point = PointToClient(new(e.X, e.Y)); int scroll = -AutoScrollPosition.Y;
                if (point.Y < 32) AutoScrollPosition = new(0, Math.Max(0, scroll - 18));
                else if (point.Y > ClientSize.Height - 32) AutoScrollPosition = new(0, scroll + 18);
            };
            control.DragDrop += (_, e) => { if (DraggedCard(e.Data) is int source) MoveCard(source, Cards.IndexOf(card)); };
            foreach (Control child in control.Controls) Bind(child);
        }
        Bind(card);
    }
    internal void UpdateState(FeatureActions actions) { foreach (var card in Cards) card.UpdateState(actions); }
}
