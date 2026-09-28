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
}
