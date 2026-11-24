using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Godot;

public partial class VisualEditor : Control
{
	public event Action<UMLNode, string> NodeNameChanged;

	/// <summary>
	/// Raised when a drag ends, with where each node it moved now sits: the
	/// dragged node alone, or the whole selection when it was one of them.
	/// </summary>
	public event Action<IReadOnlyDictionary<UMLNode, Vector2>> NodesMoved;

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

	/// <summary>
	/// Raised when the node menu's Delete is chosen, with the nodes to delete.
	/// They only disappear once they have been removed from the code.
	/// </summary>
	public event Action<IReadOnlyList<UMLNode>> NodesRemoved;

	/// <summary>
	/// Raised when the node menu's Reset Position is chosen, with the nodes
	/// whose position to delete from the code, so the layout places them again.
	/// </summary>
	public event Action<IReadOnlyList<UMLNode>> NodePositionsReset;

	/// <summary>
	/// Raised for Undo and Redo pressed on the canvas. Every canvas edit is an
	/// edit of the code, so they step through the code editor's history.
	/// </summary>
	public event Action UndoRequested;

	/// <inheritdoc cref="UndoRequested"/>
	public event Action RedoRequested;

	/// <summary>
	/// Raised when nodes are pasted, with the code declaring them to append.
	/// They only appear once it has been written into the code.
	/// </summary>
	public event Action<string> NodesPasted;

	/// <summary>
	/// Ids of the node menu's own items. The Connect from Here submenu uses the
	/// relationship types' values instead, as the Connect menu does.
	/// </summary>
	private enum NodeMenuItem
	{
		Rename,
		Delete,
		ResetPosition,
	}

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

	[Export]
	private Color BackgroundColor = new(0.180392f, 0.180392f, 0.180392f);

	/// <summary>The color of the line a click would delete.</summary>
	[Export]
	private Color DeleteHighlightColor = new(0.937255f, 0.32549f, 0.313726f);

	/// <summary>The color of the frame around selected nodes.</summary>
	[Export]
	private Color SelectionColor = new(0.352941f, 0.6f, 1.0f);

	/// <summary>How far the selection frame stands off a node's edge.</summary>
	private const float SelectionMargin = 4.0f;

	/// <summary>
	/// How far, in screen pixels, the mouse must move before a press on empty
	/// canvas counts as drawing a box rather than a click.
	/// </summary>
	private const float BoxDragThreshold = 4.0f;

	/// <summary>
	/// The parent of every node container. Its position and scale are the pan
	/// and zoom, mapping diagram coordinates onto the editor.
	/// </summary>
	private CanvasCamera world;
	private ColorRect grayOut;
	private Control menuPanel;
	private MenuBar menuBar;
	private PopupMenu addMenu;
	private PopupMenu connectMenu;
	private Label hintLabel;
	private Control selectionBox;
	private PopupMenu nodeMenu;
	private readonly PopupMenu connectFromHereMenu = new();

	/// <summary>
	/// The nodes the open node menu acts on: the right-clicked node, or the
	/// whole selection it belongs to.
	/// </summary>
	private List<UMLNodeContainer> menuTargets = [];

	private CanvasMode mode = CanvasMode.Normal;

	private readonly NodeSelection selection = new();

	/// <summary>
	/// Where the box being drawn was started, in the editor's own coordinates,
	/// or <c>null</c> when no box is being drawn.
	/// </summary>
	private Vector2? boxStart = null;

	/// <summary>Whether the box being drawn adds to the selection (Shift).</summary>
	private bool boxAddsToSelection = false;

	/// <summary>The containers moving together in the drag under way.</summary>
	private List<UMLNodeContainer> dragGroup = [];

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

	/// <summary>
	/// Whether the code parsed the last time it was submitted. While it did not,
	/// <see cref="diagram"/> is the last good one, shown grayed out and closed to
	/// editing. False until the first diagram arrives, as the scene starts
	/// grayed out.
	/// </summary>
	private bool isDiagramValid = false;

	private UMLNodeContainer draggedNodeContainer = null;
	private readonly Dictionary<UMLNode, UMLNodeContainer> containers = [];

	/// <summary>
	/// Where the relationship lines run between the containers, and how they
	/// are drawn and picked.
	/// </summary>
	private readonly RelationshipLines relationshipLines;

	/// <summary>
	/// Where each auto-positioned node sits when nothing pushes it. Every push is
	/// computed afresh from these, so where a node is shown depends only on where
	/// the other nodes are now, not on how they got there.
	/// </summary>
	private readonly UMLLayoutMemory layoutMemory = new();

	public VisualEditor()
	{
		relationshipLines = new RelationshipLines(node => GetRect(containers[node]));
	}

	public override void _Ready()
	{
		world = GetNode<CanvasCamera>("%World");
		grayOut = GetNode<ColorRect>("%GrayOut");
		menuPanel = GetNode<Control>("%MenuPanel");
		menuBar = GetNode<MenuBar>("%MenuBar");
		addMenu = GetNode<PopupMenu>("%Add");
		connectMenu = GetNode<PopupMenu>("%Connect");
		hintLabel = GetNode<Label>("%HintLabel");
		selectionBox = GetNode<Control>("%SelectionBox");
		nodeMenu = GetNode<PopupMenu>("%NodeMenu");

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

		foreach (UMLRelationshipType type in Enum.GetValues<UMLRelationshipType>())
		{
			connectFromHereMenu.AddItem(GetDisplayName(type), (int)type);
		}

		// The submenu is made in code, so it needs a parent to be freed with the
		// scene; otherwise quitting before the node menu first opens leaks it.
		// Clearing the node menu keeps it as a child.
		nodeMenu.AddChild(connectFromHereMenu);
		nodeMenu.IdPressed += OnNodeMenuIdPressed;
		connectFromHereMenu.IdPressed += OnConnectFromHereIdPressed;
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
		if (!isDiagramValid)
		{
			return;
		}

		UMLNodeType type = (UMLNodeType)id;
		string name = diagram.GetUniqueNodeName(type.ToString());
		NodeAdded?.Invoke(type, name, GetNewNodePosition());
	}

	private void OnConnectMenuIdPressed(long id)
	{
		if (!isDiagramValid)
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
		ToggleNodes(isDiagramValid);
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

		Vector2 point = world.ToDiagram(localPosition);
		return relationshipLines.GetRelationshipAt(
			diagram.Relationships,
			point,
			LinePickDistance / world.Scale.X
		);
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
		Godot.Collections.Array<Node> children = world.GetChildren();
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
		Vector2 mouse = world.ToDiagram(pointerPosition);
		RelationshipLines.DrawPreview(
			this,
			type,
			GetDirection(type).DecoratesTo(),
			GetRect(source),
			mouse,
			Colors.White,
			BackgroundColor
		);
	}

	/// <summary>
	/// Where a node added from the menu goes: the middle of the visible canvas,
	/// nudged along until it no longer lands exactly on another node.
	/// </summary>
	private Vector2 GetNewNodePosition()
	{
		Vector2 position = world.ToDiagram(GetCanvasRect().GetCenter()).Round();
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
				world.ZoomIn(canvasCenter);
				break;
			case ViewMenuItem.ZoomOut:
				world.ZoomOut(canvasCenter);
				break;
			case ViewMenuItem.ResetZoom:
				world.ResetZoom(canvasCenter);
				break;
			case ViewMenuItem.FrameDiagram:
				FrameDiagram();
				break;
		}

		QueueRedraw();
	}

	/// <summary>
	/// Drops the remembered rest positions of auto-positioned nodes, so the next
	/// diagram is laid out afresh instead of inheriting spots from one that
	/// merely used the same node names.
	/// </summary>
	public void ForgetLayout()
	{
		layoutMemory.Clear();
	}

	/// <summary>
	/// Centers the diagram on the canvas at the largest zoom level, up to 100%,
	/// that fits all of it.
	/// </summary>
	public void FrameDiagram()
	{
		QueueRedraw();

		Rect2? bounds = null;
		foreach (UMLNodeContainer container in containers.Values)
		{
			Rect2 box = GetRect(container);
			bounds = bounds?.Merge(box) ?? box;
		}

		world.Frame(bounds, GetCanvasRect());
	}

	public override void _Draw()
	{
		if (diagram == null)
		{
			return;
		}

		DrawSetTransform(world.Position, 0.0f, world.Scale);

		// Drawn under the nodes, standing off their edges, so it frames them
		// whatever their shape.
		foreach (UMLNodeContainer container in GetSelectedContainers())
		{
			Rect2 frame = GetRect(container).Grow(SelectionMargin);
			DrawRect(frame, SelectionColor, false, 2.0f);
		}

		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			Debug.Assert(relationship.From != null);
			Debug.Assert(relationship.To != null);

			relationshipLines.Draw(
				this,
				relationship,
				relationship == hoveredRelationship ? DeleteHighlightColor : Colors.White,
				BackgroundColor
			);
		}

		if (mode == CanvasMode.Connecting && connectionSource != null)
		{
			DrawConnectionPreview(connectionType, connectionSource);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (diagram == null)
		{
			return;
		}

		if (HasFocus() && HandleHistoryInput(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mode != CanvasMode.Normal && HandleModeInput(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mode == CanvasMode.Normal && isDiagramValid)
		{
			HandleSelectionInput(@event);
		}

		// `_Input` sees every event in the window, so the canvas's own
		// navigation is limited to the mouse over the canvas, leaving the wheel
		// and drags over the code editor to the code editor.
		bool moved = world.HandleNavigationInput(@event, GetCanvasRect());
		if (moved)
		{
			QueueRedraw();
		}

		if (@event is InputEventMouseMotion)
		{
			if (moved)
			{
				MouseDefaultCursorShape = CursorShape.Drag;
			}
			else
			{
				MouseDefaultCursorShape =
					mode == CanvasMode.Normal ? CursorShape.Arrow : CursorShape.Cross;
			}
		}
	}

	/// <summary>
	/// Picks nodes. A click on a node selects it, or adds it to the selection
	/// with Shift, and leaves an existing selection alone when the node is
	/// already part of it, so the whole selection can be dragged. A drag across
	/// empty canvas draws a box that selects every node it touches. A click on
	/// empty canvas, or Cancel, clears the selection, and Delete deletes it.
	/// Clicks are not marked as handled, so the nodes still get their drags and
	/// double-clicks.
	/// </summary>
	private void HandleSelectionInput(InputEvent @event)
	{
		// Only while the canvas has focus, so Delete still edits text in the
		// code editor and in the rename box.
		if (@event.IsActionPressed("Delete") && HasFocus())
		{
			DeleteSelection();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("ui_text_select_all", allowEcho: false, exactMatch: true) && HasFocus())
		{
			SelectAll();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("ui_copy", allowEcho: false, exactMatch: true) && HasFocus())
		{
			CopySelection();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("ui_paste", allowEcho: false, exactMatch: true) && HasFocus())
		{
			Paste();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("Cancel"))
		{
			ClearSelection();
			return;
		}

		if (@event is InputEventMouseMotion motionEvent)
		{
			if (boxStart != null)
			{
				UpdateSelectionBox(ToLocal(motionEvent.Position));
			}

			return;
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rightClick)
		{
			OpenNodeMenu(rightClick.Position);
			return;
		}

		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseEvent)
		{
			return;
		}

		if (!mouseEvent.Pressed)
		{
			if (boxStart != null)
			{
				FinishSelectionBox(ToLocal(mouseEvent.Position));
			}

			return;
		}

		Vector2 localPosition = ToLocal(mouseEvent.Position);
		if (!GetCanvasRect().HasPoint(localPosition))
		{
			return;
		}

		GrabFocus();

		UMLNodeContainer clicked = GetContainerAt(mouseEvent.Position);
		if (clicked != null)
		{
			SelectOnPress(clicked.UmlNode, mouseEvent.ShiftPressed);
			return;
		}

		boxStart = localPosition;
		boxAddsToSelection = mouseEvent.ShiftPressed;
		if (!boxAddsToSelection)
		{
			ClearSelection();
		}
	}

	/// <summary>
	/// Handles Undo and Redo, with the same keys as the code editor's. Matching
	/// is exact, so Ctrl+Shift+Z counts as Redo rather than also as Undo.
	/// Returns whether the event was one of them.
	/// </summary>
	private bool HandleHistoryInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_redo", allowEcho: false, exactMatch: true))
		{
			RedoRequested?.Invoke();
			return true;
		}

		if (@event.IsActionPressed("ui_undo", allowEcho: false, exactMatch: true))
		{
			UndoRequested?.Invoke();
			return true;
		}

		return false;
	}

	private void SelectAll()
	{
		foreach (UMLNode node in containers.Keys)
		{
			selection.Add(node);
		}

		QueueRedraw();
	}

	private void DeleteSelection()
	{
		List<UMLNode> nodes = GetSelectedNodes();
		if (nodes.Count > 0)
		{
			NodesRemoved?.Invoke(nodes);
		}
	}

	private List<UMLNode> GetSelectedNodes()
	{
		return GetSelectedContainers().ConvertAll(container => container.UmlNode);
	}

	/// <summary>
	/// The containers of the selected nodes, looked up by name in the
	/// containers of the current render.
	/// </summary>
	private List<UMLNodeContainer> GetSelectedContainers()
	{
		List<UMLNodeContainer> selected = [];
		foreach ((UMLNode node, UMLNodeContainer container) in containers)
		{
			if (selection.Contains(node))
			{
				selected.Add(container);
			}
		}

		return selected;
	}

	/// <summary>Puts the selected nodes on the system clipboard as code.</summary>
	private void CopySelection()
	{
		List<UMLNode> nodes = GetSelectedNodes();
		if (nodes.Count > 0)
		{
			DisplayServer.ClipboardSet(UMLClipboard.Copy(diagram, nodes));
		}
	}

	/// <summary>
	/// Pastes the clipboard as nodes, stepped clear of the nodes already on
	/// the canvas, and selects them so they can be dragged into place straight
	/// away. Clipboard text that is not nodes is ignored.
	/// </summary>
	private void Paste()
	{
		string text = DisplayServer.ClipboardGet();
		if (!UMLClipboard.TryPaste(text, diagram, NewNodeOffset, out string code, out List<string> names))
		{
			return;
		}

		selection.Replace(names);
		NodesPasted?.Invoke(code);
	}

	/// <summary>
	/// Opens the node menu for the node under a right-click. Like a left click,
	/// it first selects that node unless it is already part of the selection,
	/// so the menu acts on the whole selection or on the clicked node alone.
	/// Items that only make sense for one node are left out for several.
	/// </summary>
	private void OpenNodeMenu(Vector2 eventPosition)
	{
		pointerPosition = ToLocal(eventPosition);
		if (!GetCanvasRect().HasPoint(pointerPosition))
		{
			return;
		}

		GrabFocus();

		UMLNodeContainer clicked = GetContainerAt(eventPosition);
		if (clicked == null)
		{
			return;
		}

		SelectOnPress(clicked.UmlNode, false);
		menuTargets = GetSelectionGroup(clicked);

		bool single = menuTargets.Count == 1;
		nodeMenu.Clear();
		if (single)
		{
			nodeMenu.AddItem("Rename...", (int)NodeMenuItem.Rename);
			nodeMenu.AddSubmenuNodeItem("Connect from Here", connectFromHereMenu);
		}

		// Left out when every target is already placed by the layout, as it
		// would have nothing to delete.
		if (menuTargets.Exists(target => !target.UmlNode.IsAutoPositioned))
		{
			nodeMenu.AddItem(single ? "Reset Position" : "Reset Positions", (int)NodeMenuItem.ResetPosition);
		}

		if (nodeMenu.ItemCount > 0)
		{
			nodeMenu.AddSeparator();
		}

		string delete = single ? "Delete Node" : $"Delete {menuTargets.Count} Nodes";
		nodeMenu.AddItem(delete, (int)NodeMenuItem.Delete, Key.Delete);

		nodeMenu.Popup(new Rect2I((Vector2I)eventPosition, Vector2I.Zero));
	}

	private void OnNodeMenuIdPressed(long id)
	{
		// A re-parse while the menu was open rebuilds the containers.
		menuTargets.RemoveAll(container => !IsInstanceValid(container));
		if (menuTargets.Count == 0)
		{
			return;
		}

		switch ((NodeMenuItem)id)
		{
			case NodeMenuItem.Rename:
				menuTargets[0].StartRename();
				break;
			case NodeMenuItem.Delete:
				List<UMLNode> nodes = menuTargets.ConvertAll(container => container.UmlNode);
				menuTargets = [];
				NodesRemoved?.Invoke(nodes);
				break;
			case NodeMenuItem.ResetPosition:
				List<UMLNode> placed = menuTargets.ConvertAll(container => container.UmlNode);
				menuTargets = [];
				NodePositionsReset?.Invoke(placed);
				break;
		}
	}

	/// <summary>
	/// Starts connecting with the menu's node already picked as the first end,
	/// so only the other end is left to click.
	/// </summary>
	private void OnConnectFromHereIdPressed(long id)
	{
		if (menuTargets.Count == 0 || !IsInstanceValid(menuTargets[0]))
		{
			return;
		}

		UMLNodeContainer source = menuTargets[0];
		connectionType = (UMLRelationshipType)id;
		StartMode(CanvasMode.Connecting);
		connectionSource = source;
		UpdateHint();
		QueueRedraw();
	}

	private void SelectOnPress(UMLNode node, bool addToSelection)
	{
		selection.Press(node, addToSelection);
		QueueRedraw();
	}

	private void ClearSelection()
	{
		if (selection.Clear())
		{
			QueueRedraw();
		}
	}

	private void UpdateSelectionBox(Vector2 pointer)
	{
		Rect2 box = GetBox(boxStart.Value, pointer);
		if (!selectionBox.Visible && !IsBoxDrag(box))
		{
			return;
		}

		selectionBox.Position = box.Position;
		selectionBox.Size = box.Size;
		selectionBox.Show();
	}

	/// <summary>
	/// Selects every node the finished box touches, on top of the existing
	/// selection when the box was started with Shift. A box too small to count
	/// was a click, which cleared the selection when it was pressed.
	/// </summary>
	private void FinishSelectionBox(Vector2 pointer)
	{
		Rect2 box = GetBox(boxStart.Value, pointer);
		boxStart = null;
		selectionBox.Hide();

		if (!IsBoxDrag(box))
		{
			return;
		}

		Rect2 diagramBox = world.ToDiagram(box);
		foreach ((UMLNode node, UMLNodeContainer container) in containers)
		{
			if (GetRect(container).Intersects(diagramBox, true))
			{
				selection.Add(node);
			}
		}

		QueueRedraw();
	}

	private static Rect2 GetBox(Vector2 corner, Vector2 oppositeCorner)
	{
		return new Rect2(corner, Vector2.Zero).Expand(oppositeCorner);
	}

	private static bool IsBoxDrag(Rect2 box)
	{
		return box.Size.X >= BoxDragThreshold || box.Size.Y >= BoxDragThreshold;
	}

	public void RenderDiagram(UMLDiagram newDiagram)
	{
		isDiagramValid = newDiagram != null;
		grayOut.Visible = !isDiagramValid;
		EndMode();
		SetMenuEnabled(addMenu, isDiagramValid);
		SetMenuEnabled(connectMenu, isDiagramValid);
		ToggleNodes(isDiagramValid);

		if (!isDiagramValid)
		{
			return;
		}

		diagram = newDiagram;
		selection.RemoveMissing(newDiagram);
		connectMenu.SetItemDisabled(
			connectMenu.GetItemIndex(DeleteConnectionId),
			newDiagram.Relationships.Count == 0
		);

		foreach (Node child in world.GetChildren())
		{
			world.RemoveChild(child);
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
				AddNodeContainer(UMLNodeContainerFactory.Create(node));
			}
		}

		layoutMemory.Update(newDiagram);
		PushAutoPositioned([]);
		QueueRedraw();
	}

	/// <summary>
	/// Enables or grays out one of the menu bar's menus together with every
	/// item in it. Graying out the menu's title alone still let the menu be
	/// opened and used.
	/// </summary>
	private void SetMenuEnabled(PopupMenu menu, bool enabled)
	{
		menuBar.SetMenuDisabled(menu.GetIndex(), !enabled);
		for (int i = 0; i < menu.ItemCount; i++)
		{
			menu.SetItemDisabled(i, !enabled);
		}
	}

	private void AddNodeContainer(UMLNodeContainer container)
	{
		world.AddChild(container);
		container.Dragged += OnNodeContainerDragged;
		container.Dropped += OnNodeContainerDropped;
		container.NameChanged += OnNodeContainerNameChanged;
		containers[container.UmlNode] = container;
	}

	private void ToggleNodes(bool enabled)
	{
		foreach (Node child in world.GetChildren())
		{
			if (child is UMLNodeContainer container)
			{
				container.ToggleInput(enabled);
			}
		}
	}

	private void OnNodeContainerDragged(UMLNodeContainer container, Vector2 delta)
	{
		if (draggedNodeContainer == null)
		{
			draggedNodeContainer = container;
			dragGroup = GetSelectionGroup(container);
		}
		else if (draggedNodeContainer != container)
		{
			return;
		}

		foreach (UMLNodeContainer member in dragGroup)
		{
			member.Position += delta / world.Scale;
		}

		PushAutoPositioned(dragGroup);
		QueueRedraw();
	}

	/// <summary>
	/// The containers that dragging <paramref name="container"/>, or its node
	/// menu, acts on: the whole selection when it is part of it, and just
	/// itself otherwise.
	/// </summary>
	private List<UMLNodeContainer> GetSelectionGroup(UMLNodeContainer container)
	{
		if (!selection.Contains(container.UmlNode))
		{
			return [container];
		}

		return GetSelectedContainers();
	}

	/// <summary>
	/// Shoves the auto-positioned containers out of the way of every other node,
	/// and of the <paramref name="dragged"/> ones in particular, so they part
	/// around them as they move. Each push starts over from the rest positions,
	/// so moving the dragged nodes back lets the others fall back to exactly
	/// where they were. Only container positions change; the model and the
	/// source stay untouched.
	/// </summary>
	private void PushAutoPositioned(ICollection<UMLNodeContainer> dragged)
	{
		List<UMLNodeContainer> movable = [];
		List<Rect2> restBoxes = [];
		List<Rect2> fixedBoxes = [];
		foreach ((UMLNode node, UMLNodeContainer container) in containers)
		{
			if (node.IsAutoPositioned && !dragged.Contains(container))
			{
				movable.Add(container);
				restBoxes.Add(new Rect2(layoutMemory.GetRestPosition(node), GetSize(container)));
			}
			else
			{
				fixedBoxes.Add(GetRect(container));
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

	/// <summary>
	/// The box a container covers on the canvas, in diagram coordinates, sized
	/// as by <see cref="GetSize"/>.
	/// </summary>
	private static Rect2 GetRect(UMLNodeContainer container)
	{
		return new Rect2(container.Position, GetSize(container));
	}

	private void OnNodeContainerDropped(UMLNodeContainer container)
	{
		if (draggedNodeContainer != container)
		{
			return;
		}

		draggedNodeContainer = null;

		var positions = new Dictionary<UMLNode, Vector2>();
		foreach (UMLNodeContainer member in dragGroup)
		{
			positions[member.UmlNode] = member.Position;
		}

		dragGroup = [];
		NodesMoved?.Invoke(positions);
	}

	private void OnNodeContainerNameChanged(UMLNode node, string newName)
	{
		layoutMemory.Rename(node.Name, newName);
		NodeNameChanged?.Invoke(node, newName);
	}
}
