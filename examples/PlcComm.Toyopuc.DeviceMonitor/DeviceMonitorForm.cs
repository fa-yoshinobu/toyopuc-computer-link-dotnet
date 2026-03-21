using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using PlcComm.Toyopuc;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal sealed class DeviceMonitorForm : Form
{
    private const int InitialLoadedRowCount = 64;
    private const int ScrollShiftRowCount = 32;
    private const string BitColumnNamePrefix = "Bit";
    private static readonly TimeSpan AutoReconnectRetryInterval = TimeSpan.FromSeconds(2);

    private readonly BindingList<MonitorRow> _rows = [];
    private readonly System.Windows.Forms.Timer _pollTimer = new();
    private readonly System.Windows.Forms.Timer _scrollSettleTimer = new() { Interval = 120 };
    private readonly DataGridView _grid = new();

    private readonly TextBox _hostTextBox = new() { Text = "192.168.250.101" };
    private readonly TextBox _portTextBox = new() { Text = "1025" };
    private readonly ComboBox _transportComboBox = new();
    private readonly ComboBox _profileComboBox = new();
    private readonly TextBox _localPortTextBox = new() { Text = "0" };
    private readonly TextBox _timeoutTextBox = new() { Text = "3" };
    private readonly TextBox _retriesTextBox = new() { Text = "0" };
    private readonly TextBox _intervalTextBox = new() { Text = "0.5" };
    private readonly TextBox _hopsTextBox = new();
    private readonly Label _loadedRowsValueLabel = new()
    {
        AutoSize = true,
        Text = "0",
        Padding = new Padding(4, 8, 8, 0),
    };

    private readonly ComboBox _programComboBox = new();
    private readonly ComboBox _deviceComboBox = new();
    private readonly ComboBox _startAddressComboBox = new();

    private readonly Button _monitorButton = new() { Text = "Monitor Start", AutoSize = true };
    private readonly Button _pollNowButton = new() { Text = "Poll Once", AutoSize = true };
    private readonly Button _cpuStatusRefreshButton = new() { Text = "Refresh", AutoSize = true };
    private readonly ToolStripMenuItem _sessionMenuItem = new("Session");
    private readonly ToolStripMenuItem _connectMenuItem = new("Connect");
    private readonly ToolStripMenuItem _disconnectMenuItem = new("Disconnect");
    private readonly ToolStripMenuItem _settingsMenuItem = new("Settings");
    private readonly ToolStripMenuItem _windowsMenuItem = new("Diagnostics");
    private readonly ToolStripMenuItem _cpuStatusMenuItem = new("CPU Status");
    private readonly ToolStripMenuItem _clockMenuItem = new("Clock");
    private readonly ToolStripMenuItem _helpMenuItem = new("Help");
    private readonly ToolStripMenuItem _versionMenuItem = new("Version");

    private readonly ToolStripStatusLabel _connectionStatusLabel = new() { Text = "Disconnected" };
    private readonly ToolStripStatusLabel _selectedLabel = new() { Text = "Selected: -" };
    private readonly ToolStripStatusLabel _lastPollLabel = new() { Text = "Last poll: -" };
    private readonly ToolStripStatusLabel _messageLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _endpointLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _connectionSummaryLabel = new()
    {
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 4, 0, 0),
    };
    private readonly Label _connectionStateLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        Padding = new Padding(0, 4, 12, 0),
    };

    private ToyopucDeviceClient? _client;
    private DetailViewerForm? _cpuStatusWindow;
    private ClockViewerForm? _clockWindow;
    private DetailViewerForm? _versionWindow;
    private bool _busy;
    private bool _monitorRunning;
    private bool _loadingMoreRows;
    private bool _scrollingUi;
    private bool _suppressSelectionSearch;
    private bool _canShiftForward;
    private bool _rowStylesDirty = true;
    private bool _restoreSelectionOnStartup;
    private bool _startupSelectionPending;
    private bool _autoReconnectInProgress;
    private DateTime _lastAutoReconnectAttemptUtc = DateTime.MinValue;
    private readonly Dictionary<(bool On, bool Selected, int Diameter), Bitmap> _bitGlyphCache = [];
    private string _loadedPrefix = string.Empty;
    private string _loadedArea = string.Empty;
    private string _loadedSuffix = string.Empty;
    private IReadOnlyList<ToyopucAddressRange> _loadedRanges = [];
    private int _loadedAddressWidth;
    private int _loadedStep = 1;
    private int _windowOffset;

    public DeviceMonitorForm()
    {
        Text = "Toyopuc Register Monitor";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScroll = true;
        MinimumSize = new Size(980, 680);
        Size = new Size(1220, 1170);
        BackColor = SystemColors.Control;

        _transportComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _transportComboBox.Items.AddRange(["tcp", "udp"]);
        _transportComboBox.SelectedIndex = 0;

        _profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var profileName in ToyopucDeviceProfiles.GetNames())
        {
            _profileComboBox.Items.Add(profileName);
        }

        _profileComboBox.SelectedIndex = 0;

        _programComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _programComboBox.Items.AddRange(["P1", "P2", "P3", "Direct"]);
        _programComboBox.SelectedIndex = 0;

        _deviceComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _startAddressComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _startAddressComboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _startAddressComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        RefreshProgramChoices();
        RefreshAreaChoices();
        RefreshStartAddressChoices();

        _pollTimer.Tick += PollTimer_Tick;
        _scrollSettleTimer.Tick += ScrollSettleTimer_Tick;

        BuildLayout();
        LoadPersistedConnectionSettings();
        UpdateConnectionStatus("Disconnected");
        WireEvents();
        RefreshEndpointLabel();
        RefreshMonitorButtonText();
        _startupSelectionPending = _restoreSelectionOnStartup;
        Shown += async (_, _) => await RunStartupSelectionRestoreAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildMenuBar(), 0, 0);
        root.Controls.Add(BuildSessionBar(), 0, 1);
        root.Controls.Add(BuildSelectionGroup(), 0, 2);
        root.Controls.Add(BuildContentArea(), 0, 3);

        var statusStrip = new StatusStrip();
        statusStrip.Items.AddRange(
        [
            _connectionStatusLabel,
            new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left, Text = " " },
            _selectedLabel,
            new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left, Text = " " },
            _lastPollLabel,
            new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left, Text = " " },
            _messageLabel,
            _endpointLabel,
        ]);
        Controls.Add(statusStrip);
    }

    private Control BuildMenuBar()
    {
        _sessionMenuItem.DropDownItems.AddRange(
        [
            _connectMenuItem,
            _disconnectMenuItem,
            new ToolStripSeparator(),
            _settingsMenuItem,
        ]);
        _windowsMenuItem.DropDownItems.AddRange(
        [
            _cpuStatusMenuItem,
            _clockMenuItem,
        ]);
        _helpMenuItem.DropDownItems.Add(_versionMenuItem);

        var menuStrip = new MenuStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Visible,
        };
        menuStrip.Items.AddRange([_sessionMenuItem, _windowsMenuItem, _helpMenuItem]);
        MainMenuStrip = menuStrip;
        return menuStrip;
    }

    private Control BuildSessionBar()
    {
        var group = new GroupBox
        {
            Text = "Session",
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(_connectionStateLabel, 0, 0);
        layout.Controls.Add(_connectionSummaryLabel, 1, 0);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildSelectionGroup()
    {
        var group = new GroupBox
        {
            Text = "Register Monitor",
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8),
            AutoSize = true,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 11,
            RowCount = 1,
            AutoSize = true,
        };
        for (var i = 0; i < 11; i++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(i == 10 ? SizeType.Percent : SizeType.AutoSize, i == 10 ? 100 : 0));
        }

        AddLabeledControl(layout, 0, 0, "Program", _programComboBox, 80);
        AddLabeledControl(layout, 2, 0, "Device", _deviceComboBox, 90);
        AddLabeledControl(layout, 4, 0, "Address", _startAddressComboBox, 120);
        var countLabel = CreateLabel("Rows");
        layout.Controls.Add(countLabel, 6, 0);
        layout.Controls.Add(_loadedRowsValueLabel, 7, 0);
        layout.Controls.Add(Sized(_monitorButton, 140), 8, 0);
        layout.Controls.Add(Sized(_pollNowButton, 120), 9, 0);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildContentArea()
    {
        ConfigureGrid();
        return _grid;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.DataSource = _rows;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeColumns = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.Font = new Font("Consolas", 10f, FontStyle.Regular);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.RowTemplate.Height = 26;
        EnableDoubleBuffer(_grid);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MonitorRow.AddressLabel),
            HeaderText = "Address",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 130,
        });

        for (var bit = 15; bit >= 0; bit--)
        {
            var bitColumn = new DataGridViewTextBoxColumn
            {
                Name = $"{BitColumnNamePrefix}{bit}",
                HeaderText = bit.ToString("X1", CultureInfo.InvariantCulture),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 28,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Tag = bit,
            };
            bitColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            bitColumn.DefaultCellStyle.Font = new Font("Segoe UI Symbol", 10f, FontStyle.Regular);
            bitColumn.DefaultCellStyle.Padding = new Padding(0, 1, 0, 1);
            if (bit is 12 or 8 or 4)
            {
                bitColumn.DividerWidth = 2;
            }

            _grid.Columns.Add(bitColumn);
        }

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MonitorRow.HexText),
            HeaderText = "Hex",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 82,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MonitorRow.DecText),
            HeaderText = "Dec",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 82,
        });
        _grid.Scroll += Grid_Scroll;
        _grid.Resize += (_, _) =>
        {
            ClearBitGlyphCache();
            ScheduleGridWindowRefresh();
        };
    }

    private void WireEvents()
    {
        _connectMenuItem.Click += async (_, _) => await ConnectAsync(showErrorDialog: true, startMonitor: true);
        _disconnectMenuItem.Click += (_, _) =>
        {
            DisconnectClient("Disconnected");
            SetMessage("disconnected");
        };
        _settingsMenuItem.Click += (_, _) => OpenConnectionSettings();
        _pollNowButton.Click += async (_, _) => await PollAllAsync(manual: true);
        _cpuStatusMenuItem.Click += async (_, _) => await ReadStatusAsync();
        _clockMenuItem.Click += async (_, _) => await ReadClockAsync();
        _versionMenuItem.Click += (_, _) => ShowVersionWindow();
        _cpuStatusRefreshButton.Click += async (_, _) => await ReadStatusAsync();
        _monitorButton.Click += async (_, _) => await ToggleMonitorAsync();
        _grid.SelectionChanged += (_, _) => RefreshSelectionStatus();
        _grid.CellPainting += Grid_CellPainting;
        _grid.CellMouseDoubleClick += Grid_CellMouseDoubleClick;
        _programComboBox.SelectedIndexChanged += (_, _) =>
        {
            RefreshAreaChoices();
            RefreshStartAddressChoices();
            RefreshEndpointLabel();
            ScheduleSelectionSearch();
        };
        _deviceComboBox.SelectedIndexChanged += (_, _) =>
        {
            RefreshStartAddressChoices();
            ScheduleSelectionSearch();
        };
        _startAddressComboBox.SelectionChangeCommitted += (_, _) => ScheduleSelectionSearch();
        _startAddressComboBox.Validated += (_, _) => ScheduleSelectionSearch();
        _startAddressComboBox.KeyDown += async (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Enter)
            {
                eventArgs.SuppressKeyPress = true;
                await SearchAsync();
            }
        };

        _hostTextBox.TextChanged += (_, _) => RefreshEndpointLabel();
        _portTextBox.TextChanged += (_, _) => RefreshEndpointLabel();
        _transportComboBox.SelectedIndexChanged += (_, _) => RefreshEndpointLabel();
        _profileComboBox.SelectedIndexChanged += (_, _) =>
        {
            RefreshProgramChoices();
            RefreshAreaChoices();
            RefreshEndpointLabel();
            RefreshStartAddressChoices();
            ScheduleSelectionSearch();
        };
        _localPortTextBox.TextChanged += (_, _) => RefreshEndpointLabel();
        _hopsTextBox.TextChanged += (_, _) => RefreshEndpointLabel();
        _intervalTextBox.TextChanged += (_, _) => RefreshPollingState();
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_busy || !_monitorRunning)
        {
            return;
        }

        if (_scrollingUi)
        {
            return;
        }

        _ = PollAllAsync(manual: false);
    }

    private void ScrollSettleTimer_Tick(object? sender, EventArgs e)
    {
        _scrollSettleTimer.Stop();
        _scrollingUi = false;

        var shifted = TryShiftGridWindow();
        RefreshPollingState();

        if (shifted && _client is not null && _monitorRunning && !_busy)
        {
            _ = PollAllAsync(manual: false);
        }
    }

    private async Task SearchAsync(bool showErrorDialog = true)
    {
        try
        {
            var request = BuildSelectionRequest();
            InitializeLoadedRange(request);
            if (!ReloadSelectionWindow(request.InitialOffset))
            {
                throw new InvalidOperationException("no rows could be loaded for the selected range");
            }

            SetMessage($"{_rows.Count} rows loaded from {_rows[0].AddressLabel}");
        }
        catch (Exception exception)
        {
            if (showErrorDialog)
            {
                MessageBox.Show(this, exception.Message, "Search", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            SetMessage(exception.Message, isError: true);
        }

        if (_client is not null && _monitorRunning)
        {
            await PollAllAsync(manual: true);
        }
    }

    private async Task<bool> ConnectAsync(bool showErrorDialog, bool startMonitor, bool triggerInitialPoll = true)
    {
        var runInitialPoll = false;
        var connected = false;

        if (!await BeginBusyAsync())
        {
            return false;
        }

        try
        {
            DisconnectClient(null);
            var settings = ReadConnectionSettings();
            var client = CreateClient(settings);
            var status = await Task.Run(
                () => settings.Hops is null
                    ? client.ReadCpuStatus()
                    : client.RelayReadCpuStatus(settings.Hops));
            _client = client;
            _monitorRunning = startMonitor;
            UpdateConnectionStatus("Connected");
            RefreshMonitorButtonText();
            RefreshEndpointLabel(settings);
            RefreshPollingState();
            SetMessage(startMonitor
                ? $"connected: {SummarizeStatus(status)}; monitor started"
                : $"connected: {SummarizeStatus(status)}");
            runInitialPoll = triggerInitialPoll && _monitorRunning && _rows.Count > 0;
            connected = true;
        }
        catch (Exception exception)
        {
            DisconnectClient("Connect failed");
            SetMessage($"connect failed: {exception.Message}", isError: true);
            if (showErrorDialog)
            {
                MessageBox.Show(this, exception.Message, "Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            EndBusy();
        }

        if (runInitialPoll)
        {
            await PollAllAsync(manual: true);
        }

        return connected;
    }

    private async Task PollAllAsync(bool manual)
    {
        if (_rows.Count == 0)
        {
            if (manual)
            {
                SetMessage("no rows loaded");
            }

            return;
        }

        var client = _client;
        if (client is null)
        {
            if (!manual)
            {
                if (_monitorRunning)
                {
                    await TryAutoReconnectAsync();
                }
                else
                {
                    SetMessage("not connected", isError: false);
                }

                return;
            }

            var connected = await ConnectAsync(showErrorDialog: true, startMonitor: false, triggerInitialPoll: false);
            if (!connected)
            {
                return;
            }

            client = _client;
            if (client is null)
            {
                return;
            }
        }

        if (!await BeginBusyAsync(skipUiDisable: !manual))
        {
            return;
        }

        var shouldAutoReconnect = false;
        try
        {
            var settings = ReadConnectionSettings();
            var containsPackedWordRows = _rows.Any(static row => row.Device.EndsWith("W", StringComparison.OrdinalIgnoreCase));
            if (!containsPackedWordRows)
            {
                var devices = _rows.Select(static row => (object)row.Device).ToArray();
                try
                {
                    var values = await Task.Run(
                        () => settings.Hops is null
                            ? client.ReadMany(devices)
                            : client.RelayReadMany(settings.Hops, devices));

                    WithGridLayoutSuspended(() =>
                    {
                        for (var i = 0; i < values.Length; i++)
                        {
                            UpdateRowValue(_rows[i], values[i], manual ? "manual" : "ok");
                        }
                    });

                    _lastPollLabel.Text = $"Last poll: {DateTime.Now:HH:mm:ss}";
                    SetMessage(manual ? "poll complete" : "monitor updated");
                    return;
                }
                catch (Exception exception)
                {
                    if (IsConnectionFault(exception))
                    {
                        throw;
                    }

                    // Fall back to single-device reads below.
                }
            }
            else
            {
                await ReadPackedWordRowsBySegmentsAsync(client, settings, manual);
            }

            _lastPollLabel.Text = $"Last poll: {DateTime.Now:HH:mm:ss}";
            SetMessage(manual ? "poll complete" : "monitor updated");
        }
        catch (Exception exception)
        {
            if (!manual && _monitorRunning && IsConnectionFault(exception))
            {
                shouldAutoReconnect = true;
                SetMessage($"connection lost: {exception.Message}", isError: true);
            }
            else
            {
                SetMessage($"poll failed: {exception.Message}", isError: true);
            }
        }
        finally
        {
            ApplyRowStyles();
            EndBusy(skipUiEnable: !manual);
        }

        if (shouldAutoReconnect)
        {
            await TryAutoReconnectAsync();
        }
    }

    private async Task ReadPackedWordRowsBySegmentsAsync(ToyopucDeviceClient client, ConnectionSettings settings, bool manual)
    {
        var runStart = 0;
        while (runStart < _rows.Count)
        {
            if (!TryParseLoadedIndex(_rows[runStart].Device, out var runEndIndex))
            {
                await ReadRowsIndividuallyAsync(client, settings, runStart, runStart, manual);
                runStart++;
                continue;
            }

            var runEnd = runStart;
            while (runEnd + 1 < _rows.Count
                && TryParseLoadedIndex(_rows[runEnd + 1].Device, out var nextIndex)
                && nextIndex == runEndIndex + _loadedStep)
            {
                runEnd++;
                runEndIndex = nextIndex;
            }

            var runLength = runEnd - runStart + 1;
            try
            {
                if (runLength == 1)
                {
                    var singleValue = await Task.Run(
                        () => settings.Hops is null
                            ? client.Read(_rows[runStart].Device)
                            : client.RelayRead(settings.Hops, _rows[runStart].Device));
                    UpdateRowValue(_rows[runStart], singleValue, manual ? "manual" : "ok");
                }
                else
                {
                    var valuesObject = await Task.Run(
                        () => settings.Hops is null
                            ? client.Read(_rows[runStart].Device, runLength)
                            : client.RelayRead(settings.Hops, _rows[runStart].Device, runLength));
                    if (valuesObject is not object[] values || values.Length != runLength)
                    {
                        throw new InvalidOperationException(
                            $"packed-word segment read returned unexpected payload for {_rows[runStart].Device} count={runLength}");
                    }

                    WithGridLayoutSuspended(() =>
                    {
                        for (var i = 0; i < runLength; i++)
                        {
                            UpdateRowValue(_rows[runStart + i], values[i], manual ? "manual" : "ok");
                        }
                    });
                }
            }
            catch (Exception exception)
            {
                if (IsConnectionFault(exception))
                {
                    throw;
                }

                await ReadRowsIndividuallyAsync(client, settings, runStart, runEnd, manual);
            }

            runStart = runEnd + 1;
        }
    }

    private async Task ReadRowsIndividuallyAsync(
        ToyopucDeviceClient client,
        ConnectionSettings settings,
        int startIndex,
        int endIndex,
        bool manual)
    {
        for (var i = startIndex; i <= endIndex; i++)
        {
            var row = _rows[i];
            try
            {
                var value = await Task.Run(
                    () => settings.Hops is null
                        ? client.Read(row.Device)
                        : client.RelayRead(settings.Hops, row.Device));
                UpdateRowValue(row, value, manual ? "manual" : "ok");
            }
            catch (Exception exception)
            {
                if (IsConnectionFault(exception))
                {
                    throw;
                }

                SetRowState(row, "error");
                row.HexText = "ERR";
                row.DecText = string.Empty;
                row.LastError = exception.Message;
            }
        }
    }

    private bool TryParseLoadedIndex(string device, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(_loadedArea))
        {
            return false;
        }

        var prefixArea = $"{_loadedPrefix}{_loadedArea}";
        if (!device.StartsWith(prefixArea, StringComparison.Ordinal))
        {
            return false;
        }

        var suffixLength = _loadedSuffix.Length;
        if (device.Length <= prefixArea.Length + suffixLength)
        {
            return false;
        }

        var numericPartLength = device.Length - prefixArea.Length - suffixLength;
        var numericPart = device.Substring(prefixArea.Length, numericPartLength);
        return int.TryParse(numericPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out index);
    }

    private async Task ReadSelectedAsync()
    {
        var row = SelectedRow();
        if (row is null)
        {
            SetMessage("select a row first", isError: true);
            return;
        }

        var client = _client;
        if (client is null)
        {
            SetMessage("not connected", isError: true);
            return;
        }

        if (!await BeginBusyAsync())
        {
            return;
        }

        try
        {
            var settings = ReadConnectionSettings();
            var value = await Task.Run(
                () => settings.Hops is null
                    ? client.Read(row.Device)
                    : client.RelayRead(settings.Hops, row.Device));
            UpdateRowValue(row, value, "manual");
            ApplyRowStyles();
            SetMessage($"read: {row.AddressLabel} = {row.HexText}");
        }
        catch (Exception exception)
        {
            SetMessage($"read failed: {exception.Message}", isError: true);
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task EditSelectedAsync()
    {
        var row = SelectedRow();
        if (row is null)
        {
            SetMessage("select a row first", isError: true);
            return;
        }

        await EditRowAsync(row);
    }

    private async Task EditRowAsync(MonitorRow row)
    {
        if (_client is null)
        {
            SetMessage("not connected", isError: true);
            return;
        }

        using var dialog = new ValueEditDialog(
            row.AddressLabel,
            row.Unit,
            row.LastValue,
            isPackedBitWord: row.Device.EndsWith("W", StringComparison.OrdinalIgnoreCase));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        await WriteRowAsync(row, dialog.Value);
    }

    private async Task WriteRowAsync(MonitorRow row, object value)
    {
        var client = _client;
        if (client is null)
        {
            SetMessage("not connected", isError: true);
            return;
        }

        if (!await BeginBusyAsync())
        {
            return;
        }

        try
        {
            var settings = ReadConnectionSettings();
            var expectedText = FormatValue(row.Unit, value);
            await Task.Run(
                () =>
                {
                    if (settings.Hops is null)
                    {
                        client.Write(row.Device, value);
                    }
                    else
                    {
                        client.RelayWrite(settings.Hops, row.Device, value);
                    }
                });

            SetMessage($"write: {row.AddressLabel} <= {expectedText}");
            var verifyValue = await Task.Run(
                () => settings.Hops is null
                    ? client.Read(row.Device)
                    : client.RelayRead(settings.Hops, row.Device));
            UpdateRowValue(row, verifyValue, "manual");
            ApplyRowStyles();
            if (!ValuesEqual(value, verifyValue))
            {
                SetRowState(row, "error");
                ApplyRowStyles();
                SetMessage(
                    $"verify mismatch: {row.AddressLabel} wrote {expectedText}, read back {FormatValue(row.Unit, verifyValue)}",
                    isError: true);
                return;
            }

            SetMessage($"verify: {row.AddressLabel} = {FormatValue(row.Unit, verifyValue)}");
        }
        catch (Exception exception)
        {
            SetMessage($"write failed: {exception.Message}", isError: true);
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReadStatusAsync()
    {
        var client = _client;
        if (client is null)
        {
            var connected = await ConnectAsync(showErrorDialog: true, startMonitor: false, triggerInitialPoll: false);
            if (!connected)
            {
                return;
            }

            client = _client;
            if (client is null)
            {
                return;
            }
        }

        if (!await BeginBusyAsync())
        {
            return;
        }

        try
        {
            var settings = ReadConnectionSettings();
            var status = await Task.Run(
                () => settings.Hops is null
                    ? client.ReadCpuStatus()
                    : client.RelayReadCpuStatus(settings.Hops));
            ShowCpuStatusWindow(settings, status);
            SetMessage("cpu status window updated");
        }
        catch (Exception exception)
        {
            SetMessage($"status failed: {exception.Message}", isError: true);
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReadClockAsync()
    {
        var client = _client;
        if (client is null)
        {
            var connected = await ConnectAsync(showErrorDialog: true, startMonitor: false, triggerInitialPoll: false);
            if (!connected)
            {
                return;
            }

            client = _client;
            if (client is null)
            {
                return;
            }
        }

        if (!await BeginBusyAsync())
        {
            return;
        }

        try
        {
            var settings = ReadConnectionSettings();
            var clock = await Task.Run(
                () => settings.Hops is null
                    ? client.ReadClock()
                    : client.RelayReadClock(settings.Hops));
            ShowClockWindow(settings, clock);
            SetMessage("clock window updated");
        }
        catch (Exception exception)
        {
            SetMessage($"clock failed: {exception.Message}", isError: true);
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task WriteClockFromWindowAsync(DateTime value)
    {
        var client = _client;
        if (client is null)
        {
            throw new InvalidOperationException("not connected");
        }

        if (!await BeginBusyAsync())
        {
            throw new InvalidOperationException("device monitor is busy");
        }

        try
        {
            var settings = ReadConnectionSettings();
            await Task.Run(
                () =>
                {
                    if (settings.Hops is null)
                    {
                        client.WriteClock(value);
                    }
                    else
                    {
                        client.RelayWriteClock(settings.Hops, value);
                    }
                });

            var updatedClock = await Task.Run(
                () => settings.Hops is null
                    ? client.ReadClock()
                    : client.RelayReadClock(settings.Hops));
            ShowClockWindow(settings, updatedClock);
            SetMessage($"clock set: {updatedClock.AsDateTime():yyyy-MM-dd HH:mm:ss}");
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ReadClockFromWindowAsync()
    {
        var client = _client;
        if (client is null)
        {
            throw new InvalidOperationException("not connected");
        }

        if (!await BeginBusyAsync())
        {
            throw new InvalidOperationException("device monitor is busy");
        }

        try
        {
            var settings = ReadConnectionSettings();
            var clock = await Task.Run(
                () => settings.Hops is null
                    ? client.ReadClock()
                    : client.RelayReadClock(settings.Hops));
            ShowClockWindow(settings, clock);
            SetMessage("clock window updated");
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task ToggleMonitorAsync()
    {
        if (_client is null && !_monitorRunning)
        {
            await ConnectAsync(showErrorDialog: true, startMonitor: true);
            return;
        }

        _monitorRunning = !_monitorRunning;
        RefreshPollingState();
        RefreshMonitorButtonText();
        SetMessage(_monitorRunning ? "monitor started" : "monitor stopped");
        if (_monitorRunning && _client is not null && _rows.Count > 0)
        {
            _ = PollAllAsync(manual: true);
        }
    }

    private void RefreshPollingState()
    {
        try
        {
            var intervalMs = (int)(ReadIntervalSeconds() * 1000);
            _pollTimer.Interval = Math.Max(intervalMs, 200);
        }
        catch
        {
            _pollTimer.Interval = 1000;
        }

        _pollTimer.Enabled = _monitorRunning;
    }

    private void RefreshMonitorButtonText()
    {
        _monitorButton.Text = _monitorRunning ? "Monitor Stop" : "Monitor Start";
    }

    private DeviceMonitorSelectionRequest BuildSelectionRequest()
    {
        return DeviceMonitorCatalogHelper.BuildSelectionRequest(
            _profileComboBox.Text,
            _programComboBox.Text,
            _deviceComboBox.Text,
            _startAddressComboBox.Text);
    }

    private void InitializeLoadedRange(DeviceMonitorSelectionRequest request)
    {
        _loadedPrefix = request.Prefix;
        _loadedArea = request.Area;
        _loadedAddressWidth = request.Width;
        _loadedSuffix = request.Suffix;
        _loadedRanges = request.Ranges;
        _loadedStep = request.Step;
        _windowOffset = request.InitialOffset;
        _canShiftForward = true;
        _loadingMoreRows = false;
    }

    private bool ReloadSelectionWindow(int windowOffset, int? firstDisplayedRowIndex = null)
    {
        if (string.IsNullOrWhiteSpace(_loadedArea) || windowOffset < 0)
        {
            return false;
        }

        var existingRows = _rows.ToDictionary(static row => row.Device, StringComparer.Ordinal);
        var selectedDevice = SelectedRow()?.Device;
        var windowRows = BuildWindowRows(windowOffset, existingRows);
        if (windowRows.Count == 0)
        {
            return false;
        }

        _loadingMoreRows = true;
        _grid.SuspendLayout();
        try
        {
            _rows.Clear();
            foreach (var row in windowRows)
            {
                _rows.Add(row);
            }
        }
        finally
        {
            _grid.ResumeLayout();
            _loadingMoreRows = false;
        }

        _windowOffset = windowOffset;
        _canShiftForward = windowRows.Count == InitialLoadedRowCount;
        if (TryGetLoadedIndex(windowOffset, out var windowStartIndex))
        {
            SetStartAddressText(windowStartIndex);
        }

        _rowStylesDirty = true;
        ApplyRowStyles();
        RestoreSelection(selectedDevice);
        RefreshSelectionStatus();

        if (firstDisplayedRowIndex is not null)
        {
            BeginInvoke(() => SetFirstDisplayedRowIndexSafe(firstDisplayedRowIndex.Value));
        }
        else
        {
            BeginInvoke(() => SetFirstDisplayedRowIndexSafe(0));
        }

        return true;
    }

    private List<MonitorRow> BuildWindowRows(int windowOffset, IReadOnlyDictionary<string, MonitorRow> existingRows)
    {
        var rows = new List<MonitorRow>(InitialLoadedRowCount);
        for (var i = 0; i < InitialLoadedRowCount; i++)
        {
            if (!TryGetLoadedIndex(windowOffset + i, out var index))
            {
                break;
            }

            var device = $"{_loadedPrefix}{_loadedArea}{index.ToString($"X{_loadedAddressWidth}", CultureInfo.InvariantCulture)}{_loadedSuffix}";
            try
            {
                rows.Add(CreateOrReuseRow(device, existingRows));
            }
            catch when (rows.Count > 0)
            {
                break;
            }
            catch
            {
                return [];
            }
        }

        return rows;
    }

    private bool TryGetLoadedIndex(int offset, out int index)
    {
        if (offset < 0)
        {
            index = 0;
            return false;
        }

        var remaining = offset;
        for (var i = 0; i < _loadedRanges.Count; i++)
        {
            var range = _loadedRanges[i];
            var length = checked(range.End - range.Start + 1);
            if (remaining < length)
            {
                index = checked(range.Start + (remaining * _loadedStep));
                return true;
            }

            remaining -= length;
        }

        index = 0;
        return false;
    }

    private MonitorRow CreateOrReuseRow(string device, IReadOnlyDictionary<string, MonitorRow> existingRows)
    {
        if (existingRows.TryGetValue(device, out var existing))
        {
            return existing;
        }

        var normalizedProfile = DeviceMonitorCatalogHelper.NormalizeProfile(_profileComboBox.Text);
        var options = ToyopucAddressingOptions.FromProfile(normalizedProfile);
        var resolved = ToyopucDeviceResolver.ResolveDevice(device, options, normalizedProfile);
        return new MonitorRow
        {
            AddressLabel = resolved.Text,
            Device = resolved.Text,
            Unit = resolved.Unit,
            State = "watching",
        };
    }

    private void RestoreSelection(string? selectedDevice)
    {
        if (string.IsNullOrWhiteSpace(selectedDevice))
        {
            return;
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (!string.Equals(_rows[i].Device, selectedDevice, StringComparison.Ordinal))
            {
                continue;
            }

            _grid.ClearSelection();
            _grid.Rows[i].Selected = true;
            _grid.CurrentCell = _grid.Rows[i].Cells[0];
            return;
        }
    }

    private static string FormatLoadedAddress(int index, int width)
    {
        return index.ToString($"X{width}", CultureInfo.InvariantCulture);
    }

    private void SetFirstDisplayedRowIndexSafe(int rowIndex)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var clamped = Math.Max(0, Math.Min(rowIndex, _rows.Count - 1));
        _grid.FirstDisplayedScrollingRowIndex = clamped;
    }

    private void RefreshAreaChoices()
    {
        var selection = DeviceMonitorCatalogHelper.GetAreaChoices(
            _profileComboBox.Text,
            _programComboBox.Text,
            _deviceComboBox.Text);
        _deviceComboBox.BeginUpdate();
        _deviceComboBox.Items.Clear();
        foreach (var area in selection.Areas)
        {
            _deviceComboBox.Items.Add(area);
        }
        _deviceComboBox.EndUpdate();
        if (selection.Areas.Count == 0)
        {
            _deviceComboBox.SelectedIndex = -1;
            _deviceComboBox.Text = string.Empty;
            return;
        }

        _deviceComboBox.SelectedItem = selection.Selected;
    }

    private void RefreshProgramChoices()
    {
        var previousSuppressSelectionSearch = _suppressSelectionSearch;
        var selection = DeviceMonitorCatalogHelper.GetProgramChoices(_profileComboBox.Text, _programComboBox.Text);

        _suppressSelectionSearch = true;
        _programComboBox.BeginUpdate();
        try
        {
            _programComboBox.Items.Clear();
            _programComboBox.Items.AddRange(selection.Choices.ToArray());
        }
        finally
        {
            _programComboBox.EndUpdate();
            _suppressSelectionSearch = previousSuppressSelectionSearch;
        }

        _programComboBox.SelectedItem = selection.Selected;
        if (_programComboBox.SelectedIndex < 0)
        {
            _programComboBox.SelectedIndex = 0;
        }

        _programComboBox.Enabled = selection.IsEnabled;
    }

    private void RefreshStartAddressChoices()
    {
        var area = _deviceComboBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(area))
        {
            _suppressSelectionSearch = true;
            _startAddressComboBox.BeginUpdate();
            try
            {
                _startAddressComboBox.Items.Clear();
                _startAddressComboBox.Text = string.Empty;
            }
            finally
            {
                _startAddressComboBox.EndUpdate();
                _suppressSelectionSearch = false;
            }

            return;
        }

        var selection = DeviceMonitorCatalogHelper.GetStartAddressChoices(
            _profileComboBox.Text,
            _programComboBox.Text,
            area,
            _startAddressComboBox.Text);

        _suppressSelectionSearch = true;
        _startAddressComboBox.BeginUpdate();
        try
        {
            _startAddressComboBox.Items.Clear();
            foreach (var candidate in selection.Candidates)
            {
                _startAddressComboBox.Items.Add(candidate);
            }
        }
        finally
        {
            _startAddressComboBox.EndUpdate();
            _suppressSelectionSearch = false;
        }

        _startAddressComboBox.Text = selection.Selected;
    }

    private void SetStartAddressText(int index)
    {
        _suppressSelectionSearch = true;
        _startAddressComboBox.Text = FormatLoadedAddress(index, _loadedAddressWidth);
        _suppressSelectionSearch = false;
    }

    private void ScheduleSelectionSearch()
    {
        if (_suppressSelectionSearch)
        {
            return;
        }

        BeginInvoke(async () => await SearchAsync(showErrorDialog: true));
    }

    private async Task RunStartupSelectionRestoreAsync()
    {
        if (!_startupSelectionPending)
        {
            return;
        }

        _startupSelectionPending = false;
        await SearchAsync(showErrorDialog: false);
    }

    private MonitorRow? SelectedRow()
    {
        if (_grid.CurrentRow?.DataBoundItem is MonitorRow row)
        {
            return row;
        }

        return null;
    }

    private void RefreshSelectionStatus()
    {
        var row = SelectedRow();
        _loadedRowsValueLabel.Text = _rows.Count.ToString(CultureInfo.InvariantCulture);
        _selectedLabel.Text = row is null ? $"Selected: - ({_rows.Count} rows)" : $"Selected: {row.AddressLabel} ({_rows.Count} rows)";
    }

    private void Grid_Scroll(object? sender, ScrollEventArgs e)
    {
        if (e.ScrollOrientation == ScrollOrientation.VerticalScroll)
        {
            ScheduleGridWindowRefresh();
        }
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || !TryGetBitIndexFromColumn(e.ColumnIndex, out var bitIndex))
        {
            return;
        }

        var selected = (e.State & DataGridViewElementStates.Selected) != 0;
        e.PaintBackground(e.CellBounds, selected);
        e.Paint(e.CellBounds, DataGridViewPaintParts.Border);

        if (_grid.Rows[e.RowIndex].DataBoundItem is not MonitorRow row
            || !TryGetNumericValue(row.LastValue, out var numericValue))
        {
            e.Handled = true;
            return;
        }

        var bitWidth = GetBitWidth(row.Unit);
        if (bitIndex >= bitWidth)
        {
            e.Handled = true;
            return;
        }

        var bitOn = ((numericValue >> bitIndex) & 0x01) != 0;
        var diameter = Math.Max(9, Math.Min(e.CellBounds.Width, e.CellBounds.Height) - 10);
        var glyph = GetBitGlyph(bitOn, selected, diameter);
        var left = e.CellBounds.Left + ((e.CellBounds.Width - glyph.Width) / 2);
        var top = e.CellBounds.Top + ((e.CellBounds.Height - glyph.Height) / 2);
        var graphics = e.Graphics;
        if (graphics is not null)
        {
            graphics.DrawImageUnscaled(glyph, left, top);
        }

        e.Handled = true;
    }

    private async void Grid_CellMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_grid.Rows[e.RowIndex].DataBoundItem is not MonitorRow row)
        {
            return;
        }

        if (TryGetBitIndexFromColumn(e.ColumnIndex, out var bitIndex))
        {
            await ToggleRowBitAsync(row, bitIndex);
            return;
        }

        await EditRowAsync(row);
    }

    private async Task ToggleRowBitAsync(MonitorRow row, int bitIndex)
    {
        if (!TryGetNumericValue(row.LastValue, out var numericValue))
        {
            SetMessage($"toggle failed: {row.AddressLabel} has no value yet", isError: true);
            return;
        }

        var bitWidth = GetBitWidth(row.Unit);
        if (bitIndex >= bitWidth)
        {
            return;
        }

        var toggledValue = numericValue ^ (1 << bitIndex);
        object writeValue = row.Unit switch
        {
            "bit" => (toggledValue & 0x01) != 0,
            "byte" => (byte)(toggledValue & 0xFF),
            _ => toggledValue & 0xFFFF,
        };

        await WriteRowAsync(row, writeValue);
    }

    private void ScheduleGridWindowRefresh()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        _scrollingUi = true;
        _scrollSettleTimer.Stop();
        _scrollSettleTimer.Start();
        _pollTimer.Stop();
    }

    private bool TryShiftGridWindow()
    {
        if (_busy || _loadingMoreRows || _rows.Count == 0)
        {
            return false;
        }

        var firstVisibleIndex = _grid.FirstDisplayedScrollingRowIndex;
        if (firstVisibleIndex < 0)
        {
            return false;
        }

        var visibleRows = _grid.DisplayedRowCount(false);
        if (visibleRows <= 0)
        {
            return false;
        }

        var lastVisibleIndex = firstVisibleIndex + visibleRows;

        if (_windowOffset > 0 && firstVisibleIndex <= 2)
        {
            var shift = Math.Min(ScrollShiftRowCount, _windowOffset);
            var targetOffset = _windowOffset - shift;
            var targetFirstRow = firstVisibleIndex + shift;
            if (ReloadSelectionWindow(targetOffset, targetFirstRow))
            {
                return true;
            }

            return false;
        }

        if (!_canShiftForward || lastVisibleIndex < _rows.Count - 4)
        {
            return false;
        }

        var targetWindowOffset = _windowOffset + ScrollShiftRowCount;
        var nextFirstRow = Math.Max(0, firstVisibleIndex - ScrollShiftRowCount);
        if (ReloadSelectionWindow(targetWindowOffset, nextFirstRow))
        {
            return true;
        }

        _canShiftForward = false;
        return false;
    }


    private void UpdateRowValue(MonitorRow row, object value, string normalState)
    {
        var previous = row.LastValue;
        row.LastError = null;
        row.LastValue = value;
        var nextState = previous is not null && !ValuesEqual(previous, value) ? "changed" : normalState;
        SetRowState(row, nextState);

        var number = value switch
        {
            bool bit => bit ? 1 : 0,
            byte b => b,
            int word => word & 0xFFFF,
            _ => throw new ArgumentException($"Unsupported value type: {value.GetType().Name}", nameof(value)),
        };

        row.HexText = number.ToString("X4", CultureInfo.InvariantCulture);
        row.DecText = number.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyRowStyles()
    {
        if (!_rowStylesDirty)
        {
            return;
        }

        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.DataBoundItem is not MonitorRow row)
            {
                continue;
            }

            gridRow.DefaultCellStyle.BackColor = row.State switch
            {
                "changed" => Color.FromArgb(255, 248, 200),
                "error" => Color.FromArgb(255, 228, 228),
                "manual" => Color.FromArgb(232, 244, 255),
                "watching" => Color.FromArgb(244, 244, 244),
                _ => Color.White,
            };
        }

        _rowStylesDirty = false;
    }

    private static bool ValuesEqual(object left, object right)
    {
        return left switch
        {
            bool leftBit when right is bool rightBit => leftBit == rightBit,
            byte leftByte when right is byte rightByte => leftByte == rightByte,
            int leftWord when right is int rightWord => leftWord == rightWord,
            _ => Equals(left, right),
        };
    }

    private bool TryGetBitIndexFromColumn(int columnIndex, out int bitIndex)
    {
        bitIndex = 0;
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return false;
        }

        if (_grid.Columns[columnIndex].Tag is int bit and >= 0 and <= 15)
        {
            bitIndex = bit;
            return true;
        }

        return false;
    }

    private static bool TryGetNumericValue(object? value, out int numericValue)
    {
        switch (value)
        {
            case bool bit:
                numericValue = bit ? 1 : 0;
                return true;
            case byte valueByte:
                numericValue = valueByte;
                return true;
            case int valueWord:
                numericValue = valueWord & 0xFFFF;
                return true;
            default:
                numericValue = 0;
                return false;
        }
    }

    private static int GetBitWidth(string unit)
    {
        return unit switch
        {
            "bit" => 1,
            "byte" => 8,
            _ => 16,
        };
    }

    private void SetRowState(MonitorRow row, string state)
    {
        if (!string.Equals(row.State, state, StringComparison.Ordinal))
        {
            _rowStylesDirty = true;
            row.State = state;
            return;
        }

        row.State = state;
    }

    private void WithGridLayoutSuspended(Action action)
    {
        _grid.SuspendLayout();
        try
        {
            action();
        }
        finally
        {
            _grid.ResumeLayout(performLayout: false);
            _grid.Invalidate();
        }
    }

    private static bool IsConnectionFault(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            return aggregateException.Flatten().InnerExceptions.Any(IsConnectionFault);
        }

        if (exception is SocketException or IOException or ObjectDisposedException)
        {
            return true;
        }

        if (exception.InnerException is not null && IsConnectionFault(exception.InnerException))
        {
            return true;
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("socket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase)
            || message.Contains("network", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryAutoReconnectAsync()
    {
        if (!_monitorRunning || _autoReconnectInProgress)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now - _lastAutoReconnectAttemptUtc < AutoReconnectRetryInterval)
        {
            return;
        }

        _autoReconnectInProgress = true;
        _lastAutoReconnectAttemptUtc = now;

        try
        {
            DisconnectClient("Reconnecting...");
            SetMessage("connection lost; reconnecting...", isError: true);

            var connected = await ConnectAsync(showErrorDialog: false, startMonitor: true, triggerInitialPoll: false);
            if (connected)
            {
                SetMessage("reconnected; monitoring resumed");
                if (_rows.Count > 0)
                {
                    _ = PollAllAsync(manual: false);
                }

                return;
            }

            _monitorRunning = true;
            RefreshMonitorButtonText();
            RefreshPollingState();
            SetMessage("reconnect failed; retrying...", isError: true);
        }
        finally
        {
            _autoReconnectInProgress = false;
        }
    }

    private Bitmap GetBitGlyph(bool bitOn, bool selected, int diameter)
    {
        var key = (On: bitOn, Selected: selected, Diameter: diameter);
        if (_bitGlyphCache.TryGetValue(key, out var glyph))
        {
            return glyph;
        }

        glyph = new Bitmap(diameter, diameter);
        using (var graphics = Graphics.FromImage(glyph))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var circleRect = new Rectangle(0, 0, diameter - 1, diameter - 1);
            var onColor = selected ? Color.LightGreen : Color.ForestGreen;
            var offBorderColor = selected ? Color.WhiteSmoke : Color.DimGray;
            var borderColor = bitOn ? onColor : offBorderColor;

            if (bitOn)
            {
                using var fillBrush = new SolidBrush(onColor);
                graphics.FillEllipse(fillBrush, circleRect);
            }

            using var borderPen = new Pen(borderColor, 1f);
            graphics.DrawEllipse(borderPen, circleRect);
        }

        _bitGlyphCache[key] = glyph;
        return glyph;
    }

    private void ClearBitGlyphCache()
    {
        foreach (var glyph in _bitGlyphCache.Values)
        {
            glyph.Dispose();
        }

        _bitGlyphCache.Clear();
    }

    private static void EnableDoubleBuffer(DataGridView grid)
    {
        typeof(DataGridView)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(grid, true);
    }

    private ConnectionSettings ReadConnectionSettings()
    {
        var host = _hostTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("host is required");
        }

        var port = ParseInteger(_portTextBox.Text.Trim());
        var localPort = ParseInteger(_localPortTextBox.Text.Trim());
        var retries = ParseInteger(_retriesTextBox.Text.Trim());
        var timeout = double.Parse(_timeoutTextBox.Text.Trim(), CultureInfo.InvariantCulture);
        if (port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "port must be greater than zero");
        }

        return new ConnectionSettings(
            host,
            port,
            _transportComboBox.Text.Trim().ToLowerInvariant(),
            localPort,
            timeout,
            retries,
            string.IsNullOrWhiteSpace(_hopsTextBox.Text) ? null : _hopsTextBox.Text.Trim(),
            DeviceMonitorCatalogHelper.NormalizeProfile(_profileComboBox.Text));
    }

    private static ToyopucDeviceClient CreateClient(ConnectionSettings settings)
    {
        return new ToyopucDeviceClient(
            settings.Host,
            settings.Port,
            localPort: settings.LocalPort,
            transport: Enum.Parse<ToyopucTransportMode>(settings.Transport, ignoreCase: true),
            timeout: TimeSpan.FromSeconds(settings.Timeout),
            retries: settings.Retries,
            addressingOptions: ToyopucAddressingOptions.FromProfile(settings.Profile),
            deviceProfile: settings.Profile);
    }

    private void DisconnectClient(string? statusText)
    {
        _pollTimer.Stop();
        _client?.Dispose();
        _client = null;
        _monitorRunning = false;
        RefreshMonitorButtonText();
        RefreshPollingState();
        if (statusText is not null)
        {
            UpdateConnectionStatus(statusText);
        }
    }

    private void OpenConnectionSettings()
    {
        using var dialog = new ConnectionSettingsDialog(BuildCurrentSettingsDraft());

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var draft = dialog.Draft;
        ApplyConnectionSettingsDraft(draft);

        RefreshEndpointLabel();
        RefreshPollingState();
        SetMessage(_client is null
            ? "connection settings updated"
            : "connection settings updated; reconnect to apply transport changes");
    }

    private void LoadPersistedConnectionSettings()
    {
        try
        {
            var draft = ConnectionSettingsStore.Load();
            if (draft is null)
            {
                return;
            }

            _restoreSelectionOnStartup = ApplyConnectionSettingsDraft(draft);
        }
        catch
        {
            _restoreSelectionOnStartup = false;
            ConnectionSettingsStore.Delete();
            // Keep built-in defaults when the settings file is missing or invalid.
        }
    }

    private bool ApplyConnectionSettingsDraft(ConnectionSettingsDraft draft)
    {
        var previousSuppressSelectionSearch = _suppressSelectionSearch;
        _suppressSelectionSearch = true;
        var hasSavedProgram = !string.IsNullOrWhiteSpace(draft.Program);
        var hasSavedDevice = !string.IsNullOrWhiteSpace(draft.Device);
        var hasSavedStart = !string.IsNullOrWhiteSpace(draft.StartAddress);

        _hostTextBox.Text = draft.Host;
        _portTextBox.Text = draft.Port;
        _transportComboBox.SelectedItem = draft.Transport;
        if (_transportComboBox.SelectedIndex < 0)
        {
            _transportComboBox.Text = draft.Transport;
        }

        var normalizedProfile = DeviceMonitorCatalogHelper.NormalizeProfile(draft.Profile);
        _profileComboBox.SelectedItem = normalizedProfile;
        if (_profileComboBox.SelectedIndex < 0)
        {
            _profileComboBox.Text = normalizedProfile;
        }

        _intervalTextBox.Text = draft.Interval;
        _localPortTextBox.Text = draft.LocalPort;
        _timeoutTextBox.Text = draft.Timeout;
        _retriesTextBox.Text = draft.Retries;
        _hopsTextBox.Text = draft.Hops;
        RefreshProgramChoices();
        if (hasSavedProgram && !TryRestoreProgramChoice(draft.Program))
        {
            draft = draft with { Program = string.Empty, Device = string.Empty, StartAddress = string.Empty };
            hasSavedDevice = false;
            hasSavedStart = false;
        }

        RefreshAreaChoices();
        if (hasSavedDevice && !TryRestoreAreaChoice(draft.Device))
        {
            _deviceComboBox.SelectedIndex = -1;
            _deviceComboBox.Text = string.Empty;
            _startAddressComboBox.Text = string.Empty;
            _suppressSelectionSearch = previousSuppressSelectionSearch;
            return false;
        }

        RefreshStartAddressChoices();
        if (!hasSavedStart)
        {
            _suppressSelectionSearch = previousSuppressSelectionSearch;
            return false;
        }

        var restoredStart = TryRestoreStartAddressChoice(draft.StartAddress);
        if (!restoredStart)
        {
            _startAddressComboBox.Text = string.Empty;
            _suppressSelectionSearch = previousSuppressSelectionSearch;
            return false;
        }

        _suppressSelectionSearch = previousSuppressSelectionSearch;
        return true;
    }

    private bool TryRestoreProgramChoice(string? program)
    {
        if (string.IsNullOrWhiteSpace(program))
        {
            return false;
        }

        var normalized = program.Trim().ToUpperInvariant();
        foreach (var item in _programComboBox.Items)
        {
            if (!string.Equals(item?.ToString(), normalized, StringComparison.Ordinal))
            {
                continue;
            }

            _programComboBox.SelectedItem = item;
            return true;
        }

        return false;
    }

    private bool TryRestoreAreaChoice(string? area)
    {
        if (string.IsNullOrWhiteSpace(area))
        {
            return false;
        }

        var normalized = area.Trim().ToUpperInvariant();
        foreach (var item in _deviceComboBox.Items)
        {
            if (!string.Equals(item?.ToString(), normalized, StringComparison.Ordinal))
            {
                continue;
            }

            _deviceComboBox.SelectedItem = item;
            return true;
        }

        return false;
    }

    private bool TryRestoreStartAddressChoice(string? startAddress)
    {
        if (string.IsNullOrWhiteSpace(startAddress))
        {
            return false;
        }

        var area = _deviceComboBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(area))
        {
            return false;
        }

        DeviceMonitorStartAddressChoices startChoices;
        try
        {
            startChoices = DeviceMonitorCatalogHelper.GetStartAddressChoices(
                _profileComboBox.Text,
                _programComboBox.Text,
                area,
                startAddress);
        }
        catch
        {
            return false;
        }

        var normalized = DeviceMonitorCatalogHelper.TryNormalizeHexText(startAddress, startChoices.Width);
        if (normalized is null || !startChoices.Candidates.Contains(normalized, StringComparer.Ordinal))
        {
            return false;
        }

        _startAddressComboBox.Text = normalized;
        return true;
    }

    private ConnectionSettingsDraft BuildCurrentSettingsDraft()
    {
        return new ConnectionSettingsDraft(
            _hostTextBox.Text.Trim(),
            _portTextBox.Text.Trim(),
            _transportComboBox.Text.Trim(),
            DeviceMonitorCatalogHelper.NormalizeProfile(_profileComboBox.Text),
            _intervalTextBox.Text.Trim(),
            _localPortTextBox.Text.Trim(),
            _timeoutTextBox.Text.Trim(),
            _retriesTextBox.Text.Trim(),
            _hopsTextBox.Text.Trim(),
            _programComboBox.Text.Trim(),
            _deviceComboBox.Text.Trim().ToUpperInvariant(),
            _startAddressComboBox.Text.Trim().ToUpperInvariant());
    }

    private void SaveCurrentSettingsDraft()
    {
        try
        {
            ConnectionSettingsStore.Save(BuildCurrentSettingsDraft());
        }
        catch
        {
            // Ignore persistence failures during runtime operations.
        }
    }

    private void UpdateConnectionStatus(string text)
    {
        _connectionStatusLabel.Text = text;
        _connectionStateLabel.Text = text;
        _connectionStateLabel.ForeColor = string.Equals(text, "Connected", StringComparison.OrdinalIgnoreCase)
            ? Color.SeaGreen
            : SystemColors.ControlText;
    }

    private double ReadIntervalSeconds()
    {
        var interval = double.Parse(_intervalTextBox.Text.Trim(), CultureInfo.InvariantCulture);
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "interval must be greater than zero");
        }

        return interval;
    }

    private void RefreshEndpointLabel()
    {
        try
        {
            RefreshEndpointLabel(ReadConnectionSettings());
        }
        catch
        {
            _endpointLabel.Text = "Endpoint: <configure connection>";
        }
    }

    private void RefreshEndpointLabel(ConnectionSettings settings)
    {
        var relay = settings.Hops is null ? string.Empty : $" via {settings.Hops}";
        _endpointLabel.Text = $"Endpoint: {settings.Transport}://{settings.Host}:{settings.Port} [{settings.Profile}]{relay}";
        _connectionSummaryLabel.Text =
            $"{settings.Transport}://{settings.Host}:{settings.Port}  [{settings.Profile}]  interval={_intervalTextBox.Text}s{relay}";
    }

    private async Task<bool> BeginBusyAsync(bool skipUiDisable = false)
    {
        if (_busy)
        {
            return false;
        }

        _busy = true;
        if (!skipUiDisable)
        {
            SetButtonsEnabled(false);
        }

        await Task.Yield();
        return true;
    }

    private void EndBusy(bool skipUiEnable = false)
    {
        _busy = false;
        if (!skipUiEnable)
        {
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _connectMenuItem.Enabled = enabled;
        _disconnectMenuItem.Enabled = enabled;
        _programComboBox.Enabled = enabled && _programComboBox.Items.Count > 1;
        _deviceComboBox.Enabled = enabled;
        _startAddressComboBox.Enabled = enabled;
        _monitorButton.Enabled = enabled;
        _pollNowButton.Enabled = enabled;
        _cpuStatusMenuItem.Enabled = enabled;
        _clockMenuItem.Enabled = enabled;
    }

    private void SetMessage(string message, bool isError = false)
    {
        _messageLabel.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
        _messageLabel.Text = message;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveCurrentSettingsDraft();
        _clockWindow?.Close();
        _cpuStatusWindow?.Close();
        _versionWindow?.Close();
        ClearBitGlyphCache();
        base.OnFormClosed(e);
    }

    private static Control Sized(Control control, int width)
    {
        control.MinimumSize = new Size(width, control.MinimumSize.Height);
        return control;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 8, 0, 0),
        };
    }

    private static Panel CreateSeparator()
    {
        return new Panel { Width = 150, Height = 8 };
    }

    private static Control BuildActionSection(string title, params Control[] controls)
    {
        var group = new GroupBox
        {
            Text = title,
            AutoSize = true,
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 8),
        };

        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };
        panel.Controls.AddRange(controls);
        group.Controls.Add(panel);
        return group;
    }

    private static void AddLabeledControl(TableLayoutPanel layout, int column, int row, string label, Control control, int width = 0)
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

    private static int ParseInteger(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string FormatValue(string unit, object value)
    {
        return unit switch
        {
            "bit" when value is bool bit => bit ? "1" : "0",
            "byte" when value is byte b => $"0x{b:X2}",
            _ when value is int word => $"0x{word & 0xFFFF:X4}",
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string SummarizeStatus(CpuStatusData status)
    {
        var flags = new List<string> { status.Run ? "RUN" : "STOP", status.Pc10Mode ? "PC10" : "PC3" };
        if (status.Alarm)
        {
            flags.Add("ALARM");
        }

        if (status.FatalFailure)
        {
            flags.Add("FATAL");
        }

        return string.Join(" / ", flags);
    }

    private void ShowCpuStatusWindow(ConnectionSettings settings, CpuStatusData status)
    {
        var window = GetOrCreateDetailWindow(ref _cpuStatusWindow, "CPU Status", horizontalOffset: 40);
        window.SetActions(_cpuStatusRefreshButton);
        window.UpdateContent(
            SummarizeStatus(status),
            BuildWindowEndpoint(settings),
            BuildCpuStatusRows(status));
        ActivateDetailWindow(window);
    }

    private void ShowClockWindow(ConnectionSettings settings, ClockData clock)
    {
        var dateTime = clock.AsDateTime();
        var window = GetOrCreateClockWindow();
        window.UpdateContent(
            $"Clock: {dateTime:yyyy-MM-dd HH:mm:ss}",
            BuildWindowEndpoint(settings),
            dateTime,
            BuildClockRows(clock));
        ActivateClockWindow(window);
    }

    private void ShowVersionWindow()
    {
        var appAssembly = typeof(DeviceMonitorForm).Assembly;
        var libraryAssembly = typeof(ToyopucDeviceClient).Assembly;
        var window = GetOrCreateDetailWindow(ref _versionWindow, "Version", horizontalOffset: 180);
        window.SetActions();
        window.UpdateContent(
            "Version Information",
            $"{Environment.OSVersion} / .NET {Environment.Version}",
            BuildVersionRows(appAssembly, libraryAssembly));
        ActivateDetailWindow(window);
    }

    private DetailViewerForm GetOrCreateDetailWindow(ref DetailViewerForm? window, string title, int horizontalOffset)
    {
        if (window is null || window.IsDisposed)
        {
            window = new DetailViewerForm(title)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(Right + horizontalOffset, Top + 40),
            };
        }

        return window;
    }

    private ClockViewerForm GetOrCreateClockWindow()
    {
        if (_clockWindow is null || _clockWindow.IsDisposed)
        {
            _clockWindow = new ClockViewerForm
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(Right + 110, Top + 40),
                ReadRequestedAsync = ReadClockFromWindowAsync,
                WriteRequestedAsync = WriteClockFromWindowAsync,
            };
        }

        return _clockWindow;
    }

    private void ActivateDetailWindow(DetailViewerForm window)
    {
        if (!window.Visible)
        {
            window.Show(this);
            return;
        }

        window.BringToFront();
    }

    private void ActivateClockWindow(ClockViewerForm window)
    {
        if (!window.Visible)
        {
            window.Show(this);
            return;
        }

        window.BringToFront();
    }

    private static string BuildWindowEndpoint(ConnectionSettings settings)
    {
        var relay = settings.Hops is null ? string.Empty : $" via {settings.Hops}";
        return $"{settings.Transport}://{settings.Host}:{settings.Port} [{settings.Profile}]{relay}";
    }

    private static IEnumerable<DetailViewerForm.DetailRow> BuildVersionRows(Assembly appAssembly, Assembly libraryAssembly)
    {
        return
        [
            Detail("Author", "fa-yoshinobu"),
            Detail("GitHub", "https://github.com/fa-yoshinobu/toyopuc-computer-link-dotnet"),
            Detail("License", "MIT"),
            Detail("Application", appAssembly.GetName().Name ?? "Toyopuc.DeviceMonitor"),
            Detail("App Assembly Version", appAssembly.GetName().Version?.ToString() ?? "-"),
            Detail("App Informational Version", appAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "-"),
            Detail("Library", libraryAssembly.GetName().Name ?? "Toyopuc"),
            Detail("Library Assembly Version", libraryAssembly.GetName().Version?.ToString() ?? "-"),
            Detail("Library Informational Version", libraryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "-"),
            Detail(".NET Runtime", Environment.Version.ToString()),
            Detail("Process Architecture", RuntimeInformation.ProcessArchitecture.ToString()),
            Detail("OS Architecture", RuntimeInformation.OSArchitecture.ToString()),
        ];
    }

    private static IEnumerable<ClockViewerForm.ClockRow> BuildClockRows(ClockData clock)
    {
        return
        [
            ClockDetail("Year", (2000 + clock.Year2Digit).ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Month", clock.Month.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Day", clock.Day.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Hour", clock.Hour.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Minute", clock.Minute.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Second", clock.Second.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("Weekday", clock.Weekday.ToString(CultureInfo.InvariantCulture)),
            ClockDetail("WeekdayName", WeekdayName(clock.Weekday)),
        ];
    }

    private static IEnumerable<DetailViewerForm.DetailRow> BuildCpuStatusRows(CpuStatusData status)
    {
        return
        [
            Detail("Run", BoolText(status.Run)),
            Detail("UnderStop", BoolText(status.UnderStop)),
            Detail("UnderStopRequestContinuity", BoolText(status.UnderStopRequestContinuity)),
            Detail("UnderPseudoStop", BoolText(status.UnderPseudoStop)),
            Detail("DebugMode", BoolText(status.DebugMode)),
            Detail("IoMonitorUserMode", BoolText(status.IoMonitorUserMode)),
            Detail("Pc3Mode", BoolText(status.Pc3Mode)),
            Detail("Pc10Mode", BoolText(status.Pc10Mode)),
            Detail("FatalFailure", BoolText(status.FatalFailure)),
            Detail("FaintFailure", BoolText(status.FaintFailure)),
            Detail("Alarm", BoolText(status.Alarm)),
            Detail("IoAllocationParameterAltered", BoolText(status.IoAllocationParameterAltered)),
            Detail("WithMemoryCard", BoolText(status.WithMemoryCard)),
            Detail("MemoryCardOperation", BoolText(status.MemoryCardOperation)),
            Detail("WriteProtectedProgramInfo", BoolText(status.WriteProtectedProgramInfo)),
            Detail("ReadProtectedSystemMemory", BoolText(status.ReadProtectedSystemMemory)),
            Detail("WriteProtectedSystemMemory", BoolText(status.WriteProtectedSystemMemory)),
            Detail("ReadProtectedSystemIo", BoolText(status.ReadProtectedSystemIo)),
            Detail("WriteProtectedSystemIo", BoolText(status.WriteProtectedSystemIo)),
            Detail("Trace", BoolText(status.Trace)),
            Detail("ScanSamplingTrace", BoolText(status.ScanSamplingTrace)),
            Detail("PeriodicSamplingTrace", BoolText(status.PeriodicSamplingTrace)),
            Detail("EnableDetected", BoolText(status.EnableDetected)),
            Detail("TriggerDetected", BoolText(status.TriggerDetected)),
            Detail("OneScanStep", BoolText(status.OneScanStep)),
            Detail("OneBlockStep", BoolText(status.OneBlockStep)),
            Detail("OneInstructionStep", BoolText(status.OneInstructionStep)),
            Detail("IoOffline", BoolText(status.IoOffline)),
            Detail("RemoteRunSetting", BoolText(status.RemoteRunSetting)),
            Detail("StatusLatchSetting", BoolText(status.StatusLatchSetting)),
            Detail("WritePriorityLimitedProgramInfo", BoolText(status.WritePriorityLimitedProgramInfo)),
            Detail("AbnormalWriteFlashRegister", BoolText(status.AbnormalWriteFlashRegister)),
            Detail("UnderWritingFlashRegister", BoolText(status.UnderWritingFlashRegister)),
            Detail("AbnormalWriteEquipmentInfo", BoolText(status.AbnormalWriteEquipmentInfo)),
            Detail("AbnormalWritingEquipmentInfo", BoolText(status.AbnormalWritingEquipmentInfo)),
            Detail("AbnormalWriteDuringRun", BoolText(status.AbnormalWriteDuringRun)),
            Detail("UnderWritingDuringRun", BoolText(status.UnderWritingDuringRun)),
            Detail("Program3Running", BoolText(status.Program3Running)),
            Detail("Program2Running", BoolText(status.Program2Running)),
            Detail("Program1Running", BoolText(status.Program1Running)),
        ];
    }

    private static DetailViewerForm.DetailRow Detail(string name, string value)
    {
        return new DetailViewerForm.DetailRow { Name = name, Value = value };
    }

    private static ClockViewerForm.ClockRow ClockDetail(string name, string value)
    {
        return new ClockViewerForm.ClockRow { Name = name, Value = value };
    }

    private static string BoolText(bool value)
    {
        return value ? "ON" : "OFF";
    }

    private static string WeekdayName(int weekday)
    {
        return weekday switch
        {
            0 => DayOfWeek.Sunday.ToString(),
            1 => DayOfWeek.Monday.ToString(),
            2 => DayOfWeek.Tuesday.ToString(),
            3 => DayOfWeek.Wednesday.ToString(),
            4 => DayOfWeek.Thursday.ToString(),
            5 => DayOfWeek.Friday.ToString(),
            6 => DayOfWeek.Saturday.ToString(),
            _ => "Unknown",
        };
    }

    private readonly record struct ConnectionSettings(
        string Host,
        int Port,
        string Transport,
        int LocalPort,
        double Timeout,
        int Retries,
        string? Hops,
        string Profile);

    private sealed class MonitorRow : INotifyPropertyChanged
    {
        private string _addressLabel = string.Empty;
        private string _bitsHigh = string.Empty;
        private string _bitsLow = string.Empty;
        private string _hexText = string.Empty;
        private string _decText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Device { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string State { get; set; } = "watching";
        public object? LastValue { get; set; }
        public string? LastError { get; set; }

        public string AddressLabel
        {
            get => _addressLabel;
            set => SetField(ref _addressLabel, value);
        }

        public string BitsHigh
        {
            get => _bitsHigh;
            set => SetField(ref _bitsHigh, value);
        }

        public string BitsLow
        {
            get => _bitsLow;
            set => SetField(ref _bitsLow, value);
        }

        public string HexText
        {
            get => _hexText;
            set => SetField(ref _hexText, value);
        }

        public string DecText
        {
            get => _decText;
            set => SetField(ref _decText, value);
        }

        private void SetField(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

internal static class DeviceMonitorBinaryExtensions
{
    public static string ToBinaryString(this int value)
    {
        return Convert.ToString(value & 0xFF, 2)?.PadLeft(8, '0') ?? "00000000";
    }
}
