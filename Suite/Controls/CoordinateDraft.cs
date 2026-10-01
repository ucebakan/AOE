namespace UnityTools.Controls;

public sealed class CoordinateDraft
{
    readonly string[] values = ["", "", ""];
    public event Action? Changed;
    public string this[int index] { get => values[index]; set { if (values[index] == value) return; values[index] = value; Changed?.Invoke(); } }
}
