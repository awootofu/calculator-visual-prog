namespace CalculatorEngine;

/// <summary>
/// Exception khusus kalkulator dengan pesan Bahasa Indonesia yang aman ditampilkan ke pengguna.
/// </summary>
public sealed class CalculatorException : Exception
{
    public CalculatorException(string message) : base(message) { }
}

/// <summary>
/// Mesin evaluasi ekspresi matematika yang aman (tanpa eval).
/// Mendukung prioritas operator, tanda kurung, fungsi scientific,
/// konstanta pi/e, operator biner mod / yroot, faktorial postfix, dan unary minus.
/// </summary>
public static class Calculator
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sqrt", "cbrt", "abs", "log", "ln", "log2",
        "exp", "pow10", "pow2", "sqr", "cube", "fact"
    };

    private const double IntegerTolerance = 1e-9;

    /// <summary>Evaluasi ekspresi, melempar <see cref="CalculatorException"/> bila tidak valid.</summary>
    public static double Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new CalculatorException("Ekspresi kosong");

        string normalized = Normalize(expression);
        List<Token> tokens = Tokenize(normalized);
        tokens = InsertImplicitMultiplication(tokens);
        List<Token> rpn = ToRpn(tokens);
        return EvalRpn(rpn);
    }

    /// <summary>Versi aman tanpa exception untuk UI.</summary>
    public static bool TryEvaluate(string expression, out double result, out string error)
    {
        try
        {
            result = Evaluate(expression);
            error = string.Empty;
            return true;
        }
        catch (CalculatorException ex)
        {
            result = double.NaN;
            error = ex.Message;
            return false;
        }
        catch (Exception)
        {
            result = double.NaN;
            error = "Ekspresi tidak valid";
            return false;
        }
    }

    /// <summary>Format hasil agar tidak menampilkan NaN/Infinity dan rapi di display.</summary>
    public static string FormatResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "Error";
        if (Math.Abs(value) < 0.0000000001 && value != 0)
        {
            // Sangat kecil -> notasi ilmiah pendek
            return value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
        }
        // G12 memberi presisi cukup tanpa noise floating point.
        string s = value.ToString("G12", System.Globalization.CultureInfo.InvariantCulture);
        // Normalisasi "-0" menjadi "0"
        if (s == "-0") s = "0";
        return s;
    }

    // ------------------------------------------------------------------
    // Normalisasi simbol tampilan (× ÷ − π ² ³) ke bentuk ASCII internal.
    // ------------------------------------------------------------------
    private static string Normalize(string expr)
    {
        string s = expr.Trim();
        s = s.Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace('–', '-');
        s = s.Replace("π", "pi");
        s = s.Replace("²", "^2").Replace("³", "^3");
        // Akar simbol umum (cadangan bila ditempel dari luar)
        s = s.Replace("√", "sqrt");
        return s;
    }

    // ------------------------------------------------------------------
    // Token
    // ------------------------------------------------------------------
    private enum TokenKind { Number, Operator, Function, LeftParen, RightParen, Factorial }

    private sealed class Token
    {
        public TokenKind Kind;
        public string Text = "";
        public double Number;
    }

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        string lower = s.ToLowerInvariant();
        int i = 0;
        while (i < lower.Length)
        {
            char c = lower[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Angka (termasuk desimal dan notasi ilmiah 1E3 agar hasil sebelumnya bisa dihitung lagi)
            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool dotSeen = false;
                while (i < lower.Length && (char.IsDigit(lower[i]) || lower[i] == '.'))
                {
                    if (lower[i] == '.')
                    {
                        if (dotSeen) break;
                        dotSeen = true;
                    }
                    i++;
                }
                // Eksponen ilmiah: e/E diikuti opsional +/- dan digit, hanya jika menempel pada angka
                if (i < lower.Length && (lower[i] == 'e'))
                {
                    int j = i + 1;
                    if (j < lower.Length && (lower[j] == '+' || lower[j] == '-')) j++;
                    int k = j;
                    while (k < lower.Length && char.IsDigit(lower[k])) k++;
                    if (k > j)
                    {
                        // "e" adalah bagian dari angka, bukan konstanta
                        i = k;
                    }
                }
                string numStr = lower.Substring(start, i - start);
                if (!double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                    throw new CalculatorException("Angka tidak valid");
                if (double.IsNaN(val) || double.IsInfinity(val))
                    throw new CalculatorException("Angka terlalu besar");
                tokens.Add(new Token { Kind = TokenKind.Number, Text = numStr, Number = val });
                continue;
            }

            // Identifikator: fungsi, konstanta, atau operator kata (mod, yroot)
            if (char.IsLetter(c))
            {
                int start = i;
                while (i < lower.Length && (char.IsLetterOrDigit(lower[i]))) i++;
                string name = lower.Substring(start, i - start);
                if (name == "pi")
                {
                    tokens.Add(new Token { Kind = TokenKind.Number, Text = "pi", Number = Math.PI });
                }
                else if (name == "e")
                {
                    tokens.Add(new Token { Kind = TokenKind.Number, Text = "e", Number = Math.E });
                }
                else if (name == "mod" || name == "yroot")
                {
                    tokens.Add(new Token { Kind = TokenKind.Operator, Text = name });
                }
                else if (Functions.Contains(name))
                {
                    tokens.Add(new Token { Kind = TokenKind.Function, Text = name });
                }
                else
                {
                    throw new CalculatorException($"Fungsi '{name}' tidak dikenal");
                }
                continue;
            }

            switch (c)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '^':
                    tokens.Add(new Token { Kind = TokenKind.Operator, Text = c.ToString() });
                    i++;
                    break;
                case '!':
                    tokens.Add(new Token { Kind = TokenKind.Factorial, Text = "!" });
                    i++;
                    break;
                case '(':
                    tokens.Add(new Token { Kind = TokenKind.LeftParen, Text = "(" });
                    i++;
                    break;
                case ')':
                    tokens.Add(new Token { Kind = TokenKind.RightParen, Text = ")" });
                    i++;
                    break;
                default:
                    throw new CalculatorException($"Karakter '{s[i]}' tidak valid");
            }
        }
        if (tokens.Count == 0)
            throw new CalculatorException("Ekspresi kosong");
        return tokens;
    }

    // Sisipkan perkalian implisit: 2pi, 2(3+4), (2)(3), 2sqrt(9) -> 2*sqrt(9)
    private static List<Token> InsertImplicitMultiplication(List<Token> tokens)
    {
        var out_ = new List<Token>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var cur = tokens[i];
            if (i > 0)
            {
                var prev = out_[out_.Count - 1];
                bool prevIsValue = prev.Kind == TokenKind.Number || prev.Kind == TokenKind.RightParen || prev.Kind == TokenKind.Factorial;
                bool curStartsValue = cur.Kind == TokenKind.Number || cur.Kind == TokenKind.Function || cur.Kind == TokenKind.LeftParen;
                if (prevIsValue && curStartsValue)
                    out_.Add(new Token { Kind = TokenKind.Operator, Text = "*" });
            }
            out_.Add(cur);
        }
        return out_;
    }

    private static int Precedence(string op) => op switch
    {
        "u-" => 5,
        "^" or "yroot" => 4,
        "*" or "/" or "mod" => 3,
        "+" or "-" => 2,
        _ => 0,
    };

    private static bool IsRightAssoc(string op) => op == "^" || op == "yroot" || op == "u-";

    // Shunting-yard: infix -> RPN. Mendeteksi unary minus/plus dari konteks.
    private static List<Token> ToRpn(List<Token> tokens)
    {
        var output = new List<Token>();
        var stack = new Stack<Token>();
        Token? prev = null;

        foreach (var tok in tokens)
        {
            switch (tok.Kind)
            {
                case TokenKind.Number:
                    output.Add(tok);
                    break;
                case TokenKind.Function:
                    stack.Push(tok);
                    break;
                case TokenKind.Factorial:
                    // Postfix prioritas tertinggi: langsung ke output
                    output.Add(tok);
                    break;
                case TokenKind.Operator:
                {
                    string op = tok.Text;
                    // Deteksi unary: di awal, setelah operator biner/unary, atau setelah '('
                    bool unaryContext = prev == null
                        || prev.Kind == TokenKind.Operator
                        || prev.Kind == TokenKind.LeftParen
                        || (prev.Kind == TokenKind.Operator && prev.Text == "u-");
                    // prev tidak pernah menyimpan "u-" sebagai Operator terpisah di list asli,
                    // jadi cukup cek tiga kondisi pertama. (Kondisi terakhir defensif.)
                    if ((op == "-" || op == "+") && unaryContext)
                    {
                        if (op == "+")
                        {
                            // Unary plus = no-op, lewati
                            prev = tok;
                            continue;
                        }
                        op = "u-";
                        var u = new Token { Kind = TokenKind.Operator, Text = op };
                        stack.Push(u);
                        prev = tok;
                        continue;
                    }
                    while (stack.Count > 0)
                    {
                        var top = stack.Peek();
                        if (top.Kind == TokenKind.Function)
                        {
                            output.Add(stack.Pop());
                            continue;
                        }
                        if (top.Kind == TokenKind.Operator)
                        {
                            int pTop = Precedence(top.Text);
                            int pCur = Precedence(op);
                            if (pTop > pCur || (pTop == pCur && !IsRightAssoc(op)))
                            {
                                output.Add(stack.Pop());
                                continue;
                            }
                        }
                        break;
                    }
                    stack.Push(new Token { Kind = TokenKind.Operator, Text = op });
                    break;
                }
                case TokenKind.LeftParen:
                    stack.Push(tok);
                    break;
                case TokenKind.RightParen:
                {
                    bool found = false;
                    while (stack.Count > 0)
                    {
                        var top = stack.Pop();
                        if (top.Kind == TokenKind.LeftParen) { found = true; break; }
                        output.Add(top);
                    }
                    if (!found)
                        throw new CalculatorException("Kurung tidak seimbang");
                    // Fungsi atau unary minus di depan '(' ikut keluar, mis. sqrt(...) / -(...)
                    if (stack.Count > 0)
                    {
                        var top = stack.Peek();
                        if (top.Kind == TokenKind.Function ||
                            (top.Kind == TokenKind.Operator && top.Text == "u-"))
                            output.Add(stack.Pop());
                    }
                    break;
                }
            }
            prev = tok;
        }

        while (stack.Count > 0)
        {
            var top = stack.Pop();
            if (top.Kind == TokenKind.LeftParen || top.Kind == TokenKind.RightParen)
                throw new CalculatorException("Kurung tidak seimbang");
            output.Add(top);
        }
        return output;
    }

    private static double EvalRpn(List<Token> rpn)
    {
        var st = new Stack<double>();
        foreach (var tok in rpn)
        {
            switch (tok.Kind)
            {
                case TokenKind.Number:
                    st.Push(tok.Number);
                    break;
                case TokenKind.Factorial:
                    Need(st, 1);
                    st.Push(Factorial(st.Pop()));
                    break;
                case TokenKind.Function:
                    Need(st, 1);
                    st.Push(ApplyFunction(tok.Text, st.Pop()));
                    break;
                case TokenKind.Operator:
                    if (tok.Text == "u-")
                    {
                        Need(st, 1);
                        st.Push(-st.Pop());
                    }
                    else
                    {
                        Need(st, 2);
                        double b = st.Pop();
                        double a = st.Pop();
                        st.Push(ApplyBinary(tok.Text, a, b));
                    }
                    break;
                default:
                    throw new CalculatorException("Ekspresi tidak valid");
            }
        }
        if (st.Count != 1)
            throw new CalculatorException("Ekspresi tidak valid");
        double result = st.Pop();
        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new CalculatorException("Hasil tidak valid");
        return result;
    }

    private static void Need(Stack<double> st, int n)
    {
        if (st.Count < n)
            throw new CalculatorException("Ekspresi tidak valid");
    }

    private static void CheckFinite(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new CalculatorException("Hasil tidak valid");
    }

    private static double ApplyBinary(string op, double a, double b)
    {
        switch (op)
        {
            case "+": { double r = a + b; CheckFinite(r); return r; }
            case "-": { double r = a - b; CheckFinite(r); return r; }
            case "*": { double r = a * b; CheckFinite(r); return r; }
            case "/":
                if (b == 0) throw new CalculatorException("Pembagian dengan nol");
                { double r = a / b; CheckFinite(r); return r; }
            case "mod":
                if (b == 0) throw new CalculatorException("Modulo dengan nol");
                { double r = a % b; CheckFinite(r); return r; }
            case "^":
                return Pow(a, b);
            case "yroot":
                return YRoot(a, b);
            default:
                throw new CalculatorException("Operator tidak dikenal");
        }
    }

    private static double Pow(double a, double b)
    {
        // Basis negatif dengan pangkat pecahan tidak real
        if (a < 0 && !IsInteger(b))
            throw new CalculatorException("Pangkat pecahan dari bilangan negatif");
        if (a == 0 && b < 0)
            throw new CalculatorException("Pembagian dengan nol");
        double r = Math.Pow(a, b);
        if (double.IsNaN(r))
            throw new CalculatorException("Pangkat tidak valid");
        CheckFinite(r);
        return r;
    }

    // a yroot b = akar pangkat b dari a  ==  a^(1/b)
    private static double YRoot(double a, double b)
    {
        if (b == 0)
            throw new CalculatorException("Akar dengan indeks nol");
        if (a < 0)
        {
            if (!IsInteger(b))
                throw new CalculatorException("Akar bilangan negatif");
            // Indeks genap dari bilangan negatif tidak real; indeks ganjil valid.
            long n = (long)Math.Round(b);
            if (n % 2 == 0)
                throw new CalculatorException("Akar genap dari bilangan negatif");
            double r = -Math.Pow(Math.Abs(a), 1.0 / b);
            CheckFinite(r);
            return r;
        }
        double res = Math.Pow(a, 1.0 / b);
        if (double.IsNaN(res))
            throw new CalculatorException("Akar tidak valid");
        CheckFinite(res);
        return res;
    }

    private static double ApplyFunction(string name, double a)
    {
        switch (name.ToLowerInvariant())
        {
            case "sqrt":
                if (a < 0) throw new CalculatorException("Akar dari bilangan negatif");
                { double r = Math.Sqrt(a); CheckFinite(r); return r; }
            case "cbrt":
                { double r = Math.Cbrt(a); CheckFinite(r); return r; } // cbrt real mendukung negatif
            case "abs":
                return Math.Abs(a);
            case "log":
                if (a <= 0) throw new CalculatorException("Log dari nol/negatif");
                { double r = Math.Log10(a); CheckFinite(r); return r; }
            case "ln":
                if (a <= 0) throw new CalculatorException("Ln dari nol/negatif");
                { double r = Math.Log(a); CheckFinite(r); return r; }
            case "log2":
                if (a <= 0) throw new CalculatorException("Log dari nol/negatif");
                { double r = Math.Log2(a); CheckFinite(r); return r; }
            case "exp":
                { double r = Math.Exp(a); CheckFinite(r); return r; } // e^a
            case "pow10":
                { double r = Math.Pow(10, a); CheckFinite(r); return r; }
            case "pow2":
                { double r = Math.Pow(2, a); CheckFinite(r); return r; }
            case "sqr":
                { double r = a * a; CheckFinite(r); return r; }
            case "cube":
                { double r = a * a * a; CheckFinite(r); return r; }
            case "fact":
                return Factorial(a);
            default:
                throw new CalculatorException("Fungsi tidak dikenal");
        }
    }

    private static bool IsInteger(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return false;
        return Math.Abs(v - Math.Round(v)) < IntegerTolerance;
    }

    // Faktorial hanya untuk bilangan bulat 0..170 (171! sudah Infinity untuk double).
    private static double Factorial(double n)
    {
        if (!IsInteger(n) || n < 0)
            throw new CalculatorException("Faktorial hanya bilangan bulat ≥ 0");
        if (n > 170)
            throw new CalculatorException("Faktorial terlalu besar (maks 170)");
        long k = (long)Math.Round(n);
        double r = 1;
        for (long i = 2; i <= k; i++) r *= i;
        CheckFinite(r);
        return r;
    }
}
