# Tessera Explorer

A native Windows (WPF, .NET 10) developer tool for exploring everything
[Tessera.Nxpsc](../Tessera.Nxpsc) offers, against a real PC/SC card or
libnxpsc's mock card. Styled with the Obsidian Pay design system.

```
dotnet run
dotnet run -- --mock --seed --page files      # straight into a seeded mock card
dotnet run -- --theme light|dark|system
```

## Packages

`Tessera.Nxpsc` and `Tessera.Nxpsc.MockCard` come from the ObsidianPay GitHub Packages
feed (`nuget.config`, same feed key as `ObsidianPay/tessera`, so the credentials in
your user-level NuGet.Config apply). Version `0.5.1-ci.8` is also in the local NuGet cache.

## Layout

| Page | Library surface |
|---|---|
| Connection | reader list, `PcscTransport`, `NxpscCard.Open`, session state, cmd set, comm mode, ISO chaining, GetVersion / Identify / GetCardUID / GetFreeMemory / GetSignature, self test |
| Keys & authentication | SelectApplication, Authenticate, AuthenticateNonFirst, ChangeKey, ChangeKeyEV2, GetKeyVersion, GetKeySettings, ChangeKeySettings |
| Applications | GetApplicationIds, GetDfNames, CreateApplication (plain, ISO, full config), DeleteApplication, FormatPICC, SetPiccConfig, SetDefaultKey, SetAts, SetConfiguration |
| Files | file list with settings, Read/WriteData, value ops, record ops, Commit/Abort, CommitReaderId, Get/ChangeFileSettings, DeleteFile, create all file types |
| EV2 / EV3 | transaction MAC file, key sets, delegated apps, MFC mapping, proximity check |
| ISO 7816 & NTAG | ISO select, READ/UPDATE BINARY, Ntag424Select, ConfigureSdm |
| MIFARE Plus | authenticate, read/write, value blocks, personalisation |
| Raw command | `NxpscCard.Command` |
| Offline tools | AN10922, access rights, SDM payload, `NxpscTmac`, name lookups |
| Mock lab | everything `MockCard` exposes |

The output panel is persistent: every call is logged with timing, result, and on failure
the `NxpscErrorCode`, the card status byte and whether the PICC dropped the session.
**View > Trace APDU frames** (Ctrl+T) logs every frame; they contain challenges and
enciphered key material, so it is off by default. Keys are never written to the output.
The output panel can be docked top, bottom, left or right: drag its header to an edge, or use **View > Dock output**. The choice and size are remembered (`%AppData%\Tessera.Explorer\layout.json`).
One-way operations are marked and ask for confirmation.

## The mock card and real readers

Creating a mock card fixes libnxpsc's RNG for the whole process, after which
`NxpscCard.Open` refuses real transports. The explorer tells you and offers
**Session > Restart explorer** to switch back to a reader.
