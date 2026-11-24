using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The relationship lines of the class canvas: where each one runs between
/// the boxes of its two nodes, which one a point picks, and how they are
/// drawn. It is a plain class, not a node, so the geometry runs in the tests
/// without Godot; only the Draw methods need the engine. All positions are in
/// diagram coordinates.
/// </summary>
public class RelationshipLines
{
	private const float EndingLength = 16.0f;
	private const float EndingHalfWidth = 7.0f;
	private const float LabelMargin = 4.0f;

	/// <summary>The box each node covers on the canvas.</summary>
	private readonly Func<UMLNode, Rect2> getRect;

	public RelationshipLines(Func<UMLNode, Rect2> getRect)
	{
		this.getRect = getRect;
	}

	/// <summary>
	/// Where a relationship's line meets the edges of its two nodes. Drawing
	/// and picking both go through here so a click lands on exactly the line
	/// that is shown. False when the nodes overlap so much that no line is
	/// drawn.
	/// </summary>
	public bool TryGetEdges(UMLRelationship relationship, out Vector2 fromEdge, out Vector2 toEdge)
	{
		Rect2 fromRect = getRect(relationship.From);
		Rect2 toRect = getRect(relationship.To);
		Vector2 fromCenter = fromRect.GetCenter();
		Vector2 toCenter = toRect.GetCenter();

		fromEdge = ClipToOutline(relationship.From, fromRect, toCenter);
		toEdge = ClipToOutline(relationship.To, toRect, fromCenter);

		return (toEdge - fromEdge).LengthSquared() >= 0.0001f;
	}

	/// <summary>
	/// The relationship whose line passes closest to <paramref name="point"/>,
	/// if any passes within <paramref name="maxDistance"/> of it.
	/// </summary>
	public UMLRelationship GetRelationshipAt(
		IEnumerable<UMLRelationship> relationships,
		Vector2 point,
		float maxDistance
	)
	{
		float closestDistance = maxDistance;
		UMLRelationship closest = null;

		foreach (UMLRelationship relationship in relationships)
		{
			if (!TryGetEdges(relationship, out Vector2 fromEdge, out Vector2 toEdge))
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
	/// Where a line from the middle of <paramref name="node"/> toward
	/// <paramref name="towards"/> crosses the node's outline: its ellipse for a
	/// use case, its bounding box for everything else.
	/// </summary>
	private static Vector2 ClipToOutline(UMLNode node, Rect2 rect, Vector2 towards)
	{
		return node.Type == UMLNodeType.UseCase
			? UMLGeometry.ClipToEllipse(rect, towards)
			: UMLGeometry.ClipToRect(rect, rect.GetCenter(), towards);
	}

	/// <summary>
	/// Draws a relationship's line, endings, label and multiplicities on
	/// <paramref name="canvas"/>. Hollow endings are filled with
	/// <paramref name="background"/> so the line does not show through them.
	/// </summary>
	public void Draw(Control canvas, UMLRelationship relationship, Color color, Color background)
	{
		if (!TryGetEdges(relationship, out Vector2 fromEdge, out Vector2 toEdge))
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
			canvas.DrawDashedLine(lineStart, lineEnd, color, 2.0f, 6.0f);
		}
		else
		{
			canvas.DrawLine(lineStart, lineEnd, color, 2.0f, true);
		}

		DrawEnding(canvas, relationship.FromEnding, fromEdge, direction, color, background);
		DrawEnding(canvas, relationship.ToEnding, toEdge, -direction, color, background);

		Vector2 perpendicular = new(-direction.Y, direction.X);

		if (!string.IsNullOrEmpty(relationship.Label))
		{
			DrawText(canvas, relationship.Label, (fromEdge + toEdge) / 2.0f + perpendicular * LabelMargin, color);
		}

		if (!string.IsNullOrEmpty(relationship.FromMultiplicity))
		{
			DrawText(
				canvas,
				relationship.FromMultiplicity,
				fromEdge + direction * (fromEndingLength + LabelMargin) + perpendicular * LabelMargin,
				color
			);
		}

		if (!string.IsNullOrEmpty(relationship.ToMultiplicity))
		{
			DrawText(
				canvas,
				relationship.ToMultiplicity,
				toEdge - direction * (toEndingLength + LabelMargin) + perpendicular * LabelMargin,
				color
			);
		}
	}

	/// <summary>
	/// Draws a relationship of <paramref name="type"/> still being clicked out,
	/// from the edge of <paramref name="sourceRect"/> to <paramref name="end"/>,
	/// styled like the line it will become. With <paramref name="decorated"/>,
	/// its ending sits at <paramref name="end"/>.
	/// </summary>
	public static void DrawPreview(
		Control canvas,
		UMLRelationshipType type,
		bool decorated,
		Rect2 sourceRect,
		Vector2 end,
		Color color,
		Color background
	)
	{
		if (sourceRect.HasPoint(end))
		{
			return;
		}

		Vector2 start = UMLGeometry.ClipToRect(sourceRect, sourceRect.GetCenter(), end);
		Vector2 delta = end - start;
		if (delta.LengthSquared() < 0.0001f)
		{
			return;
		}

		Vector2 direction = delta.Normalized();
		Vector2 lineEnd = decorated ? end - direction * EndingLength : end;

		if (type.IsDashed())
		{
			canvas.DrawDashedLine(start, lineEnd, color, 2.0f, 6.0f);
		}
		else
		{
			canvas.DrawLine(start, lineEnd, color, 2.0f, true);
		}

		if (decorated)
		{
			DrawEnding(canvas, type.GetEnding(), end, -direction, color, background);
		}
	}

	private static void DrawText(Control canvas, string text, Vector2 position, Color color)
	{
		Font font = canvas.GetThemeDefaultFont();
		int fontSize = canvas.GetThemeDefaultFontSize();
		canvas.DrawString(font, position, text, HorizontalAlignment.Left, -1, fontSize, color);
	}

	/// <summary>
	/// Draws the shape a relationship's ending calls for, with its tip touching
	/// the node at <paramref name="tip"/> and its body spreading out along
	/// <paramref name="outward"/>, the direction away from that node.
	/// </summary>
	private static void DrawEnding(
		Control canvas,
		UMLRelationshipEnding ending,
		Vector2 tip,
		Vector2 outward,
		Color color,
		Color background
	)
	{
		if (ending == UMLRelationshipEnding.None)
		{
			return;
		}

		Vector2 perpendicular = new(-outward.Y, outward.X);

		if (ending == UMLRelationshipEnding.OpenArrow)
		{
			Vector2 baseCenter = tip + outward * EndingLength;
			canvas.DrawLine(tip, baseCenter + perpendicular * EndingHalfWidth, color, 2.0f, true);
			canvas.DrawLine(tip, baseCenter - perpendicular * EndingHalfWidth, color, 2.0f, true);
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
			canvas.DrawColoredPolygon(points, color);
		}
		else
		{
			canvas.DrawColoredPolygon(points, background);
			canvas.DrawPolyline([.. points, points[0]], color, 2.0f, true);
		}
	}

	private static float GetEndingLength(UMLRelationshipEnding ending)
	{
		return ending == UMLRelationshipEnding.None ? 0.0f : EndingLength;
	}
}
