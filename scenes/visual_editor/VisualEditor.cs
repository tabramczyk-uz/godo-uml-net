using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Godot;

public partial class VisualEditor : Control
{
	public event Action<UMLNode, string> NodeNameChanged;

	public event Action<UMLNode, Vector2> NodePositionChanged;

	/// <summary>
	/// Raised when the Add menu asks for a new node of the given type, name and
	/// position. The node only appears once it has been written into the code.
	/// </summary>
	public event Action<UMLNodeType, string, Vector2> NodeAdded;

	/// <summary>Ids of the View menu's items, as set in the scene.</summary>
	private enum ViewMenuItem
	{
		ZoomIn,
		ZoomOut,
		ResetZoom,
		FrameDiagram,
	}

	private const float EndingLength = 16.0f;
	private const float EndingHalfWidth = 7.0f;
	private const float LabelMargin = 4.0f;
	private const float FrameMargin = 32.0f;

	/// <summary>
	/// How far each new node is nudged from the previous one when several are
	/// added in the same spot, so they cascade instead of stacking.
	/// </summary>
	private static readonly Vector2 NewNodeOffset = new(24.0f, 24.0f);

	private static readonly int[] ZoomLevels = [
		20, 30, 40, 50, 60, 70, 80, 90, // 0-7
		100, // 8
		110, 120, 130, 140, 150, 160, 170, 180, 190, // 9-17
		200, // 18
		220, 240, 260, 280, // 19-22
		300, // 23
	];
	private const int DefaultZoomLevel = 8;

	[Export]
	public float ScrollSensitivity { get; set; } = 5.0f;

	[Export]
	private Color BackgroundColor = new(0.180392f, 0.180392f, 0.180392f);

	private Control anchor;
	private ColorRect grayOut;
	private Control menuPanel;
	private MenuBar menuBar;
	private PopupMenu addMenu;

	private UMLDiagram diagram = null;
	private UMLNodeContainer draggedNodeContainer = null;
	private readonly Dictionary<UMLNode, UMLNodeContainer> containers = [];

	/// <summary>
	/// Where each auto-positioned node sits when nothing pushes it, by name: the
	/// spot the layered layout gave it when it first appeared. Every push is
	/// computed afresh from these, so where a node is shown depends only on where
	/// the other nodes are now, not on how they got there. Keeping them here also
	/// stops a re-parse from reshuffling the layout when the set of auto-positioned
	/// nodes changes.
	/// </summary>
	private readonly Dictionary<string, Vector2> restPositions = [];

	private int zoomLevel = DefaultZoomLevel;
	private int Zoom
	{
		get => zoomLevel;
		set => SetZoom(value, GetLocalMousePosition());
	}

	public override void _Ready()
	{
		anchor = GetNode<Control>("%Anchor");
		grayOut = GetNode<ColorRect>("%GrayOut");
		menuPanel = GetNode<Control>("%MenuPanel");
		menuBar = GetNode<MenuBar>("%MenuBar");
		addMenu = GetNode<PopupMenu>("%Add");

		foreach (UMLNodeType type in Enum.GetValues<UMLNodeType>())
		{
			addMenu.AddItem(GetDisplayName(type), (int)type);
		}

		addMenu.IdPressed += OnAddMenuIdPressed;
		GetNode<PopupMenu>("%View").IdPressed += OnViewMenuIdPressed;
	}

	/// <summary>
	/// Changes the zoom level while keeping the canvas point under
	/// <paramref name="pivot"/> where it is.
	/// </summary>
	private void SetZoom(int level, Vector2 pivot)
	{
		zoomLevel = Math.Clamp(level, 0, ZoomLevels.Length - 1);

		float zoom = ZoomLevels[zoomLevel] / 100f;
		Vector2 anchorLocalPivot = (pivot - anchor.Position) / anchor.Scale;
		anchor.Scale = Vector2.One * zoom;
		anchor.Position = pivot - anchorLocalPivot * anchor.Scale;
	}

	/// <summary>
	/// The part of the editor the diagram is visible in, below the menu bar.
	/// </summary>
	private Rect2 GetCanvasRect()
	{
		float top = menuPanel.Size.Y;
		return new Rect2(0.0f, top, Size.X, Mathf.Max(Size.Y - top, 0.0f));
	}

	/// <summary>
	/// Spells an enum name out for a menu, e.g. <c>AbstractClass</c> as
	/// <c>Abstract Class</c>.
	/// </summary>
	private static string GetDisplayName(UMLNodeType type)
	{
		string name = type.ToString();
		var displayName = new StringBuilder();
		for (int i = 0; i < name.Length; i++)
		{
			if (i > 0 && char.IsUpper(name[i]))
			{
				displayName.Append(' ');
			}

			displayName.Append(name[i]);
		}

		return displayName.ToString();
	}

	private void OnAddMenuIdPressed(long id)
	{
		if (diagram == null)
		{
			return;
		}

		UMLNodeType type = (UMLNodeType)id;
		string name = diagram.GetUniqueNodeName(type.ToString());
		NodeAdded?.Invoke(type, name, GetNewNodePosition());
	}

	/// <summary>
	/// Where a node added from the menu goes: the middle of the visible canvas,
	/// nudged along until it no longer lands exactly on another node.
	/// </summary>
	private Vector2 GetNewNodePosition()
	{
		Vector2 position = ((GetCanvasRect().GetCenter() - anchor.Position) / anchor.Scale).Round();
		while (IsNodeAt(position))
		{
			position += NewNodeOffset;
		}

		return position;
	}

	private bool IsNodeAt(Vector2 position)
	{
		foreach (UMLNodeContainer container in containers.Values)
		{
			if (container.Position.DistanceTo(position) < NewNodeOffset.X / 2.0f)
			{
				return true;
			}
		}

		return false;
	}

	private void OnViewMenuIdPressed(long id)
	{
		Vector2 canvasCenter = GetCanvasRect().GetCenter();
		switch ((ViewMenuItem)id)
		{
			case ViewMenuItem.ZoomIn:
				SetZoom(zoomLevel + 1, canvasCenter);
				break;
			case ViewMenuItem.ZoomOut:
				SetZoom(zoomLevel - 1, canvasCenter);
				break;
			case ViewMenuItem.ResetZoom:
				SetZoom(DefaultZoomLevel, canvasCenter);
				break;
			case ViewMenuItem.FrameDiagram:
				FrameDiagram();
				break;
		}

		QueueRedraw();
	}

	/// <summary>
	/// Centers the diagram on the canvas at the largest zoom level, up to 100%,
	/// that fits all of it.
	/// </summary>
	private void FrameDiagram()
	{
		Rect2 canvas = GetCanvasRect();
		if (containers.Count == 0)
		{
			SetZoom(DefaultZoomLevel, canvas.GetCenter());
			anchor.Position = canvas.Position;
			return;
		}

		Rect2? bounds = null;
		foreach (UMLNodeContainer container in containers.Values)
		{
			Rect2 box = new(container.Position, GetSize(container));
			bounds = bounds?.Merge(box) ?? box;
		}

		Vector2 available = canvas.Size - 2.0f * FrameMargin * Vector2.One;
		int level = DefaultZoomLevel;
		while (level > 0 && !FitsIn(bounds.Value.Size * (ZoomLevels[level] / 100f), available))
		{
			level--;
		}

		SetZoom(level, Vector2.Zero);
		anchor.Position = canvas.GetCenter() - bounds.Value.GetCenter() * anchor.Scale;
	}

	private static bool FitsIn(Vector2 size, Vector2 available)
	{
		return size.X <= available.X && size.Y <= available.Y;
	}

	public override void _Draw()
	{
		if (diagram == null)
		{
			return;
		}

		DrawSetTransform(anchor.Position, 0.0f, anchor.Scale);

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

		Rect2 fromRect = new(fromContainer.Position, fromContainer.Size);
		Rect2 toRect  = new(toContainer.Position, toContainer.Size);
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
				fromEdge + direction * (fromEndingLength + LabelMargin) + perpendicular * LabelMargin
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
			ending == UMLRelationshipEnding.HollowDiamond || ending == UMLRelationshipEnding.FilledDiamond
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
					Zoom++;
					QueueRedraw();
				}
				else if (Input.IsActionJustPressed("ZoomOut"))
				{
					Zoom--;
					QueueRedraw();
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
				anchor.Position += motionEvent.Relative;
				MouseDefaultCursorShape = CursorShape.Drag;
				QueueRedraw();
			}
			else
			{
				MouseDefaultCursorShape = CursorShape.Arrow;
			}
		}
	}

	public void RenderDiagram(UMLDiagram newDiagram)
	{
		bool isDiagramRendered = newDiagram != null;
		grayOut.Visible = !isDiagramRendered;
		menuBar.SetMenuDisabled(addMenu.GetIndex(), !isDiagramRendered);
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
			if (containers.TryGetValue(node, out UMLNodeContainer container))
			{
				container.SetNode(node);
			}
			else
			{
				AddNodeContainer(node.ToContainer());
			}

			if (node.IsAutoPositioned)
			{
				restPositions.TryAdd(node.Name, node.Position.Value);
			}
		}

		PushAutoPositioned(null);
		QueueRedraw();
	}

	private void AddNodeContainer(UMLNodeContainer container)
	{
		anchor.AddChild(container);
		container.Dragged += OnNodeContainerDragged;
		container.Dropped += OnNodeContainerDropped;
		container.NameChanged += OnNodeContainerNameChanged;
		containers[container.UmlNode] = container;
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
		container.Position += delta / anchor.Scale;
		PushAutoPositioned(container);
		QueueRedraw();
	}

	/// <summary>
	/// Shoves the auto-positioned containers out of the way of every other node,
	/// and of <paramref name="dragged"/> in particular, so they part around it as
	/// it moves. Each push starts over from the rest positions, so moving the
	/// dragged node back lets the others fall back to exactly where they were.
	/// Only container positions change; the model and the source stay untouched.
	/// </summary>
	private void PushAutoPositioned(UMLNodeContainer dragged)
	{
		List<UMLNodeContainer> movable = [];
		List<Rect2> restBoxes = [];
		List<Rect2> fixedBoxes = [];
		foreach ((UMLNode node, UMLNodeContainer container) in containers)
		{
			if (node.IsAutoPositioned && container != dragged)
			{
				movable.Add(container);
				restBoxes.Add(new Rect2(restPositions[node.Name], GetSize(container)));
			}
			else
			{
				fixedBoxes.Add(new Rect2(container.Position, GetSize(container)));
			}
		}

		Vector2[] positions = UMLAutoLayout.Separate(restBoxes, fixedBoxes);
		for (int i = 0; i < movable.Count; i++)
		{
			movable[i].Position = positions[i];
		}
	}

	/// <summary>
	/// The size of a container on the canvas. A container added this frame has
	/// not been sized by its layout yet, so its minimum size stands in.
	/// </summary>
	private static Vector2 GetSize(UMLNodeContainer container)
	{
		return container.Size.Max(container.GetCombinedMinimumSize());
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
		if (restPositions.TryGetValue(node.Name, out Vector2 position))
		{
			restPositions[newName] = position;
		}

		NodeNameChanged?.Invoke(node, newName);
	}
}
