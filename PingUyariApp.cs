using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PingUyari
{
    public class EmailSettings
    {
        public bool EnableEmailAlerts { get; set; }
        public string RecipientEmail { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public string SenderEmail { get; set; }
        public string SenderPassword { get; set; }

        public EmailSettings()
        {
            EnableEmailAlerts = true;
            RecipientEmail = "bilgiislem@alnusyatirim.com";
            SmtpHost = "smtp.office365.com";
            SmtpPort = 587;
            EnableSsl = true;
            SenderEmail = "";
            SenderPassword = "";
        }
    }

    public class TelegramSettings
    {
        public bool EnableTelegram { get; set; }
        public string BotToken { get; set; }
        public string ChatId { get; set; }

        public TelegramSettings()
        {
            EnableTelegram = false;
            BotToken = "";
            ChatId = "";
        }
    }

    public class UpdateSettings
    {
        public bool AutoCheckOnStartup { get; set; }
        public string UpdateSourceUrl { get; set; }
        public bool SilentUpdate { get; set; }

        public UpdateSettings()
        {
            AutoCheckOnStartup = true;
            UpdateSourceUrl = "https://raw.githubusercontent.com/suqer34/ping-uyari/main/version.txt";
            SilentUpdate = false;
        }
    }

    public class TargetItem
    {
        public string Host { get; set; }
        public int Port { get; set; } // 0 = ICMP Ping, >0 = TCP/HTTP/SQL Port Check
        public string Description { get; set; }
        public string GroupName { get; set; }
        public string Status { get; set; }
        public long LastLatency { get; set; }
        public long MinLatency { get; set; }
        public long MaxLatency { get; set; }
        public long TotalLatencySum { get; set; }
        public int SuccessCount { get; set; }
        public int TotalPings { get; set; }
        public int ConsecutiveFailures { get; set; }
        public string LastChecked { get; set; }
        public string LastFailureTime { get; set; }
        public string FirstOfflineTime { get; set; }
        public DateTime? OfflineStartDateTime { get; set; }
        public string ResolvedIP { get; set; }
        public string BaselineIP { get; set; }
        public string SslExpiryInfo { get; set; }
        public string WhoisExpiryInfo { get; set; }
        public string RblStatus { get; set; }

        // Host-Specific Custom Settings (0 / Empty = Use Global Default)
        public int IntervalSeconds { get; set; }
        public int TimeoutMs { get; set; }
        public int FailureThreshold { get; set; }
        public bool EnableEmailAlert { get; set; }
        public string CustomEmail { get; set; }
        public DateTime LastPingTime { get; set; }

        public List<long> LatencyHistory { get; set; }

        public string ProtocolDisplay
        {
            get
            {
                if (Port <= 0) return "ICMP Ping";
                if (Port == 80) return "HTTP 80";
                if (Port == 443) return "HTTPS 443 (SSL)";
                if (Port == 1433) return "MSSQL 1433";
                if (Port == 5432) return "PGSQL 5432";
                if (Port == 3389) return "RDP 3389";
                return string.Format("TCP {0}", Port);
            }
        }

        public string EffectiveIntervalDisplay
        {
            get { return IntervalSeconds > 0 ? string.Format("Özel: {0} sn", IntervalSeconds) : "Varsayılan"; }
        }

        public string EffectiveTimeoutDisplay
        {
            get { return TimeoutMs > 0 ? string.Format("Özel: {0} ms", TimeoutMs) : "Varsayılan"; }
        }

        public string EffectiveThresholdDisplay
        {
            get { return FailureThreshold > 0 ? string.Format("Özel: {0}", FailureThreshold) : "Varsayılan"; }
        }

        public string EmailAlertDisplay
        {
            get
            {
                if (!EnableEmailAlert) return "Kapalı";
                if (!string.IsNullOrWhiteSpace(CustomEmail)) return string.Format("Özel ({0})", CustomEmail);
                return "Açık";
            }
        }

        public string UptimePercentage
        {
            get
            {
                if (TotalPings <= 0) return "%100.0";
                double pct = ((double)SuccessCount / TotalPings) * 100.0;
                return string.Format("%{0:F1}", pct);
            }
        }

        public string DowntimeDuration
        {
            get
            {
                if (Status == "OFFLINE" && OfflineStartDateTime.HasValue)
                {
                    TimeSpan span = DateTime.Now - OfflineStartDateTime.Value;
                    if (span.TotalHours >= 1)
                        return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)span.TotalHours, span.Minutes, span.Seconds);
                    else
                        return string.Format("{0:D2}:{1:D2}", span.Minutes, span.Seconds);
                }
                return "-";
            }
        }

        public TargetItem()
        {
            Port = 0;
            Status = "BEKLENİYOR";
            LastLatency = -1;
            MinLatency = 999999;
            MaxLatency = 0;
            TotalLatencySum = 0;
            SuccessCount = 0;
            TotalPings = 0;
            ConsecutiveFailures = 0;
            LastChecked = "-";
            LastFailureTime = "-";
            FirstOfflineTime = "-";
            OfflineStartDateTime = null;
            ResolvedIP = "-";
            BaselineIP = "-";
            SslExpiryInfo = "-";
            WhoisExpiryInfo = "-";
            RblStatus = "TEMİZ";
            IntervalSeconds = 0;
            TimeoutMs = 0;
            FailureThreshold = 0;
            EnableEmailAlert = true;
            CustomEmail = "";
            LastPingTime = DateTime.MinValue;
            LatencyHistory = new List<long>();
        }

        public void AddLatencyHistory(long ms)
        {
            LatencyHistory.Add(ms);
            if (LatencyHistory.Count > 30)
            {
                LatencyHistory.RemoveAt(0);
            }
        }
    }

    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
            }
            catch {}
            Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try { File.AppendAllText(Path.Combine(Application.StartupPath, "error.log"), DateTime.Now.ToString() + " ThreadException: " + e.Exception + "\r\n"); } catch {}
            MessageBox.Show("Uygulama Hatası: " + (e.Exception != null ? e.Exception.ToString() : "Bilinmeyen Hata"), "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try { File.AppendAllText(Path.Combine(Application.StartupPath, "error.log"), DateTime.Now.ToString() + " UnhandledException: " + e.ExceptionObject + "\r\n"); } catch {}
            Exception ex = e.ExceptionObject as Exception;
            MessageBox.Show("Kritik Hata: " + (ex != null ? ex.ToString() : "Bilinmeyen Hata"), "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ------------------------------------------------------------------------
    // Multi-Host 10-Second Countdown Alert Popup Window
    // ------------------------------------------------------------------------
    public class OfflineAlertPopupForm : Form
    {
        private System.Windows.Forms.Timer timerCountdown;
        private int remainingSeconds = 10;
        private Label lblHeader;
        private Label lblTimerInfo;
        private ListView lstOfflineHosts;
        private Button btnClose;

        private static OfflineAlertPopupForm activeInstance = null;

        public static void ShowAlert(Form parentForm, List<TargetItem> offlineItems)
        {
            if (offlineItems == null || offlineItems.Count == 0) return;

            if (parentForm != null && parentForm.InvokeRequired)
            {
                parentForm.BeginInvoke(new Action(delegate() { ShowAlert(parentForm, offlineItems); }));
                return;
            }

            if (activeInstance == null || activeInstance.IsDisposed)
            {
                activeInstance = new OfflineAlertPopupForm();
                activeInstance.UpdateItems(offlineItems);
                activeInstance.Show();
            }
            else
            {
                activeInstance.UpdateItems(offlineItems);
                activeInstance.ResetTimer();
                activeInstance.BringToFront();
            }
        }

        public OfflineAlertPopupForm()
        {
            this.Text = "🔴 KESİNTİ UYARISI - ERİŞİM YOK!";
            this.Size = new Size(520, 330);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(30, 20, 26);
            this.ForeColor = Color.FromArgb(243, 139, 168);

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 15, screen.Bottom - this.Height - 15);

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            lblHeader = new Label
            {
                Text = "⚠️ ERİŞİM YOK! (KESİNTİ BİLDİRİMİ)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(243, 139, 168),
                Location = new Point(14, 10),
                AutoSize = true
            };

            lblTimerInfo = new Label
            {
                Text = "⏳ Bu pencere 10 saniye sonra otomatik kapanacak... (10)",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(205, 214, 244),
                Location = new Point(15, 38),
                AutoSize = true
            };

            lstOfflineHosts = new ListView
            {
                Location = new Point(15, 62),
                Size = new Size(475, 190),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 15, 22),
                ForeColor = Color.FromArgb(243, 139, 168),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle
            };
            lstOfflineHosts.Columns.Add("Hedef IP / Host", 140);
            lstOfflineHosts.Columns.Add("Açıklama", 185);
            lstOfflineHosts.Columns.Add("Durum", 125);

            btnClose = new Button
            {
                Text = "✅ TAMAM / KAPAT (10)",
                Location = new Point(340, 260),
                Size = new Size(150, 32),
                BackColor = Color.FromArgb(243, 139, 168),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += delegate(object s, EventArgs e) { this.Close(); };

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblTimerInfo);
            this.Controls.Add(lstOfflineHosts);
            this.Controls.Add(btnClose);

            timerCountdown = new System.Windows.Forms.Timer();
            timerCountdown.Interval = 1000;
            timerCountdown.Tick += TimerCountdown_Tick;
            timerCountdown.Start();
        }

        public void UpdateItems(List<TargetItem> offlineItems)
        {
            lstOfflineHosts.Items.Clear();
            lblHeader.Text = string.Format("⚠️ ERİŞİM YOK! ({0} ADET HEDEF KESİNTİDE)", offlineItems.Count);

            foreach (var item in offlineItems)
            {
                ListViewItem lvi = new ListViewItem(item.Host);
                lvi.SubItems.Add(string.IsNullOrEmpty(item.Description) ? item.Host : item.Description);
                lvi.SubItems.Add("ERİŞİM YOK!");
                lstOfflineHosts.Items.Add(lvi);
            }
        }

        public void ResetTimer()
        {
            remainingSeconds = 10;
            lblTimerInfo.Text = string.Format("⏳ Bu pencere 10 saniye sonra otomatik kapanacak... ({0})", remainingSeconds);
            btnClose.Text = string.Format("✅ TAMAM / KAPAT ({0})", remainingSeconds);
            if (!timerCountdown.Enabled) timerCountdown.Start();
        }

        private void TimerCountdown_Tick(object sender, EventArgs e)
        {
            remainingSeconds--;
            if (remainingSeconds <= 0)
            {
                timerCountdown.Stop();
                this.Close();
            }
            else
            {
                lblTimerInfo.Text = string.Format("⏳ Bu pencere 10 saniye sonra otomatik kapanacak... ({0})", remainingSeconds);
                btnClose.Text = string.Format("✅ TAMAM / KAPAT ({0})", remainingSeconds);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timerCountdown != null)
            {
                timerCountdown.Stop();
                timerCountdown.Dispose();
            }
            activeInstance = null;
            base.OnFormClosed(e);
        }
    }

    // ------------------------------------------------------------------------
    // Subnet / IP Range Scanner Dialog Form
    // ------------------------------------------------------------------------
    public class IPRangeScannerForm : Form
    {
        private TextBox txtStartIP;
        private TextBox txtEndIP;
        private NumericUpDown numScanTimeout;
        private ProgressBar progressBar;
        private Label lblStatus;
        private ListView lstDiscoveredIPs;
        private Button btnStartScan;
        private Button btnAddSelected;

        private List<string> discoveredIPs = new List<string>();
        private MainForm parentMainForm;

        public IPRangeScannerForm(MainForm parent)
        {
            this.parentMainForm = parent;
            this.Text = "🌐 IP Aralığı / Subnet Otomatik Tarayıcı";
            this.Size = new Size(500, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 37);
            this.ForeColor = Color.FromArgb(205, 214, 244);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Label lblStart = new Label { Text = "Başlangıç IP:", Location = new Point(15, 18), AutoSize = true };
            txtStartIP = new TextBox { Text = "192.168.1.1", Location = new Point(110, 15), Width = 110, BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244) };

            Label lblEnd = new Label { Text = "Bitiş IP:", Location = new Point(235, 18), AutoSize = true };
            txtEndIP = new TextBox { Text = "192.168.1.50", Location = new Point(295, 15), Width = 110, BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244) };

            Label lblTimeout = new Label { Text = "Timeout (ms):", Location = new Point(15, 50), AutoSize = true };
            numScanTimeout = new NumericUpDown { Minimum = 100, Maximum = 3000, Value = 400, Location = new Point(110, 48), Width = 70, BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244) };

            btnStartScan = new Button
            {
                Text = "🔍 Taramayı Başlat",
                Location = new Point(295, 46),
                Size = new Size(165, 28),
                BackColor = Color.FromArgb(137, 180, 250),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStartScan.Click += BtnStartScan_Click;

            progressBar = new ProgressBar { Location = new Point(15, 85), Size = new Size(445, 18) };

            lblStatus = new Label { Text = "Taramayı başlatmak için IP aralığını belirleyip butona tıklayın.", Location = new Point(15, 108), Size = new Size(445, 20), ForeColor = Color.FromArgb(166, 173, 200) };

            lstDiscoveredIPs = new ListView
            {
                Location = new Point(15, 130),
                Size = new Size(445, 195),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = true,
                GridLines = true,
                BackColor = Color.FromArgb(30, 30, 46),
                ForeColor = Color.FromArgb(166, 227, 161),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            lstDiscoveredIPs.Columns.Add("Aktif IP Adresi", 180);
            lstDiscoveredIPs.Columns.Add("Durum", 130);
            lstDiscoveredIPs.Columns.Add("Yanıt Süresi", 110);

            btnAddSelected = new Button
            {
                Text = "➕ Seçili IP'leri Monitöre Ekle",
                Location = new Point(255, 335),
                Size = new Size(205, 30),
                BackColor = Color.FromArgb(166, 227, 161),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddSelected.Click += BtnAddSelected_Click;

            this.Controls.AddRange(new Control[] {
                lblStart, txtStartIP, lblEnd, txtEndIP, lblTimeout, numScanTimeout,
                btnStartScan, progressBar, lblStatus, lstDiscoveredIPs, btnAddSelected
            });
        }

        private async void BtnStartScan_Click(object sender, EventArgs e)
        {
            IPAddress startIP, endIP;
            if (!IPAddress.TryParse(txtStartIP.Text.Trim(), out startIP) || !IPAddress.TryParse(txtEndIP.Text.Trim(), out endIP))
            {
                MessageBox.Show("Geçerli bir başlangıç ve bitiş IP adresi girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[] startBytes = startIP.GetAddressBytes();
            byte[] endBytes = endIP.GetAddressBytes();

            if (startBytes.Length != 4 || endBytes.Length != 4 || startBytes[0] != endBytes[0] || startBytes[1] != endBytes[1])
            {
                MessageBox.Show("Lütfen aynı subnet/alt ağ içindeki IP aralığını tarayın (Örn: 192.168.1.1 - 192.168.1.50).", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int startNum = startBytes[3];
            int endNum = endBytes[3];
            if (startNum > endNum) { int tmp = startNum; startNum = endNum; endNum = tmp; }

            int totalIPs = endNum - startNum + 1;
            int timeout = (int)numScanTimeout.Value;

            btnStartScan.Enabled = false;
            lstDiscoveredIPs.Items.Clear();
            discoveredIPs.Clear();
            progressBar.Value = 0;
            progressBar.Maximum = totalIPs;

            string baseIP = string.Format("{0}.{1}.{2}.", startBytes[0], startBytes[1], startBytes[2]);
            int completed = 0;
            int found = 0;

            List<Task> scanTasks = new List<Task>();

            for (int i = startNum; i <= endNum; i++)
            {
                string targetIP = baseIP + i;
                scanTasks.Add(Task.Run(delegate()
                {
                    using (Ping pinger = new Ping())
                    {
                        try
                        {
                            PingReply reply = pinger.Send(targetIP, timeout);
                            if (reply != null && reply.Status == IPStatus.Success)
                            {
                                this.BeginInvoke(new Action(delegate()
                                {
                                    ListViewItem lvi = new ListViewItem(targetIP);
                                    lvi.SubItems.Add("ONLINE (Aktif)");
                                    lvi.SubItems.Add(string.Format("{0} ms", reply.RoundtripTime));
                                    lvi.Checked = true;
                                    lstDiscoveredIPs.Items.Add(lvi);
                                    discoveredIPs.Add(targetIP);
                                    found++;
                                }));
                            }
                        }
                        catch { }
                    }

                    Interlocked.Increment(ref completed);
                    this.BeginInvoke(new Action(delegate()
                    {
                        progressBar.Value = Math.Min(completed, progressBar.Maximum);
                        lblStatus.Text = string.Format("Tarama Devam Ediyor: {0}/{1} tamamlandı. ({2} aktif cihaz bulundu)", completed, totalIPs, found);
                    }));
                }));
            }

            await Task.WhenAll(scanTasks);

            lblStatus.Text = string.Format("Tarama Tamamlandı! Toplam {0} aktif cihaz tespit edildi.", found);
            btnStartScan.Enabled = true;
        }

        private void BtnAddSelected_Click(object sender, EventArgs e)
        {
            int addedCount = 0;
            foreach (ListViewItem item in lstDiscoveredIPs.Items)
            {
                if (item.Checked)
                {
                    string ip = item.Text;
                    if (parentMainForm.AddTargetIfNotExists(ip, 0, "Tarama Sonucu (" + ip + ")"))
                    {
                        addedCount++;
                    }
                }
            }

            MessageBox.Show(string.Format("{0} adet yeni aktif IP izleme listesine eklendi.", addedCount), "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }

    // ------------------------------------------------------------------------
    // Open Port Security Vulnerability Scanner Form
    // ------------------------------------------------------------------------
    public class SecurityPortScannerForm : Form
    {
        private TextBox txtTargetIP;
        private Button btnStartPortScan;
        private ListView lstPortResults;
        private ProgressBar progressBar;
        private Label lblStatus;

        private readonly int[] portsToScan = new int[] { 21, 22, 23, 80, 135, 139, 443, 445, 1433, 3306, 3389, 8080 };
        private readonly string[] portDescs = new string[] {
            "21 (FTP - Unencrypted File Transfer)",
            "22 (SSH - Secure Shell Management)",
            "23 (Telnet - Unencrypted Remote Terminal)",
            "80 (HTTP - Web Server)",
            "135 (RPC - Windows Remote Procedure Call)",
            "139 (NetBIOS - Windows Networking)",
            "443 (HTTPS - Secure Web)",
            "445 (SMB - Windows File Sharing / Ransomware Target)",
            "1433 (MSSQL - Microsoft SQL Database)",
            "3306 (MySQL - MySQL Database)",
            "3389 (RDP - Remote Desktop Management)",
            "8080 (HTTP Alternate / Management Interface)"
        };

        public SecurityPortScannerForm(string defaultHost)
        {
            this.Text = "🛡️ Siber Güvenlik Port & Zafiyet Taraması";
            this.Size = new Size(560, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 37);
            this.ForeColor = Color.FromArgb(205, 214, 244);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            InitializeComponent(defaultHost);
        }

        private void InitializeComponent(string defaultHost)
        {
            Label lblTarget = new Label { Text = "Hedef IP / Host:", Location = new Point(15, 18), AutoSize = true };
            txtTargetIP = new TextBox { Text = string.IsNullOrEmpty(defaultHost) ? "127.0.0.1" : defaultHost, Location = new Point(125, 15), Width = 230, BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244) };

            btnStartPortScan = new Button
            {
                Text = "🛡️ Güvenlik Taraması Yap",
                Location = new Point(365, 13),
                Size = new Size(165, 28),
                BackColor = Color.FromArgb(243, 139, 168),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStartPortScan.Click += BtnStartPortScan_Click;

            progressBar = new ProgressBar { Location = new Point(15, 52), Size = new Size(515, 16) };
            lblStatus = new Label { Text = "Sunucudaki kritik yönetim ve veri portlarını taramak için butona basın.", Location = new Point(15, 75), Size = new Size(515, 20), ForeColor = Color.FromArgb(166, 173, 200) };

            lstPortResults = new ListView
            {
                Location = new Point(15, 100),
                Size = new Size(515, 290),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(30, 30, 46),
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            lstPortResults.Columns.Add("Port & Servis", 260);
            lstPortResults.Columns.Add("Erişim Durumu", 120);
            lstPortResults.Columns.Add("Güvenlik Riski", 120);

            this.Controls.AddRange(new Control[] { lblTarget, txtTargetIP, btnStartPortScan, progressBar, lblStatus, lstPortResults });
        }

        private async void BtnStartPortScan_Click(object sender, EventArgs e)
        {
            string target = txtTargetIP.Text.Trim();
            if (string.IsNullOrEmpty(target)) return;

            btnStartPortScan.Enabled = false;
            lstPortResults.Items.Clear();
            progressBar.Value = 0;
            progressBar.Maximum = portsToScan.Length;

            int openCount = 0;

            for (int i = 0; i < portsToScan.Length; i++)
            {
                int p = portsToScan[i];
                string desc = portDescs[i];

                bool isOpen = await Task.Run(delegate()
                {
                    try
                    {
                        using (TcpClient client = new TcpClient())
                        {
                            Task task = client.ConnectAsync(target, p);
                            if (Task.WaitAny(new Task[] { task }, 500) == 0 && client.Connected)
                                return true;
                        }
                    }
                    catch { }
                    return false;
                });

                ListViewItem lvi = new ListViewItem(desc);
                if (isOpen)
                {
                    openCount++;
                    lvi.SubItems.Add("AÇIK (Open)");
                    string risk = (p == 21 || p == 23 || p == 445 || p == 3389) ? "🔴 YÜKSEK RİSK" : "🟡 ORTA RİSK";
                    lvi.SubItems.Add(risk);
                    lvi.ForeColor = (p == 21 || p == 23 || p == 445 || p == 3389) ? Color.FromArgb(243, 139, 168) : Color.FromArgb(249, 226, 175);
                }
                else
                {
                    lvi.SubItems.Add("KAPALI (Closed)");
                    lvi.SubItems.Add("🟢 GÜVENLİ");
                    lvi.ForeColor = Color.FromArgb(166, 173, 200);
                }

                lstPortResults.Items.Add(lvi);
                progressBar.Value = i + 1;
                lblStatus.Text = string.Format("Taranıyor: {0}/{1} tamamlandı. ({2} açık port bulundu)", i + 1, portsToScan.Length, openCount);
            }

            lblStatus.Text = string.Format("Tarama Bitti! Toplam {0} açık port tespit edildi.", openCount);
            btnStartPortScan.Enabled = true;
        }
    }

    public class MainForm : Form
    {
        // Controls
        private DataGridView dgvTargets;
        private TextBox txtHost;
        private TextBox txtPort;
        private TextBox txtDesc;
        private Button btnAdd;
        private Button btnBatchAdd;
        private Button btnScanRange;
        private Button btnScanSecurityPorts;
        private Button btnRemove;
        private Button btnResetAlert;
        private Button btnSave;
        private Button btnLoad;
        private Button btnStartStop;
        private Button btnTestSound;
        private Button btnClearLog;
        private Button btnImportXml;
        private Button btnEdit;
        private TreeView treeGroups;
        private Button btnExportReport;

        private NumericUpDown numInterval;
        private NumericUpDown numTimeout;
        private NumericUpDown numThreshold;
        private NumericUpDown numRepeatInterval;

        private DateTime lastRepeatAlarmTime = DateTime.MinValue;
        private bool isPingTaskRunning = false;

        private CheckBox chkAudioAlert;
        private CheckBox chkToastAlert;
        private CheckBox chkEmailAlert;
        public const string APP_VERSION = "6.2.0";

        private CheckBox chkTelegramAlert;
        private CheckBox chkMinimizeToTray;
        private Button btnEmailSettings;
        private Button btnTelegramSettings;
        private Button btnServiceManager;
        private Button btnUpdateSettings;
        private Button btnCheckUpdate;
        private Button btnBatchEditSettings;

        private EmailSettings emailSettings = new EmailSettings();
        private TelegramSettings telegramSettings = new TelegramSettings();
        private UpdateSettings updateSettings = new UpdateSettings();

        private Label lblTotalCount;
        private Label lblOnlineCount;
        private Label lblOfflineCount;
        private Label lblAvgLatency;

        private RichTextBox txtLog;
        private Panel panelGraph;
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;

        // Data & State
        private BindingList<TargetItem> targets = new BindingList<TargetItem>();
        private System.Threading.Timer pingTimer;
        private bool isMonitoring = false;
        private string configFilePath;

        // Colors (Dark Theme - Mocha Palette)
        private readonly Color bgDark = Color.FromArgb(27, 38, 49);
        private readonly Color bgCard = Color.FromArgb(33, 47, 61);
        private readonly Color bgInput = Color.FromArgb(44, 62, 80);
        private readonly Color fgText = Color.FromArgb(236, 240, 241);
        private readonly Color fgMuted = Color.FromArgb(189, 195, 199);
        private readonly Color accentBlue = Color.FromArgb(52, 152, 219);
        private readonly Color colorOnline = Color.FromArgb(39, 174, 96);
        private readonly Color colorOffline = Color.FromArgb(192, 57, 43);
        private readonly Color colorWarning = Color.FromArgb(241, 196, 15);

        public MainForm()
        {
            configFilePath = Path.Combine(Application.StartupPath, "targets.txt");

            InitializeComponent();
            SetupTrayIcon();
            LoadDefaultTargets();
            LoadEmailSettings();
            LoadTelegramSettings();
            LoadUpdateSettings();

            this.FormClosing += MainForm_FormClosing;
            this.Shown += delegate {
                if (updateSettings.AutoCheckOnStartup && !string.IsNullOrWhiteSpace(updateSettings.UpdateSourceUrl))
                {
                    Task.Run(async delegate() { await CheckForUpdatesAsync(false); });
                }
            };
        }

        private void InitializeComponent()
        {
            this.Text = "📡 Ping Uyarı & CyberSecurity Threat Monitor v6.0 SOC/NOC Edition";
            this.Size = new Size(1340, 860);
            this.MinimumSize = new Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = bgDark;
            this.ForeColor = fgText;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

            this.Icon = SystemIcons.Application;

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(12),
                BackColor = bgDark
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));  // Header & Stat Cards
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 145F)); // Toolbar Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));   // DataGrid Table
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));   // Log & Trend Graph Split

            // -------------------------------------------------------------
            // 1. HEADER & RESPONSIVE COUNTER CARDS
            // -------------------------------------------------------------
            Panel panelHeader = new Panel { Dock = DockStyle.Fill, BackColor = bgDark };
            
            Label lblTitle = new Label
            {
                Text = string.Format("📡 PING UYARI & SİBER GÜVENLİK MONİTÖRÜ v{0}", APP_VERSION),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = accentBlue,
                AutoSize = true,
                Location = new Point(5, 5)
            };
            Label lblSubtitle = new Label
            {
                Text = GetSystemDriveAndSecurityInfo(),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = fgMuted,
                AutoSize = true,
                Location = new Point(8, 38)
            };
            panelHeader.Controls.Add(lblTitle);
            panelHeader.Controls.Add(lblSubtitle);

            FlowLayoutPanel cardsLayout = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Size = new Size(640, 75),
                Location = new Point(680, 5),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            lblTotalCount = CreateCounterCard(cardsLayout, "TOPLAM HEDEF", "0", Color.FromArgb(147, 153, 178));
            lblOnlineCount = CreateCounterCard(cardsLayout, "ÇEVRİMİÇİ (ONLINE)", "0", colorOnline);
            lblOfflineCount = CreateCounterCard(cardsLayout, "KESİNTİ (OFFLINE)", "0", colorOffline);
            lblAvgLatency = CreateCounterCard(cardsLayout, "ORT. PİNG", "0 ms", accentBlue);

            panelHeader.Controls.Add(cardsLayout);
            mainLayout.Controls.Add(panelHeader, 0, 0);

            // -------------------------------------------------------------
            // 2. INPUT & TOOLBAR PANEL
            // -------------------------------------------------------------
            GroupBox gbControl = new GroupBox
            {
                Text = " Kontrol & Ayarlar ",
                Dock = DockStyle.Fill,
                ForeColor = accentBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = bgCard,
                Padding = new Padding(10)
            };

            FlowLayoutPanel flowTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = false
            };

            Label lblHost = new Label { Text = "IP/Adres:", AutoSize = true, ForeColor = fgText, Margin = new Padding(3, 8, 3, 3) };
            txtHost = new TextBox { Width = 110, BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };
            
            Label lblPort = new Label { Text = "Port:", AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 8, 3, 3) };
            txtPort = new TextBox { Width = 40, Text = "0", BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };

            Label lblDesc = new Label { Text = "Açıklama:", AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 8, 3, 3) };
            txtDesc = new TextBox { Width = 90, BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };

            btnAdd = CreateStyledButton("➕ Ekle", Color.FromArgb(69, 71, 90));
            btnAdd.Click += BtnAdd_Click;

            btnBatchAdd = CreateStyledButton("📋 Toplu Ekle", Color.FromArgb(69, 71, 90));
            btnBatchAdd.Click += BtnBatchAdd_Click;

            btnScanRange = CreateStyledButton("🌐 IP Aralığı Tara", Color.FromArgb(137, 180, 250));
            btnScanRange.ForeColor = Color.Black;
            btnScanRange.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnScanRange.Click += delegate(object s, EventArgs e) {
                IPRangeScannerForm dlg = new IPRangeScannerForm(this);
                dlg.ShowDialog(this);
            };

            btnScanSecurityPorts = CreateStyledButton("🛡️ Port Zafiyet Taraması", Color.FromArgb(243, 139, 168));
            btnScanSecurityPorts.ForeColor = Color.Black;
            btnScanSecurityPorts.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnScanSecurityPorts.Click += delegate(object s, EventArgs e) {
                string selectedHost = "";
                if (dgvTargets.SelectedRows.Count > 0)
                {
                    TargetItem sel = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (sel != null) selectedHost = sel.Host;
                }
                SecurityPortScannerForm dlg = new SecurityPortScannerForm(selectedHost);
                dlg.ShowDialog(this);
            };

            btnEdit = CreateStyledButton("✏️ Düzenle", Color.FromArgb(211, 84, 0));
            btnEdit.ForeColor = Color.White;
            btnEdit.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnEdit.Click += delegate(object s, EventArgs e)
            {
                if (dgvTargets.SelectedRows.Count > 0)
                {
                    TargetItem sel = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (sel != null) OpenEditDeviceDialog(sel);
                }
                else
                {
                    MessageBox.Show("Düzenlemek için tablodan bir cihaz seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            btnRemove = CreateStyledButton("🗑️ Sil", Color.FromArgb(69, 71, 90));
            btnRemove.Click += BtnRemove_Click;

            btnResetAlert = CreateStyledButton("✅ Çözüldü (Yeniden Tetikle)", Color.FromArgb(137, 180, 250));
            btnResetAlert.ForeColor = Color.FromArgb(17, 17, 27);
            btnResetAlert.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnResetAlert.Click += BtnResetAlert_Click;

            btnExportReport = CreateStyledButton("📊 SLA Raporu Al", Color.FromArgb(147, 153, 178));
            btnExportReport.ForeColor = Color.FromArgb(17, 17, 27);
            btnExportReport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExportReport.Click += delegate(object s, EventArgs e) { GenerateSlaReport(); };

            btnImportXml = CreateStyledButton("📂 TNM XML YÜKLE", Color.FromArgb(41, 128, 185));
            btnImportXml.ForeColor = Color.White;
            btnImportXml.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnImportXml.Click += delegate(object s, EventArgs e) { ImportTnmXmlFile(); };

            btnSave = CreateStyledButton("💾 Kaydet", Color.FromArgb(69, 71, 90));
            btnSave.Click += delegate(object s, EventArgs e) { SaveTargetsToFile(true); };

            btnLoad = CreateStyledButton("📂 Yükle", Color.FromArgb(69, 71, 90));
            btnLoad.Click += delegate(object s, EventArgs e) { LoadTargetsFromFile(); };

            // Settings
            Label lblInt = new Label { Text = "Aralık (sn):", AutoSize = true, ForeColor = fgText, Margin = new Padding(10, 8, 3, 3) };
            numInterval = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 3, Width = 45, BackColor = bgInput, ForeColor = fgText };

            Label lblTo = new Label { Text = "Timeout (ms):", AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 8, 3, 3) };
            numTimeout = new NumericUpDown { Minimum = 100, Maximum = 10000, Increment = 100, Value = 1000, Width = 55, BackColor = bgInput, ForeColor = fgText };

            Label lblTh = new Label { Text = "Eşik:", AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 8, 3, 3) };
            numThreshold = new NumericUpDown { Minimum = 1, Maximum = 10, Value = 1, Width = 35, BackColor = bgInput, ForeColor = fgText };

            Label lblRepeat = new Label { Text = "Tekrar (sn):", AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 8, 3, 3) };
            numRepeatInterval = new NumericUpDown { Minimum = 0, Maximum = 300, Value = 15, Width = 45, BackColor = bgInput, ForeColor = fgText };

            chkAudioAlert = new CheckBox { Text = "🔔 Sesli İkaz", Checked = true, AutoSize = true, ForeColor = fgText, Margin = new Padding(6, 7, 3, 3) };
            chkToastAlert = new CheckBox { Text = "💬 Popup Bildirim", Checked = true, AutoSize = true, ForeColor = fgText, Margin = new Padding(4, 7, 3, 3) };
            chkEmailAlert = new CheckBox { Text = "📧 E-Posta", Checked = true, AutoSize = true, ForeColor = fgText, Margin = new Padding(4, 7, 3, 3) };
            chkTelegramAlert = new CheckBox { Text = "📲 Telegram", Checked = false, AutoSize = true, ForeColor = fgText, Margin = new Padding(4, 7, 3, 3) };
            chkMinimizeToTray = new CheckBox { Text = "🔻 Tepsiye Küçült", Checked = true, AutoSize = true, ForeColor = fgText, Margin = new Padding(4, 7, 3, 3) };

            btnEmailSettings = CreateStyledButton("⚙️ E-Posta Ayarları", Color.FromArgb(69, 71, 90));
            btnEmailSettings.Click += delegate(object s, EventArgs e) { OpenEmailSettingsDialog(); };

            btnTelegramSettings = CreateStyledButton("📲 Telegram Ayarları", Color.FromArgb(69, 71, 90));
            btnTelegramSettings.Click += delegate(object s, EventArgs e) { OpenTelegramSettingsDialog(); };

            btnServiceManager = CreateStyledButton("⚙️ Windows Servis Modu", Color.FromArgb(88, 91, 112));
            btnServiceManager.Click += delegate(object s, EventArgs e) { OpenServiceManagerDialog(); };

            btnUpdateSettings = CreateStyledButton("🔄 Oto-Güncelleme Ayarları", Color.FromArgb(41, 128, 185));
            btnUpdateSettings.ForeColor = Color.White;
            btnUpdateSettings.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnUpdateSettings.Click += delegate(object s, EventArgs e) { OpenUpdateSettingsDialog(); };

            btnCheckUpdate = CreateStyledButton("🔍 Güncelleme Kontrol Et", Color.FromArgb(137, 180, 250));
            btnCheckUpdate.ForeColor = Color.Black;
            btnCheckUpdate.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCheckUpdate.Click += delegate(object s, EventArgs e) { Task.Run(async delegate() { await CheckForUpdatesAsync(true); }); };

            btnBatchEditSettings = CreateStyledButton("⚙️ Toplu Ayar Düzenle", Color.FromArgb(166, 227, 161));
            btnBatchEditSettings.ForeColor = Color.Black;
            btnBatchEditSettings.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnBatchEditSettings.Click += delegate(object s, EventArgs e) { OpenBatchEditDialog(GetSelectedTargets()); };

            btnTestSound = CreateStyledButton("🔊 Siren Test", Color.FromArgb(88, 91, 112));
            btnTestSound.Click += delegate(object s, EventArgs e) { PlayAlarmSound(); };

            Button btnTestPopup = CreateStyledButton("🖥️ Popup Test", Color.FromArgb(88, 91, 112));
            btnTestPopup.Click += delegate(object s, EventArgs e) {
                var sampleOffline = new List<TargetItem> {
                    new TargetItem { Host = "169.122.59.23", Description = "Ana Sunucu (Erişim Yok)" },
                    new TargetItem { Host = "10.0.0.1", Description = "Kenar Yönlendirici (Erişim Yok)" }
                };
                OfflineAlertPopupForm.ShowAlert(this, sampleOffline);
            };

            btnStartStop = CreateStyledButton("▶ İZLEMEYİ BAŞLAT", Color.FromArgb(166, 227, 161));
            btnStartStop.ForeColor = Color.FromArgb(17, 17, 27);
            btnStartStop.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnStartStop.Width = 140;
            btnStartStop.Click += BtnStartStop_Click;

            flowTools.Controls.AddRange(new Control[] {
                lblHost, txtHost, lblPort, txtPort, lblDesc, txtDesc, btnAdd, btnBatchAdd, btnScanRange, btnScanSecurityPorts, btnEdit, btnBatchEditSettings, btnRemove, btnResetAlert, btnExportReport, btnImportXml, btnSave, btnLoad,
                lblInt, numInterval, lblTo, numTimeout, lblTh, numThreshold, lblRepeat, numRepeatInterval,
                chkAudioAlert, chkToastAlert, chkEmailAlert, chkTelegramAlert, btnEmailSettings, btnTelegramSettings, btnServiceManager, btnUpdateSettings, btnCheckUpdate, chkMinimizeToTray, btnTestSound, btnTestPopup, btnStartStop
            });

            gbControl.Controls.Add(flowTools);
            mainLayout.Controls.Add(gbControl, 0, 1);

            // -------------------------------------------------------------
            // 3. RESPONSIVE DATAGRIDVIEW MONITORED TARGETS (WITH CYBERSEC COLUMNS)
            // -------------------------------------------------------------
            dgvTargets = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = bgCard,
                ForeColor = fgText,
                GridColor = Color.FromArgb(49, 50, 68),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            ContextMenuStrip gridMenu = new ContextMenuStrip();
            gridMenu.Items.Add("✏️ Cihaz Özelliklerini Düzenle", null, delegate(object s, EventArgs e) {
                if (dgvTargets.SelectedRows.Count > 0) {
                    TargetItem sel = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (sel != null) OpenEditDeviceDialog(sel);
                }
            });
            gridMenu.Items.Add("⚙️ Seçili Hedeflerin Ayarlarını Toplu Düzenle", null, delegate(object s, EventArgs e) {
                var selected = GetSelectedTargets();
                if (selected.Count > 0) OpenBatchEditDialog(selected);
            });
            gridMenu.Items.Add("✅ Çözüldü Olarak İşaretle (Yeniden Tetikle)", null, delegate(object s, EventArgs e) {
                if (dgvTargets.SelectedRows.Count > 0) {
                    TargetItem item = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (item != null) ResetItemAlert(item);
                }
            });
            gridMenu.Items.Add("🛡️ Bu Hedefe Port Zafiyet Taraması Yap", null, delegate(object s, EventArgs e) {
                if (dgvTargets.SelectedRows.Count > 0) {
                    TargetItem sel = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (sel != null) {
                        SecurityPortScannerForm dlg = new SecurityPortScannerForm(sel.Host);
                        dlg.ShowDialog(this);
                    }
                }
            });
            gridMenu.Items.Add("📊 Bu Hedefin SLA Raporunu Al", null, delegate(object s, EventArgs e) { GenerateSlaReport(); });
            gridMenu.Items.Add("🗑️ Seçili Hedefleri Sil", null, BtnRemove_Click);
            gridMenu.Items.Add("-");
            gridMenu.Items.Add("🔊 Zabbix Alarm Testi", null, delegate(object s, EventArgs e) { PlayAlarmSound(); });

            dgvTargets.ContextMenuStrip = gridMenu;

            dgvTargets.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(17, 17, 27);
            dgvTargets.ColumnHeadersDefaultCellStyle.ForeColor = accentBlue;
            dgvTargets.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvTargets.ColumnHeadersHeight = 34;

            dgvTargets.DefaultCellStyle.BackColor = bgCard;
            dgvTargets.DefaultCellStyle.ForeColor = fgText;
            dgvTargets.DefaultCellStyle.SelectionBackColor = Color.FromArgb(69, 71, 90);
            dgvTargets.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvTargets.RowTemplate.Height = 30;

            // Define Responsive FillWeight Columns
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "GroupName", HeaderText = "Grup", FillWeight = 85 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Host", HeaderText = "IP / Alan Adı", FillWeight = 105 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProtocolDisplay", HeaderText = "Protokol", FillWeight = 75 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Description", HeaderText = "Açıklama", FillWeight = 100 });
            
            DataGridViewTextBoxColumn colStatus = new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Durum", FillWeight = 75 };
            dgvTargets.Columns.Add(colStatus);

            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastLatency", HeaderText = "Ping (ms)", FillWeight = 60 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EffectiveIntervalDisplay", HeaderText = "Aralık", FillWeight = 65 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EffectiveTimeoutDisplay", HeaderText = "Timeout", FillWeight = 70 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EffectiveThresholdDisplay", HeaderText = "Eşik", FillWeight = 60 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "EmailAlertDisplay", HeaderText = "E-Posta", FillWeight = 85 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UptimePercentage", HeaderText = "Uptime", FillWeight = 55 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SslExpiryInfo", HeaderText = "SSL Vadesi", FillWeight = 75 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RblStatus", HeaderText = "RBL Liste", FillWeight = 80 });
            dgvTargets.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DowntimeDuration", HeaderText = "Kesinti Süresi", FillWeight = 75 });

            dgvTargets.DataSource = targets;
            dgvTargets.CellFormatting += DgvTargets_CellFormatting;
            dgvTargets.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < dgvTargets.Rows.Count)
                {
                    TargetItem sel = dgvTargets.Rows[e.RowIndex].DataBoundItem as TargetItem;
                    if (sel != null) OpenEditDeviceDialog(sel);
                }
            };
            dgvTargets.SelectionChanged += delegate(object s, EventArgs e)
            {
                if (dgvTargets.SelectedRows.Count > 0)
                {
                    TargetItem sel = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                    if (sel != null)
                    {
                        txtHost.Text = sel.Host;
                        txtPort.Text = sel.Port.ToString();
                        txtDesc.Text = sel.Description;
                    }
                }
                if (panelGraph != null) panelGraph.Invalidate();
            };

            // -------------------------------------------------------------
            // SPLIT CONTAINER FOR GROUP TREE (LEFT) & DATAGRIDVIEW (RIGHT)
            // -------------------------------------------------------------
            SplitContainer splitCenter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 220,
                Margin = new Padding(0),
                BackColor = bgDark
            };

            GroupBox gbGroups = new GroupBox
            {
                Text = " Cihaz Grupları (TNM Tree) ",
                Dock = DockStyle.Fill,
                ForeColor = accentBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = bgCard,
                Padding = new Padding(6)
            };

            treeGroups = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(21, 31, 40),
                ForeColor = Color.FromArgb(236, 240, 241),
                LineColor = Color.FromArgb(52, 152, 219),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FullRowSelect = true,
                ShowLines = true,
                ShowPlusMinus = true
            };
            treeGroups.AfterSelect += TreeGroups_AfterSelect;
            gbGroups.Controls.Add(treeGroups);
            splitCenter.Panel1.Controls.Add(gbGroups);

            splitCenter.Panel2.Controls.Add(dgvTargets);

            mainLayout.Controls.Add(splitCenter, 0, 2);

            // -------------------------------------------------------------
            // 4. LOG & TREND GRAPH SPLIT PANEL
            // -------------------------------------------------------------
            TableLayoutPanel bottomSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
                Margin = new Padding(0)
            };
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F)); // Log
            bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F)); // Live Graph

            GroupBox gbLog = new GroupBox
            {
                Text = " Canlı Tehdit & Olay Günlüğü ",
                Dock = DockStyle.Fill,
                ForeColor = accentBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = bgCard,
                Padding = new Padding(6)
            };

            Panel panelLogHeader = new Panel { Dock = DockStyle.Bottom, Height = 25 };
            btnClearLog = CreateStyledButton("🧹 Günlüğü Temizle", Color.FromArgb(69, 71, 90));
            btnClearLog.Height = 24;
            btnClearLog.Dock = DockStyle.Right;
            btnClearLog.Click += delegate(object s, EventArgs e) { txtLog.Clear(); };
            panelLogHeader.Controls.Add(btnClearLog);

            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(17, 17, 27),
                ForeColor = fgText,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 8.5F, FontStyle.Regular)
            };

            gbLog.Controls.Add(txtLog);
            gbLog.Controls.Add(panelLogHeader);

            GroupBox gbGraph = new GroupBox
            {
                Text = " Canlı Ping Trend Grafiği ",
                Dock = DockStyle.Fill,
                ForeColor = accentBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = bgCard,
                Padding = new Padding(6)
            };

            panelGraph = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgCard
            };
            panelGraph.Paint += PanelGraph_Paint;
            gbGraph.Controls.Add(panelGraph);

            bottomSplit.Controls.Add(gbLog, 0, 0);
            bottomSplit.Controls.Add(gbGraph, 1, 0);

            mainLayout.Controls.Add(bottomSplit, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private string GetSystemDriveAndSecurityInfo()
        {
            try
            {
                DriveInfo c = new DriveInfo("C");
                long freeGb = c.AvailableFreeSpace / (1024 * 1024 * 1024);
                long totalGb = c.TotalSize / (1024 * 1024 * 1024);
                return string.Format("C: Disk: {0} GB Boş / {1} GB | RBL Blacklist + WHOIS + DNS Poisoning Koruması Aktif", freeGb, totalGb);
            }
            catch
            {
                return "Siber Güvenlik Tehdit & Kesintisiz Sunucu İzleme Otomasyonu";
            }
        }

        public bool AddTargetIfNotExists(string host, int port, string description)
        {
            if (string.IsNullOrEmpty(host)) return false;
            if (!targets.Any(t => t.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && t.Port == port))
            {
                targets.Add(new TargetItem { Host = host, Port = port, Description = description });
                SaveTargetsToFile(false);
                UpdateCounters();
                return true;
            }
            return false;
        }

        private Label CreateCounterCard(Control parent, string title, string defaultValue, Color valColor)
        {
            Panel card = new Panel
            {
                Size = new Size(145, 65),
                BackColor = bgCard,
                Margin = new Padding(5, 0, 5, 0)
            };

            Label lblT = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = fgMuted,
                Location = new Point(8, 8),
                AutoSize = true
            };

            Label lblV = new Label
            {
                Text = defaultValue,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = valColor,
                Location = new Point(8, 26),
                AutoSize = true
            };

            card.Controls.Add(lblT);
            card.Controls.Add(lblV);
            parent.Controls.Add(card);

            return lblV;
        }

        private Button CreateStyledButton(string text, Color backColor)
        {
            Button btn = new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = fgText,
                FlatStyle = FlatStyle.Flat,
                Height = 28,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(3, 3, 3, 3)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void SetupTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Göster", null, delegate(object s, EventArgs e) { RestoreFromTray(); });
            trayMenu.Items.Add("İzlemeyi Başlat/Durdur", null, delegate(object s, EventArgs e) { BtnStartStop_Click(s, e); });
            trayMenu.Items.Add("🔄 Güncellemeleri Kontrol Et", null, delegate(object s, EventArgs e) { Task.Run(async delegate() { await CheckForUpdatesAsync(true); }); });
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Çıkış", null, delegate(object s, EventArgs e) {
                chkMinimizeToTray.Checked = false;
                Application.Exit();
            });

            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Ping & Siber Güvenlik Monitörü",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            notifyIcon.DoubleClick += delegate(object s, EventArgs e) { RestoreFromTray(); };
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (chkMinimizeToTray.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.ShowBalloonTip(2000, "Ping Uyarı Programı", "Program arka planda çalışmaya devam ediyor.", ToolTipIcon.Info);
            }
            else
            {
                StopMonitoring();
                notifyIcon.Dispose();
            }
        }

        private void LoadDefaultTargets()
        {
            if (File.Exists(configFilePath))
            {
                LoadTargetsFromFile();
            }
            else
            {
                targets.Add(new TargetItem { Host = "8.8.8.8", Port = 0, Description = "Google DNS" });
                targets.Add(new TargetItem { Host = "1.1.1.1", Port = 0, Description = "Cloudflare DNS" });
                targets.Add(new TargetItem { Host = "google.com", Port = 443, Description = "Google HTTPS Web" });
                SaveTargetsToFile(false);
                UpdateCounters();
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string host = txtHost.Text.Trim();
            string desc = txtDesc.Text.Trim();
            int port = 0;
            int.TryParse(txtPort.Text.Trim(), out port);

            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Lütfen bir IP adresi veya alan adı girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (AddTargetIfNotExists(host, port, string.IsNullOrEmpty(desc) ? host : desc))
            {
                txtHost.Clear();
                txtDesc.Clear();
                txtPort.Text = "0";
                LogMessage(string.Format("➕ Eklendi: {0}:{1} ({2})", host, port, desc), Color.LightGreen);
            }
            else
            {
                MessageBox.Show("Bu adres ve port zaten listede ekli.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnBatchAdd_Click(object sender, EventArgs e)
        {
            Form batchForm = new Form
            {
                Text = "Toplu IP / Alan Adı Ekleme",
                Size = new Size(450, 350),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblInfo = new Label
            {
                Text = "Her satıra bir IP veya alan adı girin (İsteğe bağlı port için IP:Port):",
                Location = new Point(12, 10),
                AutoSize = true
            };

            TextBox txtBatch = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(12, 35),
                Size = new Size(410, 220),
                BackColor = bgInput,
                ForeColor = fgText
            };

            Button btnConfirm = CreateStyledButton("Ekle", accentBlue);
            btnConfirm.ForeColor = Color.Black;
            btnConfirm.Location = new Point(345, 265);
            btnConfirm.Click += delegate(object s, EventArgs ev)
            {
                string[] lines = txtBatch.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                int added = 0;
                foreach (string line in lines)
                {
                    string h = line.Trim();
                    if (!string.IsNullOrEmpty(h))
                    {
                        int p = 0;
                        if (h.Contains(":"))
                        {
                            string[] parts = h.Split(':');
                            h = parts[0].Trim();
                            int.TryParse(parts[1].Trim(), out p);
                        }

                        if (AddTargetIfNotExists(h, p, h))
                        {
                            added++;
                        }
                    }
                }
                LogMessage(string.Format("📋 Toplu ekleme yapıldı ({0} adet).", added), Color.LightGreen);
                batchForm.Close();
            };

            batchForm.Controls.Add(lblInfo);
            batchForm.Controls.Add(txtBatch);
            batchForm.Controls.Add(btnConfirm);
            batchForm.ShowDialog(this);
        }

        private List<TargetItem> GetSelectedTargets()
        {
            List<TargetItem> list = new List<TargetItem>();
            foreach (DataGridViewRow row in dgvTargets.SelectedRows)
            {
                TargetItem item = row.DataBoundItem as TargetItem;
                if (item != null && !list.Contains(item))
                {
                    list.Add(item);
                }
            }
            return list;
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            var selectedList = GetSelectedTargets();
            if (selectedList.Count > 0)
            {
                foreach (var item in selectedList)
                {
                    targets.Remove(item);
                }
                SaveTargetsToFile(false);
                UpdateCounters();
                LogMessage(string.Format("🗑️ {0} adet cihaz silindi.", selectedList.Count), Color.Orange);
                if (panelGraph != null) panelGraph.Invalidate();
            }
            else
            {
                MessageBox.Show("Lütfen silmek için tablodan en az bir satır seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenEditDeviceDialog(TargetItem item)
        {
            if (item == null) return;

            Form dlg = new Form
            {
                Text = string.Format("✏️ Cihaz Özelliklerini Düzenle - {0}", item.Host),
                Size = new Size(520, 520),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblHost = new Label { Text = "IP / Alan Adı:", Location = new Point(20, 20), AutoSize = true };
            TextBox txtEditHost = new TextBox { Text = item.Host, Location = new Point(160, 17), Width = 320, BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };

            Label lblPort = new Label { Text = "Port / Protokol:", Location = new Point(20, 55), AutoSize = true };
            NumericUpDown numEditPort = new NumericUpDown { Minimum = 0, Maximum = 65535, Value = item.Port, Location = new Point(160, 52), Width = 100, BackColor = bgInput, ForeColor = fgText };
            Label lblPortInfo = new Label { Text = "(0 = ICMP Ping, 80/443 = HTTP)", Location = new Point(270, 55), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            Label lblDesc = new Label { Text = "Açıklama / Ad:", Location = new Point(20, 90), AutoSize = true };
            TextBox txtEditDesc = new TextBox { Text = item.Description, Location = new Point(160, 87), Width = 320, BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };

            Label lblGroup = new Label { Text = "Cihaz Grubu:", Location = new Point(20, 125), AutoSize = true };
            ComboBox cmbGroup = new ComboBox { Location = new Point(160, 122), Width = 320, BackColor = bgInput, ForeColor = fgText, DropDownStyle = ComboBoxStyle.DropDown };
            var existingGroups = targets.Select(t => string.IsNullOrWhiteSpace(t.GroupName) ? "Genel Cihazlar" : t.GroupName).Distinct().OrderBy(g => g).ToArray();
            cmbGroup.Items.AddRange(existingGroups);
            cmbGroup.Text = string.IsNullOrWhiteSpace(item.GroupName) ? "Genel Cihazlar" : item.GroupName;

            Label lblDivider = new Label
            {
                Text = "⚙️ Hosta Özel İzleme & Alarm Ayarları (0 = Genel Varsayılan)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = accentBlue,
                Location = new Point(20, 165),
                AutoSize = true
            };

            Label lblInterval = new Label { Text = "Özel Ping Aralığı (sn):", Location = new Point(20, 200), AutoSize = true };
            NumericUpDown numEditInterval = new NumericUpDown { Minimum = 0, Maximum = 300, Value = item.IntervalSeconds, Location = new Point(160, 197), Width = 100, BackColor = bgInput, ForeColor = fgText };
            Label lblIntervalHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(270, 200), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            Label lblTimeout = new Label { Text = "Özel Timeout (ms):", Location = new Point(20, 235), AutoSize = true };
            NumericUpDown numEditTimeout = new NumericUpDown { Minimum = 0, Maximum = 10000, Increment = 100, Value = item.TimeoutMs, Location = new Point(160, 232), Width = 100, BackColor = bgInput, ForeColor = fgText };
            Label lblTimeoutHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(270, 235), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            Label lblThreshold = new Label { Text = "Özel Kesinti Eşiği:", Location = new Point(20, 270), AutoSize = true };
            NumericUpDown numEditThreshold = new NumericUpDown { Minimum = 0, Maximum = 10, Value = item.FailureThreshold, Location = new Point(160, 267), Width = 100, BackColor = bgInput, ForeColor = fgText };
            Label lblThresholdHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(270, 270), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            CheckBox chkEnableEmail = new CheckBox { Text = "Bu Hosta Özel E-Posta Bildirimi Gönder", Checked = item.EnableEmailAlert, Location = new Point(160, 305), AutoSize = true, ForeColor = fgText };

            Label lblCustomEmail = new Label { Text = "Özel Alıcı E-Posta:", Location = new Point(20, 340), AutoSize = true };
            TextBox txtEditCustomEmail = new TextBox { Text = item.CustomEmail, Location = new Point(160, 337), Width = 320, BackColor = bgInput, ForeColor = fgText, BorderStyle = BorderStyle.FixedSingle };
            Label lblCustomEmailHelp = new Label { Text = "(Boş bırakılırsa genel e-posta alıcısı kullanılır)", Location = new Point(160, 365), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };

            Button btnCancel = CreateStyledButton("❌ İptal", Color.FromArgb(120, 40, 40));
            btnCancel.Location = new Point(270, 420);
            btnCancel.Width = 100;
            btnCancel.Click += delegate(object s, EventArgs ev) { dlg.Close(); };

            Button btnSaveEdit = CreateStyledButton("💾 Kaydet", colorOnline);
            btnSaveEdit.ForeColor = Color.Black;
            btnSaveEdit.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnSaveEdit.Location = new Point(380, 420);
            btnSaveEdit.Width = 100;
            btnSaveEdit.Click += delegate(object s, EventArgs ev)
            {
                string newHost = txtEditHost.Text.Trim();
                if (string.IsNullOrEmpty(newHost))
                {
                    MessageBox.Show("Lütfen geçerli bir IP adresi veya alan adı girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                item.Host = newHost;
                item.Port = (int)numEditPort.Value;
                item.Description = txtEditDesc.Text.Trim();
                item.GroupName = string.IsNullOrWhiteSpace(cmbGroup.Text.Trim()) ? "Genel Cihazlar" : cmbGroup.Text.Trim();
                item.IntervalSeconds = (int)numEditInterval.Value;
                item.TimeoutMs = (int)numEditTimeout.Value;
                item.FailureThreshold = (int)numEditThreshold.Value;
                item.EnableEmailAlert = chkEnableEmail.Checked;
                item.CustomEmail = txtEditCustomEmail.Text.Trim();

                SaveTargetsToFile(false);
                PopulateGroupTree();
                dgvTargets.Refresh();
                UpdateCounters();
                LogMessage(string.Format("✏️ Güncellendi: {0} ({1}) - Özel Ayarlar Saklandı", item.Host, item.Description), accentBlue);
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] {
                lblHost, txtEditHost, lblPort, numEditPort, lblPortInfo, lblDesc, txtEditDesc, lblGroup, cmbGroup,
                lblDivider, lblInterval, numEditInterval, lblIntervalHelp, lblTimeout, numEditTimeout, lblTimeoutHelp,
                lblThreshold, numEditThreshold, lblThresholdHelp, chkEnableEmail, lblCustomEmail, txtEditCustomEmail, lblCustomEmailHelp,
                btnCancel, btnSaveEdit
            });

            dlg.ShowDialog(this);
        }

        private void OpenBatchEditDialog(List<TargetItem> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("Lütfen toplu ayar düzenlemek için tablodan en az bir cihaz seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Form dlg = new Form
            {
                Text = string.Format("⚙️ Seçili {0} Adet Cihazın Ayarlarını Toplu Düzenle", selectedItems.Count),
                Size = new Size(540, 440),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblInfo = new Label
            {
                Text = string.Format("Seçili {0} adet cihaz için değiştirmek istediğiniz ayarların kutucuğunu işaretleyin:", selectedItems.Count),
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = accentBlue
            };

            CheckBox chkGroup = new CheckBox { Text = "Cihaz Grubunu Değiştir:", Location = new Point(20, 50), AutoSize = true, ForeColor = fgText };
            ComboBox cmbGroup = new ComboBox { Location = new Point(230, 48), Width = 270, BackColor = bgInput, ForeColor = fgText, Enabled = false };
            var existingGroups = targets.Select(t => string.IsNullOrWhiteSpace(t.GroupName) ? "Genel Cihazlar" : t.GroupName).Distinct().OrderBy(g => g).ToArray();
            cmbGroup.Items.AddRange(existingGroups);
            chkGroup.CheckedChanged += delegate { cmbGroup.Enabled = chkGroup.Checked; };

            CheckBox chkInterval = new CheckBox { Text = "Ping Aralığını Değiştir (sn):", Location = new Point(20, 90), AutoSize = true, ForeColor = fgText };
            NumericUpDown numEditInterval = new NumericUpDown { Minimum = 0, Maximum = 300, Value = 0, Location = new Point(230, 88), Width = 90, BackColor = bgInput, ForeColor = fgText, Enabled = false };
            Label lblIntHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(330, 90), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };
            chkInterval.CheckedChanged += delegate { numEditInterval.Enabled = chkInterval.Checked; };

            CheckBox chkTimeout = new CheckBox { Text = "Timeout Süresini Değiştir (ms):", Location = new Point(20, 130), AutoSize = true, ForeColor = fgText };
            NumericUpDown numEditTimeout = new NumericUpDown { Minimum = 0, Maximum = 10000, Increment = 100, Value = 0, Location = new Point(230, 128), Width = 90, BackColor = bgInput, ForeColor = fgText, Enabled = false };
            Label lblToHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(330, 130), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };
            chkTimeout.CheckedChanged += delegate { numEditTimeout.Enabled = chkTimeout.Checked; };

            CheckBox chkThreshold = new CheckBox { Text = "Kesinti Eşiğini Değiştir:", Location = new Point(20, 170), AutoSize = true, ForeColor = fgText };
            NumericUpDown numEditThreshold = new NumericUpDown { Minimum = 0, Maximum = 10, Value = 0, Location = new Point(230, 168), Width = 90, BackColor = bgInput, ForeColor = fgText, Enabled = false };
            Label lblThHelp = new Label { Text = "(0 = Genel Varsayılan)", Location = new Point(330, 170), AutoSize = true, ForeColor = fgMuted, Font = new Font("Segoe UI", 8F, FontStyle.Italic) };
            chkThreshold.CheckedChanged += delegate { numEditThreshold.Enabled = chkThreshold.Checked; };

            CheckBox chkEmailAlert = new CheckBox { Text = "E-Posta Bildirim Yetkisi:", Location = new Point(20, 210), AutoSize = true, ForeColor = fgText };
            CheckBox chkEnableEmailValue = new CheckBox { Text = "E-Posta Bildirimi Açık", Checked = true, Location = new Point(230, 210), AutoSize = true, ForeColor = fgText, Enabled = false };
            chkEmailAlert.CheckedChanged += delegate { chkEnableEmailValue.Enabled = chkEmailAlert.Checked; };

            CheckBox chkCustomEmail = new CheckBox { Text = "Özel E-Posta Alıcısını Değiştir:", Location = new Point(20, 250), AutoSize = true, ForeColor = fgText };
            TextBox txtCustomEmail = new TextBox { Location = new Point(230, 248), Width = 270, BackColor = bgInput, ForeColor = fgText, Enabled = false };
            chkCustomEmail.CheckedChanged += delegate { txtCustomEmail.Enabled = chkCustomEmail.Checked; };

            Button btnCancel = CreateStyledButton("❌ İptal", Color.FromArgb(120, 40, 40));
            btnCancel.Location = new Point(280, 330);
            btnCancel.Width = 100;
            btnCancel.Click += delegate(object s, EventArgs ev) { dlg.Close(); };

            Button btnApplyBatch = CreateStyledButton("💾 Toplu Uygula", colorOnline);
            btnApplyBatch.ForeColor = Color.Black;
            btnApplyBatch.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnApplyBatch.Location = new Point(390, 330);
            btnApplyBatch.Width = 110;
            btnApplyBatch.Click += delegate(object s, EventArgs ev)
            {
                foreach (var item in selectedItems)
                {
                    if (chkGroup.Checked) item.GroupName = string.IsNullOrWhiteSpace(cmbGroup.Text.Trim()) ? "Genel Cihazlar" : cmbGroup.Text.Trim();
                    if (chkInterval.Checked) item.IntervalSeconds = (int)numEditInterval.Value;
                    if (chkTimeout.Checked) item.TimeoutMs = (int)numEditTimeout.Value;
                    if (chkThreshold.Checked) item.FailureThreshold = (int)numEditThreshold.Value;
                    if (chkEmailAlert.Checked) item.EnableEmailAlert = chkEnableEmailValue.Checked;
                    if (chkCustomEmail.Checked) item.CustomEmail = txtCustomEmail.Text.Trim();
                }

                SaveTargetsToFile(false);
                PopulateGroupTree();
                dgvTargets.Refresh();
                UpdateCounters();
                LogMessage(string.Format("⚙️ Toplu ayar güncellendi: {0} adet cihaz güncellendi.", selectedItems.Count), Color.LightGreen);
                MessageBox.Show(string.Format("{0} adet cihazın ayarları başarıyla toplu olarak güncellendi.", selectedItems.Count), "Toplu Güncelleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] {
                lblInfo, chkGroup, cmbGroup, chkInterval, numEditInterval, lblIntHelp, chkTimeout, numEditTimeout, lblToHelp,
                chkThreshold, numEditThreshold, lblThHelp, chkEmailAlert, chkEnableEmailValue, chkCustomEmail, txtCustomEmail,
                btnCancel, btnApplyBatch
            });

            dlg.ShowDialog(this);
        }

        private void BtnResetAlert_Click(object sender, EventArgs e)
        {
            if (dgvTargets.SelectedRows.Count > 0)
            {
                TargetItem item = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
                if (item != null)
                {
                    ResetItemAlert(item);
                    return;
                }
            }

            var offlineItems = targets.Where(t => t.Status == "OFFLINE").ToList();
            if (offlineItems.Count > 0)
            {
                foreach (var item in offlineItems)
                {
                    ResetItemAlert(item);
                }
            }
            else
            {
                MessageBox.Show("Sıfırlanacak kesinti uyarısı bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ResetItemAlert(TargetItem item)
        {
            item.Status = "BEKLENİYOR";
            item.ConsecutiveFailures = 0;
            item.FirstOfflineTime = "-";
            item.OfflineStartDateTime = null;
            LogMessage(string.Format("✅ {0} ({1}) uyarısı 'Çözüldü' olarak sıfırlandı. Sonraki kontrolde durum kesintideyse tekrar tetiklenecektir.", item.Host, item.Description), accentBlue);
            dgvTargets.Refresh();
            UpdateCounters();
            if (panelGraph != null) panelGraph.Invalidate();
        }

        private void LogDowntimeToCsv(TargetItem item, string eventType, string reason)
        {
            try
            {
                string csvPath = Path.Combine(Application.StartupPath, "kesinti_gecmisi.csv");
                bool exists = File.Exists(csvPath);

                using (StreamWriter sw = new StreamWriter(csvPath, true, Encoding.UTF8))
                {
                    if (!exists)
                    {
                        sw.WriteLine("Tarih;Saat;IP/Host;Port/Protokol;Açıklama;Olay_Türü;İlk_Kesinti;Kesinti_Süresi;Sebep");
                    }

                    string line = string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}",
                        DateTime.Now.ToString("dd.MM.yyyy"),
                        DateTime.Now.ToString("HH:mm:ss"),
                        item.Host,
                        item.ProtocolDisplay,
                        item.Description,
                        eventType,
                        item.FirstOfflineTime,
                        item.DowntimeDuration,
                        reason);

                    sw.WriteLine(line);
                }
            }
            catch { }
        }

        private void GenerateSlaReport()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Ping & SLA Erişilebilirlik Raporu</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, sans-serif; background: #1e1e2e; color: #cdd6f4; margin: 20px; }");
                sb.AppendLine("h1 { color: #89b4fa; } table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
                sb.AppendLine("th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #313244; }");
                sb.AppendLine("th { background: #11111b; color: #89b4fa; } tr:nth-child(even) { background: #181825; }");
                sb.AppendLine(".online { color: #a6e3a1; font-weight: bold; } .offline { color: #f38ba8; font-weight: bold; }");
                sb.AppendLine("</style></head><body>");
                sb.AppendLine(string.Format("<h1>📡 Ping & SLA Erişilebilirlik ve Güvenlik Raporu</h1><p>Rapor Tarihi: {0}</p>", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")));

                sb.AppendLine("<table><tr><th>IP / Host</th><th>Protokol</th><th>Açıklama</th><th>Durum</th><th>Uptime %</th><th>SSL Vadesi</th><th>Domain Vadesi</th><th>Kara Liste (RBL)</th><th>İlk Kesinti</th><th>Kesinti Süresi</th></tr>");

                foreach (var item in targets)
                {
                    string statusClass = item.Status == "ONLINE" ? "online" : (item.Status == "OFFLINE" ? "offline" : "");
                    sb.AppendLine(string.Format(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td class='{3}'>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td><td>{9}</td><td>{10}</td></tr>",
                        item.Host, item.ProtocolDisplay, item.Description, statusClass, item.Status,
                        item.UptimePercentage, item.SslExpiryInfo, item.WhoisExpiryInfo, item.RblStatus, item.FirstOfflineTime, item.DowntimeDuration));
                }

                sb.AppendLine("</table></body></html>");

                string fileName = string.Format("Ping_SLA_Raporu_{0:yyyyMMdd_HHmmss}.html", DateTime.Now);
                string filePath = Path.Combine(Application.StartupPath, fileName);
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                Process.Start(filePath);
                LogMessage("📊 SLA & Güvenlik Raporu oluşturuldu ve tarayıcıda açıldı: " + fileName, colorOnline);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rapor hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveTargetsToFile(bool showMessageBox = true)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in targets)
                {
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                        item.Host,
                        item.Port,
                        item.Description,
                        item.GroupName,
                        item.IntervalSeconds,
                        item.TimeoutMs,
                        item.FailureThreshold,
                        item.EnableEmailAlert,
                        item.CustomEmail));
                }
                File.WriteAllText(configFilePath, sb.ToString(), Encoding.UTF8);
                if (showMessageBox)
                {
                    MessageBox.Show("Hedef ve grup listesi kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (showMessageBox)
                {
                    MessageBox.Show("Kaydetme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadTargetsFromFile()
        {
            if (!File.Exists(configFilePath)) return;
            try
            {
                targets.Clear();
                string[] lines = File.ReadAllLines(configFilePath, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('|');
                    string host = parts[0].Trim();
                    int port = 0;
                    if (parts.Length > 1) int.TryParse(parts[1].Trim(), out port);
                    string desc = parts.Length > 2 ? parts[2].Trim() : host;
                    string group = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3].Trim()) ? parts[3].Trim() : "Genel Cihazlar";

                    int interval = 0;
                    if (parts.Length > 4) int.TryParse(parts[4].Trim(), out interval);

                    int timeout = 0;
                    if (parts.Length > 5) int.TryParse(parts[5].Trim(), out timeout);

                    int threshold = 0;
                    if (parts.Length > 6) int.TryParse(parts[6].Trim(), out threshold);

                    bool enableEmail = true;
                    if (parts.Length > 7) bool.TryParse(parts[7].Trim(), out enableEmail);

                    string customEmail = parts.Length > 8 ? parts[8].Trim() : "";

                    targets.Add(new TargetItem
                    {
                        Host = host,
                        Port = port,
                        Description = desc,
                        GroupName = group,
                        IntervalSeconds = interval,
                        TimeoutMs = timeout,
                        FailureThreshold = threshold,
                        EnableEmailAlert = enableEmail,
                        CustomEmail = customEmail,
                        Status = "BEKLENİYOR"
                    });
                }
                PopulateGroupTree();
                UpdateCounters();
            }
            catch (Exception ex)
            {
                LogMessage("Dosya yükleme hatası: " + ex.Message, Color.Red);
            }
        }

        private void PopulateGroupTree()
        {
            if (treeGroups == null) return;
            treeGroups.Nodes.Clear();

            TreeNode rootNode = new TreeNode(string.Format("🌐 Tüm Cihazlar ({0})", targets.Count));
            rootNode.Tag = "ALL";

            var groupNames = targets.Select(t => string.IsNullOrWhiteSpace(t.GroupName) ? "Genel Cihazlar" : t.GroupName).Distinct().OrderBy(g => g).ToList();

            foreach (var gName in groupNames)
            {
                int count = targets.Count(t => (string.IsNullOrWhiteSpace(t.GroupName) ? "Genel Cihazlar" : t.GroupName) == gName);
                TreeNode gNode = new TreeNode(string.Format("📁 {0} ({1})", gName, count));
                gNode.Tag = gName;
                rootNode.Nodes.Add(gNode);
            }

            treeGroups.Nodes.Add(rootNode);
            rootNode.ExpandAll();
        }

        private void TreeGroups_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null || e.Node.Tag == null) return;
            string selectedGroup = e.Node.Tag.ToString();

            if (selectedGroup == "ALL")
            {
                dgvTargets.DataSource = targets;
            }
            else
            {
                var filtered = targets.Where(t => (string.IsNullOrWhiteSpace(t.GroupName) ? "Genel Cihazlar" : t.GroupName) == selectedGroup).ToList();
                dgvTargets.DataSource = new BindingList<TargetItem>(filtered);
            }
        }

        private void ImportTnmXmlFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Total Network Monitor XML Proje (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*";
                ofd.Title = "Total Network Monitor XML Proje Dosyası Seçin (ress.xml)";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                        doc.Load(ofd.FileName);

                        targets.Clear();
                        var groups = doc.SelectNodes("//Group");
                        int importedCount = 0;

                        if (groups != null && groups.Count > 0)
                        {
                            foreach (System.Xml.XmlNode grp in groups)
                            {
                                string groupName = grp.Attributes["name"] != null ? grp.Attributes["name"].Value : "Genel Cihazlar";
                                var devices = grp.SelectNodes("./Device");
                                if (devices == null) continue;
                                foreach (System.Xml.XmlNode dev in devices)
                                {
                                    string host = dev.Attributes["ipAddress"] != null && !string.IsNullOrWhiteSpace(dev.Attributes["ipAddress"].Value)
                                        ? dev.Attributes["ipAddress"].Value.Trim()
                                        : (dev.Attributes["hostname"] != null ? dev.Attributes["hostname"].Value.Trim() : "");

                                    if (string.IsNullOrEmpty(host)) continue;

                                    string name = dev.Attributes["name"] != null ? dev.Attributes["name"].Value.Trim() : "";
                                    string hostname = dev.Attributes["hostname"] != null ? dev.Attributes["hostname"].Value.Trim() : "";
                                    string descr = dev.Attributes["descr"] != null ? dev.Attributes["descr"].Value.Trim() : "";

                                    string desc = name;
                                    if (!string.Equals(name, hostname, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(hostname))
                                    {
                                        desc = string.Format("{0} ({1})", hostname, name);
                                    }
                                    if (!string.IsNullOrEmpty(descr) && !string.Equals(descr, name, StringComparison.OrdinalIgnoreCase) && !string.Equals(descr, hostname, StringComparison.OrdinalIgnoreCase))
                                    {
                                        desc += " - " + descr;
                                    }

                                    targets.Add(new TargetItem { Host = host, Port = 0, Description = desc, GroupName = groupName, Status = "BEKLENİYOR" });
                                    importedCount++;
                                }
                            }
                        }

                        SaveTargetsToFile(false);
                        PopulateGroupTree();
                        UpdateCounters();
                        LogMessage(string.Format("📂 Total Network Monitor XML dosyasından {0} cihaz içe aktarıldı: {1}", importedCount, Path.GetFileName(ofd.FileName)), accentBlue);
                        MessageBox.Show(string.Format("Total Network Monitor projesinden {0} adet cihaz ve grupları başarıyla yüklendi.", importedCount), "TNM İçe Aktarım Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("XML Okuma Hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isMonitoring)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        private void StartMonitoring()
        {
            if (targets.Count == 0)
            {
                MessageBox.Show("İzlemek için en az bir hedef ekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isMonitoring = true;
            isPingTaskRunning = false;
            btnStartStop.Text = "⏹ DURDUR";
            btnStartStop.BackColor = colorOffline;

            if (pingTimer != null)
            {
                try { pingTimer.Dispose(); } catch { }
                pingTimer = null;
            }
            pingTimer = new System.Threading.Timer(PingCallback, null, 0, 1000);

            LogMessage("▶ Siber Güvenlik & Ağ İzleme başlatıldı.", accentBlue);
        }

        private void StopMonitoring()
        {
            isMonitoring = false;
            isPingTaskRunning = false;

            if (pingTimer != null)
            {
                try
                {
                    pingTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                    pingTimer.Dispose();
                }
                catch { }
                pingTimer = null;
            }

            btnStartStop.Text = "▶ İZLEMEYİ BAŞLAT";
            btnStartStop.BackColor = colorOnline;

            LogMessage("⏹ İzleme durduruldu.", Color.Orange);
        }

        private async void PingCallback(object state)
        {
            if (!isMonitoring) return;
            if (isPingTaskRunning) return;

            isPingTaskRunning = true;
            try
            {
                int defaultInterval = (int)numInterval.Value;
                int defaultTimeout = (int)numTimeout.Value;
                int defaultThreshold = (int)numThreshold.Value;

                DateTime now = DateTime.Now;
                List<Task> pingTasks = new List<Task>();

                foreach (var item in targets.ToList())
                {
                    if (!isMonitoring) break;

                    int itemInterval = item.IntervalSeconds > 0 ? item.IntervalSeconds : defaultInterval;
                    if (item.LastPingTime == DateTime.MinValue || (now - item.LastPingTime).TotalSeconds >= itemInterval)
                    {
                        item.LastPingTime = now;
                        int itemTimeout = item.TimeoutMs > 0 ? item.TimeoutMs : defaultTimeout;
                        int itemThreshold = item.FailureThreshold > 0 ? item.FailureThreshold : defaultThreshold;

                        pingTasks.Add(PingItemAsync(item, itemTimeout, itemThreshold));
                    }
                }

                if (pingTasks.Count > 0)
                {
                    await Task.WhenAll(pingTasks);
                }

                if (!isMonitoring) return;

                // Repeat alert & Multi-Host Popup Notification for ongoing OFFLINE targets
                var offlineList = targets.Where(t => t.Status == "OFFLINE").ToList();
                if (offlineList.Count > 0 && isMonitoring)
                {
                    if (chkToastAlert.Checked && isMonitoring)
                    {
                        OfflineAlertPopupForm.ShowAlert(this, offlineList);
                    }

                    if (chkAudioAlert.Checked && isMonitoring)
                    {
                        int repeatSec = (int)numRepeatInterval.Value;
                        if (repeatSec > 0)
                        {
                            TimeSpan elapsed = DateTime.Now - lastRepeatAlarmTime;
                            if (elapsed.TotalSeconds >= repeatSec)
                            {
                                lastRepeatAlarmTime = DateTime.Now;
                                PlayAlarmSound();
                                LogMessage(string.Format("🚨 Kesinti devam ediyor ({0} adet). {1} sn ikaz alarmı tekrarlandı.", offlineList.Count, repeatSec), colorOffline);
                            }
                        }
                    }
                }

                if (this.IsHandleCreated && isMonitoring)
                {
                    this.BeginInvoke(new Action(delegate()
                    {
                        if (!isMonitoring) return;
                        dgvTargets.Refresh();
                        UpdateCounters();
                        if (panelGraph != null) panelGraph.Invalidate();
                    }));
                }
            }
            finally
            {
                isPingTaskRunning = false;
            }
        }

        private async Task<int> CheckSslExpiryDaysAsync(string host, int port, int timeout)
        {
            return await Task.Run(delegate()
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        Task task = client.ConnectAsync(host, port > 0 ? port : 443);
                        if (Task.WaitAny(new Task[] { task }, timeout) == 0 && client.Connected)
                        {
                            using (SslStream sslStream = new SslStream(client.GetStream(), false, (sender, cert, chain, errors) => true))
                            {
                                sslStream.AuthenticateAsClient(host);
                                X509Certificate2 cert2 = new X509Certificate2(sslStream.RemoteCertificate);
                                TimeSpan remaining = cert2.NotAfter - DateTime.Now;
                                return (int)remaining.TotalDays;
                            }
                        }
                    }
                }
                catch { }
                return -999;
            });
        }

        private async Task<bool> CheckRblBlacklistAsync(string ipOrHost)
        {
            return await Task.Run(delegate()
            {
                try
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(ipOrHost, out ip))
                    {
                        IPHostEntry entry = Dns.GetHostEntry(ipOrHost);
                        if (entry.AddressList.Length > 0) ip = entry.AddressList[0];
                    }

                    if (ip != null)
                    {
                        byte[] bytes = ip.GetAddressBytes();
                        string reversedIP = string.Format("{0}.{1}.{2}.{3}", bytes[3], bytes[2], bytes[1], bytes[0]);
                        string query = reversedIP + ".zen.spamhaus.org";

                        IPHostEntry rblEntry = Dns.GetHostEntry(query);
                        return (rblEntry != null && rblEntry.AddressList.Length > 0);
                    }
                }
                catch { }
                return false;
            });
        }

        private async Task PingItemAsync(TargetItem item, int timeout, int threshold)
        {
            if (!isMonitoring) return;
            try
            {
                item.LastChecked = DateTime.Now.ToString("HH:mm:ss");
                item.TotalPings++;

                bool isValidSuccess = false;
                long latency = -1;

                // 1. CyberSec: SSL Certificate Expiry Check
                if (item.Port == 443 || item.Host.StartsWith("https://"))
                {
                    if (!isMonitoring) return;
                    int sslDays = await CheckSslExpiryDaysAsync(item.Host.Replace("https://", ""), item.Port, timeout);
                    if (!isMonitoring) return;
                    if (sslDays != -999)
                    {
                        item.SslExpiryInfo = string.Format("{0} Gün", sslDays);
                        if (sslDays <= 15)
                        {
                            LogMessage(string.Format("⚠️ SSL UYARISI: {0} sertifika vadesine {1} gün kaldı!", item.Host, sslDays), Color.Yellow);
                        }
                    }
                }

                if (!isMonitoring) return;

                // 2. CyberSec: Global RBL Kara Liste (Blacklist) Scanning
                bool isBlacklisted = await CheckRblBlacklistAsync(item.Host);
                if (!isMonitoring) return;

                if (isBlacklisted)
                {
                    item.RblStatus = "⚠️ KARA LİSTEDE!";
                    LogMessage(string.Format("🔴 SİBER UYARI: {0} adresi küresel Spamhaus RBL Kara Listesinde tespit edildi!", item.Host), Color.Red);
                }
                else
                {
                    item.RblStatus = "TEMİZ";
                }

                if (!isMonitoring) return;

                if (item.Port == 80 || item.Port == 443)
                {
                    // Full HTTP 200 OK Web Service Verification
                    item.ResolvedIP = item.Host;
                    Stopwatch sw = Stopwatch.StartNew();
                    bool httpOk = await Task.Run(delegate()
                    {
                        if (!isMonitoring) return false;
                        try
                        {
                            string scheme = item.Port == 443 ? "https://" : "http://";
                            string url = item.Host.StartsWith("http") ? item.Host : (scheme + item.Host);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                            req.Timeout = timeout;
                            req.Method = "HEAD";
                            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                            {
                                return (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 400;
                            }
                        }
                        catch { }
                        return false;
                    });
                    sw.Stop();

                    if (!isMonitoring) return;

                    if (httpOk)
                    {
                        isValidSuccess = true;
                        latency = sw.ElapsedMilliseconds;
                    }
                }
                else if (item.Port > 0)
                {
                    // Generic TCP Socket / MSSQL / PGSQL Port Check
                    item.ResolvedIP = item.Host;
                    Stopwatch sw = Stopwatch.StartNew();
                    bool connected = await Task.Run(delegate()
                    {
                        if (!isMonitoring) return false;
                        try
                        {
                            using (TcpClient client = new TcpClient())
                            {
                                Task task = client.ConnectAsync(item.Host, item.Port);
                                if (Task.WaitAny(new Task[] { task }, timeout) == 0 && client.Connected)
                                {
                                    return true;
                                }
                            }
                        }
                        catch { }
                        return false;
                    });
                    sw.Stop();

                    if (!isMonitoring) return;

                    if (connected)
                    {
                        isValidSuccess = true;
                        latency = sw.ElapsedMilliseconds;
                    }
                }
                else
                {
                    // ICMP Ping Check & DNS Poisoning Hijack Monitor
                    IPAddress parsedIP;
                    if (IPAddress.TryParse(item.Host, out parsedIP))
                    {
                        item.ResolvedIP = item.Host;
                    }
                    else
                    {
                        try
                        {
                            IPHostEntry entry = await Task.Run(delegate() {
                                if (!isMonitoring) return null;
                                return Dns.GetHostEntry(item.Host);
                            });
                            if (!isMonitoring) return;
                            if (entry != null && entry.AddressList.Length > 0)
                            {
                                string currentResolved = entry.AddressList[0].ToString();
                                if (item.BaselineIP == "-")
                                {
                                    item.BaselineIP = currentResolved;
                                }
                                else if (!item.BaselineIP.Equals(currentResolved, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 3. CyberSec: DNS Poisoning / Hijack Detection
                                    LogMessage(string.Format("🚨 SİBER SALDIRI / DNS DEĞİŞİKLİĞİ UYARISI: {0} IP adresi ({1}) değiştirilerek ({2}) oldu!", item.Host, item.BaselineIP, currentResolved), Color.Red);
                                    SendTelegramAlert(string.Format("🚨 SİBER UYARI: {0} DNS IP Değişikliği Tespit Edildi! Eski: {1}, Yeni: {2}", item.Host, item.BaselineIP, currentResolved));
                                    item.BaselineIP = currentResolved;
                                }
                                item.ResolvedIP = currentResolved;
                            }
                        }
                        catch { item.ResolvedIP = item.Host; }
                    }

                    if (!isMonitoring) return;

                    PingReply reply = await Task.Run(delegate()
                    {
                        if (!isMonitoring) return null;
                        using (Ping pinger = new Ping())
                        {
                            try
                            {
                                return pinger.Send(item.Host, timeout);
                            }
                            catch { return null; }
                        }
                    });

                    if (!isMonitoring) return;

                    isValidSuccess = (reply != null &&
                                           reply.Status == IPStatus.Success &&
                                           reply.RoundtripTime >= 0 &&
                                           reply.RoundtripTime <= (timeout + 100));
                    if (isValidSuccess)
                    {
                        latency = reply.RoundtripTime;
                    }
                }

                if (!isMonitoring) return;

                if (isValidSuccess)
                {
                    item.LastLatency = latency;
                    item.TotalLatencySum += latency;
                    item.SuccessCount++;
                    item.AddLatencyHistory(latency);

                    if (latency < item.MinLatency) item.MinLatency = latency;
                    if (latency > item.MaxLatency) item.MaxLatency = latency;

                    if (item.Status == "OFFLINE")
                    {
                        LogMessage(string.Format("🟢 GEÇERLİ: {0} ({1}) tekrar ONLINE! Latency: {2} ms", item.Host, item.Description, latency), colorOnline);
                        ShowNotification("🟢 Bağlantı Geri Geldi", string.Format("{0} ({1}) tekrar erişilebilir durumda ({2} ms).", item.Host, item.Description, latency), ToolTipIcon.Info);
                        LogDowntimeToCsv(item, "DUZELDI", "Online Oldu");

                        string msgText = string.Format("🟢 KESİNTİ DÜZELDİ: {0} ({1}) tekrar ONLINE! Latency: {2} ms", item.Host, item.Description, latency);
                        SendTelegramAlert(msgText);

                        if (chkEmailAlert.Checked)
                        {
                            string subject = string.Format("[PİNG UYARI] DÜZELDİ: {0} ({1}) - ONLINE", item.Host, item.Description);
                            string body = string.Format(
                                "🟢 KESİNTİ DÜZELDİ BİLDİRİMİ\n\n" +
                                "Tarih / Saat: {0}\n" +
                                "Hedef Adres: {1}\n" +
                                "Açıklama: {2}\n" +
                                "Durum: ÇEVRİMİÇİ (ONLINE)\n" +
                                "Gecikme Süresi: {3} ms\n\n" +
                                "Bu e-posta Ping Uyarı Otomasyon Programı tarafından otomatik gönderilmiştir.",
                                DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), item.Host, item.Description, latency);

                            SendEmailAlert(subject, body);
                        }
                    }

                    item.Status = "ONLINE";
                    item.ConsecutiveFailures = 0;
                    item.FirstOfflineTime = "-";
                    item.OfflineStartDateTime = null;
                }
                else
                {
                    item.AddLatencyHistory(-1);
                    string failReason = item.Port == 80 || item.Port == 443 ? "HTTP Yanıt Yok" : (item.Port > 0 ? "Port Kapalı" : "Timeout");
                    HandlePingFailure(item, failReason, threshold);
                }
            }
            catch (Exception ex)
            {
                HandlePingFailure(item, ex.Message, threshold);
            }
        }

        private void HandlePingFailure(TargetItem item, string reason, int threshold)
        {
            item.ConsecutiveFailures++;
            item.LastFailureTime = DateTime.Now.ToString("HH:mm:ss");
            item.LastLatency = -1;

            if (item.ConsecutiveFailures >= threshold)
            {
                bool wasAlreadyOffline = (item.Status == "OFFLINE");
                item.Status = "OFFLINE";

                if (!wasAlreadyOffline)
                {
                    item.FirstOfflineTime = DateTime.Now.ToString("HH:mm:ss");
                    item.OfflineStartDateTime = DateTime.Now;
                    lastRepeatAlarmTime = DateTime.Now;

                    LogMessage(string.Format("🔴 KESİNTİ UYARISI: {0} ({1}) ERİŞİM YOK! Sebep: {2} (Üst üste {3} hata)", item.Host, item.Description, reason, item.ConsecutiveFailures), colorOffline);
                    LogDowntimeToCsv(item, "KESINTI", reason);

                    string alertText = string.Format("🔴 KESİNTİ UYARISI: {0} ({1}) PİNG DÜŞTÜ! Sebep: {2}", item.Host, item.Description, reason);
                    SendTelegramAlert(alertText);

                    if (chkAudioAlert.Checked)
                    {
                        PlayAlarmSound();
                    }

                    if (chkToastAlert.Checked)
                    {
                        var offlineList = targets.Where(t => t.Status == "OFFLINE").ToList();
                        OfflineAlertPopupForm.ShowAlert(this, offlineList);
                    }

                    if (chkEmailAlert.Checked && item.EnableEmailAlert)
                    {
                        string targetRecipient = !string.IsNullOrWhiteSpace(item.CustomEmail) ? item.CustomEmail : emailSettings.RecipientEmail;
                        string subject = string.Format("[PİNG UYARI] KESİNTİ: {0} ({1}) - PİNG DÜŞTÜ!", item.Host, item.Description);
                        string body = string.Format(
                            "⚠️ KESİNTİ BİLDİRİMİ\n\n" +
                            "Tarih / Saat: {0}\n" +
                            "Hedef Adres: {1}\n" +
                            "Açıklama: {2}\n" +
                            "Durum: KESİNTİ (OFFLINE / ERİŞİM YOK)\n" +
                            "Sebep: {3}\n" +
                            "Üst Üste Hata Sayısı: {4}\n\n" +
                            "Bu e-posta Ping Uyarı Otomasyon Programı tarafından otomatik gönderilmiştir.",
                            DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), item.Host, item.Description, reason, item.ConsecutiveFailures);

                        SendEmailAlert(subject, body, targetRecipient);
                    }
                }
            }
        }



        private void OpenTelegramSettingsDialog()
        {
            Form dlg = new Form
            {
                Text = "📲 Telegram Bot Bildirim Ayarları",
                Size = new Size(480, 260),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblToken = new Label { Text = "Bot Token:", Location = new Point(20, 20), AutoSize = true };
            TextBox txtToken = new TextBox { Text = telegramSettings.BotToken, Location = new Point(120, 17), Width = 320, BackColor = bgInput, ForeColor = fgText };

            Label lblChat = new Label { Text = "Chat / Kanal ID:", Location = new Point(20, 60), AutoSize = true };
            TextBox txtChat = new TextBox { Text = telegramSettings.ChatId, Location = new Point(120, 57), Width = 320, BackColor = bgInput, ForeColor = fgText };

            CheckBox chkEnable = new CheckBox { Text = "Telegram Bildirimlerini Etkinleştir", Checked = telegramSettings.EnableTelegram, Location = new Point(120, 95), AutoSize = true, ForeColor = fgText };

            Button btnTestTelegram = CreateStyledButton("📲 Test Mesajı Gönder", Color.FromArgb(88, 91, 112));
            btnTestTelegram.Location = new Point(20, 150);
            btnTestTelegram.Click += delegate(object s, EventArgs ev)
            {
                telegramSettings.BotToken = txtToken.Text.Trim();
                telegramSettings.ChatId = txtChat.Text.Trim();
                telegramSettings.EnableTelegram = chkEnable.Checked;

                if (string.IsNullOrEmpty(telegramSettings.BotToken) || string.IsNullOrEmpty(telegramSettings.ChatId))
                {
                    MessageBox.Show("Lütfen Telegram Bot Token ve Chat ID bilgilerini girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SendTelegramAlert("📲 [PİNG UYARI] Test bildirimi. Telegram bot entegrasyonu başarıyla çalışıyor!");
                MessageBox.Show("Test mesajı Telegram hesabınıza gönderildi. Lütfen sohbetinizi kontrol edin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnSaveTelegram = CreateStyledButton("💾 Kaydet", accentBlue);
            btnSaveTelegram.ForeColor = Color.Black;
            btnSaveTelegram.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnSaveTelegram.Location = new Point(340, 150);
            btnSaveTelegram.Width = 100;
            btnSaveTelegram.Click += delegate(object s, EventArgs ev)
            {
                telegramSettings.BotToken = txtToken.Text.Trim();
                telegramSettings.ChatId = txtChat.Text.Trim();
                telegramSettings.EnableTelegram = chkEnable.Checked;
                chkTelegramAlert.Checked = telegramSettings.EnableTelegram;

                SaveTelegramSettings();
                MessageBox.Show("Telegram ayarları kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] {
                lblToken, txtToken, lblChat, txtChat, chkEnable, btnTestTelegram, btnSaveTelegram
            });

            dlg.ShowDialog(this);
        }

        private void SaveTelegramSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "telegram_settings.txt");
                string content = string.Format("{0}|{1}|{2}",
                    telegramSettings.EnableTelegram,
                    telegramSettings.BotToken,
                    telegramSettings.ChatId);
                File.WriteAllText(path, content);
            }
            catch { }
        }

        private void LoadTelegramSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "telegram_settings.txt");
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    string[] parts = text.Split('|');
                    if (parts.Length >= 3)
                    {
                        telegramSettings.EnableTelegram = bool.Parse(parts[0]);
                        telegramSettings.BotToken = parts[1];
                        telegramSettings.ChatId = parts[2];
                        chkTelegramAlert.Checked = telegramSettings.EnableTelegram;
                    }
                }
            }
            catch { }
        }

        private void SendTelegramAlert(string message)
        {
            if (!chkTelegramAlert.Checked || !telegramSettings.EnableTelegram || string.IsNullOrEmpty(telegramSettings.BotToken) || string.IsNullOrEmpty(telegramSettings.ChatId))
                return;

            string token = telegramSettings.BotToken;
            string chatId = telegramSettings.ChatId;

            Task.Run(delegate()
            {
                try
                {
                    string url = string.Format("https://api.telegram.org/bot{0}/sendMessage?chat_id={1}&text={2}",
                        token,
                        chatId,
                        Uri.EscapeDataString(message));

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 5000;
                    using (WebResponse resp = req.GetResponse()) { }
                    LogMessage("📲 Telegram bildirimi başarıyla gönderildi.", Color.LightGreen);
                }
                catch (Exception ex)
                {
                    LogMessage("⚠️ Telegram gönderim hatası: " + ex.Message, Color.Orange);
                }
            });
        }

        private void OpenServiceManagerDialog()
        {
            Form dlg = new Form
            {
                Text = "⚙️ Windows Servis Modu Yöneticisi",
                Size = new Size(480, 240),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblInfo = new Label
            {
                Text = "Programı arka planda bir Windows Servisi (PingUyariService) olarak çalışacak şekilde kurabilir veya durdurabilirsiniz.",
                Location = new Point(20, 20),
                Size = new Size(420, 45)
            };

            Button btnInstall = CreateStyledButton("➕ Servis Olarak Kur (sc create)", accentBlue);
            btnInstall.ForeColor = Color.Black;
            btnInstall.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnInstall.Location = new Point(20, 80);
            btnInstall.Click += delegate(object s, EventArgs e)
            {
                try
                {
                    string binPath = Application.ExecutablePath;
                    ProcessStartInfo psi = new ProcessStartInfo("sc.exe", string.Format("create PingUyariService binPath= \"{0}\" start= auto", binPath));
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process p = Process.Start(psi);
                    p.WaitForExit();
                    MessageBox.Show("Windows Servisi (PingUyariService) oluşturuldu.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            Button btnStartService = CreateStyledButton("▶ Servisi Başlat", colorOnline);
            btnStartService.ForeColor = Color.Black;
            btnStartService.Location = new Point(250, 80);
            btnStartService.Click += delegate(object s, EventArgs e)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("net.exe", "start PingUyariService");
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process p = Process.Start(psi);
                    p.WaitForExit();
                    MessageBox.Show("PingUyariService başlatıldı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            Button btnRemoveService = CreateStyledButton("🗑️ Servisi Sil (sc delete)", colorOffline);
            btnRemoveService.ForeColor = Color.Black;
            btnRemoveService.Location = new Point(20, 130);
            btnRemoveService.Click += delegate(object s, EventArgs e)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("sc.exe", "delete PingUyariService");
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process p = Process.Start(psi);
                    p.WaitForExit();
                    MessageBox.Show("PingUyariService silindi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            dlg.Controls.AddRange(new Control[] { lblInfo, btnInstall, btnStartService, btnRemoveService });
            dlg.ShowDialog(this);
        }

        private void OpenUpdateSettingsDialog()
        {
            Form dlg = new Form
            {
                Text = "🔄 Otomatik Güncelleme (Auto-Updater) Ayarları",
                Size = new Size(560, 320),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblUrl = new Label { Text = "Güncelleme Adresi / Ağ Yolu:", Location = new Point(20, 20), AutoSize = true };
            TextBox txtUrl = new TextBox { Text = updateSettings.UpdateSourceUrl, Location = new Point(20, 45), Width = 500, BackColor = bgInput, ForeColor = fgText };

            Label lblHelp = new Label
            {
                Text = "Örnek Web URL: http://192.168.1.50/update/version.txt\n" +
                       "Örnek Ortak Ağ Yolu: \\\\192.168.1.10\\Ortak\\PingUyari\\version.txt veya Z:\\PingUyari\\version.txt",
                Location = new Point(20, 78),
                Size = new Size(500, 35),
                ForeColor = fgMuted,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            CheckBox chkAuto = new CheckBox
            {
                Text = "Program açılışında otomatik güncelleme kontrolü yap",
                Checked = updateSettings.AutoCheckOnStartup,
                Location = new Point(20, 125),
                AutoSize = true,
                ForeColor = fgText
            };

            CheckBox chkSilent = new CheckBox
            {
                Text = "Yeni güncelleme bulunduğunda sormadan otomatik güncelle",
                Checked = updateSettings.SilentUpdate,
                Location = new Point(20, 155),
                AutoSize = true,
                ForeColor = fgText
            };

            Button btnCheckNow = CreateStyledButton("🔍 Şimdi Güncellemeleri Kontrol Et", Color.FromArgb(88, 91, 112));
            btnCheckNow.Location = new Point(20, 205);
            btnCheckNow.Click += delegate(object s, EventArgs ev)
            {
                updateSettings.UpdateSourceUrl = txtUrl.Text.Trim();
                updateSettings.AutoCheckOnStartup = chkAuto.Checked;
                updateSettings.SilentUpdate = chkSilent.Checked;
                SaveUpdateSettings();
                Task.Run(async delegate() { await CheckForUpdatesAsync(true); });
            };

            Button btnSave = CreateStyledButton("💾 Kaydet", accentBlue);
            btnSave.ForeColor = Color.Black;
            btnSave.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnSave.Location = new Point(420, 205);
            btnSave.Width = 100;
            btnSave.Click += delegate(object s, EventArgs ev)
            {
                updateSettings.UpdateSourceUrl = txtUrl.Text.Trim();
                updateSettings.AutoCheckOnStartup = chkAuto.Checked;
                updateSettings.SilentUpdate = chkSilent.Checked;
                SaveUpdateSettings();
                MessageBox.Show("Otomatik güncelleme ayarları kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] {
                lblUrl, txtUrl, lblHelp, chkAuto, chkSilent, btnCheckNow, btnSave
            });

            dlg.ShowDialog(this);
        }

        private void SaveUpdateSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "update_settings.txt");
                string content = string.Format("{0}|{1}|{2}",
                    updateSettings.AutoCheckOnStartup,
                    updateSettings.UpdateSourceUrl,
                    updateSettings.SilentUpdate);
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            catch { }
        }

        private void LoadUpdateSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "update_settings.txt");
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    string[] parts = text.Split('|');
                    if (parts.Length >= 2)
                    {
                        updateSettings.AutoCheckOnStartup = bool.Parse(parts[0]);
                        updateSettings.UpdateSourceUrl = parts[1];
                        if (parts.Length >= 3)
                        {
                            updateSettings.SilentUpdate = bool.Parse(parts[2]);
                        }
                    }
                }
            }
            catch { }
        }

        private string NormalizeUpdateUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
            string url = rawUrl.Trim();

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (url.Contains("github.com"))
                {
                    string path = url;
                    int ghIdx = path.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase);
                    if (ghIdx >= 0)
                    {
                        path = path.Substring(ghIdx + "github.com/".Length).TrimEnd('/');
                    }

                    if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    {
                        path = path.Substring(0, path.Length - 4);
                    }

                    if (path.Contains("/blob/"))
                    {
                        return "https://raw.githubusercontent.com/" + path.Replace("/blob/", "/");
                    }
                    else if (path.Contains("/raw/"))
                    {
                        return "https://raw.githubusercontent.com/" + path.Replace("/raw/", "/");
                    }
                    else
                    {
                        string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            return string.Format("https://raw.githubusercontent.com/{0}/{1}/main/version.txt", parts[0], parts[1]);
                        }
                        else if (parts.Length > 2 && !path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            return string.Format("https://raw.githubusercontent.com/{0}/main/version.txt", path);
                        }
                        else
                        {
                            return "https://raw.githubusercontent.com/" + path;
                        }
                    }
                }
            }
            return url;
        }

        private async Task CheckForUpdatesAsync(bool isManualCheck)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
            }
            catch {}

            string rawUrl = updateSettings.UpdateSourceUrl;
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                if (isManualCheck)
                {
                    this.BeginInvoke(new Action(delegate()
                    {
                        MessageBox.Show("Lütfen önce Oto-Güncelleme ayarlarından bir Güncelleme Adresi veya Ağ Yolu tanımlayın.\n\nÖrnek GitHub Repo: https://github.com/suqer34/ping-uyari\nÖrnek Web URL: https://raw.githubusercontent.com/suqer34/ping-uyari/main/version.txt", "Güncelleme Adresi Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
                return;
            }

            string sourceUrl = NormalizeUpdateUrl(rawUrl);

            try
            {
                if (isManualCheck)
                {
                    LogMessage("🔍 Güncellemeler kontrol ediliyor: " + sourceUrl, accentBlue);
                }

                string content = "";

                if (sourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PingUyariAutoUpdater/6.2");
                        wc.Encoding = Encoding.UTF8;
                        content = await wc.DownloadStringTaskAsync(sourceUrl);
                    }
                }
                else
                {
                    if (File.Exists(sourceUrl))
                    {
                        content = File.ReadAllText(sourceUrl, Encoding.UTF8);
                    }
                    else
                    {
                        if (isManualCheck)
                        {
                            this.BeginInvoke(new Action(delegate()
                            {
                                MessageBox.Show("Güncelleme dosyası bulunamadı: " + sourceUrl, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }));
                        }
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(content)) return;

                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 1) return;

                string latestVersionStr = lines[0].Trim();
                Version latestVersion;
                Version currentVersion;

                if (!Version.TryParse(latestVersionStr, out latestVersion))
                {
                    latestVersion = new Version(1, 0, 0);
                }

                if (!Version.TryParse(APP_VERSION, out currentVersion))
                {
                    currentVersion = new Version(1, 0, 0);
                }

                if (latestVersion > currentVersion)
                {
                    string downloadUrl = lines.Length > 1 ? lines[1].Trim() : "";
                    StringBuilder notes = new StringBuilder();
                    for (int i = 2; i < lines.Length; i++)
                    {
                        notes.AppendLine(lines[i]);
                    }

                    if (string.IsNullOrEmpty(downloadUrl) || downloadUrl.Equals("PingUyari.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (sourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            int lastSlash = sourceUrl.LastIndexOf('/');
                            downloadUrl = sourceUrl.Substring(0, lastSlash + 1) + "PingUyari.exe";
                        }
                        else
                        {
                            string dir = Path.GetDirectoryName(sourceUrl);
                            downloadUrl = Path.Combine(dir, "PingUyari.exe");
                        }
                    }
                    else if (!downloadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !downloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !File.Exists(downloadUrl))
                    {
                        if (sourceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            int lastSlash = sourceUrl.LastIndexOf('/');
                            downloadUrl = sourceUrl.Substring(0, lastSlash + 1) + downloadUrl;
                        }
                        else
                        {
                            string dir = Path.GetDirectoryName(sourceUrl);
                            downloadUrl = Path.Combine(dir, downloadUrl);
                        }
                    }

                    downloadUrl = NormalizeUpdateUrl(downloadUrl);

                    LogMessage(string.Format("🚀 YENİ SÜRÜM BULUNDU: v{0} (Mevcut: v{1})", latestVersionStr, APP_VERSION), Color.LightGreen);

                    bool shouldUpdate = updateSettings.SilentUpdate;
                    if (!shouldUpdate)
                    {
                        DialogResult dr = DialogResult.No;
                        this.Invoke(new Action(delegate()
                        {
                            string msg = string.Format(
                                "🎉 YENİ SÜRÜM MEVCUT!\n\n" +
                                "Yeni Sürüm: v{0}\n" +
                                "Mevcut Sürüm: v{1}\n\n" +
                                "Yenilikler / Değişiklikler:\n{2}\n\n" +
                                "Şimdi güncellemek istiyor musunuz?\n" +
                                "(Not: Özel ayarlarınız ve hedef listeniz aynen korunacaktır.)",
                                latestVersionStr, APP_VERSION, notes.Length > 0 ? notes.ToString() : "Hata düzeltmeleri ve performans iyileştirmeleri.");

                            dr = MessageBox.Show(msg, "Güncelleme Mevcut", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        }));

                        shouldUpdate = (dr == DialogResult.Yes);
                    }

                    if (shouldUpdate)
                    {
                        string tempExePath = Path.Combine(Application.StartupPath, "PingUyari_new.exe");

                        LogMessage("📥 Yeni sürüm indiriliyor: " + downloadUrl, accentBlue);

                        if (downloadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || downloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            using (WebClient wc = new WebClient())
                            {
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PingUyariAutoUpdater/6.2");
                                await wc.DownloadFileTaskAsync(downloadUrl, tempExePath);
                            }
                        }
                        else
                        {
                            File.Copy(downloadUrl, tempExePath, true);
                        }

                        if (File.Exists(tempExePath))
                        {
                            this.Invoke(new Action(delegate()
                            {
                                PerformUpdate(tempExePath);
                            }));
                        }
                    }
                }
                else
                {
                    if (isManualCheck)
                    {
                        this.BeginInvoke(new Action(delegate()
                        {
                            MessageBox.Show(string.Format("Zaten en güncel sürümü kullanıyorsunuz! (v{0})", APP_VERSION), "Güncel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage("⚠️ Güncelleme kontrol hatası: " + ex.Message, Color.Orange);
                if (isManualCheck)
                {
                    this.BeginInvoke(new Action(delegate()
                    {
                        MessageBox.Show("Güncelleme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }
        }

        private void PerformUpdate(string newExePath)
        {
            try
            {
                string currentExe = Application.ExecutablePath;
                string updaterBat = Path.Combine(Application.StartupPath, "updater.bat");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("chcp 65001 > nul");
                sb.AppendLine("timeout /t 2 /nobreak > nul");
                sb.AppendLine(string.Format("copy /y \"{0}\" \"{1}\"", newExePath, currentExe));
                sb.AppendLine(string.Format("del \"{0}\"", newExePath));
                sb.AppendLine(string.Format("start \"\" \"{0}\"", currentExe));
                sb.AppendLine("del \"%~f0\"");

                File.WriteAllText(updaterBat, sb.ToString(), Encoding.Default);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = updaterBat,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
                StopMonitoring();
                if (notifyIcon != null) notifyIcon.Dispose();
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Güncelleme başlatma hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenEmailSettingsDialog()
        {
            Form dlg = new Form
            {
                Text = "⚙️ E-Posta & SMTP Bildirim Ayarları",
                Size = new Size(520, 420),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = bgDark,
                ForeColor = fgText,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblRec = new Label { Text = "Alıcı E-Posta:", Location = new Point(20, 20), AutoSize = true };
            TextBox txtRec = new TextBox { Text = emailSettings.RecipientEmail, Location = new Point(140, 17), Width = 330, BackColor = bgInput, ForeColor = fgText };

            Label lblHost = new Label { Text = "SMTP Sunucu:", Location = new Point(20, 60), AutoSize = true };
            TextBox txtSmtpHost = new TextBox { Text = emailSettings.SmtpHost, Location = new Point(140, 57), Width = 230, BackColor = bgInput, ForeColor = fgText };

            Label lblPort = new Label { Text = "Port:", Location = new Point(380, 60), AutoSize = true };
            NumericUpDown numPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = emailSettings.SmtpPort, Location = new Point(415, 57), Width = 55, BackColor = bgInput, ForeColor = fgText };

            CheckBox chkSsl = new CheckBox { Text = "SSL / TLS Kullan", Checked = emailSettings.EnableSsl, Location = new Point(140, 95), AutoSize = true, ForeColor = fgText };

            Label lblSend = new Label { Text = "Gönderen E-Posta:", Location = new Point(20, 135), AutoSize = true };
            TextBox txtSender = new TextBox { Text = emailSettings.SenderEmail, Location = new Point(140, 132), Width = 330, BackColor = bgInput, ForeColor = fgText };

            Label lblPass = new Label { Text = "Gönderen Parola:", Location = new Point(20, 175), AutoSize = true };
            TextBox txtPass = new TextBox { Text = emailSettings.SenderPassword, Location = new Point(140, 172), Width = 330, PasswordChar = '*', BackColor = bgInput, ForeColor = fgText };

            Label lblNote = new Label
            {
                Text = "Not: Office365 veya şirket mail sunucunuz için SMTP yetkili bilgilerini giriniz.\nÖrnek alıcı: bilgiislem@alnusyatirim.com",
                Location = new Point(20, 215),
                Size = new Size(450, 45),
                ForeColor = fgMuted,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
            };

            Button btnTestEmail = CreateStyledButton("📧 Test E-Postası Gönder", Color.FromArgb(88, 91, 112));
            btnTestEmail.Location = new Point(20, 275);
            btnTestEmail.Click += delegate(object s, EventArgs ev)
            {
                emailSettings.RecipientEmail = txtRec.Text.Trim();
                emailSettings.SmtpHost = txtSmtpHost.Text.Trim();
                emailSettings.SmtpPort = (int)numPort.Value;
                emailSettings.EnableSsl = chkSsl.Checked;
                emailSettings.SenderEmail = txtSender.Text.Trim();
                emailSettings.SenderPassword = txtPass.Text.Trim();

                if (string.IsNullOrEmpty(emailSettings.RecipientEmail))
                {
                    MessageBox.Show("Lütfen bir alıcı e-posta adresi girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SendEmailAlert("[PİNG UYARI] Test E-Postası", "Merhaba,\n\nBu e-posta Ping Uyarı Programı tarafından test amacıyla gönderilmiştir.\nE-posta bildirim sisteminiz başarıyla çalışmaktadır!");
                MessageBox.Show("Test e-postası arka planda gönderiliyor. Lütfen gelen kutusunu ve log penceresini kontrol edin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnSaveEmail = CreateStyledButton("💾 Kaydet", accentBlue);
            btnSaveEmail.ForeColor = Color.Black;
            btnSaveEmail.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnSaveEmail.Location = new Point(370, 275);
            btnSaveEmail.Width = 100;
            btnSaveEmail.Click += delegate(object s, EventArgs ev)
            {
                emailSettings.RecipientEmail = txtRec.Text.Trim();
                emailSettings.SmtpHost = txtSmtpHost.Text.Trim();
                emailSettings.SmtpPort = (int)numPort.Value;
                emailSettings.EnableSsl = chkSsl.Checked;
                emailSettings.SenderEmail = txtSender.Text.Trim();
                emailSettings.SenderPassword = txtPass.Text.Trim();

                SaveEmailSettings();
                MessageBox.Show("E-posta ayarları kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dlg.Close();
            };

            dlg.Controls.AddRange(new Control[] {
                lblRec, txtRec, lblHost, txtSmtpHost, lblPort, numPort, chkSsl,
                lblSend, txtSender, lblPass, txtPass, lblNote, btnTestEmail, btnSaveEmail
            });

            dlg.ShowDialog(this);
        }

        private void SaveEmailSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "email_settings.txt");
                string content = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                    chkEmailAlert.Checked,
                    emailSettings.RecipientEmail,
                    emailSettings.SmtpHost,
                    emailSettings.SmtpPort,
                    emailSettings.EnableSsl,
                    emailSettings.SenderEmail,
                    emailSettings.SenderPassword);
                File.WriteAllText(path, content);
            }
            catch { }
        }

        private void LoadEmailSettings()
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "email_settings.txt");
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    string[] parts = text.Split('|');
                    if (parts.Length >= 7)
                    {
                        chkEmailAlert.Checked = bool.Parse(parts[0]);
                        emailSettings.RecipientEmail = parts[1];
                        emailSettings.SmtpHost = parts[2];
                        emailSettings.SmtpPort = int.Parse(parts[3]);
                        emailSettings.EnableSsl = bool.Parse(parts[4]);
                        emailSettings.SenderEmail = parts[5];
                        emailSettings.SenderPassword = parts[6];
                    }
                }
            }
            catch { }
        }

        private void SendEmailAlert(string subject, string body, string targetRecipient = null)
        {
            if (!chkEmailAlert.Checked)
                return;

            string recipient = !string.IsNullOrWhiteSpace(targetRecipient) ? targetRecipient : emailSettings.RecipientEmail;
            if (string.IsNullOrEmpty(recipient))
                return;
            string host = emailSettings.SmtpHost;
            int port = emailSettings.SmtpPort;
            bool ssl = emailSettings.EnableSsl;
            string sender = string.IsNullOrEmpty(emailSettings.SenderEmail) ? recipient : emailSettings.SenderEmail;
            string password = emailSettings.SenderPassword;

            Task.Run(delegate()
            {
                try
                {
                    using (MailMessage mail = new MailMessage())
                    {
                        mail.From = new MailAddress(sender, "Ping Uyarı Otomasyonu");
                        mail.To.Add(recipient);
                        mail.Subject = subject;
                        mail.Body = body;
                        mail.IsBodyHtml = false;

                        using (SmtpClient smtp = new SmtpClient(host, port))
                        {
                            if (!string.IsNullOrEmpty(password))
                            {
                                smtp.Credentials = new NetworkCredential(sender, password);
                            }
                            smtp.EnableSsl = ssl;
                            smtp.Send(mail);
                        }
                    }
                    LogMessage(string.Format("📧 E-posta uyarısı gönderildi: {0}", recipient), Color.LightCyan);
                }
                catch (Exception ex)
                {
                    LogMessage(string.Format("⚠️ E-posta gönderilemedi ({0}): {1}", recipient, ex.Message), Color.Orange);
                }
            });
        }

        private static byte[] zabbixAlarmWavCache = null;

        private static byte[] GetZabbixAlarmWav()
        {
            if (zabbixAlarmWavCache != null) return zabbixAlarmWavCache;

            int sampleRate = 44100;
            short bitsPerSample = 16;
            short channels = 1;

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    List<short> samples = new List<short>();

                    int toneSamples = (int)(sampleRate * 0.14);

                    for (int repeat = 0; repeat < 5; repeat++)
                    {
                        for (int i = 0; i < toneSamples; i++)
                        {
                            double t = (double)i / sampleRate;
                            short val = (short)(Math.Sin(2.0 * Math.PI * 1400.0 * t) * 30000.0);
                            samples.Add(val);
                        }
                        for (int i = 0; i < toneSamples; i++)
                        {
                            double t = (double)i / sampleRate;
                            short val = (short)(Math.Sin(2.0 * Math.PI * 950.0 * t) * 30000.0);
                            samples.Add(val);
                        }
                    }

                    int finalSamples = (int)(sampleRate * 0.22);
                    for (int i = 0; i < finalSamples; i++)
                    {
                        double t = (double)i / sampleRate;
                        short val = (short)(Math.Sin(2.0 * Math.PI * 1800.0 * t) * 31000.0);
                        samples.Add(val);
                    }

                    int dataSize = samples.Count * sizeof(short);

                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + dataSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1);
                    bw.Write(channels);
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * channels * (bitsPerSample / 8));
                    bw.Write((short)(channels * (bitsPerSample / 8)));
                    bw.Write(bitsPerSample);

                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(dataSize);
                    foreach (short s in samples)
                    {
                        bw.Write(s);
                    }

                    bw.Flush();
                    zabbixAlarmWavCache = ms.ToArray();
                    return zabbixAlarmWavCache;
                }
            }
        }

        private void PlayAlarmSound()
        {
            Task.Run(delegate()
            {
                try
                {
                    byte[] wavData = GetZabbixAlarmWav();
                    using (MemoryStream ms = new MemoryStream(wavData))
                    {
                        using (SoundPlayer player = new SoundPlayer(ms))
                        {
                            player.PlaySync();
                        }
                    }
                }
                catch
                {
                    try { SystemSounds.Hand.Play(); } catch { }
                }
            });
        }

        private void ShowNotification(string title, string msg, ToolTipIcon icon)
        {
            if (this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(delegate()
                {
                    notifyIcon.ShowBalloonTip(4000, title, msg, icon);
                }));
            }
        }

        private void UpdateCounters()
        {
            int total = targets.Count;
            int online = targets.Count(t => t.Status == "ONLINE");
            int offline = targets.Count(t => t.Status == "OFFLINE");

            long totalLat = 0;
            int onlineWithLat = 0;
            foreach (var t in targets)
            {
                if (t.Status == "ONLINE" && t.LastLatency >= 0)
                {
                    totalLat += t.LastLatency;
                    onlineWithLat++;
                }
            }
            long avgLat = onlineWithLat > 0 ? totalLat / onlineWithLat : 0;

            lblTotalCount.Text = total.ToString();
            lblOnlineCount.Text = online.ToString();
            lblOfflineCount.Text = offline.ToString();
            lblAvgLatency.Text = string.Format("{0} ms", avgLat);
        }

        private void LogMessage(string text, Color color)
        {
            if (txtLog.IsDisposed) return;

            if (txtLog.InvokeRequired)
            {
                txtLog.BeginInvoke(new Action(delegate() { LogMessage(text, color); }));
                return;
            }

            string timestamp = string.Format("[{0:HH:mm:ss}] ", DateTime.Now);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;

            txtLog.SelectionColor = fgMuted;
            txtLog.AppendText(timestamp);

            txtLog.SelectionColor = color;
            txtLog.AppendText(text + "\n");

            txtLog.ScrollToCaret();
        }

        private void PanelGraph_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(bgCard);

            TargetItem item = null;
            if (dgvTargets.SelectedRows.Count > 0)
            {
                item = dgvTargets.SelectedRows[0].DataBoundItem as TargetItem;
            }

            Rectangle rect = panelGraph.ClientRectangle;
            rect.Inflate(-10, -10);

            using (Pen borderPen = new Pen(Color.FromArgb(49, 50, 68)))
            {
                g.DrawRectangle(borderPen, rect);
            }

            if (item == null || item.LatencyHistory.Count < 2)
            {
                using (Font font = new Font("Segoe UI", 9F, FontStyle.Italic))
                {
                    string text = item == null ? "Grafik için tablodan bir hedef seçin" : "Yeterli ping verisi bekleniyor...";
                    SizeF sz = g.MeasureString(text, font);
                    g.DrawString(text, font, new SolidBrush(fgMuted), rect.X + (rect.Width - sz.Width) / 2, rect.Y + (rect.Height - sz.Height) / 2);
                }
                return;
            }

            var hist = item.LatencyHistory.ToList();
            long maxLat = Math.Max(100, hist.Where(h => h >= 0).DefaultIfEmpty(100).Max());

            List<PointF> points = new List<PointF>();
            float stepX = (float)rect.Width / Math.Max(1, hist.Count - 1);

            for (int i = 0; i < hist.Count; i++)
            {
                float x = rect.X + (i * stepX);
                long val = hist[i];
                float y = (val < 0) ? rect.Bottom : rect.Bottom - ((float)val / maxLat * rect.Height);
                points.Add(new PointF(x, Math.Max(rect.Y, Math.Min(rect.Bottom, y))));
            }

            using (Pen linePen = new Pen(accentBlue, 2F))
            {
                g.DrawLines(linePen, points.ToArray());
            }

            using (Font titleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold))
            {
                string infoStr = string.Format("📈 {0} Ping Trend (Son: {1} ms | Max: {2} ms)", item.Host, item.LastLatency >= 0 ? item.LastLatency : 0, maxLat);
                g.DrawString(infoStr, titleFont, new SolidBrush(accentBlue), rect.X + 5, rect.Y + 3);
            }
        }

        private void DgvTargets_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvTargets.Rows.Count) return;

            TargetItem item = dgvTargets.Rows[e.RowIndex].DataBoundItem as TargetItem;
            if (item == null) return;

            if (item.Status == "OFFLINE")
            {
                e.CellStyle.BackColor = Color.FromArgb(64, 21, 28);
            }
            else if (e.RowIndex % 2 == 1)
            {
                e.CellStyle.BackColor = Color.FromArgb(21, 31, 40);
            }

            if (dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "Status")
            {
                if (item.Status == "ONLINE")
                {
                    e.CellStyle.BackColor = Color.FromArgb(30, 80, 50);
                    e.CellStyle.ForeColor = Color.FromArgb(46, 204, 113);
                    e.CellStyle.Font = new Font(dgvTargets.Font, FontStyle.Bold);
                }
                else if (item.Status == "OFFLINE")
                {
                    e.CellStyle.BackColor = Color.FromArgb(120, 30, 30);
                    e.CellStyle.ForeColor = Color.FromArgb(231, 76, 60);
                    e.CellStyle.Font = new Font(dgvTargets.Font, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 70, 20);
                    e.CellStyle.ForeColor = Color.FromArgb(241, 196, 15);
                    e.CellStyle.Font = new Font(dgvTargets.Font, FontStyle.Bold);
                }
            }

            if (dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "RblStatus")
            {
                if (item.RblStatus != null && item.RblStatus.Contains("KARA LİSTEDE"))
                {
                    e.CellStyle.ForeColor = colorOffline;
                    e.CellStyle.Font = new Font(dgvTargets.Font, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = colorOnline;
                }
            }

            if (dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "FirstOfflineTime" ||
                dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "DowntimeDuration")
            {
                if (item.Status == "OFFLINE")
                {
                    e.CellStyle.ForeColor = colorOffline;
                    e.CellStyle.Font = new Font(dgvTargets.Font, FontStyle.Bold);
                }
            }

            if (dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "SslExpiryInfo")
            {
                if (item.SslExpiryInfo != null && item.SslExpiryInfo.Contains("Gün"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(249, 226, 175);
                }
            }

            if (dgvTargets.Columns[e.ColumnIndex].DataPropertyName == "LastLatency")
            {
                if (item.LastLatency < 0)
                {
                    e.Value = "TIMEOUT";
                    e.CellStyle.ForeColor = colorOffline;
                }
                else
                {
                    e.Value = string.Format("{0} ms", item.LastLatency);
                }
            }
        }
    }
}

