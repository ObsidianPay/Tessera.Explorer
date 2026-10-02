using System.Text;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Services;

/// <summary>Renders library results as the plain, hex-first text a protocol developer wants in the log.</summary>
public static class Describe
{
    public static string? Auto(object? value) => value switch
    {
        null => null,
        byte[] b => $"{b.Length} bytes\n{Hex.Dump(b)}",
        string s => s,
        uint u => $"0x{u:X}  ({u})",
        int i => $"{i}  (0x{i:X})",
        byte b => $"0x{b:X2}  ({b})",
        ushort us => $"0x{us:X4}  ({us})",
        bool flag => flag ? "true" : "false",
        uint[] aids => aids.Length == 0 ? "(none)" : string.Join("  ", aids.Select(a => a.ToString("X6"))),
        ushort[] fids => fids.Length == 0 ? "(none)" : string.Join("  ", fids.Select(a => a.ToString("X4"))),
        NxpscCardType t => NxpscLibrary.CardTypeName(t),
        CardVersion v => Version(v),
        NxpscKeySettings ks => KeySettings(ks),
        NxpscFileSettings fs => FileSettings(fs),
        TransactionMac tm => $"TMC {Hex.Format(tm.Tmc)} (counter {tm.Counter})\nTMV {Hex.Format(tm.Tmv)}",
        NxpscDelegateInfo d => $"DAMSlotVersion 0x{d.DamSlotVersion:X2}  quota {d.QuotaLimit}  free blocks {d.FreeBlocks}  AID {d.Aid:X6}",
        _ => value.ToString(),
    };

    public static string Version(CardVersion v)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HW  vendor 0x{v.HwVendor:X2}  type 0x{v.HwType:X2}  subtype 0x{v.HwSubtype:X2}  version {v.HwMajor}.{v.HwMinor}  storage 0x{v.HwStorage:X2}  protocol 0x{v.HwProtocol:X2}");
        sb.AppendLine($"SW  vendor 0x{v.SwVendor:X2}  type 0x{v.SwType:X2}  subtype 0x{v.SwSubtype:X2}  version {v.SwMajor}.{v.SwMinor}  storage 0x{v.SwStorage:X2}  protocol 0x{v.SwProtocol:X2}");
        sb.AppendLine($"UID {Hex.Format(v.Uid)}");
        sb.Append($"Batch {Hex.Format(v.Batch)}  week {v.ProductionWeek:X2}  year {v.ProductionYear:X2}{(v.HasBatchExtra ? "  (extended batch)" : "")}");
        return sb.ToString();
    }

    public static string KeySettings(NxpscKeySettings k) =>
        $"settings 0x{k.Settings:X2} ({Bits(k.Settings)})  keys {k.NumKeys}  type {NxpscLibrary.KeyTypeName(k.KeyType)}";

    public static string Access(NxpscAccessRights a) =>
        $"R={Key(a.Read)} W={Key(a.Write)} RW={Key(a.ReadWrite)} CAR={Key(a.Change)}  (0x{a.Pack():X4})";

    public static string Key(byte k) => k switch { 0x0E => "free", 0x0F => "denied", _ => $"key {k:X}" };

    public static string FileSettings(NxpscFileSettings f)
    {
        var sb = new StringBuilder();
        sb.Append($"type {f.Type}  comm {f.CommMode}  options 0x{f.Options:X2}  access {Access(f.Access)}");
        switch (f.Type)
        {
            case NxpscFileType.Standard:
            case NxpscFileType.Backup:
                sb.Append($"  size {f.Size}");
                break;
            case NxpscFileType.Value:
                sb.Append($"  limits [{f.LowerLimit}, {f.UpperLimit}]  value {f.Value}  limitedCredit {f.LimitedCredit}");
                break;
            case NxpscFileType.LinearRecord:
            case NxpscFileType.CyclicRecord:
                sb.Append($"  recordSize {f.RecordSize}  records {f.CurrentRecords}/{f.MaxRecords}");
                break;
        }
        if (f.SdmEnabled) sb.Append($"  SDM on (options 0x{f.SdmOptions:X})");
        return sb.ToString();
    }

    public static string Bits(byte b) => Convert.ToString(b, 2).PadLeft(8, '0');

    public static string Aid(uint aid) => aid.ToString("X6");

    public static string Status(byte status) => $"0x{status:X2} {NxpscLibrary.StatusText(status)}";
}
