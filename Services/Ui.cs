using Tessera.Explorer.Dialogs;

namespace Tessera.Explorer.Services;

public static class Ui
{
    /// <summary>Runs a handler body; input mistakes (bad hex, out of range) go to the output instead of crashing.</summary>
    public static async void Guard(Func<Task> body)
    {
        try { await body(); }
        catch (FormatException ex) { OutputLog.Error("input", ex.Message); }
        catch (ArgumentException ex) { OutputLog.Error("input", ex.Message); }
        catch (OperationCanceledException) { OutputLog.Warn("cancelled", "Operation cancelled."); }
        catch (Exception ex) { OutputLog.Error(ex.GetType().Name, ex.Message); }
    }

    public static bool Confirm(string title, string message, string confirmText = "Send it", bool danger = true) =>
        new ConfirmDialog(title, message, confirmText, danger, showCancel: true).ShowDialog() == true;

    public static void Notice(string title, string message) =>
        new ConfirmDialog(title, message, "OK", danger: false, showCancel: false).ShowDialog();

    /// <summary>Gate for one-way card operations.</summary>
    public static bool ConfirmOneWay(string operation, string consequence) =>
        Confirm($"{operation} is one-way", consequence + "\n\nThere is no command to undo this. Send it only to a card you are prepared to lose.", "Send to card");
}
