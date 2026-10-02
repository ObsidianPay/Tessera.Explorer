using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class FilesPage : UserControl, IPage
{
    private sealed record FileRow(string No, string Type, string Comm, string Size, NxpscFileSettings? Settings, byte FileNo);

    private readonly CardSession _s = CardSession.Instance;
    private static readonly string[] CreateTypes = ["Standard data file", "Backup data file", "Value file", "Linear record file", "Cyclic record file"];

    public FilesPage()
    {
        InitializeComponent();
        CreateType.ItemsSource = CreateTypes;
        CreateType.SelectedIndex = 0;
    }

    private uint _scannedAid = uint.MaxValue;

    void IPage.OnShown()
    {
        AppLabel.Text = $"application {_s.State.SelectedAid:X6}";
        // scan once per selected application, so the explorer opens populated
        if (_s.IsConnected && _scannedAid != _s.State.SelectedAid)
        {
            _scannedAid = _s.State.SelectedAid;
            Scan_Click(this, null!);
        }
    }
    void IPage.OnStateChanged() => AppLabel.Text = $"application {_s.State.SelectedAid:X6}";

    private byte File() => Hex.ParseByte(FileNo.Text, "file no");
    private NxpscCommMode CommMode() => Comm.Selected<NxpscCommMode>();

    // ---- list ----------------------------------------------------------------------------------------------

    private static string Summary(NxpscFileSettings f) => f.Type switch
    {
        NxpscFileType.Standard or NxpscFileType.Backup => f.Size + " B",
        NxpscFileType.Value => $"val {f.Value}",
        NxpscFileType.LinearRecord or NxpscFileType.CyclicRecord => $"{f.CurrentRecords}/{f.MaxRecords} x {f.RecordSize}",
        _ => "",
    };

    private void Scan_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var ids = await _s.Run("GetFileIds", c => c.GetFileIds(), b => b.Length == 0 ? "(none)" : string.Join("  ", b.Select(x => x.ToString("X2"))));
        if (!ids.Ok || ids.Value is null) return;
        var rows = new List<FileRow>();
        foreach (var id in ids.Value)
        {
            var r = await _s.Run("GetFileSettings", c => c.GetFileSettings(id), Describe.FileSettings, quiet: true);
            rows.Add(r.Ok && r.Value is { } f
                ? new FileRow(id.ToString("X2"), f.Type.ToString(), f.CommMode.ToString(), Summary(f), f, id)
                : new FileRow(id.ToString("X2"), "?", "", "settings unreadable", null, id));
        }
        FileGrid.ItemsSource = rows;
        if (rows.Count > 0) OutputLog.Info("Files", string.Join("\n", rows.Where(r => r.Settings is not null).Select(r => $"{r.No}  {Describe.FileSettings(r.Settings!)}")));
    });

    private void IsoIds_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("GetIsoFileIds", c => c.GetIsoFileIds()));

    private void FileGrid_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (FileGrid.SelectedItem is not FileRow row) return;
        FileNo.Text = row.No;
        if (row.Settings is { } f)
        {
            Comm.Select(f.CommMode);
            ChangeAccess.Value = f.Access;
            FileSummary.Text = Describe.FileSettings(f);
        }
        else FileSummary.Text = "";
    }

    // ---- data ------------------------------------------------------------------------------------------------

    private void ReadData_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        var offset = Hex.ParseUInt(DataOffset.Text, "offset"); var length = Hex.ParseUInt(DataLength.Text, "length");
        await _s.Run("ReadData", c => c.ReadData(f, offset, length, comm));
    });

    private void WriteData_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        var offset = Hex.ParseUInt(DataOffset.Text, "offset"); var data = Hex.ParseData(DataBytes.Text);
        await _s.Run("WriteData", c => c.WriteData(f, offset, data, comm), $"file {f:X2} offset {offset}: {data.Length} bytes");
    });

    // ---- values ------------------------------------------------------------------------------------------------

    private void GetValue_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        await _s.Run("GetValue", c => c.GetValue(f, comm));
    });

    private void Credit_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode(); var d = Hex.ParseInt(ValueDelta.Text, "delta");
        await _s.Run("Credit", c => c.Credit(f, d, comm), $"file {f:X2} {d:+#;-#;0} (staged, commit to apply)");
    });

    private void LimitedCredit_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode(); var d = Hex.ParseInt(ValueDelta.Text, "delta");
        await _s.Run("LimitedCredit", c => c.LimitedCredit(f, d, comm), $"file {f:X2} {d:+#;-#;0} (staged, commit to apply)");
    });

    private void Debit_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode(); var d = Hex.ParseInt(ValueDelta.Text, "delta");
        await _s.Run("Debit", c => c.Debit(f, d, comm), $"file {f:X2} -{d} (staged, commit to apply)");
    });

    // ---- records -----------------------------------------------------------------------------------------------

    private void ReadRecords_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        var no = Hex.ParseUInt(RecNo.Text, "record no"); var count = Hex.ParseUInt(RecCount.Text, "count");
        await _s.Run("ReadRecords", c => c.ReadRecords(f, no, count, comm));
    });

    private void WriteRecord_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        var offset = Hex.ParseUInt(RecOffset.Text, "offset"); var data = Hex.ParseData(RecData.Text);
        await _s.Run("WriteRecord", c => c.WriteRecord(f, offset, data, comm), $"file {f:X2}: {data.Length} bytes staged");
    });

    private void UpdateRecord_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode();
        var no = Hex.ParseUInt(RecNo.Text, "record no"); var offset = Hex.ParseUInt(RecOffset.Text, "offset"); var data = Hex.ParseData(RecData.Text);
        await _s.Run("UpdateRecord", c => c.UpdateRecord(f, no, offset, data, comm), $"file {f:X2} record {no}: {data.Length} bytes staged");
    });

    private void ClearRecords_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File();
        if (!Ui.Confirm("Clear record file", $"ClearRecordFile {f:X2} empties the file once committed.", "Clear")) return;
        await _s.Run("ClearRecordFile", c => c.ClearRecordFile(f), $"file {f:X2} (staged, commit to apply)");
    });

    // ---- transactions ----------------------------------------------------------------------------------------------

    private void Commit_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("CommitTransaction", c => c.CommitTransaction(), "committed"));
    private void CommitMac_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("CommitTransaction+MAC", c => c.CommitTransactionWithMac()));
    private void Abort_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("AbortTransaction", c => c.AbortTransaction(), "aborted"));

    private void CommitReaderId_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var id = Hex.ParseBytes(ReaderId.Text, "reader id", 16);
        await _s.Run("CommitReaderID", c => c.CommitReaderId(id), b => $"previous reader id (enciphered): {Hex.Format(b)}");
    });

    // ---- settings ------------------------------------------------------------------------------------------------------

    private void GetSettings_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File();
        var r = await _s.Run("GetFileSettings", c => c.GetFileSettings(f), Describe.FileSettings);
        if (r.Ok && r.Value is { } s)
        {
            Comm.Select(s.CommMode);
            ChangeAccess.Value = s.Access;
            FileSummary.Text = Describe.FileSettings(s);
        }
    });

    private void ChangeSettings_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File();
        if (!string.IsNullOrWhiteSpace(ChangeRaw.Text))
        {
            var payload = Hex.ParseBytes(ChangeRaw.Text, "payload");
            if (!Ui.ConfirmOneWay("ChangeFileSettings", $"Raw payload {Hex.Format(payload)} goes to file {f:X2} verbatim.")) return;
            await _s.Run("ChangeFileSettings(raw)", c => c.ChangeFileSettings(f, payload), Hex.Format(payload));
            return;
        }
        var comm = CommMode(); var access = ChangeAccess.Value;
        if (access.Change == NxpscAccessRights.Denied &&
            !Ui.ConfirmOneWay("ChangeFileSettings", "Change right 'denied' freezes this file's settings.")) return;
        await _s.Run("ChangeFileSettings", c => c.ChangeFileSettings(f, comm, access), $"file {f:X2} comm {comm} {Describe.Access(access)}");
    });

    private void DeleteFile_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File();
        if (!Ui.Confirm("Delete file", $"DeleteFile {f:X2} removes the file and its contents.", "Delete")) return;
        await _s.Run("DeleteFile", c => c.DeleteFile(f), $"file {f:X2}");
    });

    // ---- create ------------------------------------------------------------------------------------------------------------

    private void CreateType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SizePanel is null) return;
        var i = CreateType.SelectedIndex;
        SizePanel.Visibility = i <= 1 ? Visibility.Visible : Visibility.Collapsed;
        ValuePanel.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        RecordPanel.Visibility = i >= 3 ? Visibility.Visible : Visibility.Collapsed;
        CreateFid.IsEnabled = i != 2;
    }

    private void CreateFile_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = File(); var comm = CommMode(); var access = CreateAccess.Value;
        var fid = Hex.ParseUShort(CreateFid.Text, "ISO FID");
        switch (CreateType.SelectedIndex)
        {
            case 0:
            {
                var size = Hex.ParseUInt(CreateSize.Text, "size");
                await _s.Run("CreateStdFile", c => c.CreateStdFile(f, comm, access, size, fid), $"file {f:X2}, {size} B, {comm}, {Describe.Access(access)}");
                break;
            }
            case 1:
            {
                var size = Hex.ParseUInt(CreateSize.Text, "size");
                await _s.Run("CreateBackupFile", c => c.CreateBackupFile(f, comm, access, size, fid), $"file {f:X2}, {size} B, {comm}, {Describe.Access(access)}");
                break;
            }
            case 2:
            {
                var lo = Hex.ParseInt(ValLower.Text, "lower limit"); var hi = Hex.ParseInt(ValUpper.Text, "upper limit"); var v = Hex.ParseInt(ValInitial.Text, "initial value");
                var limited = ValLimited.IsChecked == true;
                await _s.Run("CreateValueFile", c => c.CreateValueFile(f, comm, access, lo, hi, v, limited), $"file {f:X2}, [{lo}, {hi}], value {v}, {comm}");
                break;
            }
            default:
            {
                var size = Hex.ParseUInt(RecSize.Text, "record size"); var max = Hex.ParseUInt(RecMax.Text, "max records");
                var cyclic = CreateType.SelectedIndex == 4;
                await _s.Run("CreateRecordFile", c => c.CreateRecordFile(f, cyclic, comm, access, size, max, fid),
                    $"file {f:X2}, {(cyclic ? "cyclic" : "linear")}, {max} x {size} B, {comm}");
                break;
            }
        }
    });
}
