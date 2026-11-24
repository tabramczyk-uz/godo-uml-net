using System;
using Godot;

/// <summary>
/// The canvas of a sequence diagram: participants left to right, each heading
/// its lifeline, messages top to bottom and the activation bars they imply,
/// all placed by <see cref="SequenceLayout"/>. Like the class canvas it never
/// changes the diagram itself: each edit is raised as an event, and only shows
/// once it has been written into the code and the code re-parsed.
/// </summary>
// `partial` lets Godot's source generators add the glue code every C# node
// class needs.
public partial class SequenceDiagramCanvas : Control
{
	/// <summary>
	/// Raised when the Add menu asks for a new participant of the given kind
	/// and name, which goes to the right of the others.
	/// </summary>
	public event Action<SequenceParticipantKind, string> ParticipantAdded;

	/// <summary>Raised when a participant's name has been edited on the canvas.</summary>
	public event Action<SequenceParticipant, string> ParticipantNameChanged;

	/// <summary>Raised when a participant is to be deleted, together with its messages.</summary>
	public event Action<SequenceParticipant> ParticipantRemoved;

	/// <summary>
	/// Raised when a participant has been dragged sideways, with the index,
	/// counting from the left, it is to take.
	/// </summary>
	public event Action<SequenceParticipant, int> ParticipantMoved;

	/// <summary>
	/// Raised when the Message menu has had both lifelines clicked, with the
	/// sender, the receiver, the kind and the index, counting from the top,
	/// the new message is to take.
	/// </summary>
	public event Action<SequenceParticipant, SequenceParticipant, SequenceMessageKind, int> MessageAdded;

	/// <summary>Raised when a message is to be deleted.</summary>
	public event Action<SequenceMessage> MessageRemoved;

	/// <summary>
	/// Raised when a message has been dragged up or down, with the index,
	/// counting from the top, it is to take.
	/// </summary>
	public event Action<SequenceMessage, int> MessageMoved;

	/// <summary>
	/// Raised for Undo and Redo pressed on the canvas. Every canvas edit is an
	/// edit of the code, so they step through the code editor's history.
	/// </summary>
	public event Action UndoRequested;

	/// <inheritdoc cref="UndoRequested"/>
	public event Action RedoRequested;

	/// <summary>Ids of the View menu's items, as set in the scene.</summary>
	private enum ViewMenuItem
	{
		ZoomIn,
		ZoomOut,
		ResetZoom,
		FrameDiagram,
	}

	/// <summary>Ids of the items of the menu a right-click opens.</summary>
	private enum ContextMenuItem
	{
		Rename,
		Delete,
	}

	/// <summary>What a left click on the canvas does.</summary>
	private enum CanvasMode
	{
		/// <summary>Selects a participant or a message, and drags it.</summary>
		Normal,

		/// <summary>Picks the sender's lifeline, then the receiver's, of a new message.</summary>
		AddingMessage,
	}

	/// <summary>
	/// How close, in screen pixels at any zoom, a click must land to a line to
	/// pick it.
	/// </summary>
	private const float PickDistance = 6.0f;

	/// <summary>
	/// How close, in screen pixels at any zoom, a click must land to a lifeline
	/// to pick it for a new message.
	/// </summary>
	private const float LifelinePickDistance = 12.0f;

	/// <summary>
	/// How far, in screen pixels, the mouse must move with the button held
	/// before a press on a participant or message counts as a drag.
	/// </summary>
	private const float DragThreshold = 4.0f;

	/// <summary>How far the selection frame stands off a participant's head.</summary>
	private const float SelectionMargin = 4.0f;

	[Export]
	private Color BackgroundColor = new(0.180392f, 0.180392f, 0.180392f);

	/// <summary>The color of selected participants and messages, and of picking.</summary>
	[Export]
	private Color SelectionColor = new(0.352941f, 0.6f, 1.0f);

	[Export]
	private Color LifelineColor = new(0.623529f, 0.623529f, 0.623529f);

	private CanvasCamera world;
	private ColorRect grayOut;
	private Control menuPanel;
	private MenuBar menuBar;
	private PopupMenu addMenu;
	private PopupMenu messageMenu;
	private Label hintLabel;
	private PopupMenu contextMenu;
	private EditPopup editPopup;

	private SequenceDiagram diagram = null;
	private SequenceLayout layout = null;

	/// <summary>
	/// Whether the code parsed the last time it was submitted. While it did not,
	/// <see cref="diagram"/> is the last good one, shown grayed out and closed to
	/// editing. False until the first diagram arrives, as the scene starts
	/// grayed out.
	/// </summary>
	private bool isDiagramValid = false;

	private readonly SequenceSelection selection = new();

	private CanvasMode mode = CanvasMode.Normal;

	/// <summary>The kind of the message being clicked out.</summary>
	private SequenceMessageKind messageKind;

	/// <summary>The index of the new message's sender once clicked, or -1.</summary>
	private int messageSender = -1;

	/// <summary>The lifeline a click would pick right now, highlighted, or -1.</summary>
	private int hoveredLifeline = -1;

	/// <summary>
	/// The last mouse position seen, in the canvas's own coordinates. The new
	/// message's preview ends here.
	/// </summary>
	private Vector2 pointerPosition;

	/// <summary>
	/// The participant (by index) pressed on and maybe being dragged, or -1.
	/// At most one of this and <see cref="pressedMessage"/> is set.
	/// </summary>
	private int pressedParticipant = -1;

	/// <summary>The message (by index) pressed on and maybe being dragged, or -1.</summary>
	private int pressedMessage = -1;

	/// <summary>Where the press started, in the canvas's own coordinates.</summary>
	private Vector2 pressPosition;

	/// <summary>Where the press started, in diagram coordinates, which panning does not move.</summary>
	private Vector2 pressDiagramPoint;

	/// <summary>Whether the press has moved far enough to be a drag.</summary>
	private bool isDragging = false;

	/// <summary>How far the pressed participant or message has been dragged, in diagram coordinates.</summary>
	private Vector2 dragOffset;

	/// <summary>
	/// The diagram the open context menu was built for, and what in it the
	/// menu acts on. A re-parse while the menu is open makes it stale.
	/// </summary>
	private SequenceDiagram menuDiagram;
	private SequenceParticipant menuParticipant;
	private SequenceMessage menuMessage;

	/// <summary>The name of the participant being renamed in the edit popup.</summary>
	private string renamedName;

	public override void _Ready()
	{
		// "%Name" finds the node marked as a unique name in this scene (Access as
		// Unique Name in the editor), wherever it sits in the tree.
		world = GetNode<CanvasCamera>("%World");
		grayOut = GetNode<ColorRect>("%GrayOut");
		menuPanel = GetNode<Control>("%MenuPanel");
		menuBar = GetNode<MenuBar>("%MenuBar");
		addMenu = GetNode<PopupMenu>("%Add");
		messageMenu = GetNode<PopupMenu>("%Message");
		hintLabel = GetNode<Label>("%HintLabel");
		contextMenu = GetNode<PopupMenu>("%ContextMenu");
		editPopup = GetNode<EditPopup>("%EditPopup");

		foreach (SequenceParticipantKind kind in Enum.GetValues<SequenceParticipantKind>())
		{
			addMenu.AddItem(kind.ToString(), (int)kind);
		}

		foreach (SequenceMessageKind kind in Enum.GetValues<SequenceMessageKind>())
		{
			messageMenu.AddItem(kind.ToString(), (int)kind);
		}

		addMenu.IdPressed += OnAddMenuIdPressed;
		messageMenu.IdPressed += OnMessageMenuIdPressed;
		GetNode<PopupMenu>("%View").IdPressed += OnViewMenuIdPressed;
		contextMenu.IdPressed += OnContextMenuIdPressed;
		editPopup.EditFinished += OnRenameFinished;
	}

	/// <summary>
	/// Shows <paramref name="newDiagram"/>, or, for <c>null</c> after a parse
	/// failure, grays out the last one shown and closes it to editing.
	/// </summary>
	public void RenderDiagram(SequenceDiagram newDiagram)
	{
		isDiagramValid = newDiagram != null;
		grayOut.Visible = !isDiagramValid;
		EndMode();
		CancelPress();
		SetMenuEnabled(addMenu, isDiagramValid);
		SetMenuEnabled(messageMenu, isDiagramValid && newDiagram.Participants.Count > 0);

		if (isDiagramValid)
		{
			diagram = newDiagram;
			layout = new SequenceLayout(diagram, MeasureText);
			selection.Update(diagram);
		}

		QueueRedraw();
	}

	/// <summary>
	/// Centers the diagram on the canvas at the largest zoom level, up to 100%,
	/// that fits all of it.
	/// </summary>
	public void FrameDiagram()
	{
		world.Frame(layout?.Bounds, GetCanvasRect());
		QueueRedraw();
	}

	/// <summary>
	/// The part of the canvas the diagram is visible in, below the menu bar.
	/// </summary>
	private Rect2 GetCanvasRect()
	{
		float top = menuPanel.Size.Y;
		return new Rect2(0.0f, top, Size.X, Mathf.Max(Size.Y - top, 0.0f));
	}

	/// <summary>
	/// Enables or grays out one of the menu bar's menus together with every
	/// item in it. Graying out the menu's title alone still lets it be opened
	/// and used.
	/// </summary>
	private void SetMenuEnabled(PopupMenu menu, bool enabled)
	{
		menuBar.SetMenuDisabled(menu.GetIndex(), !enabled);
		for (int i = 0; i < menu.ItemCount; i++)
		{
			menu.SetItemDisabled(i, !enabled);
		}
	}

	private Vector2 MeasureText(string text)
	{
		return GetThemeDefaultFont().GetStringSize(text, HorizontalAlignment.Left, -1, GetThemeDefaultFontSize());
	}

	private void OnAddMenuIdPressed(long id)
	{
		if (!isDiagramValid)
		{
			return;
		}

		SequenceParticipantKind kind = (SequenceParticipantKind)id;
		ParticipantAdded?.Invoke(kind, GetUniqueName(kind.ToString()));
	}

	/// <summary>
	/// <paramref name="baseName"/>, or, if a participant already has it, the
	/// first of <c>baseName2</c>, <c>baseName3</c>, … that none has.
	/// </summary>
	private string GetUniqueName(string baseName)
	{
		string name = baseName;
		for (int suffix = 2; diagram.Participants.Exists(participant => participant.Name == name); suffix++)
		{
			name = baseName + suffix;
		}

		return name;
	}

	private void OnMessageMenuIdPressed(long id)
	{
		if (!isDiagramValid || diagram.Participants.Count == 0)
		{
			return;
		}

		messageKind = (SequenceMessageKind)id;
		CancelPress();
		mode = CanvasMode.AddingMessage;
		messageSender = -1;
		hoveredLifeline = -1;
		MouseDefaultCursorShape = CursorShape.Cross;
		UpdateHint();
		QueueRedraw();
	}

	private void UpdateHint()
	{
		hintLabel.Text = messageSender < 0
			? $"{messageKind} message: click the sender's lifeline (Esc cancels)"
			: $"{messageKind} message: {diagram.Participants[messageSender].Name} → click the receiver's lifeline where the message goes (Esc cancels)";
		hintLabel.Show();
	}

	/// <summary>Leaves the message-adding mode, if it is on.</summary>
	private void EndMode()
	{
		if (mode == CanvasMode.Normal)
		{
			return;
		}

		mode = CanvasMode.Normal;
		messageSender = -1;
		hoveredLifeline = -1;
		MouseDefaultCursorShape = CursorShape.Arrow;
		hintLabel.Hide();
		QueueRedraw();
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

	public override void _Draw()
	{
		if (diagram == null)
		{
			return;
		}

		DrawSetTransform(world.Position, 0.0f, world.Scale);

		StyleBoxFlat box = GetThemeStylebox("panel", "PanelContainer") as StyleBoxFlat;
		SequenceDiagramDrawing drawing = new(this, box?.BgColor ?? BackgroundColor, box?.BorderColor ?? Colors.White);

		bool draggingParticipant = isDragging && pressedParticipant >= 0;
		for (int i = 0; i < diagram.Participants.Count; i++)
		{
			if (!draggingParticipant || i != pressedParticipant)
			{
				DrawParticipant(drawing, i);
			}
		}

		for (int a = 0; a < diagram.Activations.Count; a++)
		{
			Rect2 bar = layout.GetActivationRect(a);
			bar.Position += GetDragShift(diagram.Participants.IndexOf(diagram.Activations[a].Participant));
			drawing.DrawActivation(bar);
		}

		int selectedMessage = selection.GetMessageIndex(diagram);
		for (int n = 0; n < diagram.Messages.Count; n++)
		{
			Vector2 offset = isDragging && n == pressedMessage ? new Vector2(0.0f, dragOffset.Y) : Vector2.Zero;
			drawing.DrawMessage(
				diagram.Messages[n],
				layout.GetMessagePath(n),
				layout.GetLabelRect(n),
				offset,
				n == selectedMessage || (isDragging && n == pressedMessage) ? SelectionColor : Colors.White
			);
		}

		// Last, so the dragged head passes over everything it is dragged across.
		if (draggingParticipant)
		{
			DrawParticipant(drawing, pressedParticipant);
		}

		if (mode == CanvasMode.AddingMessage && messageSender >= 0)
		{
			DrawMessagePreview(drawing);
		}
	}

	/// <summary>
	/// Draws a participant's head and lifeline, framing it when it is selected
	/// and highlighting its lifeline when the Message menu has picked it or
	/// would pick it.
	/// </summary>
	private void DrawParticipant(SequenceDiagramDrawing drawing, int participantIndex)
	{
		Rect2 head = layout.GetHeadRect(participantIndex);
		head.Position += GetDragShift(participantIndex);

		bool picked =
			mode == CanvasMode.AddingMessage
			&& (participantIndex == hoveredLifeline || participantIndex == messageSender);
		drawing.DrawParticipant(
			diagram.Participants[participantIndex],
			head,
			layout.LifelineBottom,
			picked ? SelectionColor : LifelineColor
		);

		if (diagram.Participants[participantIndex] == selection.GetParticipant(diagram))
		{
			DrawRect(head.Grow(SelectionMargin), SelectionColor, false, 2.0f);
		}
	}

	/// <summary>How far the participant is shown off its place while it is dragged.</summary>
	private Vector2 GetDragShift(int participantIndex)
	{
		return isDragging && participantIndex == pressedParticipant
			? new Vector2(dragOffset.X, 0.0f)
			: Vector2.Zero;
	}

	/// <summary>
	/// Draws the new message from the sender's lifeline to the mouse, at the
	/// mouse's height: to the lifeline under the mouse if there is one, and as
	/// a loop when that is the sender's own.
	/// </summary>
	private void DrawMessagePreview(SequenceDiagramDrawing drawing)
	{
		Vector2 mouse = world.ToDiagram(pointerPosition);
		float senderX = layout.GetLifelineX(messageSender);
		int receiver = GetLifelineAt(pointerPosition);

		Vector2[] points;
		if (receiver == messageSender)
		{
			float loopX = senderX + SequenceLayout.ActivationWidth / 2.0f + SequenceLayout.SelfMessageWidth;
			float returnY = mouse.Y + SequenceLayout.SelfMessageDrop;
			points = [
				new Vector2(senderX, mouse.Y),
				new Vector2(loopX, mouse.Y),
				new Vector2(loopX, returnY),
				new Vector2(senderX, returnY),
			];
		}
		else
		{
			float endX = receiver >= 0 ? layout.GetLifelineX(receiver) : mouse.X;
			if (Mathf.Abs(endX - senderX) < 1.0f)
			{
				return;
			}

			points = [new Vector2(senderX, mouse.Y), new Vector2(endX, mouse.Y)];
		}

		drawing.DrawArrow(messageKind, points, SelectionColor);
	}

	public override void _Input(InputEvent @event)
	{
		if (diagram == null)
		{
			return;
		}

		if (@event is InputEventMouse mouseEvent)
		{
			pointerPosition = ToLocal(mouseEvent.Position);
		}

		if (HasFocus() && HandleHistoryInput(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}

		bool handled = mode == CanvasMode.AddingMessage
			? HandleMessageModeInput(@event)
			: isDiagramValid && HandleEditInput(@event);
		if (handled)
		{
			GetViewport().SetInputAsHandled();
			return;
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
			UpdateCursor(moved);
		}
	}

	private void UpdateCursor(bool panning)
	{
		if (panning)
		{
			MouseDefaultCursorShape = CursorShape.Drag;
		}
		else if (mode == CanvasMode.AddingMessage)
		{
			MouseDefaultCursorShape = CursorShape.Cross;
		}
		else if (isDragging)
		{
			MouseDefaultCursorShape = pressedParticipant >= 0 ? CursorShape.Hsize : CursorShape.Vsize;
		}
		else
		{
			MouseDefaultCursorShape = CursorShape.Arrow;
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

	/// <summary>
	/// Converts an input event's position into the canvas's own coordinates.
	/// </summary>
	private Vector2 ToLocal(Vector2 eventPosition)
	{
		return GetGlobalTransform().AffineInverse() * eventPosition;
	}

	/// <summary>
	/// The participant whose head or lifeline is under
	/// <paramref name="localPosition"/>, or -1.
	/// </summary>
	private int GetLifelineAt(Vector2 localPosition)
	{
		return layout.GetLifelineAt(world.ToDiagram(localPosition), LifelinePickDistance / world.Scale.X);
	}

	/// <summary>
	/// Takes the input of the message-adding mode: a left click picks a
	/// lifeline, a right click or Cancel gives up. Returns whether the event
	/// was used up.
	/// </summary>
	private bool HandleMessageModeInput(InputEvent @event)
	{
		if (@event.IsActionPressed("Cancel"))
		{
			EndMode();
			return true;
		}

		if (@event is InputEventMouseMotion)
		{
			int hovered = GetCanvasRect().HasPoint(pointerPosition) ? GetLifelineAt(pointerPosition) : -1;
			if (hovered != hoveredLifeline || messageSender >= 0)
			{
				hoveredLifeline = hovered;
				QueueRedraw();
			}

			return false;
		}

		if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
		{
			return false;
		}

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

		GrabFocus();
		PickMessageEnd(GetLifelineAt(pointerPosition));
		return true;
	}

	/// <summary>
	/// Takes a click on <paramref name="participantIndex"/>'s lifeline as the
	/// new message's sender, or, once that is picked, as its receiver, the
	/// height of the click deciding where among the messages it goes. A click
	/// on the sender's lifeline again makes a self-message.
	/// </summary>
	private void PickMessageEnd(int participantIndex)
	{
		if (participantIndex < 0)
		{
			return;
		}

		if (messageSender < 0)
		{
			messageSender = participantIndex;
			UpdateHint();
			QueueRedraw();
			return;
		}

		// The mode ends before the message is announced, since writing it
		// re-parses the code and lays the diagram out again.
		SequenceParticipant from = diagram.Participants[messageSender];
		SequenceParticipant to = diagram.Participants[participantIndex];
		SequenceMessageKind kind = messageKind;
		int index = layout.GetInsertIndex(world.ToDiagram(pointerPosition).Y);
		EndMode();
		selection.SelectMessageAfterEdit(index);
		MessageAdded?.Invoke(from, to, kind, index);
	}

	/// <summary>
	/// Selects, drags, renames and deletes. A press on a participant's head or
	/// on a message selects it, and dragging it on moves it; a double-click on
	/// a head renames it; a press on empty canvas, or Cancel, clears the
	/// selection, and Delete deletes it. Returns whether the event was used up.
	/// </summary>
	private bool HandleEditInput(InputEvent @event)
	{
		// Only while the canvas has focus, so Delete still edits text in the
		// code editor and in the rename box.
		if (HasFocus() && @event.IsActionPressed("Delete"))
		{
			DeleteSelection();
			return true;
		}

		if (@event.IsActionPressed("Cancel"))
		{
			if (pressedParticipant >= 0 || pressedMessage >= 0)
			{
				CancelPress();
			}
			else
			{
				ClearSelection();
			}

			return false;
		}

		switch (@event)
		{
			case InputEventMouseMotion:
				UpdateDrag();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
				return OnLeftPress(press);
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
				FinishDrag();
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rightClick:
				OpenContextMenu(rightClick.Position);
				break;
		}

		return false;
	}

	/// <summary>
	/// Selects what was pressed on and gets ready to drag it, or opens the
	/// rename box for a double-click on a head. Returns whether the press is
	/// used up, which a rename must be: otherwise the canvas would take the
	/// focus for the click, and the box closes when it loses the focus.
	/// </summary>
	private bool OnLeftPress(InputEventMouseButton press)
	{
		bool onEditPopup = editPopup.Visible && editPopup.GetGlobalRect().HasPoint(press.Position);
		if (!GetCanvasRect().HasPoint(pointerPosition) || onEditPopup)
		{
			return false;
		}

		GrabFocus();
		Vector2 point = world.ToDiagram(pointerPosition);

		int participant = layout.GetParticipantAt(point);
		if (participant >= 0)
		{
			selection.SelectParticipant(diagram.Participants[participant]);
			QueueRedraw();
			if (press.DoubleClick)
			{
				CancelPress();
				StartRename(diagram.Participants[participant]);
				return true;
			}

			StartPress(participant, -1, point);
			return false;
		}

		int message = layout.GetMessageAt(point, PickDistance / world.Scale.X);
		if (message >= 0)
		{
			selection.SelectMessage(diagram, message);
			StartPress(-1, message, point);
			QueueRedraw();
			return false;
		}

		ClearSelection();
		return false;
	}

	private void StartPress(int participant, int message, Vector2 diagramPoint)
	{
		pressedParticipant = participant;
		pressedMessage = message;
		pressPosition = pointerPosition;
		dragOffset = Vector2.Zero;
		isDragging = false;
		pressDiagramPoint = diagramPoint;
	}

	/// <summary>
	/// Follows the mouse while a participant or message is held, once it has
	/// moved far enough from the press to count as a drag.
	/// </summary>
	private void UpdateDrag()
	{
		if (pressedParticipant < 0 && pressedMessage < 0)
		{
			return;
		}

		if (!isDragging && pointerPosition.DistanceTo(pressPosition) < DragThreshold)
		{
			return;
		}

		isDragging = true;
		dragOffset = world.ToDiagram(pointerPosition) - pressDiagramPoint;
		QueueRedraw();
	}

	/// <summary>
	/// Drops the dragged participant or message, moving it to the place among
	/// the others where it was let go, if that differs from where it was.
	/// </summary>
	private void FinishDrag()
	{
		if (!isDragging)
		{
			CancelPress();
			return;
		}

		int participant = pressedParticipant;
		int message = pressedMessage;
		Vector2 offset = dragOffset;
		CancelPress();

		if (participant >= 0)
		{
			int newIndex = layout.GetParticipantDropIndex(participant, layout.GetLifelineX(participant) + offset.X);
			if (newIndex != participant)
			{
				ParticipantMoved?.Invoke(diagram.Participants[participant], newIndex);
			}
		}
		else if (message >= 0)
		{
			int newIndex = layout.GetMessageDropIndex(message, layout.GetMessageY(message) + offset.Y);
			if (newIndex != message)
			{
				selection.SelectMessageAfterEdit(newIndex);
				MessageMoved?.Invoke(diagram.Messages[message], newIndex);
			}
		}
	}

	/// <summary>Lets go of the participant or message held, without moving it.</summary>
	private void CancelPress()
	{
		if (isDragging)
		{
			QueueRedraw();
		}

		pressedParticipant = -1;
		pressedMessage = -1;
		isDragging = false;
		dragOffset = Vector2.Zero;
	}

	private void ClearSelection()
	{
		if (selection.Clear())
		{
			QueueRedraw();
		}
	}

	/// <summary>Deletes the selected participant, with its messages, or the selected message.</summary>
	private void DeleteSelection()
	{
		SequenceParticipant participant = selection.GetParticipant(diagram);
		int message = selection.GetMessageIndex(diagram);

		// Cleared first: after the re-parse, a message written just like the
		// deleted one would otherwise take over its selection.
		selection.Clear();
		CancelPress();
		if (participant != null)
		{
			ParticipantRemoved?.Invoke(participant);
		}
		else if (message >= 0)
		{
			MessageRemoved?.Invoke(diagram.Messages[message]);
		}
	}

	/// <summary>
	/// Opens the menu for the participant or message under a right-click,
	/// selecting it first so it is clear what the menu acts on.
	/// </summary>
	private void OpenContextMenu(Vector2 eventPosition)
	{
		if (!GetCanvasRect().HasPoint(pointerPosition))
		{
			return;
		}

		GrabFocus();
		CancelPress();
		Vector2 point = world.ToDiagram(pointerPosition);
		int participant = layout.GetParticipantAt(point);
		int message = participant < 0 ? layout.GetMessageAt(point, PickDistance / world.Scale.X) : -1;

		contextMenu.Clear();
		menuDiagram = diagram;
		menuParticipant = participant >= 0 ? diagram.Participants[participant] : null;
		menuMessage = message >= 0 ? diagram.Messages[message] : null;
		if (menuParticipant != null)
		{
			selection.SelectParticipant(menuParticipant);
			contextMenu.AddItem("Rename...", (int)ContextMenuItem.Rename);
			contextMenu.AddSeparator();
			contextMenu.AddItem("Delete Participant", (int)ContextMenuItem.Delete, Key.Delete);
		}
		else if (menuMessage != null)
		{
			selection.SelectMessage(diagram, message);
			contextMenu.AddItem("Delete Message", (int)ContextMenuItem.Delete, Key.Delete);
		}
		else
		{
			return;
		}

		QueueRedraw();
		contextMenu.Popup(new Rect2I((Vector2I)eventPosition, Vector2I.Zero));
	}

	private void OnContextMenuIdPressed(long id)
	{
		// A re-parse while the menu was open leaves it pointing into an old diagram.
		if (menuDiagram != diagram || !isDiagramValid)
		{
			return;
		}

		switch ((ContextMenuItem)id)
		{
			case ContextMenuItem.Rename:
				StartRename(menuParticipant);
				break;
			case ContextMenuItem.Delete when menuParticipant != null:
				selection.Clear();
				ParticipantRemoved?.Invoke(menuParticipant);
				break;
			case ContextMenuItem.Delete:
				selection.Clear();
				MessageRemoved?.Invoke(menuMessage);
				break;
		}
	}

	/// <summary>Opens the box for typing a participant's new name, at the mouse.</summary>
	private void StartRename(SequenceParticipant participant)
	{
		renamedName = participant.Name;
		editPopup.ShowAtMousePosition(participant.Name);
	}

	/// <summary>
	/// Announces the name typed into the edit popup, looking the participant up
	/// again by its old name, since the code may have been re-parsed meanwhile.
	/// </summary>
	private void OnRenameFinished(string newName)
	{
		if (!isDiagramValid)
		{
			return;
		}

		SequenceParticipant participant = diagram.Participants.Find(p => p.Name == renamedName);
		if (participant != null && newName != participant.Name)
		{
			ParticipantNameChanged?.Invoke(participant, newName);
		}
	}
}
