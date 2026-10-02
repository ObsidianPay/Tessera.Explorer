using System.Globalization;
using System.Text;

namespace Tessera.Explorer.Services;

/// <summary>Hex and number parsing for form fields. Throws <see cref="FormatException"/> with a message fit for the log.</summary>
public static class Hex
{
    public static byte[] ParseBytes(string? text, string field = "value", int? exactLength = null, int? maxLength = null)
    {
        var clean = new string((text ?? "").Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '-' && c != ',').ToArray());
        if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) clean = clean[2..];
        if (clean.Length % 2 != 0) throw new FormatException($"{field}: odd number of hex digits.");
        byte[] bytes;
        try { bytes = Convert.FromHexString(clean); }
        catch (FormatException) { throw new FormatException($"{field}: not valid hex."); }
        if (exactLength is { } n && bytes.Length != n) throw new FormatException($"{field}: expected {n} bytes, got {bytes.Length}.");
        if (maxLength is { } m && bytes.Length > m) throw new FormatException($"{field}: at most {m} bytes, got {bytes.Length}.");
        return bytes;
    }

    /// <summary>Hex, or ASCII when the text is wrapped in double quotes: "hello" is 68656C6C6F.</summary>
    public static byte[] ParseData(string? text, string field = "data", int? maxLength = null)
    {
        var t = (text ?? "").Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
        {
            var bytes = Encoding.UTF8.GetBytes(t[1..^1]);
            if (maxLength is { } m && bytes.Length > m) throw new FormatException($"{field}: at most {m} bytes, got {bytes.Length}.");
            return bytes;
        }
        return ParseBytes(t, field, null, maxLength);
    }

    public static uint ParseUInt(string? text, string field, uint max = uint.MaxValue, bool hexDefault = false)
    {
        var t = (text ?? "").Trim().Replace("_", "");
        if (t.Length == 0) throw new FormatException($"{field}: empty.");
        bool hex = hexDefault;
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { hex = true; t = t[2..]; }
        else if (t.StartsWith("$")) { hex = true; t = t[1..]; }
        if (!uint.TryParse(t, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"{field}: '{text}' is not a valid {(hex ? "hex" : "decimal")} number.");
        if (v > max) throw new FormatException($"{field}: {text} exceeds the maximum of {max}.");
        return v;
    }

    public static int ParseInt(string? text, string field)
    {
        var t = (text ?? "").Trim();
        if (!int.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"{field}: '{text}' is not a valid signed integer.");
        return v;
    }

    /// <summary>Hex number (0x prefix optional) for byte-sized fields: key numbers, file numbers, options.</summary>
    public static byte ParseByte(string? text, string field) => (byte)ParseUInt(text, field, 0xFF, hexDefault: true);
    public static ushort ParseUShort(string? text, string field) => (ushort)ParseUInt(text, field, 0xFFFF, hexDefault: true);
    public static uint ParseAid(string? text, string field = "AID") => ParseUInt(text, field, 0xFFFFFF, hexDefault: true);

    public static string Format(ReadOnlySpan<byte> data) => data.IsEmpty ? "" : Convert.ToHexString(data);

    public static string Spaced(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length * 3);
        for (int i = 0; i < data.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(data[i].ToString("X2")); }
        return sb.ToString();
    }

    /// <summary>offset  hex  ascii, 16 bytes a row.</summary>
    public static string Dump(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return "(empty)";
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            var row = data.Slice(i, Math.Min(16, data.Length - i));
            sb.Append(i.ToString("X4")).Append("  ");
            for (int j = 0; j < 16; j++)
            {
                sb.Append(j < row.Length ? row[j].ToString("X2") : "  ").Append(' ');
                if (j == 7) sb.Append(' ');
            }
            sb.Append(' ');
            foreach (var b in row) sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            if (i + 16 < data.Length) sb.AppendLine();
        }
        return sb.ToString();
    }
}
