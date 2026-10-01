using PlayerXYZ;
using UnityTools.Controls;

namespace UnityTools;

sealed class CoordinateEntry : TableLayoutPanel
{
    internal readonly CoordinateTextBox[] Inputs = [new(), new(), new()];
    internal bool Valid => Inputs.All(i => CoordinateTextBox.TryValue(i.Text, out _));
    public CoordinateEntry(CoordinateDraft draft)
    {
        AutoSize = true; Dock = DockStyle.Fill; ColumnCount = 3; RowCount = 2; Margin = Padding.Empty;
        RowStyles.Add(new(SizeType.AutoSize)); RowStyles.Add(new(SizeType.AutoSize));
        for (int i = 0; i < 3; i++)
        {
            int axis = i; ColumnStyles.Add(new(SizeType.Percent, 100f / 3));
            Controls.Add(Responsive.Text(new[] { "X", "Y", "Z" }[i], 9, true), i, 0);
            var input = Inputs[i]; input.Text = draft[i]; input.Dock = DockStyle.Fill; input.Margin = new(3, 0, 9, 8);
            input.BackColor = Palette.Elevated; input.ForeColor = Palette.Ink; input.BorderStyle = BorderStyle.FixedSingle;
            input.AccessibleName = "Hedef " + new[] { "X", "Y", "Z" }[i];
            input.TextChanged += (_, _) => draft[axis] = input.Text; Controls.Add(input, i, 1);
        }
        void Sync() { for (int i = 0; i < 3; i++) if (Inputs[i].Text != draft[i]) Inputs[i].Text = draft[i]; }
        draft.Changed += Sync; Disposed += (_, _) => draft.Changed -= Sync;
    }
}
