using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MemoryMap.App;

/// <summary>Change notification shared by the view models: a property is announced only when it changes.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Announces <paramref name="name"/>; an empty name tells bindings every property changed.</summary>
    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Returns whether the value changed.</summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        Raise(name!);
        return true;
    }

    // Items are records: an unchanged list keeps the old instance, so the view does not rebuild its items.
    protected void SetList<T>(ref IReadOnlyList<T> field, IReadOnlyList<T> value, [CallerMemberName] string? name = null)
    {
        if (!field.SequenceEqual(value))
        {
            Set(ref field, value, name);
        }
    }
}
