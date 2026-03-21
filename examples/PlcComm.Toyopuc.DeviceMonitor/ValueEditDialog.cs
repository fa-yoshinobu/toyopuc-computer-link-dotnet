using System.Globalization;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal sealed class ValueEditDialog : Form
{
    private readonly string _unit;
    private readonly bool _isPackedBitWord;
    private readonly int _hexDigits;
    private readonly int _maxNumericValue;
    private readonly TextBox _hexTextBox = new();
    private readonly TextBox _decTextBox = new();
    private readonly Button _bitZeroButton = new() { Text = "0 OFF", AutoSize = true };
    private readonly Button _bitOneButton = new() { Text = "1 ON", AutoSize = true };
    private readonly Label _bitValueLabel = new() { AutoSize = true };
    private readonly CheckBox[] _packedBitButtons = new CheckBox[16];
    private bool _bitValue;
    private bool _syncingInputs;

    public ValueEditDialog(string addressLabel, string unit, object? currentValue, bool isPackedBitWord = false)
    {
        _unit = unit;
        _isPackedBitWord = isPackedBitWord;
        (_hexDigits, _maxNumericValue) = unit switch
        {
            "byte" => (2, 0xFF),
            "word" => (4, 0xFFFF),
            _ => (0, 1),
        };

        Text = $"Edit {addressLabel}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, _isPackedBitWord ? 470 : unit == "bit" ? 260 : 430);

        BuildLayout(addressLabel, currentValue);
    }

    public object Value { get; private set; } = 0;

    private void BuildLayout(string addressLabel, object? currentValue)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(CreateLabel("Address"), 0, 0);
        root.Controls.Add(new Label { Text = addressLabel, AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 1, 0);

        root.Controls.Add(CreateLabel("Unit"), 0, 1);
        root.Controls.Add(new Label { Text = UnitDisplayText(), AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 1, 1);

        root.Controls.Add(CreateLabel("Current"), 0, 2);
        root.Controls.Add(new Label { Text = FormatCurrentValue(currentValue), AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, 1, 2);

        root.Controls.Add(CreateLabel("Input"), 0, 3);
        root.Controls.Add(CreateEditor(currentValue), 1, 3);

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
        okButton.Click += OkButton_Click;
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 0, 4);
        root.SetColumnSpan(buttons, 2);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private string UnitDisplayText()
    {
        return _isPackedBitWord ? $"{_unit} (packed bits)" : _unit;
    }

    private Control CreateEditor(object? currentValue)
    {
        if (_unit == "bit" && !_isPackedBitWord)
        {
            return CreateSingleBitEditor(currentValue);
        }

        return CreateNumericEditor(currentValue);
    }

    private Control CreateSingleBitEditor(object? currentValue)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        _bitZeroButton.Click += (_, _) => SetBitValue(false);
        _bitOneButton.Click += (_, _) => SetBitValue(true);
        buttons.Controls.Add(_bitZeroButton);
        buttons.Controls.Add(_bitOneButton);
        panel.Controls.Add(buttons);
        panel.Controls.Add(_bitValueLabel);

        SetBitValue(currentValue as bool? == true);
        return panel;
    }

    private Control CreateNumericEditor(object? currentValue)
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = _isPackedBitWord ? 4 : 4,
            Margin = new Padding(0),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateLabel("Hex"), 0, 0);
        var hexPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        hexPanel.Controls.Add(new Label { AutoSize = true, Text = "0x", Padding = new Padding(0, 8, 4, 0) });
        _hexTextBox.Width = 120;
        _hexTextBox.MaxLength = _hexDigits;
        _hexTextBox.CharacterCasing = CharacterCasing.Upper;
        _hexTextBox.TextChanged += (_, _) => SyncFromHexInput();
        hexPanel.Controls.Add(_hexTextBox);
        panel.Controls.Add(hexPanel, 1, 0);

        panel.Controls.Add(CreateLabel("Dec"), 0, 1);
        _decTextBox.Width = 160;
        _decTextBox.TextChanged += (_, _) => SyncFromDecInput();
        panel.Controls.Add(_decTextBox, 1, 1);

        if (_isPackedBitWord)
        {
            panel.Controls.Add(CreateLabel("Bits"), 0, 2);
            panel.Controls.Add(CreatePackedBitEditor(), 1, 2);
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "toggle bits directly or enter Hex / Dec",
                Margin = new Padding(0, 8, 0, 0),
            }, 1, 3);
        }
        else
        {
            panel.Controls.Add(CreateLabel("Hex keypad"), 0, 2);
            panel.Controls.Add(CreateHexKeypad(), 1, 2);
            panel.Controls.Add(new Label
            {
                AutoSize = true,
                Text = _unit == "byte"
                    ? "byte: Hex 00-FF / Dec 0-255"
                    : "word: Hex 0000-FFFF / Dec 0-65535",
                Margin = new Padding(0, 8, 0, 0),
            }, 1, 3);
        }

        SetNumericValue(ToNumericValue(currentValue));
        return panel;
    }

    private Control CreatePackedBitEditor()
    {
        var wrapper = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
        };

        wrapper.Controls.Add(CreatePackedBitRow(15, 8));
        wrapper.Controls.Add(CreatePackedBitRow(7, 0));
        return wrapper;
    }

    private Control CreatePackedBitRow(int fromBit, int toBit)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };

        for (var bit = fromBit; bit >= toBit; bit--)
        {
            var toggle = new CheckBox
            {
                Appearance = Appearance.Button,
                AutoSize = false,
                Width = 38,
                Height = 32,
                Text = bit.ToString("X1", CultureInfo.InvariantCulture),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 4, 4),
            };
            var capturedBit = bit;
            toggle.CheckedChanged += (_, _) => SyncFromPackedBits();
            _packedBitButtons[capturedBit] = toggle;
            row.Controls.Add(toggle);
        }

        return row;
    }

    private Control CreateHexKeypad()
    {
        var wrapper = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
        };

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 8,
            RowCount = 2,
            Margin = new Padding(0),
        };

        foreach (var digit in "0123456789ABCDEF")
        {
            var button = new Button
            {
                Text = digit.ToString(),
                Width = 34,
                Height = 30,
                Margin = new Padding(0, 0, 4, 4),
            };
            button.Click += (_, _) => AppendHexDigit(digit);
            grid.Controls.Add(button);
        }

        var tools = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        var backButton = new Button { Text = "Back", AutoSize = true };
        backButton.Click += (_, _) => BackspaceHexDigit();
        var clearButton = new Button { Text = "Clear", AutoSize = true };
        clearButton.Click += (_, _) => ClearNumericInputs();
        tools.Controls.Add(backButton);
        tools.Controls.Add(clearButton);

        wrapper.Controls.Add(grid);
        wrapper.Controls.Add(tools);
        return wrapper;
    }

    private void SetBitValue(bool value)
    {
        _bitValue = value;
        _bitZeroButton.BackColor = value ? SystemColors.Control : Color.LightSteelBlue;
        _bitOneButton.BackColor = value ? Color.LightSteelBlue : SystemColors.Control;
        _bitValueLabel.Text = $"Selected: {(value ? "1 ON" : "0 OFF")}";
    }

    private void SetNumericValue(int value)
    {
        value = Math.Clamp(value, 0, _maxNumericValue);
        _syncingInputs = true;
        _hexTextBox.Text = value.ToString($"X{_hexDigits}", CultureInfo.InvariantCulture);
        _hexTextBox.SelectionStart = _hexTextBox.TextLength;
        SyncNumericMirrors(value);

        _syncingInputs = false;
    }

    private void SyncFromHexInput()
    {
        if (_syncingInputs)
        {
            return;
        }

        var normalized = NormalizeHexDigits(_hexTextBox.Text, allowEmpty: true);
        if (normalized.Length > _hexDigits)
        {
            normalized = normalized[^_hexDigits..];
        }

        if (normalized.Length == 0)
        {
            _syncingInputs = true;
            _decTextBox.Clear();
            if (_isPackedBitWord)
            {
                for (var bit = 0; bit < 16; bit++)
                {
                    _packedBitButtons[bit].Checked = false;
                }
            }

            _syncingInputs = false;
            return;
        }

        if (!int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        _syncingInputs = true;
        SyncNumericMirrors(value);
        _syncingInputs = false;
    }

    private void SyncFromDecInput()
    {
        if (_syncingInputs)
        {
            return;
        }

        var text = _decTextBox.Text.Trim();
        if (text.Length == 0)
        {
            _syncingInputs = true;
            _hexTextBox.Clear();
            if (_isPackedBitWord)
            {
                for (var bit = 0; bit < 16; bit++)
                {
                    _packedBitButtons[bit].Checked = false;
                }
            }

            _syncingInputs = false;
            return;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        SetNumericValue(value);
    }

    private void SyncFromPackedBits()
    {
        if (_syncingInputs || !_isPackedBitWord)
        {
            return;
        }

        var value = 0;
        for (var bit = 0; bit < 16; bit++)
        {
            if (_packedBitButtons[bit].Checked)
            {
                value |= 1 << bit;
            }
        }

        SetNumericValue(value);
    }

    private void AppendHexDigit(char digit)
    {
        var digits = NormalizeHexDigits(_hexTextBox.Text, allowEmpty: true);
        digits = digits.Length >= _hexDigits ? digits[1..] + digit : digits + digit;
        if (!int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        SetNumericValue(value);
    }

    private void BackspaceHexDigit()
    {
        var digits = NormalizeHexDigits(_hexTextBox.Text, allowEmpty: true);
        if (digits.Length == 0)
        {
            ClearNumericInputs();
            return;
        }

        digits = digits[..^1];
        if (digits.Length == 0)
        {
            ClearNumericInputs();
            return;
        }

        if (!int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        SetNumericValue(value);
    }

    private void ClearNumericInputs()
    {
        _syncingInputs = true;
        _hexTextBox.Clear();
        _decTextBox.Clear();
        if (_isPackedBitWord)
        {
            for (var bit = 0; bit < 16; bit++)
            {
                _packedBitButtons[bit].Checked = false;
            }
        }

        _syncingInputs = false;
        _hexTextBox.Focus();
    }

    private void SyncNumericMirrors(int value)
    {
        _decTextBox.Text = value.ToString(CultureInfo.InvariantCulture);
        _decTextBox.SelectionStart = _decTextBox.TextLength;

        if (!_isPackedBitWord)
        {
            return;
        }

        for (var bit = 0; bit < 16; bit++)
        {
            _packedBitButtons[bit].Checked = ((value >> bit) & 0x01) != 0;
        }
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            Value = ParseValue();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Edit Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }

    private object ParseValue()
    {
        if (_unit == "bit" && !_isPackedBitWord)
        {
            return _bitValue;
        }

        var value = ParseNumericValue();
        return _unit switch
        {
            "byte" => (byte)value,
            _ => value,
        };
    }

    private int ParseNumericValue()
    {
        if (_hexTextBox.Text.Trim().Length > 0)
        {
            var hexDigits = NormalizeHexDigits(_hexTextBox.Text.Trim(), allowEmpty: false);
            var hexValue = int.Parse(hexDigits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (hexValue > _maxNumericValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), RangeErrorText());
            }

            return hexValue;
        }

        if (_decTextBox.Text.Trim().Length > 0)
        {
            var decValue = int.Parse(_decTextBox.Text.Trim(), CultureInfo.InvariantCulture);
            if (decValue is < 0 || decValue > _maxNumericValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Value), RangeErrorText());
            }

            return decValue;
        }

        throw new ArgumentException("write value is required", nameof(Value));
    }

    private string RangeErrorText()
    {
        return _unit == "byte" ? "byte value must be 0x00-0xFF / 0-255" : "word value must be 0x0000-0xFFFF / 0-65535";
    }

    private static int ToNumericValue(object? currentValue)
    {
        return currentValue switch
        {
            byte valueByte => valueByte,
            int valueWord => valueWord & 0xFFFF,
            _ => 0,
        };
    }

    private string FormatCurrentValue(object? currentValue)
    {
        return currentValue switch
        {
            bool bit => bit ? "1" : "0",
            byte valueByte => $"0x{valueByte:X2} / {valueByte}",
            int valueWord when _isPackedBitWord => $"0x{valueWord & 0xFFFF:X4} / {valueWord & 0xFFFF} / bits",
            int valueWord => $"0x{valueWord & 0xFFFF:X4} / {valueWord & 0xFFFF}",
            _ => "-",
        };
    }

    private static string NormalizeHexDigits(string text, bool allowEmpty)
    {
        var normalized = text.Trim().ToUpperInvariant();
        if (normalized.StartsWith("0X", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        normalized = new string(normalized.Where(static ch => Uri.IsHexDigit(ch)).ToArray());
        if (!allowEmpty && normalized.Length == 0)
        {
            throw new ArgumentException("hex value is required", nameof(text));
        }

        return normalized;
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
}
