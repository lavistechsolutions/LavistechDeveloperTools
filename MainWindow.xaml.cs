using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LavistechDeveloperTools;

public partial class MainWindow : Window
{
    private string _activeTool = "Dashboard";
    private readonly Dictionary<string, TextBox> _fields = new();
    private readonly Dictionary<string, ComboBox> _choices = new();

    public MainWindow() => InitializeComponent();

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tool) return;
        _activeTool = tool;
        Dashboard.Visibility = tool == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ToolHost.Visibility = tool == "Dashboard" ? Visibility.Collapsed : Visibility.Visible;
        PageTitle.Text = tool == "Dashboard" ? "Developer Tools Dashboard" : tool;
        PageSubtitle.Text = tool == "Dashboard" ? "A collection of useful utilities for developers" : "Runs locally on your Windows PC. Your input is not uploaded.";
        if (tool != "Dashboard") BuildTool(tool);
        foreach (var nav in NavPanel.Children.OfType<Button>())
        {
            bool active = Equals(nav.Tag, tool);
            nav.Background = active ? System.Windows.Media.Brushes.DodgerBlue : System.Windows.Media.Brushes.Transparent;
            nav.Foreground = active ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Gainsboro;
        }
    }

    private void BuildTool(string tool)
    {
        ToolControls.Children.Clear(); OutputBox.Clear(); StatusText.Text = ""; _fields.Clear(); _choices.Clear();
        ToolDescription.Text = tool switch
        {
            "JSON Formatter" => "Paste JSON to validate, pretty-print or minify it.",
            "XML to JSON" => "Convert an XML document into a JSON representation.",
            "JWT Decoder" => "Decode a JSON Web Token locally. This does not validate its signature.",
            "GUID Generator" => "Generate standard GUID identifiers.",
            "Regex Tester" => "Test a .NET regular expression against sample text.",
            "Base64 Encoder" => "Encode UTF-8 text to Base64 or decode Base64 to text.",
            "Hash Generator" => "Generate SHA-256 or SHA-512 hashes from text.",
            "Cron Helper" => "A lightweight five-field cron expression explainer (minute hour day month weekday).",
            "File Checksum" => "Calculate a file hash and optionally compare it with an expected checksum.",
            _ => ""
        };

        switch (tool)
        {
            case "JSON Formatter":
                AddInput("json", "JSON input", 220);
                AddAction("Format JSON", () => { var doc = JsonDocument.Parse(Get("json")); OutputBox.Text = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }); StatusText.Text = "Valid JSON."; });
                AddAction("Minify JSON", () => { var doc = JsonDocument.Parse(Get("json")); OutputBox.Text = JsonSerializer.Serialize(doc); StatusText.Text = "Valid JSON."; });
                break;
            case "XML to JSON":
                AddInput("xml", "XML input", 220);
                AddAction("Convert to JSON", () => { var x = XDocument.Parse(Get("xml")); OutputBox.Text = JsonSerializer.Serialize(ToObject(x.Root!), new JsonSerializerOptions { WriteIndented = true }); });
                break;
            case "JWT Decoder":
                AddInput("jwt", "JWT token", 120);
                AddAction("Decode JWT", DecodeJwt);
                break;
            case "GUID Generator":
                AddChoice("format", "Format", new[] { "D (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)", "N (32 digits)", "B ({...})", "P ((...))" });
                AddInput("count", "Number to generate (1–500)", 35, "10");
                AddAction("Generate GUIDs", GenerateGuids);
                break;
            case "Regex Tester":
                AddInput("pattern", "Regular expression pattern", 55);
                AddInput("sample", "Sample text", 150);
                AddChoice("options", "Options", new[] { "None", "IgnoreCase", "Multiline", "Singleline" });
                AddAction("Test expression", TestRegex);
                break;
            case "Base64 Encoder":
                AddInput("base64", "Text or Base64 input", 160);
                AddChoice("mode", "Operation", new[] { "Encode text", "Decode Base64" });
                AddAction("Run", () => { var s = Get("base64"); OutputBox.Text = _choices["mode"].SelectedIndex == 0 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) : Encoding.UTF8.GetString(Convert.FromBase64String(s)); });
                break;
            case "Hash Generator":
                AddInput("hashtext", "Text to hash", 120);
                AddChoice("algorithm", "Algorithm", new[] { "SHA256", "SHA512" });
                AddAction("Generate hash", () => { byte[] bytes = Encoding.UTF8.GetBytes(Get("hashtext")); byte[] hash = _choices["algorithm"].SelectedIndex == 0 ? SHA256.HashData(bytes) : SHA512.HashData(bytes); OutputBox.Text = Convert.ToHexString(hash).ToLowerInvariant(); });
                break;
            case "Cron Helper":
                AddInput("cron", "Cron expression (minute hour day month weekday)", 50, "*/15 9-17 * * 1-5");
                AddAction("Explain expression", ExplainCron);
                break;
            case "File Checksum":
                var fileRow = new StackPanel { Orientation = Orientation.Horizontal };
                var path = new TextBox { Name = "filepath", Width = 570, Height = 38, IsReadOnly = true, AcceptsReturn = false };
                _fields["filepath"] = path;
                var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0), Background = System.Windows.Media.Brushes.LightSteelBlue };
                browse.Click += (_, _) => { var dlg = new OpenFileDialog(); if (dlg.ShowDialog() == true) path.Text = dlg.FileName; };
                fileRow.Children.Add(path); fileRow.Children.Add(browse); ToolControls.Children.Add(fileRow);
                AddChoice("filealgo", "Algorithm", new[] { "SHA256", "SHA512" });
                AddInput("expected", "Expected checksum (optional)", 55);
                AddAction("Calculate checksum", CalculateFileHash);
                break;
        }
    }

    private void AddInput(string key, string label, double height, string initial = "")
    {
        ToolControls.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 5), Foreground = System.Windows.Media.Brushes.DimGray });
        var box = new TextBox { Text = initial, Height = height, AcceptsReturn = height > 60, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
        _fields[key] = box; ToolControls.Children.Add(box);
    }

    private void AddChoice(string key, string label, string[] items)
    {
        ToolControls.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 5), Foreground = System.Windows.Media.Brushes.DimGray });
        var combo = new ComboBox { ItemsSource = items, SelectedIndex = 0, Height = 34, Width = 360, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(6) };
        _choices[key] = combo; ToolControls.Children.Add(combo);
    }

    private void AddAction(string caption, Action action)
    {
        var button = new Button { Content = caption, Background = System.Windows.Media.Brushes.DodgerBlue, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 14, 8, 0), HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 140 };
        button.Click += (_, _) => Run(action); ToolControls.Children.Add(button);
    }

    private string Get(string key) => _fields.TryGetValue(key, out var b) ? b.Text : "";
    private void Run(Action action)
    {
        try { action(); if (string.IsNullOrWhiteSpace(StatusText.Text)) StatusText.Text = "Completed."; }
        catch (Exception ex) { OutputBox.Text = ""; StatusText.Text = "Error: " + ex.Message; }
    }

    private static object? ToObject(XElement element)
    {
        var children = element.Elements().ToList();
        if (children.Count == 0) return element.HasAttributes ? new Dictionary<string, object?> { ["@attributes"] = element.Attributes().ToDictionary(a => a.Name.LocalName, a => (object?)a.Value), ["#text"] = element.Value } : element.Value;
        var result = new Dictionary<string, object?>();
        foreach (var group in children.GroupBy(c => c.Name.LocalName))
        {
            var values = group.Select(ToObject).ToList();
            result[group.Key] = values.Count == 1 ? values[0] : values;
        }
        if (element.HasAttributes) result["@attributes"] = element.Attributes().ToDictionary(a => a.Name.LocalName, a => (object?)a.Value);
        if (!string.IsNullOrWhiteSpace(element.Nodes().OfType<XText>().FirstOrDefault()?.Value)) result["#text"] = element.Nodes().OfType<XText>().First().Value.Trim();
        return result;
    }

    private void DecodeJwt()
    {
        var parts = Get("jwt").Trim().Split('.');
        if (parts.Length < 2) throw new FormatException("A JWT must contain at least a header and payload.");
        OutputBox.Text = "HEADER\n" + PrettyJson(DecodePart(parts[0])) + "\n\nPAYLOAD\n" + PrettyJson(DecodePart(parts[1]));
        StatusText.Text = "Decoded only. Signature and claims have not been verified.";
    }
    private static string DecodePart(string part)
    {
        string s = part.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
    private static string PrettyJson(string s) => JsonSerializer.Serialize(JsonDocument.Parse(s), new JsonSerializerOptions { WriteIndented = true });

    private void GenerateGuids()
    {
        if (!int.TryParse(Get("count"), out int n) || n is < 1 or > 500) throw new ArgumentException("Enter a count from 1 to 500.");
        string format = _choices["format"].SelectedIndex switch { 1 => "N", 2 => "B", 3 => "P", _ => "D" };
        OutputBox.Text = string.Join(Environment.NewLine, Enumerable.Range(0, n).Select(_ => Guid.NewGuid().ToString(format)));
    }

    private void TestRegex()
    {
        var opt = _choices["options"].SelectedIndex switch { 1 => RegexOptions.IgnoreCase, 2 => RegexOptions.Multiline, 3 => RegexOptions.Singleline, _ => RegexOptions.None };
        var matches = Regex.Matches(Get("sample"), Get("pattern"), opt);
        var sb = new StringBuilder($"Matches: {matches.Count}\n\n");
        foreach (Match m in matches) sb.AppendLine($"Index {m.Index}, Length {m.Length}: {m.Value}");
        OutputBox.Text = sb.ToString();
    }

    private void ExplainCron()
    {
        var p = Get("cron").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length != 5) throw new FormatException("Use exactly five fields: minute hour day-of-month month day-of-week.");
        string[] names = { "Minute", "Hour", "Day of month", "Month", "Day of week" };
        OutputBox.Text = string.Join(Environment.NewLine, p.Select((v, i) => $"{names[i],-15} {v}")) +
            "\n\nSpecial syntax:\n*  any value\n,  list of values\n-  range\n/  step value\n\nExample: */15 9-17 * * 1-5 = every 15 minutes during 9 AM–5 PM, Monday–Friday (cron dialects may vary).";
        StatusText.Text = "Basic field breakdown only; next-run calculation and dialect validation are not included.";
    }

    private void CalculateFileHash()
    {
        string file = Get("filepath");
        if (!File.Exists(file)) throw new FileNotFoundException("Select an existing file.");
        using var stream = File.OpenRead(file);
        string hash = _choices["filealgo"].SelectedIndex == 0 ? Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() : Convert.ToHexString(SHA512.HashData(stream)).ToLowerInvariant();
        string expected = Get("expected").Trim();
        OutputBox.Text = $"File: {file}\nAlgorithm: {_choices["filealgo"].SelectedItem}\nChecksum: {hash}";
        if (!string.IsNullOrWhiteSpace(expected))
        {
            bool equal = string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase);
            OutputBox.Text += $"\n\nExpected checksum match: {equal}";
            StatusText.Text = equal ? "Checksum matches." : "Checksum does not match.";
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var existing = Application.Current.Windows.OfType<AboutWindow>().FirstOrDefault();
        if (existing != null) { existing.Activate(); return; }
        new AboutWindow { Owner = this }.Show();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputBox.Text)) { Clipboard.SetText(OutputBox.Text); StatusText.Text = "Output copied."; }
    }
    private void Clear_Click(object sender, RoutedEventArgs e) { OutputBox.Clear(); StatusText.Text = ""; }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (NavPanel is null || SearchBox is null) return;
        string q = SearchBox.Text.Trim();
        foreach (var b in NavPanel.Children.OfType<Button>())
            b.Visibility = string.IsNullOrEmpty(q) || (b.Tag?.ToString() ?? b.Content?.ToString() ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }
}
