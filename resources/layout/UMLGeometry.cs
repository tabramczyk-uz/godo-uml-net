using Godot;

/// <summary>
/// Plain 2D math the canvas needs, kept free of engine calls so the tests can
/// run it without Godot.
/// </summary>
public static class UMLGeometry
{
	/// <summary>
	/// How far <paramref name="point"/> is from the nearest point of the segment
	/// running from <paramref name="start"/> to <paramref name="end"/>.
	/// </summary>
	public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		Vector2 segment = end - start;
		float lengthSquared = segment.LengthSquared();
		if (lengthSquared == 0.0f)
		{
			return point.DistanceTo(start);
		}

		float t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
		return point.DistanceTo(start + segment * t);
	}

	/// <summary>
	/// Where the segment from the center of <paramref name="rect"/> toward
	/// <paramref name="towards"/> leaves the ellipse inscribed in the rect, so
	/// lines meet a use case at its outline rather than its bounding box. Like
	/// clipping to a rect, it never goes past <paramref name="towards"/>.
	/// </summary>
	public static Vector2 ClipToEllipse(Rect2 rect, Vector2 towards)
	{
		Vector2 center = rect.GetCenter();
		Vector2 radii = rect.Size / 2.0f;
		Vector2 direction = towards - center;
		if (radii.X <= 0.0f || radii.Y <= 0.0f || direction == Vector2.Zero)
		{
			return center;
		}

		float scaled = (direction / radii).Length();
		return center + direction * Mathf.Min(1.0f / scaled, 1.0f);
	}

	/// <summary>
	/// <paramref name="count"/> points evenly spaced around the ellipse
	/// inscribed in <paramref name="rect"/>, for drawing it as a polygon.
	/// </summary>
	public static Vector2[] GetEllipsePoints(Rect2 rect, int count)
	{
		Vector2 center = rect.GetCenter();
		Vector2 radii = rect.Size / 2.0f;
		var points = new Vector2[count];
		for (int i = 0; i < count; i++)
		{
			float angle = Mathf.Tau * i / count;
			points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radii;
		}

		return points;
	}
}
