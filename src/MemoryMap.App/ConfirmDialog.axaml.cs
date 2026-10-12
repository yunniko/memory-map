using Avalonia.Controls;
using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>A yes/no question; <c>ShowDialog&lt;bool&gt;</c> returns true only for the confirm button.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
        : this(new ConfirmRequest("", "", "OK"))
    {
    }

    public ConfirmDialog(ConfirmRequest request)
    {
        InitializeComponent();
        Title = request.Question;
        Question.Text = request.Question;
        Detail.Text = request.Detail;
        ConfirmButton.Content = request.ConfirmLabel;
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}
