using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class VisualEditor : Control
{
	public event Action<UMLNode, string> NodeNameChanged;

	public event Action<UMLNode, Vector2> NodePositionChanged;

	private static readonly PackedScene UmlClassContainer = GD.Load<PackedScene>(
			"uid://miycnuypaj3e"
	);
	private static readonly PackedScene UmlNodeContainer = GD.Load<PackedScene>(
			"uid://255l5qlme474"
	);

	[Export]
	public float ScrollSensitivity { get; set; } = 5.0f;

	private Control anchor;
	private ColorRect grayOut;

	private UMLDiagram diagram = null;
	private UMLNodeContainer draggedNodeContainer = null;
	private readonly Dictionary<UMLNode, UMLNodeContainer> containers = [];

	public override void _Ready()
	{
		anchor = GetNode<Control>("%Anchor");
		grayOut = GetNode<ColorRect>("%GrayOut");
	}

	public override void _Draw()
	{
		if (diagram == null)
		{
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

		if (@event is InputEventMouseButton)
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

		containers.Clear();

		foreach (UMLNode node in newDiagram.Nodes)
		{
			AddUmlNode(node);
		}

		QueueRedraw();
	}

	private void AddUmlNode(UMLNode node)
	{
		UMLNodeContainer nodeContainer = null;

		UMLNodeType nodeType = UMLSyntax.GetNodeType(node);
		switch (nodeType)
		{
			case UMLNodeType.Class:
				nodeContainer = (UMLNodeContainer)UmlClassContainer.Instantiate();
				break;
			case UMLNodeType.Node:
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
		NodePositionChanged?.Invoke(container.UmlNode, container.Position);
	}

	private void OnNodeContainerNameChanged(UMLNode node, string newName)
	{
		NodeNameChanged?.Invoke(node, newName);
	}
}
