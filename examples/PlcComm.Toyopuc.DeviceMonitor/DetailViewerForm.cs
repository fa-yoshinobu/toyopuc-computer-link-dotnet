using System.ComponentModel;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal sealed class DetailViewerForm : Form
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

    private readonly FlowLayoutPanel _actionPanel = new()
    {
        AutoSize = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Dock = DockStyle.Top,
        Margin = new Padding(0, 0, 0, 8),
        Visible = false,
    };

    private readonly BindingList<DetailRow> _rows = [];
    private readonly DataGridView _grid = new();

    public DetailViewerForm(string title)
    {
        Text = title;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScroll = true;
        Size = new Size(760, 1140);
        MinimumSize = new Size(620, 630);

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
        root.Controls.Add(_actionPanel, 0, 3);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 4);

        Resize += (_, _) => UpdateHeaderWrapWidth();
        UpdateHeaderWrapWidth();
    }

    public void UpdateContent(string summary, string endpoint, IEnumerable<DetailRow> rows)
    {
        _summaryLabel.Text = summary;
        _endpointLabel.Text = endpoint;
        _updatedLabel.Text = $"Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        _rows.RaiseListChangedEvents = false;
        _rows.Clear();
        foreach (var row in rows)
        {
            _rows.Add(row);
        }

        _rows.RaiseListChangedEvents = true;
        _rows.ResetBindings();
        AutoResizeColumns();
    }

    public void SetActions(params Control[] controls)
    {
        _actionPanel.Controls.Clear();
        if (controls.Length == 0)
        {
            _actionPanel.Visible = false;
            return;
        }

        _actionPanel.Controls.AddRange(controls);
        _actionPanel.Visible = true;
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
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DetailRow.Name),
            HeaderText = "Item",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DetailRow.Value),
            HeaderText = "Value",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
        });
    }

    private void AutoResizeColumns()
    {
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        }
    }

    private void UpdateHeaderWrapWidth()
    {
        var maxWidth = Math.Max(200, ClientSize.Width - 40);
        _summaryLabel.MaximumSize = new Size(maxWidth, 0);
        _endpointLabel.MaximumSize = new Size(maxWidth, 0);
        _updatedLabel.MaximumSize = new Size(maxWidth, 0);
    }

    internal sealed class DetailRow
    {
        public required string Name { get; init; }
        public required string Value { get; init; }
    }
}
