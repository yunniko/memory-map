using Avalonia.Controls;

namespace MemoryMap.App;

/// <summary>A yes/no question; <c>ShowDialog&lt;bool&gt;</c> returns true only for the confirm button.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
        : this("", "", "OK")
    {
    }

    public ConfirmDialog(string question, string detail, string confirm)
    {
        InitializeComponent();
        Title = question;
        Question.Text = question;
        Detail.Text = detail;
        ConfirmButton.Content = confirm;
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}
