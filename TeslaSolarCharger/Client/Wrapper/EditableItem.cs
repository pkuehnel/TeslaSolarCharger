using Microsoft.AspNetCore.Components.Forms;

namespace TeslaSolarCharger.Client.Wrapper;

public class EditableItem<T>
{
    public T Item { get; set; }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public EditContext EditContext { get; set; }
    public ValidationMessageStore MessageStore { get; }

    public EditableItem(T item)
    {
        Item = item;
        EditContext = new EditContext(item ?? throw new ArgumentNullException(nameof(item)));
        MessageStore = new ValidationMessageStore(EditContext);
        // Subscribe to the FieldChanged event
        EditContext.OnFieldChanged += (sender, args) =>
        {
            // Clear validation messages for the changed field
            MessageStore.Clear(args.FieldIdentifier);

            // Notify UI to update
            EditContext.NotifyValidationStateChanged();
        };
    }

    /// <summary>
    /// Removes all validation errors for the specified property name.
    /// </summary>
    /// <param name="propertyName">The name of the property for which to clear errors.</param>
    public void ClearErrors(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        var fieldIdentifier = new FieldIdentifier(Item, propertyName);

        // Clear validation messages for the specified property
        MessageStore.Clear(fieldIdentifier);

        // Notify the EditContext to update the UI
        EditContext.NotifyValidationStateChanged();
    }
}
