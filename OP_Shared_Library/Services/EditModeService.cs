namespace OP_Shared_Library.Services;

public class EditModeService
{
    public bool IsEditing { get; private set; } = false;

    public event Action OnEditModeChanged;

    public void SetEditMode(bool value)
    {
        IsEditing = value;
        OnEditModeChanged?.Invoke();
    }

    public void ToggleEditMode()
    {
        IsEditing = !IsEditing;
        OnEditModeChanged?.Invoke();
    }
}
