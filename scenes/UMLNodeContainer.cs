using Godot;

public partial class UMLNodeContainer : Control
{
    [Signal]
    public delegate void DraggedEventHandler(UMLNodeContainer node, Vector2 delta);

    [Signal]
    public delegate void DroppedEventHandler(UMLNodeContainer node);

    [Signal]
    public delegate void NameChangedEventHandler(UMLNode node, string newName);

    private UMLNode umlNode = new UMLNode();
    public UMLNode UmlNode { 
        get 
        { 
            return umlNode; 
        } 
        set 
        {
            umlNode = value;
            nameLabel.Text = $"[center][b]{value.Name}[/b][/center]";
            Position = value.Position;
        }
    }

    private RichTextLabel nameLabel;
    private EditPopup editPopup;

    private bool isEnabled = true;
    private bool isHeld = false;

    public override void _Ready()
    {
        nameLabel = GetNode<RichTextLabel>("%Name");
        editPopup = GetNode<EditPopup>("%EditPopup");

        nameLabel.Text = $"[center][b]{umlNode.Name}[/b][/center]";
        nameLabel.GuiInput += OnNameLabelInput;
        editPopup.EditFinished += OnEditFinished;
    }

    public override void _Input(InputEvent @event)
    {
        if (!isEnabled)
        {
            return;
        }

        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.Pressed
            && mouseEvent.ButtonIndex == MouseButton.Left
            && GetGlobalRect().HasPoint(mouseEvent.Position))
            {
                isHeld = true;
            }
            else
            {
                isHeld = false;
                if (Position != umlNode.Position)
                {
                    EmitSignal(SignalName.Dropped, this);
                }
            }
        }
        else if (@event is InputEventMouseMotion motionEvent)
        {
            if (isHeld)
            {
                EmitSignal(SignalName.Dragged, this, motionEvent.Relative);
            }
        }
    }

    internal void ToggleInput(bool enabled)
    {
        isEnabled = enabled;
    }

    internal Vector2 GetConnectionPointPosition()
    {
        return GetGlobalRect().GetCenter();
    }

    private void OnNameLabelInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent &&
            mouseEvent.Pressed &&
            mouseEvent.DoubleClick &&
            mouseEvent.ButtonIndex == MouseButton.Left)
        {
            editPopup.ShowAtMousePosition(umlNode.Name);
        }
    }

    private void OnEditFinished(string newName)
    {
        EmitSignal(SignalName.NameChanged, umlNode, newName);
    }
}
