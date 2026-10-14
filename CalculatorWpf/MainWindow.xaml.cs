using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CalculatorEngine;

namespace CalculatorWpf;

/// <summary>
/// Interaction logic untuk kalkulator scientific.
/// Satu handler <see cref="OnButtonClick"/> melayani semua tombol via Tag.
/// Logic hitung didelegasikan ke <see cref="Calculator"/> agar terpisah dari UI.
/// </summary>
public partial class MainWindow : Window
{
    private string _expression = string.Empty;
    private bool _isSecond;
    private bool _errorState;
    private bool _justEvaluated;

    private static readonly Brush FuncBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
    private static readonly Brush SecondActiveBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9));

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Focusable = true;
        Loaded += (_, _) => Focus();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
            HandleAction(id);
    }

    private void HandleAction(string id)
    {
        if (_errorState && id is not ("clear" or "back" or "eq"))
        {
            _expression = string.Empty;
            _errorState = false;
            HistoryText.Text = string.Empty;
        }

        switch (id)
        {
            case "second": ToggleSecond(); return;
            case "clear": ClearAll(); return;
            case "back": Backspace(); return;
            case "eq": Evaluate(); return;
            case "neg": ToggleSign(); return;
        }

        if (_justEvaluated && IsFreshStart(id))
        {
            _expression = string.Empty;
            HistoryText.Text = string.Empty;
        }
        _justEvaluated = false;

        string insert = MapInsert(id);
        if (insert.Length == 0) return;
        _expression += insert;
        if (_isSecond) SetSecond(false);
        RefreshDisplay();
    }

    private static bool IsFreshStart(string id) =>
        id is "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9"
            or "dot" or "pi" or "e" or "lp"
            or "square" or "cbrt" or "abs" or "exp" or "pow10" or "log" or "ln";

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

    private void ToggleSecond() => SetSecond(!_isSecond);

    // Mode 2nd: 5 tombol berubah fungsi + tombol 2nd disorot biru terang.
    private void SetSecond(bool on)
    {
        _isSecond = on;
        BtnSquare.Content = on ? "√x" : "x²";
        BtnCbrt.Content = on ? "x³" : "³√x";
        BtnPow10.Content = on ? "2ˣ" : "10ˣ";
        BtnLog.Content = on ? "log₂" : "log";
        BtnLn.Content = on ? "eˣ" : "ln";
        BtnSecond.Background = on ? SecondActiveBrush : FuncBrush;
        BtnSecond.FontWeight = on ? FontWeights.Bold : FontWeights.Normal;
    }

    private void ClearAll()
    {
        _expression = string.Empty;
        HistoryText.Text = string.Empty;
        _errorState = false;
        _justEvaluated = false;
        RefreshDisplay();
    }

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
            if (_expression[i] == ')') return;
            i--;
        }
        int numStart = i + 1;
        if (numStart >= _expression.Length) return;
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
            HistoryText.Text = ToPretty(expr) + " =";
            _expression = value.ToString("R", CultureInfo.InvariantCulture);
            DisplayText.Text = Calculator.FormatResult(value);
            _justEvaluated = true;
            if (_isSecond) SetSecond(false);
        }
        else
        {
            HistoryText.Text = ToPretty(expr);
            DisplayText.Text = "Error: " + error;
            _errorState = true;
            _justEvaluated = false;
        }
    }

    private void RefreshDisplay()
    {
        DisplayText.Text = _expression.Length == 0 ? "0" : ToPretty(_expression);
    }

    private static string ToPretty(string expr) =>
        expr.Replace("*", "×").Replace("/", "÷").Replace("-", "−");

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        string? id = e.Key switch
        {
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            Key.Add => "add",
            Key.Subtract => "sub",
            Key.Multiply => "mul",
            Key.Divide => "div",
            Key.Enter => "eq",
            Key.Back => "back",
            Key.Escape => "clear",
            Key.OemPlus => "add",
            Key.OemMinus => "sub",
            Key.OemComma or Key.OemPeriod or Key.Decimal => "dot",
            _ => null,
        };
        if (id != null)
        {
            HandleAction(id);
            e.Handled = true;
        }
    }
}
