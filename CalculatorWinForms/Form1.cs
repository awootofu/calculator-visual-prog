using CalculatorEngine;

namespace CalculatorWinForms;

public partial class Form1 : Form
{
    // Ekspresi internal (ASCII + nama fungsi). Display utama menampilkannya
    // dengan simbol cantik x ÷ −, sedangkan evaluasi memakai bentuk aslinya.
    private string _expression = string.Empty;
    private bool _isSecond;
    private bool _errorState;
    private bool _justEvaluated;

    private TextBox _display = null!;
    private Label _history = null!;
    private Button _btnSecond = null!;
    private Button _btnSquare = null!;
    private Button _btnCbrt = null!;
    private Button _btnPow10 = null!;
    private Button _btnLog = null!;
    private Button _btnLn = null!;

    private static readonly Color BgColor = Color.FromArgb(31, 41, 55);
    private static readonly Color FuncColor = Color.FromArgb(55, 65, 81);
    private static readonly Color NumColor = Color.FromArgb(75, 85, 99);
    private static readonly Color EqualsColor = Color.FromArgb(37, 99, 235);
    private static readonly Color SecondActiveColor = Color.FromArgb(14, 165, 233);

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    // ------------------------------------------------------------------
    // UI: display atas + grid 5 kolom x 7 baris, proporsional saat resize.
    // ------------------------------------------------------------------
    private void BuildUi()
    {
        BackColor = BgColor;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = BgColor,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Controls.Add(root);

        _history = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(156, 163, 175),
            BackColor = BgColor,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            Text = string.Empty,
        };
        root.Controls.Add(_history, 0, 0);

        _display = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            TabStop = false,
            TextAlign = HorizontalAlignment.Right,
            BackColor = BgColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 28f, FontStyle.Bold),
            Text = "0",
            Multiline = false,
        };
        root.Controls.Add(_display, 0, 1);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 7,
            BackColor = BgColor,
        };
        for (int c = 0; c < 5; c++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        for (int r = 0; r < 7; r++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7f));
        root.Controls.Add(grid, 0, 2);

        // (id, labelNormal) — id dipakai di OnButtonClick agar satu handler untuk semua tombol.
        (string id, string label)[] rows =
        [
            ("second", "2nd"), ("pi", "π"), ("e", "e"), ("clear", "C"), ("back", "⌫"),
            ("square", "x²"), ("yroot", "ʸ√x"), ("abs", "|x|"), ("exp", "exp"), ("mod", "mod"),
            ("cbrt", "³√x"), ("lp", "("), ("rp", ")"), ("fact", "n!"), ("div", "÷"),
            ("pow", "xʸ"), ("7", "7"), ("8", "8"), ("9", "9"), ("mul", "×"),
            ("pow10", "10ˣ"), ("4", "4"), ("5", "5"), ("6", "6"), ("sub", "−"),
            ("log", "log"), ("1", "1"), ("2", "2"), ("3", "3"), ("add", "+"),
            ("ln", "ln"), ("neg", "+/−"), ("0", "0"), ("dot", "."), ("eq", "="),
        ];

        foreach (var (id, label) in rows)
        {
            bool isDigit = id.Length == 1 && char.IsDigit(id[0]);
            bool isDotOrNeg = id is "dot" or "neg";
            bool isEq = id == "eq";
            var btn = new Button
            {
                Text = label,
                Tag = id,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = isEq ? EqualsColor : (isDigit || isDotOrNeg ? NumColor : FuncColor),
                Margin = new Padding(3),
                TabStop = false,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += OnButtonClick;
            grid.Controls.Add(btn);

            if (id == "second") _btnSecond = btn;
            if (id == "square") _btnSquare = btn;
            if (id == "cbrt") _btnCbrt = btn;
            if (id == "pow10") _btnPow10 = btn;
            if (id == "log") _btnLog = btn;
            if (id == "ln") _btnLn = btn;
        }
    }

    // ------------------------------------------------------------------
    // Satu event handler untuk semua tombol (menghindari duplikasi kode).
    // ------------------------------------------------------------------
    private void OnButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        HandleAction(id);
    }

    private void HandleAction(string id)
    {
        if (_errorState && id is not ("clear" or "back" or "eq"))
        {
            _expression = string.Empty;
            _errorState = false;
            _history.Text = string.Empty;
        }

        switch (id)
        {
            case "second": ToggleSecond(); return;
            case "clear": ClearAll(); return;
            case "back": Backspace(); return;
            case "eq": Evaluate(); return;
            case "neg": ToggleSign(); return;
        }

        // Setelah "=", digit/fungsi memulai ekspresi baru, operator melanjutkan hasil.
        if (_justEvaluated && IsFreshStart(id))
        {
            _expression = string.Empty;
            _history.Text = string.Empty;
        }
        _justEvaluated = false;

        string insert = MapInsert(id);
        if (insert.Length == 0) return;
        _expression += insert;
        if (_isSecond) SetSecond(false); // mode 2nd hanya untuk satu tombol
        RefreshDisplay();
    }

    private static bool IsFreshStart(string id) =>
        id is "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9"
            or "dot" or "pi" or "e" or "lp"
            or "square" or "cbrt" or "abs" or "exp" or "pow10" or "log" or "ln";

    // Teks yang disisipkan ke ekspresi internal untuk tiap tombol.
    // x² / x³ langsung menjadi "^2" / "^3" sehingga "5 x²" = "5^2" = 25.
    private string MapInsert(string id) => id switch
    {
        "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" => id,
        "dot" => ".",
        "add" => "+",
        "sub" => "-",
        "mul" => "*",
        "div" => "/",
        "mod" => " mod ",
        "lp" => "(",
        "rp" => ")",
        "pi" => "pi",
        "e" => "e",
        "square" => _isSecond ? "sqrt(" : "^2",
        "cbrt" => _isSecond ? "^3" : "cbrt(",
        "yroot" => " yroot ",
        "pow" => "^",
        "abs" => "abs(",
        "fact" => "!",
        "exp" => "exp(",
        "pow10" => _isSecond ? "pow2(" : "pow10(",
        "log" => _isSecond ? "log2(" : "log(",
        "ln" => _isSecond ? "exp(" : "ln(",
        _ => string.Empty,
    };

    // Mode 2nd: ubah 5 tombol ke fungsi alternatif + sorot tombol 2nd.
    private void ToggleSecond() => SetSecond(!_isSecond);

    private void SetSecond(bool on)
    {
        _isSecond = on;
        _btnSquare.Text = on ? "√x" : "x²";
        _btnCbrt.Text = on ? "x³" : "³√x";
        _btnPow10.Text = on ? "2ˣ" : "10ˣ";
        _btnLog.Text = on ? "log₂" : "log";
        _btnLn.Text = on ? "eˣ" : "ln";
        _btnSecond.BackColor = on ? SecondActiveColor : FuncColor;
        _btnSecond.Font = new Font("Segoe UI", 12f, on ? FontStyle.Bold : FontStyle.Regular);
    }

    private void ClearAll()
    {
        _expression = string.Empty;
        _history.Text = string.Empty;
        _errorState = false;
        _justEvaluated = false;
        RefreshDisplay();
    }

    // Hapus cerdas: nama fungsi ("pow10(", " yroot ") dihapus sekaligus.
    private void Backspace()
    {
        if (_expression.Length == 0) return;
        string[] words = ["pow10(", "pow2(", "log2(", "sqrt(", "cbrt(", "fact(", "cube(", "sqr(",
                          "abs(", "exp(", "log(", "ln(", " yroot ", " mod "];
        foreach (var w in words)
        {
            if (_expression.EndsWith(w, StringComparison.Ordinal))
            {
                _expression = _expression[..^w.Length];
                RefreshDisplay();
                return;
            }
        }
        _expression = _expression[..^1];
        RefreshDisplay();
    }

    // +/- : balik tanda bilangan terakhir; jika tak ada, sisipkan "-" bila valid.
    private void ToggleSign()
    {
        if (_expression.Length == 0 || _expression.EndsWith("(", StringComparison.Ordinal) || EndsWithOperator())
        {
            _expression += "-";
            RefreshDisplay();
            return;
        }
        int i = _expression.Length - 1;
        while (i >= 0 && (char.IsDigit(_expression[i]) || _expression[i] == '.' || _expression[i] == ')'))
        {
            if (_expression[i] == ')')
            {
                // Sederhananya: bungkus kasus "(...)" dengan "-(...)" tidak didukung;
                // sisipkan "-" di depan bila valid, jika tidak abaikan.
                return;
            }
            i--;
        }
        int numStart = i + 1;
        if (numStart >= _expression.Length) return;
        // Cek minus unary tepat sebelum angka
        if (numStart > 0 && _expression[numStart - 1] == '-' &&
            (numStart - 1 == 0 || "+-*/^( ".Contains(_expression[numStart - 2])))
        {
            _expression = _expression.Remove(numStart - 1, 1);
        }
        else
        {
            _expression = _expression.Insert(numStart, "-");
        }
        RefreshDisplay();
    }

    private bool EndsWithOperator()
    {
        string t = _expression.TrimEnd();
        return t.EndsWith("+") || t.EndsWith("-") || t.EndsWith("*") || t.EndsWith("/") ||
               t.EndsWith("^") || t.EndsWith("mod", StringComparison.Ordinal) || t.EndsWith("yroot", StringComparison.Ordinal);
    }

    private void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(_expression))
            return;
        string expr = _expression;
        if (Calculator.TryEvaluate(expr, out double value, out string error))
        {
            string formatted = Calculator.FormatResult(value);
            _history.Text = ToPretty(expr) + " =";
            // Simpan presisi penuh untuk perhitungan lanjutan, tampilkan versi rapi.
            _expression = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            _display.Text = formatted;
            _justEvaluated = true;
            if (_isSecond) SetSecond(false);
        }
        else
        {
            _history.Text = ToPretty(expr);
            _display.Text = "Error: " + error;
            _errorState = true;
            _justEvaluated = false;
        }
    }

    private void RefreshDisplay()
    {
        _display.Text = _expression.Length == 0 ? "0" : ToPretty(_expression);
    }

    // Tampilkan simbol cantik (× ÷ −) tanpa mengubah ekspresi internal.
    private static string ToPretty(string expr) =>
        expr.Replace("*", "×").Replace("/", "÷").Replace("-", "−");

    // Dukungan keyboard dasar.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is >= Keys.D0 and <= Keys.D9 && e.Modifiers == Keys.None)
        {
            HandleAction(((int)(e.KeyCode - Keys.D0)).ToString());
            e.Handled = true;
        }
        else if (e.KeyCode is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            HandleAction(((int)(e.KeyCode - Keys.NumPad0)).ToString());
            e.Handled = true;
        }
        else switch (e.KeyCode)
        {
            case Keys.Enter or Keys.Oemplus when e.Modifiers != Keys.Shift:
                HandleAction("eq"); e.Handled = true; break;
            case Keys.Back:
                HandleAction("back"); e.Handled = true; break;
            case Keys.Escape:
                HandleAction("clear"); e.Handled = true; break;
            case Keys.Oemplus: HandleAction("add"); e.Handled = true; break;
            case Keys.OemMinus: HandleAction("sub"); e.Handled = true; break;
            case Keys.Multiply: HandleAction("mul"); e.Handled = true; break;
            case Keys.Divide: HandleAction("div"); e.Handled = true; break;
        }
    }
}
