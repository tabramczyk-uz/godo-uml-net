using Godot;
using System.Collections.Generic;
using System.Diagnostics;

public partial class VisualEditor : Control
{
	[Signal]
	public delegate void NodeNameChangedEventHandler(UMLNode node, string newName);

	[Signal]
	public delegate void NodePositionChangedEventHandler(UMLNode node, Vector2 newPosition);

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

			Vector2 fromPosition = fromContainer.GetConnectionPointPosition() - GlobalPosition;
			Vector2 toPosition = toContainer.GetConnectionPointPosition() - GlobalPosition;

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
					anchor.Scale *= 1.1f;
				}
				else if (Input.IsActionJustPressed("ZoomOut"))
				{
					anchor.Scale *= 0.9f;
				}
			}
			// TODO: Make scrolling smoother on touchpads
			else if (Input.IsActionJustPressed("ScrollUp"))
			{
				anchor.Position += ScrollSensitivity * Vector2.Up;
                QueueRedraw();
			}
			else if (Input.IsActionJustPressed("ScrollDown"))
			{
				anchor.Position += ScrollSensitivity * Vector2.Down;
                QueueRedraw();
			}
			else if (Input.IsActionJustPressed("ScrollLeft"))
			{
				anchor.Position += ScrollSensitivity * Vector2.Left;
                QueueRedraw();
			}
			else if (Input.IsActionJustPressed("ScrollRight"))
			{
				anchor.Position += ScrollSensitivity * Vector2.Right;
                QueueRedraw();
			}
		}
		else if (@event is InputEventMouseMotion motionEvent)
		{
			if (Input.IsActionPressed("Drag") || Input.IsActionPressed("AltDrag"))
			{
				anchor.Position += motionEvent.Relative / anchor.Scale;
                MouseDefaultCursorShape = CursorShape.Drag;
			} 
            else
            {
                MouseDefaultCursorShape = CursorShape.Arrow;
            }

			QueueRedraw();
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

		QueueRedraw();
	}

	private void AddUmlNode(UMLNode node)
	{
		UMLNodeContainer nodeContainer = null;

		UMLParser.NodeType nodeType = UMLParser.GetNodeType(node);
		switch (nodeType)
		{
			case UMLParser.NodeType.Class:
				nodeContainer = (UMLNodeContainer)UmlClassContainer.Instantiate();
				break;
			case UMLParser.NodeType.Node:
				nodeContainer = (UMLNodeContainer)UmlNodeContainer.Instantiate();
				break;
			default:
				GD.PushError($"Unknown node type for UMLNode: {node.Name}");
				return;
		}

		anchor.AddChild(nodeContainer);
		nodeContainer.UmlNode = node;
		nodeContainer.Dragged += OnNodeContainerDragged;
		nodeContainer.Dropped += OnNodeContainerDropped;
		nodeContainer.NameChanged += OnNodeContainerNameChanged;
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
        container.Position += delta;
        QueueRedraw();
    }

    private void OnNodeContainerDropped(UMLNodeContainer container)
    {
        if (draggedNodeContainer != container)
        {
            return;
        }

        draggedNodeContainer = null;
        EmitSignal(SignalName.NodePositionChanged, container.UmlNode, container.Position);
    }

	private void OnNodeContainerNameChanged(UMLNode node, string newName)
	{
		EmitSignal(SignalName.NodeNameChanged, node, newName);
	}
}
