using System;
using Godot;

public partial class EditPopup : LineEdit
{
    public event Action<string> EditFinished;

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
            if (Visible && keyEvent.IsActionPressed("Submit"))
            {
                EditFinished?.Invoke(Text);
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
