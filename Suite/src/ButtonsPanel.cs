using UnityTools.Controls;

namespace UnityTools;

// A compact view of the same commands used by the overview, without a second backend.
sealed class ButtonsPanel : Panel
{
    internal readonly Dictionary<Feature, Button> Actions = new();

    internal ButtonsPanel(Action<Feature> execute)
    {
        Dock = DockStyle.Fill; AutoScroll = true; BackColor = Palette.Background;
        var rows = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, Padding = new(4), Margin = Padding.Empty
        };
        rows.ColumnStyles.Add(new(SizeType.Percent, 100));
        rows.ColumnStyles.Add(new(SizeType.AutoSize));
        foreach (Feature feature in Enum.GetValues<Feature>())
        {
            int row = rows.RowCount++;
            rows.RowStyles.Add(new(SizeType.AutoSize));
            string name = feature switch
            {
                Feature.Coordinates => "Player XYZ", Feature.MobTP => "MobTP",
                _ => FeatureActions.Name(feature)
            };
            var label = Responsive.Text(name, 11);
            label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            label.Margin = new(4, 8, 8, 8);
            var button = new Button
            {
                Name = feature.ToString(), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new(12, 6, 12, 6), Margin = new(4, 4, 4, 4), MinimumSize = new(82, 0),
                AccessibleName = name, Anchor = AnchorStyles.Right
            };
            Palette.Apply(button);
            button.Click += (_, _) => execute(feature);
            Actions[feature] = button;
            rows.Controls.Add(label, 0, row); rows.Controls.Add(button, 1, row);
        }
        Controls.Add(rows);
    }

    internal void UpdateState(FeatureActions actions, bool blocked = false)
    {
        foreach (var (feature, button) in Actions)
        {
            var state = actions.Read(feature);
            string text = FeatureActions.OneShot(feature)
                ? feature is Feature.Salesman or Feature.Coordinates ? "Aç" : "Uygula"
                : state.Active ? "Kapat" : "Aç";
            if (button.Text != text) button.Text = text;
            button.BackColor = state.Active ? Color.FromArgb(80, 65, 137) : Palette.Elevated;
            button.Enabled = !actions.Busy && (!blocked || feature is Feature.Counter or Feature.SafeMode);
        }
    }
}
