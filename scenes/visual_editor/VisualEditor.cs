using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

public partial class VisualEditor : Control
{
	public event Action<UMLNode, string> NodeNameChanged;

	public event Action<UMLNode, Vector2> NodePositionChanged;

	private const float EndingLength = 16.0f;
	private const float EndingHalfWidth = 7.0f;
	private const float LabelMargin = 4.0f;

	private static readonly PackedScene UmlClassContainer = GD.Load<PackedScene>(
			"uid://miycnuypaj3e"
	);
	private static readonly PackedScene UmlNodeContainer = GD.Load<PackedScene>(
			"uid://255l5qlme474"
	);

	[Export]
	public float ScrollSensitivity { get; set; } = 5.0f;

	[Export]
	private Color BackgroundColor = new(0.180392f, 0.180392f, 0.180392f);

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

			DrawRelationship(relationship);
		}
	}

	private void DrawRelationship(UMLRelationship relationship)
	{
		UMLNodeContainer fromContainer = containers[relationship.From];
		UMLNodeContainer toContainer = containers[relationship.To];

		Rect2 fromRect = ToLocalRect(fromContainer.GetGlobalRect());
		Rect2 toRect = ToLocalRect(toContainer.GetGlobalRect());
		Vector2 fromCenter = fromRect.GetCenter();
		Vector2 toCenter = toRect.GetCenter();

		Vector2 fromEdge = ClipToRect(fromRect, fromCenter, toCenter);
		Vector2 toEdge = ClipToRect(toRect, toCenter, fromCenter);

		Vector2 delta = toEdge - fromEdge;
		if (delta.LengthSquared() < 0.0001f)
		{
			return;
		}

		Vector2 direction = delta.Normalized();

		float fromEndingLength = GetEndingLength(relationship.FromEnding);
		float toEndingLength = GetEndingLength(relationship.ToEnding);

		Vector2 lineStart = fromEdge + direction * fromEndingLength;
		Vector2 lineEnd = toEdge - direction * toEndingLength;

		if (relationship.IsDashed)
		{
			DrawDashedLine(lineStart, lineEnd, Colors.White, 2.0f, 6.0f);
		}
		else
		{
			DrawLine(lineStart, lineEnd, Colors.White, 2.0f, true);
		}

		DrawEnding(relationship.FromEnding, fromEdge, direction);
		DrawEnding(relationship.ToEnding, toEdge, -direction);

		Vector2 perpendicular = new(-direction.Y, direction.X);

		if (!string.IsNullOrEmpty(relationship.Label))
		{
			DrawText(relationship.Label, (fromEdge + toEdge) / 2.0f + perpendicular * LabelMargin);
		}

		if (!string.IsNullOrEmpty(relationship.FromMultiplicity))
		{
			DrawText(
					relationship.FromMultiplicity,
					fromEdge
							+ direction * (fromEndingLength + LabelMargin)
							+ perpendicular * LabelMargin
			);
		}

		if (!string.IsNullOrEmpty(relationship.ToMultiplicity))
		{
			DrawText(
					relationship.ToMultiplicity,
					toEdge - direction * (toEndingLength + LabelMargin) + perpendicular * LabelMargin
			);
		}
	}

	private void DrawText(string text, Vector2 position)
	{
		Font font = GetThemeDefaultFont();
		int fontSize = GetThemeDefaultFontSize();
		DrawString(font, position, text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
	}

	/// <summary>
	/// Draws the shape a relationship's ending calls for, with its tip touching
	/// the node at <paramref name="tip"/> and its body spreading out along
	/// <paramref name="outward"/>, the direction away from that node.
	/// </summary>
	private void DrawEnding(UMLRelationshipEnding ending, Vector2 tip, Vector2 outward)
	{
		if (ending == UMLRelationshipEnding.None)
		{
			return;
		}

		Vector2 perpendicular = new(-outward.Y, outward.X);

		if (ending == UMLRelationshipEnding.OpenArrow)
		{
			Vector2 baseCenter = tip + outward * EndingLength;
			DrawLine(tip, baseCenter + perpendicular * EndingHalfWidth, Colors.White, 2.0f, true);
			DrawLine(tip, baseCenter - perpendicular * EndingHalfWidth, Colors.White, 2.0f, true);
			return;
		}

		bool filled = ending == UMLRelationshipEnding.FilledDiamond;
		Vector2[] points =
				ending == UMLRelationshipEnding.HollowDiamond
				|| ending == UMLRelationshipEnding.FilledDiamond
						?
						[
								tip,
										tip + outward * (EndingLength / 2.0f) + perpendicular * EndingHalfWidth,
										tip + outward * EndingLength,
										tip + outward * (EndingLength / 2.0f) - perpendicular * EndingHalfWidth,
						]
						:
						[
								tip,
										tip + outward * EndingLength + perpendicular * EndingHalfWidth,
										tip + outward * EndingLength - perpendicular * EndingHalfWidth,
						];

		if (filled)
		{
			DrawColoredPolygon(points, Colors.White);
		}
		else
		{
			DrawColoredPolygon(points, BackgroundColor);
			DrawPolyline([.. points, points[0]], Colors.White, 2.0f, true);
		}
	}

	private static float GetEndingLength(UMLRelationshipEnding ending)
	{
		return ending == UMLRelationshipEnding.None ? 0.0f : EndingLength;
	}

	private Rect2 ToLocalRect(Rect2 globalRect)
	{
		return new Rect2(globalRect.Position - GlobalPosition, globalRect.Size);
	}

	/// <summary>
	/// Finds the point where the segment from <paramref name="origin"/> (inside
	/// <paramref name="rect"/>) toward <paramref name="towards"/> leaves the
	/// rect, so relationship lines and their endings start at the node's edge
	/// instead of its center.
	/// </summary>
	private static Vector2 ClipToRect(Rect2 rect, Vector2 origin, Vector2 towards)
	{
		Vector2 direction = towards - origin;
		float bestT = 1.0f;

		if (direction.X != 0.0f)
		{
			float left = (rect.Position.X - origin.X) / direction.X;
			float right = (rect.Position.X + rect.Size.X - origin.X) / direction.X;
			foreach (float t in new[] { left, right })
			{
				if (t > 0.0f && t < bestT)
				{
					float y = origin.Y + direction.Y * t;
					if (y >= rect.Position.Y && y <= rect.Position.Y + rect.Size.Y)
					{
						bestT = t;
					}
				}
			}
		}

		if (direction.Y != 0.0f)
		{
			float top = (rect.Position.Y - origin.Y) / direction.Y;
			float bottom = (rect.Position.Y + rect.Size.Y - origin.Y) / direction.Y;
			foreach (float t in new[] { top, bottom })
			{
				if (t > 0.0f && t < bestT)
				{
					float x = origin.X + direction.X * t;
					if (x >= rect.Position.X && x <= rect.Position.X + rect.Size.X)
					{
						bestT = t;
					}
				}
			}
		}

		return origin + direction * bestT;
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
