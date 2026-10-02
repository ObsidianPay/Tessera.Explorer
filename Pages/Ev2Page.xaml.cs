using System.Windows;
using System.Windows.Controls;
using Tessera.Explorer.Services;
using Tessera.Nxpsc;

namespace Tessera.Explorer.Pages;

public partial class Ev2Page : UserControl, IPage
{
    private readonly CardSession _s = CardSession.Instance;

    public Ev2Page()
    {
        InitializeComponent();
        TmKey.SetKey(NxpscKeyType.Aes128, new string('0', 32), 1);
        TmAccess.Value = new NxpscAccessRights(2, 0x0F, 0x0F, 0);
        TmComm.Select(NxpscCommMode.Mac);
        InitType.Select(NxpscKeyType.Aes128);
        DamType.Select(NxpscKeyType.Aes128);
        PcKey.SetKey(NxpscKeyType.Aes128, new string('0', 32));
    }

    private void CreateTmac_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var f = Hex.ParseByte(TmFile.Text, "file no"); var comm = TmComm.Selected<NxpscCommMode>(); var access = TmAccess.Value;
        using var key = TmKey.Build();
        await _s.Run("CreateTransactionMACFile", c => c.CreateTransactionMacFile(f, comm, access, key, key.Version), $"file {f:X2}, {comm}, {Describe.Access(access)}, key v{key.Version:X2}");
    });

    private void Notify_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () => await _s.Run("NotifyTransactionSuccess", c => c.NotifyTransactionSuccess(), "sent"));

    private void InitSet_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var set = Hex.ParseByte(InitSet.Text, "key set"); var type = InitType.Selected<NxpscKeyType>();
        await _s.Run("InitializeKeySet", c => c.InitKeySet(set, type), $"set {set:X2}, {type}");
    });

    private void FinalizeSet_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var set = Hex.ParseByte(FinSet.Text, "key set"); var ver = Hex.ParseByte(FinVer.Text, "version");
        await _s.Run("FinalizeKeySet", c => c.FinalizeKeySet(set, ver), $"set {set:X2}, version {ver:X2}");
    });

    private void RollSet_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var set = Hex.ParseByte(RollSet.Text, "key set");
        await _s.Run("RollKeySet", c => c.RollKeySet(set), $"set {set:X2} active; session ended");
    });

    private void CreateDam_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var aid = Hex.ParseAid(DamAid.Text);
        var slot = Hex.ParseUShort(DamSlot.Text, "DAM slot"); var slotVer = Hex.ParseByte(DamSlotVer.Text, "slot version");
        var quota = (ushort)Hex.ParseUInt(DamQuota.Text, "quota", 0xFFFF);
        var ks = Hex.ParseByte(DamKs.Text, "key settings"); var keys = (byte)Hex.ParseUInt(DamKeys.Text, "keys", 14);
        var type = DamType.Selected<NxpscKeyType>(); var fid = Hex.ParseUShort(DamFid.Text, "ISO FID");
        var name = Hex.ParseData(DamName.Text, "DF name", 16);
        var encK = Hex.ParseBytes(DamEncK.Text, "EncK"); var mac = Hex.ParseBytes(DamMac.Text, "DAMMAC");
        await _s.Run("CreateDelegatedApplication", c => c.CreateDelegatedApplication(aid, slot, slotVer, quota, ks, keys, type, fid, name, encK, mac), $"AID {aid:X6}, slot {slot:X4}");
    });

    private void DamInfo_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var slot = Hex.ParseUShort(InfoSlot.Text, "DAM slot");
        await _s.Run("GetDelegatedInfo", c => c.GetDelegatedInfo(slot));
    });

    private void MfcMap_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var payload = Hex.ParseBytes(MfcMap.Text, "payload");
        await _s.Run("CreateMFCMapping", c => c.CreateMfcMapping(payload), Hex.Format(payload));
    });

    private void MfcRestrict_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var payload = Hex.ParseBytes(MfcRestrict.Text, "payload");
        if (!Ui.ConfirmOneWay("RestrictMFCUpdate", "The restriction cannot be lifted.")) return;
        await _s.Run("RestrictMFCUpdate", c => c.RestrictMfcUpdate(payload), Hex.Format(payload));
    });

    private void Proximity_Click(object sender, RoutedEventArgs e) => Ui.Guard(async () =>
    {
        var rounds = (byte)Hex.ParseUInt(PcRounds.Text, "rounds", 8);
        using var key = PcKey.Build();
        await _s.Run("ProximityCheck", c => c.ProximityCheck(key, rounds), $"{rounds} round(s), answer MAC verified");
    });
}
