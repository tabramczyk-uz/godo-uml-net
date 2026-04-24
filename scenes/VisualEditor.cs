using Godot;
using System.Collections.Generic;
using System.Diagnostics;

public class VisualEditor : Control
{
	[Signal]
	public delegate void NodeNameChanged(UMLNode node, string newName);

	[Signal]
	public delegate void NodePositionChanged(UMLNode node, Vector2 newPosition);

	private static readonly PackedScene UmlClassContainer = GD.Load<PackedScene>("res://scenes/UMLClassContainer.tscn");
	private static readonly PackedScene UmlNodeContainer = GD.Load<PackedScene>("res://scenes/UMLNodeContainer.tscn");

	[Export]
	public float ScrollSensitivity { get; set; } = 5.0f;

	private Control anchor;
	private ColorRect grayOut;

	private UMLDiagram diagram = null;
    private UMLNodeContainer draggedNodeContainer = null;
	private Dictionary<UMLNode, UMLNodeContainer> containers = new Dictionary<UMLNode, UMLNodeContainer>();

	public override void _Ready()
	{
		anchor = GetNode<Control>("%Anchor");
		grayOut = GetNode<ColorRect>("%GrayOut");
	}

	public override void _Draw()
	{
		if (diagram == null) {
            return;        
        }

		foreach (UMLRelationship relationship in diagram.Relationships)
		{
            Debug.Assert(relationship.From != null);
            Debug.Assert(relationship.To != null);

			UMLNodeContainer fromContainer = containers[relationship.From];
			UMLNodeContainer toContainer = containers[relationship.To];

			Vector2 fromPosition = fromContainer.GetConnectionPointPosition() - RectGlobalPosition;
			Vector2 toPosition = toContainer.GetConnectionPointPosition() - RectGlobalPosition;

			DrawLine(fromPosition, toPosition, Colors.White, 2.0f, true);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (diagram == null)
        {
            return;
        }

		if (@event is InputEventMouseButton mouseEvent)
		{
			if (Input.IsActionPressed("ZoomMode"))
			{
				if (Input.IsActionJustPressed("ZoomIn"))
				{
					anchor.RectScale *= 1.1f;
				}
				else if (Input.IsActionJustPressed("ZoomOut"))
				{
					anchor.RectScale *= 0.9f;
				}
			}
			// TODO: Make scrolling smoother on touchpads
			else if (Input.IsActionJustPressed("ScrollUp"))
			{
				anchor.RectPosition += ScrollSensitivity * Vector2.Up;
                Update();
			}
			else if (Input.IsActionJustPressed("ScrollDown"))
			{
				anchor.RectPosition += ScrollSensitivity * Vector2.Down;
                Update();
			}
			else if (Input.IsActionJustPressed("ScrollLeft"))
			{
				anchor.RectPosition += ScrollSensitivity * Vector2.Left;
                Update();
			}
			else if (Input.IsActionJustPressed("ScrollRight"))
			{
				anchor.RectPosition += ScrollSensitivity * Vector2.Right;
                Update();
			}
		}
		else if (@event is InputEventMouseMotion motionEvent)
		{
			if (Input.IsActionPressed("Drag") || Input.IsActionPressed("AltDrag"))
			{
				anchor.RectPosition += motionEvent.Relative / anchor.RectScale;
                MouseDefaultCursorShape = CursorShape.Drag;
			} 
            else
            {
                MouseDefaultCursorShape = CursorShape.Arrow;
            }

			Update();
		}
	}

	public void RenderDiagram(UMLDiagram newDiagram)
	{
		bool isDiagramRendered = newDiagram != null;
		grayOut.Visible = !isDiagramRendered;
		ToggleNodes(isDiagramRendered);

		if (!isDiagramRendered)
		{
			return;
		}

		diagram = newDiagram;

		foreach (Node child in anchor.GetChildren())
		{
			anchor.RemoveChild(child);
			child.QueueFree();
		}

		foreach (UMLNode node in newDiagram.Nodes)
		{
			AddUmlNode(node);
		}

		Update();
	}

	private void AddUmlNode(UMLNode node)
	{
		UMLNodeContainer nodeContainer = null;

		UMLParser.NodeType nodeType = UMLParser.GetNodeType(node);
		switch (nodeType)
		{
			case UMLParser.NodeType.Class:
				nodeContainer = (UMLNodeContainer)UmlClassContainer.Instance();
				break;
			case UMLParser.NodeType.Node:
				nodeContainer = (UMLNodeContainer)UmlNodeContainer.Instance();
				break;
			default:
				GD.PushError($"Unknown node type for UMLNode: {node.Name}");
				return;
		}

		anchor.AddChild(nodeContainer);
		nodeContainer.UmlNode = node;
        nodeContainer.Connect(nameof(UMLNodeContainer.Dragged), this, nameof(OnNodeContainerDragged));
		nodeContainer.Connect(nameof(UMLNodeContainer.Dropped), this, nameof(OnNodeContainerDropped));
		nodeContainer.Connect(nameof(UMLNodeContainer.NameChanged), this, nameof(OnNodeContainerNameChanged));
		containers[node] = nodeContainer;
	}

	private void ToggleNodes(bool enabled)
	{
		foreach (Node child in anchor.GetChildren())
		{
			if (child is UMLNodeContainer container)
			{
				container.ToggleInput(enabled);
			}
		}
	}

    private void OnNodeContainerDragged(UMLNodeContainer container, Vector2 delta)
    {
        if (draggedNodeContainer != null && draggedNodeContainer != container)
        {
            return;
        }

        draggedNodeContainer = container;
        container.RectPosition += delta;
        EmitSignal(nameof(NodePositionChanged), container.UmlNode, container.RectPosition);
        Update();
    }

    private void OnNodeContainerDropped(UMLNodeContainer container)
    {
        if (draggedNodeContainer != container)
        {
            return;
        }

        draggedNodeContainer = null;
    }

	private void OnNodeContainerNameChanged(UMLNode node, string newName)
	{
		EmitSignal(nameof(NodeNameChanged), node, newName);
	}
}
