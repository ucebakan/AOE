using UnityTools.Controls;

namespace UnityTools.Salesman;

sealed class SalesmanForm : Form
{
    readonly bool preview;
    internal readonly Controller Controller = new();
    readonly Label state = Responsive.Text("");
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 400 };
    bool busy;
    internal bool SuiteReady => Controller.Ready;
    internal bool SuiteActive => Controller.Active;
    internal string SuiteMessage => Controller.Message;
    internal SalesmanForm(bool preview)
    {
        this.preview = preview; Text = "Salesman"; AutoScaleMode = AutoScaleMode.Dpi; BackColor = Palette.Background;
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var content = new ContentStack { Padding = new(18), BackColor = Palette.Background };
        content.AddRow(Responsive.Text("Salesman", 20, true));
        content.AddRow(Responsive.Text("NPC'ye gitmeden satış penceresini aç. Salesman butonu doğrudan açar; tuş ataması gerekmez."));
        content.AddRow(Responsive.Text("Profili doğrula yalnız okur. SELL TRASH'a oyun içinde sen basarsın; araç otomatik eşya satmaz."));
        var buttons = Responsive.Flow();
        Button Add(string text, Func<Task> action)
        {
            var b = new Button { Text = text, AutoSize = true, Padding = new(12, 8, 12, 8) }; Palette.Apply(b); Responsive.Button(b);
            b.Click += async (_, _) => { if (busy) return; busy = true; buttons.Enabled = false; try { await action(); } catch (Exception ex) { state.Text = ex.Message; } finally { busy = false; if (!IsDisposed) { buttons.Enabled = true; UpdateState(); } } }; buttons.Controls.Add(b); return b;
        }
        Add("Profili doğrula", async () => { if (preview) throw new IOException("Önizlemede oyun bağlantısı kapalı."); await Controller.PrepareAsync(); });
        Add("Salesman", async () => { if (preview) throw new IOException("Önizlemede oyun işlemleri kapalı."); await Controller.PrepareAsync(); await Controller.OpenAsync(); });
        Add("Durdur / geri al", async () => { if (!await Controller.StopAsync()) throw new IOException("Geri alma doğrulanamadı."); });
        Add("Patch Recovery · yeniden tara", async () => { if (preview) throw new IOException("Önizlemede canlı tarama kapalı."); await Controller.PrepareAsync(true); });
        content.AddRow(buttons); content.AddRow(state);
        content.AddRow(Responsive.Text("Pencere kapanınca, harita/oturum değişince, SafeMode devreye girince veya uygulama kapanınca koruma geri alınır. Oyun güncellemesinde SHA + tekil AOB + metot bağlantıları yeniden doğrulanır."));
        scroll.Controls.Add(content); Controls.Add(scroll); Palette.Apply(this); UpdateState();
        refresh.Tick += (_, _) => UpdateState(); refresh.Start();
    }
    void UpdateState() { state.Text = preview ? "Önizleme · bağlantı ve oyun yazımı kapalı." : Controller.Message; }
    internal Task SuiteRefreshAsync() => preview ? Task.CompletedTask : Controller.PrepareAsync();
    internal Task<string> SuiteOpenAsync() => Controller.OpenAsync();
    internal void SuiteQuiesce() { Enabled = false; Controller.Quiesce(); }
    internal Task<bool> SuiteStopAsync() => Controller.StopAsync(true);
    protected override void Dispose(bool disposing) { if (disposing) { refresh.Dispose(); Controller.Dispose(); } base.Dispose(disposing); }
}
