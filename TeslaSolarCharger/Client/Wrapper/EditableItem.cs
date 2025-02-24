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
    /// <param name="propertyNames">The names of the properties for which to clear errors.</param>
    public void ClearErrors(params string[] propertyNames)
    {
        if (propertyNames.Length == 0)
        {
            return;
        }

        foreach (var propertyName in propertyNames)
        {
            if (string.IsNullOrEmpty(propertyName)) continue;

            var fieldIdentifier = new FieldIdentifier(Item, propertyName);
            MessageStore.Clear(fieldIdentifier);
        }

        // Notify the EditContext to update the UI once after all properties are cleared
        EditContext.NotifyValidationStateChanged();
    }
}
