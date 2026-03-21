namespace PlcComm.Toyopuc.DeviceMonitor;

internal sealed class ConnectionSettingsDialog : Form
{
    private const string RelayHopsExample = "P1-L2:N2,P1-L2:N4,P1-L2:N6";
    private readonly ConnectionSettingsDraft _initial;

    private readonly TextBox _hostTextBox = new();
    private readonly TextBox _portTextBox = new();
    private readonly ComboBox _transportComboBox = new();
    private readonly ComboBox _profileComboBox = new();
    private readonly TextBox _intervalTextBox = new();
    private readonly TextBox _localPortTextBox = new();
    private readonly TextBox _timeoutTextBox = new();
    private readonly TextBox _retriesTextBox = new();
    private readonly TextBox _hopsTextBox = new();
    private readonly TextBox _hopsExampleTextBox = new();
    private const int ProfileFieldMinWidth = 360;

    public ConnectionSettingsDialog(ConnectionSettingsDraft initial)
    {
        _initial = initial;
        Text = "Connection Settings";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(820, 380);

        _transportComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _transportComboBox.Items.AddRange(["tcp", "udp"]);

        _profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var profileName in ToyopucDeviceProfiles.GetNames())
        {
            _profileComboBox.Items.Add(profileName);
        }

        _hostTextBox.Text = initial.Host;
        _portTextBox.Text = initial.Port;
        _transportComboBox.SelectedItem = initial.Transport;
        if (_transportComboBox.SelectedIndex < 0)
        {
            _transportComboBox.SelectedIndex = 0;
        }

        _profileComboBox.SelectedItem = DeviceMonitorCatalogHelper.NormalizeProfile(initial.Profile);
        if (_profileComboBox.SelectedIndex < 0)
        {
            _profileComboBox.SelectedIndex = 0;
        }

        _intervalTextBox.Text = initial.Interval;
        _localPortTextBox.Text = initial.LocalPort;
        _timeoutTextBox.Text = initial.Timeout;
        _retriesTextBox.Text = initial.Retries;
        _hopsTextBox.Text = initial.Hops;

        _hopsExampleTextBox.Text = RelayHopsExample;
        _hopsExampleTextBox.ReadOnly = true;
        _hopsExampleTextBox.TabStop = false;
        _hopsExampleTextBox.BorderStyle = BorderStyle.FixedSingle;
        _hopsExampleTextBox.BackColor = SystemColors.Window;
        _hopsExampleTextBox.Dock = DockStyle.Fill;

        BuildLayout();
        ConfigureProfileComboBoxSizing();
    }

    public ConnectionSettingsDraft Draft =>
        new(
            _hostTextBox.Text.Trim(),
            _portTextBox.Text.Trim(),
            _transportComboBox.Text.Trim(),
            _profileComboBox.Text.Trim(),
            _intervalTextBox.Text.Trim(),
            _localPortTextBox.Text.Trim(),
            _timeoutTextBox.Text.Trim(),
            _retriesTextBox.Text.Trim(),
            _hopsTextBox.Text.Trim(),
            _initial.Program,
            _initial.Device,
            _initial.StartAddress);

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 7,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(root);

        AddField(root, 0, 0, "Host", _hostTextBox);
        AddField(root, 2, 0, "Port", _portTextBox, 100);
        AddField(root, 0, 1, "Transport", _transportComboBox, 120);
        AddField(root, 2, 1, "Profile", _profileComboBox, ProfileFieldMinWidth);
        AddField(root, 0, 2, "Interval", _intervalTextBox, 100);
        AddField(root, 2, 2, "Local UDP", _localPortTextBox, 100);
        AddField(root, 0, 3, "Timeout", _timeoutTextBox, 100);
        AddField(root, 2, 3, "Retries", _retriesTextBox, 100);

        root.Controls.Add(CreateLabel("Relay hops"), 0, 4);
        _hopsTextBox.Dock = DockStyle.Fill;
        root.Controls.Add(_hopsTextBox, 1, 4);
        root.SetColumnSpan(_hopsTextBox, 3);

        root.Controls.Add(CreateLabel("Example"), 0, 5);
        root.Controls.Add(_hopsExampleTextBox, 1, 5);
        root.SetColumnSpan(_hopsExampleTextBox, 3);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Right,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
        };
        var okButton = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 0, 6);
        root.SetColumnSpan(buttons, 4);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private static void AddField(TableLayoutPanel layout, int column, int row, string label, Control control, int width = 0)
    {
        layout.Controls.Add(CreateLabel(label), column, row);
        if (width > 0)
        {
            control.Width = width;
            control.Anchor = AnchorStyles.Left;
        }
        else
        {
            control.Dock = DockStyle.Fill;
        }

        layout.Controls.Add(control, column + 1, row);
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Padding = new Padding(0, 8, 8, 0),
        };
    }

    private void ConfigureProfileComboBoxSizing()
    {
        var widestItemPixels = 0;
        foreach (var item in _profileComboBox.Items)
        {
            var text = item?.ToString() ?? string.Empty;
            var width = TextRenderer.MeasureText(text, _profileComboBox.Font).Width;
            if (width > widestItemPixels)
            {
                widestItemPixels = width;
            }
        }

        var desiredPixels = widestItemPixels + SystemInformation.VerticalScrollBarWidth + 24;
        _profileComboBox.DropDownWidth = Math.Max(_profileComboBox.Width, desiredPixels);
        _profileComboBox.Width = Math.Max(ProfileFieldMinWidth, Math.Min(_profileComboBox.DropDownWidth, 520));
    }
}

internal sealed record ConnectionSettingsDraft(
    string Host,
    string Port,
    string Transport,
    string Profile,
    string Interval,
    string LocalPort,
    string Timeout,
    string Retries,
    string Hops,
    string Program,
    string Device,
    string StartAddress);
