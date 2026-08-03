using System.Windows;
using System.Windows.Input;
using ReMouse.Windows.Input;

namespace ReMouse.App;

public partial class ApplicationPickerWindow : Window
{
    public ApplicationPickerWindow(
        IReadOnlyList<WindowsApplicationCandidate> applications,
        string title = "Choose application",
        string description = "Choose an application.")
    {
        ArgumentNullException.ThrowIfNull(applications);
        InitializeComponent();
        Title = title;
        DescriptionText.Text = description;
        ApplicationsList.ItemsSource = applications;
        if (ApplicationsList.Items.Count > 0)
        {
            ApplicationsList.SelectedIndex = 0;
        }
    }

    public WindowsApplicationCandidate? SelectedApplication =>
        ApplicationsList.SelectedItem as WindowsApplicationCandidate;

    private void OnApplicationDoubleClick(object sender, MouseButtonEventArgs e) =>
        AcceptSelection();

    private void OnUseSelected(object sender, RoutedEventArgs e) =>
        AcceptSelection();

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AcceptSelection()
    {
        if (SelectedApplication is null)
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
