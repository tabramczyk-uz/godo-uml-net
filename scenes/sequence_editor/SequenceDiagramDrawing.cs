using System.Collections.Generic;
using Godot;

/// <summary>
/// Draws the parts of a sequence diagram where <see cref="SequenceLayout"/>
/// puts them: heads, lifelines, activation bars and message arrows. The canvas
/// decides what to draw and in which color; this only knows the shapes. Every
/// position is in diagram coordinates, so the canvas sets its camera's
/// transform before drawing.
/// </summary>
public class SequenceDiagramDrawing
{
	private const float LineWidth = 2.0f;
	private const float ArrowLength = 12.0f;
	private const float ArrowHalfWidth = 6.0f;

	private readonly Control canvas;
	private readonly Font font;
	private readonly int fontSize;

	/// <summary>The fill of heads and activation bars.</summary>
	private readonly Color boxColor;

	/// <summary>The outline of heads and activation bars, and the color of names.</summary>
	private readonly Color borderColor;

	public SequenceDiagramDrawing(Control canvas, Color boxColor, Color borderColor)
	{
		this.canvas = canvas;
		this.boxColor = boxColor;
		this.borderColor = borderColor;
		font = canvas.GetThemeDefaultFont();
		fontSize = canvas.GetThemeDefaultFontSize();
	}

	/// <summary>
	/// Draws a participant's head in <paramref name="head"/>, as a box with its
	/// name or as a stick figure with its name underneath, and its lifeline
	/// down to <paramref name="bottom"/>.
	/// </summary>
	public void DrawParticipant(
		SequenceParticipant participant,
		Rect2 head,
		float bottom,
		Color lifelineColor
	)
	{
		float x = head.GetCenter().X;
		canvas.DrawDashedLine(new Vector2(x, head.End.Y), new Vector2(x, bottom), lifelineColor, 1.0f, 6.0f);

		if (participant.Kind == SequenceParticipantKind.Actor)
		{
			float nameHeight = font.GetHeight(fontSize);
			Rect2 figure = new(x - 12.0f, head.Position.Y, 24.0f, head.Size.Y - nameHeight - 2.0f);
			DrawStickFigure(figure);
			DrawCenteredText(participant.Name, new Rect2(head.Position.X, head.End.Y - nameHeight, head.Size.X, nameHeight), borderColor);
			return;
		}

		canvas.DrawRect(head, boxColor);
		canvas.DrawRect(head, borderColor, false, LineWidth);
		DrawCenteredText(participant.Name, head, borderColor);
	}

	/// <summary>Draws an activation bar.</summary>
	public void DrawActivation(Rect2 bar)
	{
		canvas.DrawRect(bar, boxColor);
		canvas.DrawRect(bar, borderColor, false, LineWidth);
	}

	/// <summary>
	/// Draws a message's arrow along <paramref name="path"/> with the head its
	/// kind calls for at the last point, and its label in
	/// <paramref name="labelRect"/>.
	/// </summary>
	public void DrawMessage(
		SequenceMessage message,
		IReadOnlyList<Vector2> path,
		Rect2 labelRect,
		Color color
	)
	{
		DrawArrow(message.Kind, [.. path], color);
		if (message.Label.Length > 0)
		{
			Vector2 baseline = labelRect.Position + Vector2.Down * font.GetAscent(fontSize);
			canvas.DrawString(font, baseline, message.Label, HorizontalAlignment.Left, -1, fontSize, color);
		}
	}

	/// <summary>
	/// Draws an arrow of <paramref name="kind"/> through
	/// <paramref name="points"/>: dashed for a reply, with a filled head for a
	/// synchronous message and an open one otherwise.
	/// </summary>
	public void DrawArrow(SequenceMessageKind kind, Vector2[] points, Color color)
	{
		// `^1` counts from the end: the last point, and `^2` the one before it.
		Vector2 tip = points[^1];
		Vector2 outward = (points[^2] - tip).Normalized();
		bool filled = kind == SequenceMessageKind.Synchronous;

		// A filled head covers the end of the line, so the line stops at its base.
		Vector2[] line = (Vector2[])points.Clone();
		if (filled)
		{
			line[^1] = tip + outward * ArrowLength;
		}

		for (int i = 1; i < line.Length; i++)
		{
			if (kind == SequenceMessageKind.Reply)
			{
				canvas.DrawDashedLine(line[i - 1], line[i], color, LineWidth, 6.0f);
			}
			else
			{
				canvas.DrawLine(line[i - 1], line[i], color, LineWidth, true);
			}
		}

		Vector2 perpendicular = new(-outward.Y, outward.X);
		Vector2 baseCenter = tip + outward * ArrowLength;
		Vector2 left = baseCenter + perpendicular * ArrowHalfWidth;
		Vector2 right = baseCenter - perpendicular * ArrowHalfWidth;
		if (filled)
		{
			canvas.DrawColoredPolygon([tip, left, right], color);
		}
		else
		{
			canvas.DrawLine(tip, left, color, LineWidth, true);
			canvas.DrawLine(tip, right, color, LineWidth, true);
		}
	}

	private void DrawCenteredText(string text, Rect2 rect, Color color)
	{
		float top = rect.Position.Y + (rect.Size.Y - font.GetHeight(fontSize)) / 2.0f;
		Vector2 baseline = new(rect.Position.X, top + font.GetAscent(fontSize));
		canvas.DrawString(font, baseline, text, HorizontalAlignment.Center, rect.Size.X, fontSize, color);
	}

	/// <summary>
	/// Draws a stick figure filling <paramref name="area"/>, in the proportions
	/// the class canvas's actors have.
	/// </summary>
	private void DrawStickFigure(Rect2 area)
	{
		Vector2 size = area.Size;
		Vector2 origin = area.Position;
		float headRadius = size.X / 4.0f;
		Vector2 head = origin + new Vector2(size.X / 2.0f, headRadius + LineWidth);
		Vector2 neck = head + Vector2.Down * headRadius;
		Vector2 hip = origin + new Vector2(size.X / 2.0f, size.Y * 0.62f);
		float shoulderY = neck.Y + (hip.Y - neck.Y) * 0.35f;

		canvas.DrawCircle(head, headRadius, boxColor);
		canvas.DrawArc(head, headRadius, 0.0f, Mathf.Tau, 32, borderColor, LineWidth, true);
		canvas.DrawLine(neck, hip, borderColor, LineWidth, true);
		canvas.DrawLine(new Vector2(origin.X, shoulderY), new Vector2(area.End.X, shoulderY), borderColor, LineWidth, true);
		canvas.DrawLine(hip, new Vector2(origin.X, area.End.Y), borderColor, LineWidth, true);
		canvas.DrawLine(hip, area.End, borderColor, LineWidth, true);
	}
}
