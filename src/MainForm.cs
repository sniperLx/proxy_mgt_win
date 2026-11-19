using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProxyEnvManager
{
    internal sealed class MainForm : Form
    {
        private static readonly Color AccentColor = Color.FromArgb(35, 99, 196);
        private static readonly Color AccentHoverColor = Color.FromArgb(27, 82, 165);
        private static readonly Color InkColor = Color.FromArgb(33, 41, 54);
        private static readonly Color MutedColor = Color.FromArgb(96, 107, 122);
        private static readonly Color SuccessColor = Color.FromArgb(22, 132, 83);
        private static readonly Color ErrorColor = Color.FromArgb(190, 55, 55);
        private static readonly Color SurfaceColor = Color.FromArgb(247, 249, 252);

        private readonly RadioButton clashSourceRadio;
        private readonly RadioButton systemSourceRadio;
        private readonly RadioButton manualSourceRadio;
        private readonly TextBox httpProxyTextBox;
        private readonly TextBox httpsProxyTextBox;
        private readonly TextBox noProxyTextBox;
        private readonly Label discoveryStatusLabel;
        private readonly Label discoveryDetailLabel;
        private readonly Label currentEnvironmentLabel;
        private readonly Button refreshButton;
        private readonly Button applyButton;
        private readonly Button clearButton;
        private readonly NotifyIcon notifyIcon;
        private readonly Icon windowIcon;
        private readonly Icon trayIcon;
        private bool suppressSourceChange;
        private bool trayHintShown;
        private bool trayResourcesDisposed;

        public MainForm()
        {
            Text = "代理环境变量管理器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 730);
            MinimumSize = new Size(760, 760);
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            windowIcon = AppIcon.Load(SystemInformation.IconSize);
            trayIcon = AppIcon.Load(SystemInformation.SmallIconSize);
            Icon = windowIcon ?? SystemIcons.Application;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 20, 24, 18);
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 209F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            Controls.Add(root);

            root.Controls.Add(CreateHeader(), 0, 0);

            GroupBox sourceGroup = CreateGroupBox("代理来源");
            TableLayoutPanel sourceLayout = new TableLayoutPanel();
            sourceLayout.Dock = DockStyle.Fill;
            sourceLayout.Padding = new Padding(12, 8, 12, 6);
            sourceLayout.ColumnCount = 1;
            sourceLayout.RowCount = 2;
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sourceGroup.Controls.Add(sourceLayout);

            FlowLayoutPanel radioFlow = new FlowLayoutPanel();
            radioFlow.Dock = DockStyle.Fill;
            radioFlow.FlowDirection = FlowDirection.LeftToRight;
            radioFlow.WrapContents = false;
            radioFlow.Margin = Padding.Empty;

            clashSourceRadio = CreateSourceRadio("Clash Verge", true);
            systemSourceRadio = CreateSourceRadio("Windows 系统代理", false);
            manualSourceRadio = CreateSourceRadio("手动输入", false);
            clashSourceRadio.Margin = new Padding(0, 4, 28, 0);
            systemSourceRadio.Margin = new Padding(0, 4, 28, 0);
            manualSourceRadio.Margin = new Padding(0, 4, 0, 0);
            clashSourceRadio.CheckedChanged += SourceRadioCheckedChanged;
            systemSourceRadio.CheckedChanged += SourceRadioCheckedChanged;
            manualSourceRadio.CheckedChanged += SourceRadioCheckedChanged;
            radioFlow.Controls.Add(clashSourceRadio);
            radioFlow.Controls.Add(systemSourceRadio);
            radioFlow.Controls.Add(manualSourceRadio);
            sourceLayout.Controls.Add(radioFlow, 0, 0);

            Label sourceHint = new Label();
            sourceHint.Text = "自动检测只读取现有配置，不会改变 Clash Verge 或 Windows 的系统代理设置。";
            sourceHint.ForeColor = MutedColor;
            sourceHint.Dock = DockStyle.Fill;
            sourceHint.TextAlign = ContentAlignment.MiddleLeft;
            sourceHint.AutoEllipsis = true;
            sourceHint.MinimumSize = new Size(0, 24);
            sourceLayout.Controls.Add(sourceHint, 0, 1);
            root.Controls.Add(sourceGroup, 0, 1);

            GroupBox endpointGroup = CreateGroupBox("环境变量值");
            TableLayoutPanel endpointLayout = new TableLayoutPanel();
            endpointLayout.Dock = DockStyle.Fill;
            endpointLayout.Padding = new Padding(12, 10, 12, 8);
            endpointLayout.ColumnCount = 2;
            endpointLayout.RowCount = 4;
            endpointLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            endpointLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            endpointLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43F));
            endpointLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43F));
            endpointLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43F));
            endpointLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            endpointGroup.Controls.Add(endpointLayout);

            endpointLayout.Controls.Add(CreateVariableLabel("http_proxy"), 0, 0);
            endpointLayout.Controls.Add(CreateVariableLabel("https_proxy"), 0, 1);
            endpointLayout.Controls.Add(CreateVariableLabel("no_proxy"), 0, 2);

            httpProxyTextBox = CreateProxyTextBox();
            httpsProxyTextBox = CreateProxyTextBox();
            noProxyTextBox = CreateProxyTextBox();
            httpProxyTextBox.Margin = new Padding(0, 5, 0, 6);
            httpsProxyTextBox.Margin = new Padding(0, 5, 0, 6);
            noProxyTextBox.Margin = new Padding(0, 5, 0, 6);
            endpointLayout.Controls.Add(httpProxyTextBox, 1, 0);
            endpointLayout.Controls.Add(httpsProxyTextBox, 1, 1);
            endpointLayout.Controls.Add(noProxyTextBox, 1, 2);

            Label endpointHint = new Label();
            endpointHint.Text = "代理地址可省略协议；no_proxy 使用逗号分隔，例如 localhost,127.0.0.1。";
            endpointHint.ForeColor = MutedColor;
            endpointHint.Dock = DockStyle.Fill;
            endpointHint.TextAlign = ContentAlignment.MiddleLeft;
            endpointHint.AutoEllipsis = true;
            endpointLayout.SetColumnSpan(endpointHint, 2);
            endpointLayout.Controls.Add(endpointHint, 0, 3);
            root.Controls.Add(endpointGroup, 0, 2);

            GroupBox statusGroup = CreateGroupBox("状态");
            TableLayoutPanel statusLayout = new TableLayoutPanel();
            statusLayout.Dock = DockStyle.Fill;
            statusLayout.Padding = new Padding(12, 10, 12, 10);
            statusLayout.ColumnCount = 1;
            statusLayout.RowCount = 3;
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statusGroup.Controls.Add(statusLayout);

            discoveryStatusLabel = new Label();
            discoveryStatusLabel.Dock = DockStyle.Fill;
            discoveryStatusLabel.Font = new Font(Font, FontStyle.Bold);
            discoveryStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            discoveryStatusLabel.Text = "正在检测...";
            statusLayout.Controls.Add(discoveryStatusLabel, 0, 0);

            discoveryDetailLabel = new Label();
            discoveryDetailLabel.Dock = DockStyle.Fill;
            discoveryDetailLabel.ForeColor = MutedColor;
            discoveryDetailLabel.TextAlign = ContentAlignment.TopLeft;
            discoveryDetailLabel.AutoEllipsis = true;
            statusLayout.Controls.Add(discoveryDetailLabel, 0, 1);

            currentEnvironmentLabel = new Label();
            currentEnvironmentLabel.Dock = DockStyle.Fill;
            currentEnvironmentLabel.ForeColor = InkColor;
            currentEnvironmentLabel.TextAlign = ContentAlignment.TopLeft;
            currentEnvironmentLabel.AutoEllipsis = true;
            currentEnvironmentLabel.Padding = new Padding(0, 2, 0, 0);
            statusLayout.Controls.Add(currentEnvironmentLabel, 0, 2);
            root.Controls.Add(statusGroup, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 12, 0, 0);

            applyButton = CreateActionButton("应用并保存", true);
            clearButton = CreateActionButton("清除", false);
            refreshButton = CreateActionButton("刷新检测", false);
            applyButton.Click += ApplyButtonClick;
            clearButton.Click += ClearButtonClick;
            refreshButton.Click += RefreshButtonClick;
            actions.Controls.Add(applyButton);
            actions.Controls.Add(clearButton);
            actions.Controls.Add(refreshButton);
            root.Controls.Add(actions, 0, 4);

            notifyIcon = new NotifyIcon();
            InitializeTrayIcon();
            AcceptButton = applyButton;
            Shown += MainFormShown;
            Resize += MainFormResize;
            FormClosed += MainFormClosed;
        }

        private Control CreateHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 1;
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label title = new Label();
            title.Text = "代理环境变量管理器";
            title.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = InkColor;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            header.Controls.Add(title, 0, 0);

            Label subtitle = new Label();
            subtitle.Text = "一键设置或清除当前用户的 http_proxy / https_proxy / no_proxy";
            subtitle.Font = new Font(Font.FontFamily, 10F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = MutedColor;
            subtitle.Dock = DockStyle.Fill;
            subtitle.TextAlign = ContentAlignment.TopLeft;
            header.Controls.Add(subtitle, 0, 1);
            return header;
        }

        private GroupBox CreateGroupBox(string text)
        {
            GroupBox group = new GroupBox();
            group.Text = text;
            group.Dock = DockStyle.Fill;
            group.ForeColor = InkColor;
            group.BackColor = SurfaceColor;
            group.Margin = new Padding(0, 0, 0, 8);
            return group;
        }

        private RadioButton CreateSourceRadio(string text, bool selected)
        {
            RadioButton radio = new RadioButton();
            radio.Text = text;
            radio.AutoSize = true;
            radio.Checked = selected;
            radio.ForeColor = InkColor;
            radio.BackColor = Color.Transparent;
            return radio;
        }

        private Label CreateVariableLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Consolas", 10F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = InkColor;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        private TextBox CreateProxyTextBox()
        {
            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            textBox.ForeColor = InkColor;
            textBox.BackColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            return textBox;
        }

        private Button CreateActionButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(primary ? 126 : 96, 34);
            button.Margin = new Padding(10, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(198, 205, 216);
            button.BackColor = primary ? AccentColor : Color.White;
            button.ForeColor = primary ? Color.White : InkColor;
            button.Font = new Font(Font.FontFamily, 9F, primary ? FontStyle.Bold : FontStyle.Regular);
            button.Cursor = Cursors.Hand;
            if (primary)
            {
                button.FlatAppearance.MouseOverBackColor = AccentHoverColor;
                button.FlatAppearance.MouseDownBackColor = AccentHoverColor;
            }
            else
            {
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 244, 249);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(231, 236, 243);
            }
            return button;
        }

        private void InitializeTrayIcon()
        {
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("打开主界面");
            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            openItem.Click += delegate { RestoreFromTray(); };
            exitItem.Click += delegate { Close(); };
            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitItem);

            notifyIcon.Icon = trayIcon ?? SystemIcons.Application;
            notifyIcon.Text = "代理环境变量管理器";
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { RestoreFromTray(); };
        }

        private void MainFormShown(object sender, EventArgs e)
        {
            RefreshSelectedSource();
            RefreshCurrentEnvironment();
        }

        private void MainFormResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        }

        private void MainFormClosed(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !trayResourcesDisposed)
            {
                trayResourcesDisposed = true;
                notifyIcon.Visible = false;
                notifyIcon.Icon = null;
                notifyIcon.Dispose();

                Icon = null;
                if (trayIcon != null)
                {
                    trayIcon.Dispose();
                }
                if (windowIcon != null)
                {
                    windowIcon.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;

            if (!trayHintShown)
            {
                trayHintShown = true;
                notifyIcon.ShowBalloonTip(
                    2500,
                    "代理环境变量管理器",
                    "程序已最小化到系统托盘，双击图标即可恢复。",
                    ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
        }

        private void SourceRadioCheckedChanged(object sender, EventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (suppressSourceChange || radio == null || !radio.Checked)
            {
                return;
            }

            RefreshSelectedSource();
        }

        private void RefreshSelectedSource()
        {
            if (manualSourceRadio.Checked)
            {
                SetDiscoveryStatus("手动输入代理地址", MutedColor);
                discoveryDetailLabel.Text = "填写上方两个环境变量后，点击“应用并保存”。";
                return;
            }

            Cursor previousCursor = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                ProxyDiscoveryResult result = clashSourceRadio.Checked
                    ? ClashProxyReader.Discover()
                    : SystemProxyReader.Discover();

                discoveryDetailLabel.Text = result.Detail ?? String.Empty;
                if (result.Available)
                {
                    suppressSourceChange = true;
                    httpProxyTextBox.Text = result.HttpProxy;
                    httpsProxyTextBox.Text = result.HttpsProxy;
                    noProxyTextBox.Text = result.NoProxy ?? String.Empty;
                    suppressSourceChange = false;
                    SetDiscoveryStatus(result.Status, SuccessColor);
                }
                else
                {
                    httpProxyTextBox.Text = String.Empty;
                    httpsProxyTextBox.Text = String.Empty;
                    noProxyTextBox.Text = String.Empty;
                    SetDiscoveryStatus(result.Status, ErrorColor);
                }
            }
            finally
            {
                Cursor = previousCursor;
            }
        }

        private void RefreshCurrentEnvironment()
        {
            try
            {
                EnvironmentVariableState state = UserEnvironmentStore.Read();
                string http = state.HttpFound ? DisplayValue(state.HttpValue) : "未设置";
                string https = state.HttpsFound ? DisplayValue(state.HttpsValue) : "未设置";
                string noProxy = state.NoProxyFound ? DisplayValue(state.NoProxyValue) : "未设置";
                currentEnvironmentLabel.Text =
                    "当前用户持久环境变量：" + Environment.NewLine +
                    "http_proxy = " + http + Environment.NewLine +
                    "https_proxy = " + https + Environment.NewLine +
                    "no_proxy = " + noProxy;
            }
            catch (Exception ex)
            {
                currentEnvironmentLabel.Text = "读取当前环境变量失败：" + ex.Message;
            }
        }

        private void ApplyButtonClick(object sender, EventArgs e)
        {
            string httpProxy;
            string httpsProxy;
            string noProxy;
            string error;

            if (!ProxyUrlNormalizer.TryNormalize(httpProxyTextBox.Text, out httpProxy, out error))
            {
                MessageBox.Show(this, "http_proxy 无效：" + error, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                httpProxyTextBox.Focus();
                return;
            }

            if (!ProxyUrlNormalizer.TryNormalize(httpsProxyTextBox.Text, out httpsProxy, out error))
            {
                MessageBox.Show(this, "https_proxy 无效：" + error, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                httpsProxyTextBox.Focus();
                return;
            }

            try
            {
                httpProxyTextBox.Text = httpProxy;
                httpsProxyTextBox.Text = httpsProxy;
                noProxy = NoProxyNormalizer.Normalize(noProxyTextBox.Text);
                noProxyTextBox.Text = noProxy;
                EnvironmentSaveResult result = UserEnvironmentStore.Save(httpProxy, httpsProxy, noProxy);
                RefreshCurrentEnvironment();

                string message = "已保存到当前用户环境变量：\n\n" +
                    "http_proxy = " + httpProxy + "\n" +
                    "https_proxy = " + httpsProxy + "\n" +
                    "no_proxy = " + noProxy + "\n\n" +
                    "已打开的终端和 IDE 需要重新启动后才能读取新值。";
                if (!result.BroadcastSucceeded)
                {
                    message += "\n\n环境变更广播未成功，但注册表值已经写入。";
                }

                SetDiscoveryStatus("环境变量已应用", SuccessColor);
                MessageBox.Show(this, message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetDiscoveryStatus("保存环境变量失败", ErrorColor);
                MessageBox.Show(this, "保存失败：" + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearButtonClick(object sender, EventArgs e)
        {
            try
            {
                EnvironmentSaveResult result = UserEnvironmentStore.Clear();
                RefreshCurrentEnvironment();
                SetDiscoveryStatus("http_proxy、https_proxy 和 no_proxy 已清除", SuccessColor);
                discoveryDetailLabel.Text = "只清除了当前用户的这三个环境变量，其他环境变量未变化。" +
                    (result.BroadcastSucceeded ? String.Empty : " 环境变更广播未成功，但注册表值已经清除。");
            }
            catch (Exception ex)
            {
                SetDiscoveryStatus("清除环境变量失败", ErrorColor);
                MessageBox.Show(this, "清除失败：" + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshButtonClick(object sender, EventArgs e)
        {
            RefreshSelectedSource();
            RefreshCurrentEnvironment();
        }

        private void SetDiscoveryStatus(string text, Color color)
        {
            discoveryStatusLabel.Text = text;
            discoveryStatusLabel.ForeColor = color;
        }

        private static string DisplayValue(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "空字符串";
            }

            return value;
        }
    }
}
