using Microsoft.AspNetCore.Components.Forms;

namespace TeslaSolarCharger.Client.Wrapper;

public class EditableItem<T>
{
    public T Item { get; set; }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public EditContext EditContext { get; set; }

    public EditableItem(T item)
    {
        Item = item;
        EditContext = new EditContext(item ?? throw new ArgumentNullException(nameof(item)));
    }
}
