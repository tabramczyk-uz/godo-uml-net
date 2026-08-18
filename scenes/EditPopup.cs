using Godot;

public partial class EditPopup : LineEdit
{
    [Signal]
    public delegate void EditFinishedEventHandler(string newText);

    public override void _Ready()
    {
        FocusExited += OnFocusExited;
    }

    public void ShowAtMousePosition(string originalText)
    {
        Text = originalText;
        Position = GetViewport().GetMousePosition();
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
            if (this.Visible && keyEvent.IsActionPressed("Submit"))
            {
                EmitSignal(SignalName.EditFinished, Text);
                Hide();
            }
            else if (keyEvent.IsActionPressed("Cancel"))
            {
                Hide();
            }
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (!GetGlobalRect().HasPoint(mouseEvent.Position))
            {
                Hide();
            }
        }
    }
}
