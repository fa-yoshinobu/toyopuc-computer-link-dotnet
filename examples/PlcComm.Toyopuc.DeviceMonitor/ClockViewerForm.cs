using System.ComponentModel;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal sealed class ClockViewerForm : Form
{
    private readonly Label _summaryLabel = new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        Margin = new Padding(0, 0, 0, 6),
    };

    private readonly Label _endpointLabel = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 4),
    };

    private readonly Label _updatedLabel = new()
    {
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly BindingList<ClockRow> _rows = [];
    private readonly DataGridView _grid = new();
    private readonly DateTimePicker _dateTimePicker = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd HH:mm:ss",
        Width = 240,
    };
    private readonly Button _readButton = new() { Text = "Read Clock", AutoSize = true };
    private readonly Button _usePcTimeButton = new() { Text = "Use PC Time", AutoSize = true };
    private readonly Button _writeButton = new() { Text = "Write To PLC", AutoSize = true };

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<Task>? ReadRequestedAsync { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<DateTime, Task>? WriteRequestedAsync { get; set; }

    public ClockViewerForm()
    {
        Text = "Clock";
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScroll = true;
        Size = new Size(560, 840);
        MinimumSize = new Size(480, 630);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(_summaryLabel, 0, 0);
        root.Controls.Add(_endpointLabel, 0, 1);
        root.Controls.Add(_updatedLabel, 0, 2);
        root.Controls.Add(BuildSetPanel(), 0, 3);
        ConfigureGrid();
        root.Controls.Add(_grid, 0, 4);

        _readButton.Click += async (_, _) => await ReadClockAsync();
        _usePcTimeButton.Click += (_, _) => _dateTimePicker.Value = DateTime.Now;
        _writeButton.Click += async (_, _) => await WriteClockAsync();
        Resize += (_, _) => UpdateHeaderWrapWidth();
        UpdateHeaderWrapWidth();
    }

    public void UpdateContent(string summary, string endpoint, DateTime plcTime, IEnumerable<ClockRow> rows)
    {
        _summaryLabel.Text = summary;
        _endpointLabel.Text = endpoint;
        _updatedLabel.Text = $"Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        _dateTimePicker.Value = plcTime;

        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        foreach (var row in rows)
        {
            _rows.Add(row);
        }

        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
    }

    private Control BuildSetPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
        };

        var valueLine = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 4),
        };
        valueLine.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Set time",
            Padding = new Padding(0, 8, 4, 0),
        });
        valueLine.Controls.Add(_dateTimePicker);

        var buttonLine = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Top,
            Margin = new Padding(0),
        };
        buttonLine.Controls.Add(_readButton);
        buttonLine.Controls.Add(_usePcTimeButton);
        buttonLine.Controls.Add(_writeButton);

        panel.Controls.Add(valueLine, 0, 0);
        panel.Controls.Add(buttonLine, 0, 1);
        return panel;
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
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.BackgroundColor = Color.White;
        _grid.Font = new Font("Consolas", 10f, FontStyle.Regular);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ClockRow.Name),
            HeaderText = "Item",
            FillWeight = 45,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(ClockRow.Value),
            HeaderText = "Value",
            FillWeight = 55,
        });
    }

    private async Task WriteClockAsync()
    {
        var handler = WriteRequestedAsync;
        if (handler is null)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            await handler(_dateTimePicker.Value);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Set Clock", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async Task ReadClockAsync()
    {
        var handler = ReadRequestedAsync;
        if (handler is null)
        {
            return;
        }

        SetButtonsEnabled(false);
        try
        {
            await handler();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Read Clock", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _readButton.Enabled = enabled;
        _usePcTimeButton.Enabled = enabled;
        _writeButton.Enabled = enabled;
    }

    private void UpdateHeaderWrapWidth()
    {
        var maxWidth = Math.Max(200, ClientSize.Width - 40);
        _summaryLabel.MaximumSize = new Size(maxWidth, 0);
        _endpointLabel.MaximumSize = new Size(maxWidth, 0);
        _updatedLabel.MaximumSize = new Size(maxWidth, 0);
    }

    internal sealed class ClockRow
    {
        public required string Name { get; init; }
        public required string Value { get; init; }
    }
}
