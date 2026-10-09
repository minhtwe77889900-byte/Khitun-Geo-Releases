using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var type = Type.GetType("KhitunGeo.Native.CoordinateSelectionPanel, KhitunGeo", throwOnError: true)!;
        var factory = type.GetMethod("Picker", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Coordinate picker factory not found.");
        using var picker = (ComboBox)(factory.Invoke(null, null)
            ?? throw new InvalidOperationException("Coordinate picker was not created."));

        if (picker.DropDownStyle != ComboBoxStyle.DropDownList)
            throw new InvalidOperationException("Coordinate picker must remain a non-editable list.");
        if (picker.AutoCompleteSource != AutoCompleteSource.ListItems || picker.AutoCompleteMode != AutoCompleteMode.SuggestAppend)
            throw new InvalidOperationException("Coordinate picker autocomplete configuration is invalid.");

        Console.WriteLine("PASS native CRS picker initializes with DropDownList and item autocomplete");
    }
}
