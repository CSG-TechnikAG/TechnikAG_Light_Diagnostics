using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TechnikAG_Light_Diagnostics.Models;
using TechnikAG_Light_Diagnostics.Services;

namespace TechnikAG_Light_Diagnostics.Dialogs;

public partial class SelectionDialog : Window
{
    private readonly IReadOnlyList<SelectionOption> _options;

    public IReadOnlyList<string> SelectedValues { get; private set; } = Array.Empty<string>();

    public SelectionDialog(
        string windowTitle,
        string header,
        string subHeader,
        IEnumerable<SelectionOption> options,
        bool multiSelect,
        string confirmText = "Continue",
        string? note = null)
    {
        InitializeComponent();

        _options = options.ToList();

        Title = windowTitle;
        HeaderText.Text = header;
        SubText.Text = subHeader;
        ConfirmButton.Content = confirmText;

        ItemsList.ItemsSource = _options;
        ItemsList.SelectionMode = multiSelect ? SelectionMode.Extended : SelectionMode.Single;
        SelectAllButton.Visibility = multiSelect ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(note))
        {
            NoteText.Text = note;
            NoteText.Visibility = Visibility.Visible;
        }

        if (_options.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
        }

        ConfirmButton.Click += (_, _) => ConfirmSelection();
        CancelButton.Click += (_, _) => DialogResult = false;
        ItemsList.MouseDoubleClick += OnItemsDoubleClick;

        Loaded += (_, _) => ItemsList.Focus();
        SourceInitialized += (_, _) => WindowAppearance.EnableDarkTitleBar(this);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ItemsList.SelectAll();

    private void OnItemsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItems.Count > 0)
        {
            ConfirmSelection();
        }
    }

    private void ConfirmSelection()
    {
        var selected = ItemsList.SelectedItems
            .OfType<SelectionOption>()
            .Select(option => option.Value)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Please select at least one option.",
                "Nothing selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedValues = selected;
        DialogResult = true;
    }
}
