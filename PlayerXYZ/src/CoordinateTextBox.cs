using System.Globalization;

namespace PlayerXYZ;

// The same grammar is used for typing, paste, and the final float conversion.
sealed class CoordinateTextBox : TextBox
{
    string accepted = "";
    bool reverting;
    public CoordinateTextBox() { MaxLength = 32; }
    public static bool IsEditable(string text)
    {
        if (text.Length > 32) return false;
        bool separator = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c >= '0' && c <= '9') continue;
            if (c == '-' && i == 0) continue;
            if ((c == ',' || c == '.') && !separator) { separator = true; continue; }
            return false;
        }
        return true;
    }
    public static bool TryValue(string text, out float number)
    {
        number = 0;
        return IsEditable(text) && float.TryParse(text.Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out number) && float.IsFinite(number);
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar))
        {
            string candidate = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, e.KeyChar.ToString());
            if (!IsEditable(candidate)) e.Handled = true;
        }
        base.OnKeyPress(e);
    }
    protected override void OnTextChanged(EventArgs e)
    {
        if (reverting) return;
        if (!IsEditable(Text))
        {
            int caret = SelectionStart;
            reverting = true; Text = accepted; SelectionStart = Math.Min(caret, Text.Length); reverting = false;
            return;
        }
        accepted = Text;
        base.OnTextChanged(e);
    }
}
