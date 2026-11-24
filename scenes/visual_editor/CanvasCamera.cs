using System;
using Godot;

/// <summary>
/// The pan and zoom of a diagram canvas. It is the parent of everything on the
/// canvas that sits in diagram coordinates, and its position and scale map
/// those onto the canvas, its parent: panning moves it and zooming scales it,
/// in steps through <see cref="ZoomLevels"/>.
/// </summary>
// `partial` lets Godot's source generators add the glue code every C# node
// class needs.
public partial class CanvasCamera : Control
{
	/// <summary>The zoom levels, in percent, from farthest out to closest in.</summary>
	private static readonly int[] ZoomLevels = [
		20, 30, 40, 50, 60, 70, 80, 90, // 0-7
		100, // 8
		110, 120, 130, 140, 150, 160, 170, 180, 190, // 9-17
		200, // 18
		220, 240, 260, 280, // 19-22
		300, // 23
	];
	private const int DefaultZoomLevel = 8;

	/// <summary>
	/// How much room <see cref="Frame"/> leaves between the diagram and each
	/// side of the view.
	/// </summary>
	private const float FrameMargin = 32.0f;

	/// <summary>
	/// The scroll actions and the way each one pans the canvas. Only the first
	/// one pressed counts.
	/// </summary>
	private static readonly (string Action, Vector2 Direction)[] ScrollActions = [
		("ScrollUp", Vector2.Up),
		("ScrollDown", Vector2.Down),
		("ScrollLeft", Vector2.Left),
		("ScrollRight", Vector2.Right),
	];

	[Export]
	public float ScrollSensitivity { get; set; } = 5.0f;

	/// <summary>The current zoom, as an index into <see cref="ZoomLevels"/>.</summary>
	private int zoomLevel = DefaultZoomLevel;

	/// <summary>
	/// Whether the Drag button was pressed on the view and is still held, so
	/// the canvas pans even once the mouse leaves it, but not for a drag that
	/// started elsewhere, such as over the code editor.
	/// </summary>
	private bool isDragPanning = false;

	/// <summary>
	/// Converts a point in the canvas's own coordinates into diagram
	/// coordinates.
	/// </summary>
	public Vector2 ToDiagram(Vector2 canvasPosition)
	{
		return (canvasPosition - Position) / Scale;
	}

	/// <inheritdoc cref="ToDiagram(Vector2)"/>
	public Rect2 ToDiagram(Rect2 canvasRect)
	{
		return new Rect2(ToDiagram(canvasRect.Position), canvasRect.Size / Scale);
	}

	/// <summary>Zooms in one level around <paramref name="pivot"/>.</summary>
	public void ZoomIn(Vector2 pivot)
	{
		SetZoomLevel(zoomLevel + 1, pivot);
	}

	/// <summary>Zooms out one level around <paramref name="pivot"/>.</summary>
	public void ZoomOut(Vector2 pivot)
	{
		SetZoomLevel(zoomLevel - 1, pivot);
	}

	/// <summary>Goes back to 100% around <paramref name="pivot"/>.</summary>
	public void ResetZoom(Vector2 pivot)
	{
		SetZoomLevel(DefaultZoomLevel, pivot);
	}

	/// <summary>
	/// Changes the zoom level while keeping the canvas point under
	/// <paramref name="pivot"/> where it is.
	/// </summary>
	private void SetZoomLevel(int level, Vector2 pivot)
	{
		zoomLevel = Math.Clamp(level, 0, ZoomLevels.Length - 1);

		float scale = ZoomLevels[zoomLevel] / 100f;
		Vector2 diagramPivot = ToDiagram(pivot);
		Scale = Vector2.One * scale;
		Position = pivot - diagramPivot * Scale;
	}

	/// <summary>
	/// Centers <paramref name="bounds"/>, in diagram coordinates, on
	/// <paramref name="view"/>, the visible part of the canvas, at the largest
	/// zoom level, up to 100%, that fits all of it. With nothing to frame, it
	/// goes back to 100% with the diagram's origin at the view's corner.
	/// </summary>
	public void Frame(Rect2? bounds, Rect2 view)
	{
		if (bounds == null)
		{
			ResetZoom(view.GetCenter());
			Position = view.Position;
			return;
		}

		Vector2 available = view.Size - 2.0f * FrameMargin * Vector2.One;
		int level = DefaultZoomLevel;
		while (level > 0 && !FitsIn(bounds.Value.Size * (ZoomLevels[level] / 100f), available))
		{
			level--;
		}

		SetZoomLevel(level, Vector2.Zero);
		Position = view.GetCenter() - bounds.Value.GetCenter() * Scale;
	}

	private static bool FitsIn(Vector2 size, Vector2 available)
	{
		return size.X <= available.X && size.Y <= available.Y;
	}

	/// <summary>
	/// Pans and zooms for the mouse: the wheel pans, the wheel with ZoomMode
	/// held zooms around the mouse, and dragging with the Drag or AltDrag
	/// buttons pans. Only input over <paramref name="view"/>, the visible part
	/// of the canvas, counts, except that a Drag started there keeps panning
	/// until released. Returns whether the view moved. The event is never
	/// marked as handled.
	/// </summary>
	public bool HandleNavigationInput(InputEvent @event, Rect2 view)
	{
		if (@event is InputEventMouseButton buttonEvent)
		{
			bool overView = view.HasPoint(ToCanvas(buttonEvent.Position));
			if (buttonEvent.IsAction("Drag"))
			{
				isDragPanning = buttonEvent.Pressed && overView;
			}

			return overView && HandleWheel();
		}

		if (@event is InputEventMouseMotion motionEvent)
		{
			bool panning =
				(isDragPanning && Input.IsActionPressed("Drag"))
				|| (Input.IsActionPressed("AltDrag") && view.HasPoint(ToCanvas(motionEvent.Position)));
			if (panning)
			{
				Position += motionEvent.Relative;
			}

			return panning;
		}

		return false;
	}

	/// <summary>
	/// Zooms or pans for a wheel turn over the view. Returns whether it did.
	/// </summary>
	private bool HandleWheel()
	{
		if (Input.IsActionPressed("ZoomMode"))
		{
			if (Input.IsActionJustPressed("ZoomIn"))
			{
				ZoomIn(GetParentControl().GetLocalMousePosition());
				return true;
			}

			if (Input.IsActionJustPressed("ZoomOut"))
			{
				ZoomOut(GetParentControl().GetLocalMousePosition());
				return true;
			}

			return false;
		}

		// TODO: Make scrolling smoother on touchpads
		foreach ((string action, Vector2 direction) in ScrollActions)
		{
			if (Input.IsActionJustPressed(action))
			{
				Position += ScrollSensitivity * direction;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Converts an input event's position into the canvas's own coordinates,
	/// in which the camera's position and the view are given.
	/// </summary>
	private Vector2 ToCanvas(Vector2 eventPosition)
	{
		return GetParentControl().GetGlobalTransform().AffineInverse() * eventPosition;
	}
}
