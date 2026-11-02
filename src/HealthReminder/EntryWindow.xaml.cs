using System.Windows;

namespace Strongthium.HealthReminder;

/// <summary>
/// A line typed under What that could be read more than one way (<see cref="GroceryEntry"/>): how it was read, to put
/// right or to keep as it was typed. <see cref="Result"/> is what it's to be.
/// </summary>
public sealed partial class EntryWindow : Window
{
    private readonly string typed, typedQuantity;

    public GroceryEntry? Result { get; private set; }

    public EntryWindow(string typed, string typedQuantity, GroceryEntry entry)
    {
        InitializeComponent();
        Icon = App.Icon;
        (this.typed, this.typedQuantity) = (typed.Trim(), typedQuantity.Trim());
        TypedText.Text = this.typed;
        NameBox.Text = entry.Name;
        NoteBox.Text = entry.Note ?? "";
        QuantityBox.Text = entry.Quantity;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Right_Click(object sender, RoutedEventArgs e)
    {
        if (NameBox.Text.Trim().Length == 0)
        {
            NameBox.Focus();
            return;
        }
        Result = new GroceryEntry(NameBox.Text.Trim(), NoteBox.Text.Trim() is { Length: > 0 } note ? note : null, QuantityBox.Text.Trim(), true);
        DialogResult = true;
    }

    private void AsTyped_Click(object sender, RoutedEventArgs e)
    {
        Result = new GroceryEntry(typed, null, typedQuantity, true);
        DialogResult = true;
    }
}
