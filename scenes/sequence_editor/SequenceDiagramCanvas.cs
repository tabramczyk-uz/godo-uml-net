using Godot;

/// <summary>
/// The canvas of a sequence diagram: participants left to right, each heading
/// its lifeline, messages top to bottom and the activation bars they imply,
/// all placed by <see cref="SequenceLayout"/>.
/// </summary>
// `partial` lets Godot's source generators add the glue code every C# node
// class needs.
public partial class SequenceDiagramCanvas : Control
{
	/// <summary>Ids of the View menu's items, as set in the scene.</summary>
	private enum ViewMenuItem
	{
		ZoomIn,
		ZoomOut,
		ResetZoom,
		FrameDiagram,
	}

	[Export]
	private Color BackgroundColor = new(0.180392f, 0.180392f, 0.180392f);

	[Export]
	private Color LifelineColor = new(0.623529f, 0.623529f, 0.623529f);

	private CanvasCamera world;
	private ColorRect grayOut;
	private Control menuPanel;

	private SequenceDiagram diagram = null;
	private SequenceLayout layout = null;

	public override void _Ready()
	{
		// "%Name" finds the node marked as a unique name in this scene (Access as
		// Unique Name in the editor), wherever it sits in the tree.
		world = GetNode<CanvasCamera>("%World");
		grayOut = GetNode<ColorRect>("%GrayOut");
		menuPanel = GetNode<Control>("%MenuPanel");

		GetNode<PopupMenu>("%View").IdPressed += OnViewMenuIdPressed;
	}

	/// <summary>
	/// Shows <paramref name="newDiagram"/>, or, for <c>null</c> after a parse
	/// failure, grays out the last one shown.
	/// </summary>
	public void RenderDiagram(SequenceDiagram newDiagram)
	{
		grayOut.Visible = newDiagram == null;
		if (newDiagram != null)
		{
			diagram = newDiagram;
			layout = new SequenceLayout(diagram, MeasureText);
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

	private Vector2 MeasureText(string text)
	{
		return GetThemeDefaultFont().GetStringSize(text, HorizontalAlignment.Left, -1, GetThemeDefaultFontSize());
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

		for (int i = 0; i < diagram.Participants.Count; i++)
		{
			drawing.DrawParticipant(
				diagram.Participants[i],
				layout.GetHeadRect(i),
				layout.LifelineBottom,
				LifelineColor
			);
		}

		for (int a = 0; a < diagram.Activations.Count; a++)
		{
			drawing.DrawActivation(layout.GetActivationRect(a));
		}

		for (int n = 0; n < diagram.Messages.Count; n++)
		{
			drawing.DrawMessage(
				diagram.Messages[n],
				layout.GetMessagePath(n),
				layout.GetLabelRect(n),
				Colors.White
			);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (diagram == null)
		{
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
			MouseDefaultCursorShape = moved ? CursorShape.Drag : CursorShape.Arrow;
		}
	}
}
