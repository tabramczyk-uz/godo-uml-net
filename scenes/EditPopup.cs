using Godot;

public class EditPopup : LineEdit
{
    [Signal]
    public delegate void EditFinished(string newText);

    public override void _Ready()
    {
        Connect("focus_exited", this, nameof(OnFocusExited));
    }

    public void ShowAtMousePosition(string originalText)
    {
        Text = originalText;
        RectPosition = GetViewport().GetMousePosition();
        Show();
        GrabFocus();
        SelectAll();
    }

    private void OnFocusExited()
    {
        Hide();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.IsActionPressed("Submit"))
            {
                EmitSignal(nameof(EditFinished), Text);
                Hide();
            }
            else if (keyEvent.IsActionPressed("Cancel")) 
            {
                Hide();
            }
        }
        else if (@event is InputEventMouseButton mouseEvent &&
                 mouseEvent.Pressed)
        {
            if (!GetGlobalRect().HasPoint(mouseEvent.Position))
            {
                Hide();
            }
        }
    }
}
