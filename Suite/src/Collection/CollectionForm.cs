using UnityTools.Controls;

namespace UnityTools.Collection;

sealed class CollectionForm : Form
{
    readonly bool preview;
    internal readonly Controller Controller;
    readonly Label state = Responsive.Text("");
    readonly Button toggle;
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 250 };
    bool busy;
    internal bool SuiteReady => Controller.Ready;
    internal bool SuiteActive => Controller.Active;
    internal string SuiteMessage => Controller.Message;
    internal CollectionForm(bool preview)
    {
        this.preview = preview;
        Controller = preview ? new(_ => throw new IOException("Önizlemede oyun bağlantısı kapalı.")) : new();
        Text = "Collection"; AutoScaleMode = AutoScaleMode.Dpi; BackColor = Palette.Background;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var content = new ContentStack { Padding = new(18), BackColor = Palette.Background };
        content.AddRow(Responsive.Text("Collection", 20, true));
        content.AddRow(Responsive.Text("Collection'ı açınca yeni ölen mobların loot'u hedef seçmeden, hızlı gruplar halinde toplanır. Tekrar basınca durur; tuş ataması gerekmez."));
        content.AddRow(Responsive.Text("Araç mesafe sınırı koymaz. Loot hakkını sunucu kontrol eder. Açılışta zaten ölü moblar atlanır; eşya buff'ı kapalı olmalı."));
        var buttons = Responsive.Flow();
        Button Add(string text, Func<Task> action)
        {
            var b = new Button { Text = text, AutoSize = true, Padding = new(12, 8, 12, 8) }; Palette.Apply(b); Responsive.Button(b);
            b.Click += async (_, _) => { if (busy) return; busy = true; buttons.Enabled = false; try { await action(); } catch (Exception ex) { state.Text = ex.Message; } finally { busy = false; if (!IsDisposed) { buttons.Enabled = true; UpdateState(); } } };
            buttons.Controls.Add(b); return b;
        }
        Add("Profili doğrula", SuiteRefreshAsync);
        toggle = Add("Collection · AÇ", async () => { await SuiteRefreshAsync(); await SuiteToggleAsync(); });
        Add("Durdur", async () => { if (!await Controller.StopAsync()) throw new IOException("Collection çağrı temizliği bekleniyor; yeni işlemler engelli."); });
        Add("Patch Recovery · yeniden tara", async () => { if (preview) throw new IOException("Önizlemede canlı tarama kapalı."); await Controller.PrepareAsync(true); });
        content.AddRow(buttons); content.AddRow(state);
        content.AddRow(Responsive.Text("Tarama yalnız doğrular, toplamayı başlatmaz. Oyun güncellemesinde SHA, tekil AOB ve native metot bağlantıları yeniden doğrulanır. SafeMode, oturum değişimi veya kapanışta toplama durur."));
        scroll.Controls.Add(content); Controls.Add(scroll); Palette.Apply(this); UpdateState();
        refresh.Tick += (_, _) => UpdateState(); refresh.Start();
    }
    void UpdateState() { state.Text = preview ? "Önizleme · oyun bağlantısı ve loot istekleri kapalı." : Controller.Message; toggle.Text = Controller.Active ? "Collection · KAPAT" : "Collection · AÇ"; }
    internal Task SuiteRefreshAsync() => preview ? Task.CompletedTask : Controller.PrepareAsync();
    internal Task<string> SuiteToggleAsync() => preview ? throw new IOException("Önizlemede oyun işlemi kapalı.") : Controller.ToggleAsync();
    internal void SuiteQuiesce() { Enabled = false; Controller.Quiesce(); }
    internal Task<bool> SuiteStopAsync() => Controller.StopAsync(true);
    protected override void Dispose(bool disposing) { if (disposing) { refresh.Dispose(); Controller.Dispose(); } base.Dispose(disposing); }
}
