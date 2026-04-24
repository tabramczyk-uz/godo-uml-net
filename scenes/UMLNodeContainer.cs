using Godot;

public class UMLNodeContainer : Control
{
    [Signal]
    public delegate void Dragged(UMLNodeContainer node, Vector2 delta);

    [Signal]
    public delegate void Dropped(UMLNodeContainer node);

    [Signal]
    public delegate void NameChanged(UMLNode node, string newName);

    private UMLNode umlNode = new UMLNode();
    public UMLNode UmlNode { 
        get 
        { 
            return umlNode; 
        } 
        set 
        {
            umlNode = value;
            nameLabel.BbcodeText = $"[center][b]{value.Name}[/b][/center]";
            RectPosition = value.Position;
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

        nameLabel.BbcodeText = $"[center][b]{umlNode.Name}[/b][/center]";
        nameLabel.Connect("gui_input", this, nameof(OnNameLabelInput));
        editPopup.Connect(nameof(EditPopup.EditFinished), this, nameof(OnEditFinished));
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
            && mouseEvent.ButtonIndex == ((int)ButtonList.Left)
            && GetGlobalRect().HasPoint(mouseEvent.Position))
            {
                isHeld = true;
            }
            else
            {
                isHeld = false;
                if (RectPosition != umlNode.Position)
                {
                    EmitSignal(nameof(Dropped), this);
                }
            }
        }
        else if (@event is InputEventMouseMotion motionEvent)
        {
            if (isHeld)
            {
                EmitSignal(nameof(Dragged), this, motionEvent.Relative);
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
            mouseEvent.Doubleclick &&
            mouseEvent.ButtonIndex == ((int)ButtonList.Left))
        {
            editPopup.ShowAtMousePosition(umlNode.Name);
        }
    }

    private void OnEditFinished(string newName)
    {
        EmitSignal(nameof(NameChanged), umlNode, newName);
    }
}
