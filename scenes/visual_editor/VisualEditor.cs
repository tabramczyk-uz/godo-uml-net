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

	/// <summary>
	/// Raised when the Connect menu has had both ends of a new relationship
	/// clicked. Like a new node, it only appears once written into the code.
	/// </summary>
	public event Action<UMLNode, UMLNode, UMLRelationshipType, UMLRelationshipDirection> RelationshipAdded;

	/// <summary>
	/// Raised when the Connect menu's Delete Connection has had a line clicked.
	/// The line only disappears once it has been removed from the code.
	/// </summary>
	public event Action<UMLRelationship> RelationshipRemoved;

	/// <summary>Ids of the View menu's items, as set in the scene.</summary>
	private enum ViewMenuItem
	{
		ZoomIn,
		ZoomOut,
		ResetZoom,
		FrameDiagram,
	}

	/// <summary>What a left click on the canvas does.</summary>
	private enum CanvasMode
	{
		/// <summary>Picks a node up to drag it.</summary>
		Normal,

		/// <summary>Picks the ends of a new relationship.</summary>
		Connecting,

		/// <summary>Picks a relationship's line to delete it.</summary>
		DeletingConnection,
	}

	/// <summary>
	/// Id of the Connect menu's Delete Connection item, clear of the ids of the
	/// relationship types listed above it.
	/// </summary>
	private const int DeleteConnectionId = 100;

	private const float EndingLength = 16.0f;
	private const float EndingHalfWidth = 7.0f;
	private const float LabelMargin = 4.0f;
	private const float FrameMargin = 32.0f;

	/// <summary>
	/// How close, in screen pixels at any zoom, a click must land to a line to
	/// pick it.
	/// </summary>
	private const float LinePickDistance = 6.0f;

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

	/// <summary>The color of the line a click would delete.</summary>
	[Export]
	private Color DeleteHighlightColor = new(0.937255f, 0.32549f, 0.313726f);

	private Control anchor;
	private ColorRect grayOut;
	private Control menuPanel;
	private MenuBar menuBar;
	private PopupMenu addMenu;
	private PopupMenu connectMenu;
	private Label hintLabel;

	private CanvasMode mode = CanvasMode.Normal;

	/// <summary>
	/// The relationship the Connect menu is waiting to have clicked out, while
	/// <see cref="mode"/> is <see cref="CanvasMode.Connecting"/>.
	/// </summary>
	private UMLRelationshipType connectionType;

	/// <summary>The first node clicked for the pending relationship.</summary>
	private UMLNodeContainer connectionSource = null;

	/// <summary>
	/// The relationship a click would delete right now, drawn highlighted.
	/// </summary>
	private UMLRelationship hoveredRelationship = null;

	/// <summary>
	/// The last mouse position seen while picking, in the editor's own
	/// coordinates. The pending relationship's preview ends here.
	/// </summary>
	private Vector2 pointerPosition;

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
		connectMenu = GetNode<PopupMenu>("%Connect");
		hintLabel = GetNode<Label>("%HintLabel");

		foreach (UMLNodeType type in Enum.GetValues<UMLNodeType>())
		{
			addMenu.AddItem(GetDisplayName(type), (int)type);
		}

		foreach (UMLRelationshipType type in Enum.GetValues<UMLRelationshipType>())
		{
			connectMenu.AddItem(GetDisplayName(type), (int)type);
		}

		connectMenu.AddSeparator();
		connectMenu.AddItem("Delete Connection", DeleteConnectionId);

		addMenu.IdPressed += OnAddMenuIdPressed;
		connectMenu.IdPressed += OnConnectMenuIdPressed;
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
	private static string GetDisplayName(Enum value)
	{
		string name = value.ToString();
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

	private void OnConnectMenuIdPressed(long id)
	{
		if (diagram == null)
		{
			return;
		}

		if (id == DeleteConnectionId)
		{
			StartMode(CanvasMode.DeletingConnection);
			return;
		}

		connectionType = (UMLRelationshipType)id;
		StartMode(CanvasMode.Connecting);
	}

	/// <summary>
	/// Switches to one of the click-to-pick modes, in which clicks pick nodes or
	/// lines instead of dragging nodes around.
	/// </summary>
	private void StartMode(CanvasMode newMode)
	{
		mode = newMode;
		connectionSource = null;
		hoveredRelationship = null;
		ToggleNodes(false);
		MouseDefaultCursorShape = CursorShape.Cross;
		UpdateHint();
		QueueRedraw();
	}

	private void UpdateHint()
	{
		if (mode == CanvasMode.DeletingConnection)
		{
			hintLabel.Text = "Delete: click a line (Esc cancels)";
		}
		else
		{
			string source = connectionSource == null ? string.Empty : $"{connectionSource.UmlNode.Name} → ";
			hintLabel.Text = $"{GetDisplayName(connectionType)}: {source}click a node (Esc cancels)";
		}

		hintLabel.Show();
	}

	/// <summary>
	/// Leaves the click-to-pick mode, if one is on, and gives the nodes their
	/// dragging back.
	/// </summary>
	private void EndMode()
	{
		if (mode == CanvasMode.Normal)
		{
			return;
		}

		mode = CanvasMode.Normal;
		connectionSource = null;
		hoveredRelationship = null;
		ToggleNodes(!grayOut.Visible);
		MouseDefaultCursorShape = CursorShape.Arrow;
		hintLabel.Hide();
		QueueRedraw();
	}

	/// <summary>
	/// Takes the input of the click-to-pick modes: a left click picks a node or
	/// a line, a right click or Cancel gives up. Returns whether the event was
	/// used up.
	/// </summary>
	private bool HandleModeInput(InputEvent @event)
	{
		if (@event.IsActionPressed("Cancel"))
		{
			EndMode();
			return true;
		}

		if (@event is InputEventMouseMotion motionEvent)
		{
			pointerPosition = ToLocal(motionEvent.Position);
			if (mode == CanvasMode.DeletingConnection)
			{
				UMLRelationship hovered = GetCanvasRect().HasPoint(pointerPosition)
					? GetRelationshipAt(pointerPosition)
					: null;
				if (hovered != hoveredRelationship)
				{
					hoveredRelationship = hovered;
					QueueRedraw();
				}
			}
			else if (connectionSource != null)
			{
				QueueRedraw();
			}

			return false;
		}

		if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
		{
			return false;
		}

		pointerPosition = ToLocal(mouseEvent.Position);
		if (!GetCanvasRect().HasPoint(pointerPosition))
		{
			return false;
		}

		if (mouseEvent.ButtonIndex == MouseButton.Right)
		{
			EndMode();
			return true;
		}

		if (mouseEvent.ButtonIndex != MouseButton.Left)
		{
			return false;
		}

		if (mode == CanvasMode.DeletingConnection)
		{
			PickRelationshipToDelete();
		}
		else
		{
			PickConnectionEnd(GetContainerAt(mouseEvent.Position));
		}

		return true;
	}

	/// <summary>
	/// Takes a click on <paramref name="clicked"/> as the next end of the
	/// pending relationship. Clicks on empty canvas, or on the first node again,
	/// are ignored.
	/// </summary>
	private void PickConnectionEnd(UMLNodeContainer clicked)
	{
		if (clicked == null || clicked == connectionSource)
		{
			return;
		}

		if (connectionSource == null)
		{
			connectionSource = clicked;
			UpdateHint();
			QueueRedraw();
			return;
		}

		// The mode ends before the relationship is announced, since writing it
		// re-parses the code and rebuilds every container.
		UMLRelationshipType type = connectionType;
		UMLNode from = connectionSource.UmlNode;
		UMLNode to = clicked.UmlNode;
		EndMode();
		RelationshipAdded?.Invoke(from, to, type, GetDirection(type));
	}

	/// <summary>
	/// Deletes the line under the click, if there is one. The hover highlight is
	/// not trusted for this, since a click can arrive without a move before it.
	/// </summary>
	private void PickRelationshipToDelete()
	{
		UMLRelationship relationship = GetRelationshipAt(pointerPosition);
		if (relationship == null)
		{
			return;
		}

		// As with connecting, the mode ends first: removing the line re-parses
		// the code.
		EndMode();
		RelationshipRemoved?.Invoke(relationship);
	}

	/// <summary>
	/// The relationship whose line passes closest to
	/// <paramref name="localPosition"/>, if any passes within
	/// <see cref="LinePickDistance"/> of it.
	/// </summary>
	private UMLRelationship GetRelationshipAt(Vector2 localPosition)
	{
		if (diagram == null)
		{
			return null;
		}

		Vector2 point = (localPosition - anchor.Position) / anchor.Scale;
		float closestDistance = LinePickDistance / anchor.Scale.X;
		UMLRelationship closest = null;

		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			if (!TryGetRelationshipEdges(relationship, out Vector2 fromEdge, out Vector2 toEdge))
			{
				continue;
			}

			float distance = UMLGeometry.DistanceToSegment(point, fromEdge, toEdge);
			if (distance <= closestDistance)
			{
				closestDistance = distance;
				closest = relationship;
			}
		}

		return closest;
	}

	/// <summary>
	/// Converts an input event's position into the editor's own coordinates.
	/// </summary>
	private Vector2 ToLocal(Vector2 eventPosition)
	{
		return GetGlobalTransform().AffineInverse() * eventPosition;
	}

	/// <summary>
	/// The topmost node under <paramref name="globalPosition"/>, if any.
	/// </summary>
	private UMLNodeContainer GetContainerAt(Vector2 globalPosition)
	{
		Godot.Collections.Array<Node> children = anchor.GetChildren();
		for (int i = children.Count - 1; i >= 0; i--)
		{
			if (
				children[i] is UMLNodeContainer container
				&& container.GetGlobalRect().HasPoint(globalPosition)
			)
			{
				return container;
			}
		}

		return null;
	}

	/// <summary>
	/// Which end of a relationship clicked out from the menu is decorated: a
	/// plain association has no decoration, every other kind decorates the
	/// second node clicked.
	/// </summary>
	private static UMLRelationshipDirection GetDirection(UMLRelationshipType type)
	{
		return type == UMLRelationshipType.Association
			? UMLRelationshipDirection.None
			: UMLRelationshipDirection.Forward;
	}

	/// <summary>
	/// Draws the pending relationship from its first node to the mouse, styled
	/// like the line it will become.
	/// </summary>
	private void DrawConnectionPreview(UMLRelationshipType type, UMLNodeContainer source)
	{
		Rect2 sourceRect = new(source.Position, GetSize(source));
		Vector2 mouse = (pointerPosition - anchor.Position) / anchor.Scale;
		if (sourceRect.HasPoint(mouse))
		{
			return;
		}

		Vector2 start = ClipToRect(sourceRect, sourceRect.GetCenter(), mouse);
		Vector2 delta = mouse - start;
		if (delta.LengthSquared() < 0.0001f)
		{
			return;
		}

		Vector2 direction = delta.Normalized();
		bool decorated = GetDirection(type).DecoratesTo();
		Vector2 lineEnd = decorated ? mouse - direction * EndingLength : mouse;

		if (type.IsDashed())
		{
			DrawDashedLine(start, lineEnd, Colors.White, 2.0f, 6.0f);
		}
		else
		{
			DrawLine(start, lineEnd, Colors.White, 2.0f, true);
		}

		if (decorated)
		{
			DrawEnding(type.GetEnding(), mouse, -direction, Colors.White);
		}
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

			DrawRelationship(
				relationship,
				relationship == hoveredRelationship ? DeleteHighlightColor : Colors.White
			);
		}

		if (mode == CanvasMode.Connecting && connectionSource != null)
		{
			DrawConnectionPreview(connectionType, connectionSource);
		}
	}

	/// <summary>
	/// Where a relationship's line meets the edges of its two nodes, in diagram
	/// coordinates. Drawing and picking both go through here so a click lands
	/// on exactly the line that is shown. False when the nodes overlap so much
	/// that no line is drawn.
	/// </summary>
	private bool TryGetRelationshipEdges(
		UMLRelationship relationship,
		out Vector2 fromEdge,
		out Vector2 toEdge
	)
	{
		UMLNodeContainer fromContainer = containers[relationship.From];
		UMLNodeContainer toContainer = containers[relationship.To];

		Rect2 fromRect = new(fromContainer.Position, fromContainer.Size);
		Rect2 toRect  = new(toContainer.Position, toContainer.Size);
		Vector2 fromCenter = fromRect.GetCenter();
		Vector2 toCenter = toRect.GetCenter();

		fromEdge = ClipToOutline(relationship.From, fromRect, toCenter);
		toEdge = ClipToOutline(relationship.To, toRect, fromCenter);

		return (toEdge - fromEdge).LengthSquared() >= 0.0001f;
	}

	/// <summary>
	/// Where a line from the middle of <paramref name="node"/> toward
	/// <paramref name="towards"/> crosses the node's outline: its ellipse for a
	/// use case, its bounding box for everything else.
	/// </summary>
	private static Vector2 ClipToOutline(UMLNode node, Rect2 rect, Vector2 towards)
	{
		return node.Type == UMLNodeType.UseCase
			? UMLGeometry.ClipToEllipse(rect, towards)
			: ClipToRect(rect, rect.GetCenter(), towards);
	}

	private void DrawRelationship(UMLRelationship relationship, Color color)
	{
		if (!TryGetRelationshipEdges(relationship, out Vector2 fromEdge, out Vector2 toEdge))
		{
			return;
		}

		Vector2 direction = (toEdge - fromEdge).Normalized();

		float fromEndingLength = GetEndingLength(relationship.FromEnding);
		float toEndingLength = GetEndingLength(relationship.ToEnding);

		Vector2 lineStart = fromEdge + direction * fromEndingLength;
		Vector2 lineEnd = toEdge - direction * toEndingLength;

		if (relationship.IsDashed)
		{
			DrawDashedLine(lineStart, lineEnd, color, 2.0f, 6.0f);
		}
		else
		{
			DrawLine(lineStart, lineEnd, color, 2.0f, true);
		}

		DrawEnding(relationship.FromEnding, fromEdge, direction, color);
		DrawEnding(relationship.ToEnding, toEdge, -direction, color);

		Vector2 perpendicular = new(-direction.Y, direction.X);

		if (!string.IsNullOrEmpty(relationship.Label))
		{
			DrawText(relationship.Label, (fromEdge + toEdge) / 2.0f + perpendicular * LabelMargin, color);
		}

		if (!string.IsNullOrEmpty(relationship.FromMultiplicity))
		{
			DrawText(
				relationship.FromMultiplicity,
				fromEdge + direction * (fromEndingLength + LabelMargin) + perpendicular * LabelMargin,
				color
			);
		}

		if (!string.IsNullOrEmpty(relationship.ToMultiplicity))
		{
			DrawText(
				relationship.ToMultiplicity,
				toEdge - direction * (toEndingLength + LabelMargin) + perpendicular * LabelMargin,
				color
			);
		}
	}

	private void DrawText(string text, Vector2 position, Color color)
	{
		Font font = GetThemeDefaultFont();
		int fontSize = GetThemeDefaultFontSize();
		DrawString(font, position, text, HorizontalAlignment.Left, -1, fontSize, color);
	}

	/// <summary>
	/// Draws the shape a relationship's ending calls for, with its tip touching
	/// the node at <paramref name="tip"/> and its body spreading out along
	/// <paramref name="outward"/>, the direction away from that node.
	/// </summary>
	private void DrawEnding(UMLRelationshipEnding ending, Vector2 tip, Vector2 outward, Color color)
	{
		if (ending == UMLRelationshipEnding.None)
		{
			return;
		}

		Vector2 perpendicular = new(-outward.Y, outward.X);

		if (ending == UMLRelationshipEnding.OpenArrow)
		{
			Vector2 baseCenter = tip + outward * EndingLength;
			DrawLine(tip, baseCenter + perpendicular * EndingHalfWidth, color, 2.0f, true);
			DrawLine(tip, baseCenter - perpendicular * EndingHalfWidth, color, 2.0f, true);
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
			DrawColoredPolygon(points, color);
		}
		else
		{
			DrawColoredPolygon(points, BackgroundColor);
			DrawPolyline([.. points, points[0]], color, 2.0f, true);
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

		if (mode != CanvasMode.Normal && HandleModeInput(@event))
		{
			GetViewport().SetInputAsHandled();
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
				MouseDefaultCursorShape =
					mode == CanvasMode.Normal ? CursorShape.Arrow : CursorShape.Cross;
			}
		}
	}

	public void RenderDiagram(UMLDiagram newDiagram)
	{
		bool isDiagramRendered = newDiagram != null;
		grayOut.Visible = !isDiagramRendered;
		EndMode();
		menuBar.SetMenuDisabled(addMenu.GetIndex(), !isDiagramRendered);
		menuBar.SetMenuDisabled(connectMenu.GetIndex(), !isDiagramRendered);
		ToggleNodes(isDiagramRendered);

		if (!isDiagramRendered)
		{
			return;
		}

		diagram = newDiagram;
		connectMenu.SetItemDisabled(
			connectMenu.GetItemIndex(DeleteConnectionId),
			newDiagram.Relationships.Count == 0
		);

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
