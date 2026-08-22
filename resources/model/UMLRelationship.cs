/// <summary>
/// A line between two nodes. <see cref="Direction"/> says which ends carry the
/// decoration <see cref="Type"/> calls for; <c>From</c> and <c>To</c> stay in the
/// order the source code wrote them so a rewrite never has to reorder the line.
/// </summary>
public class UMLRelationship
{
	public UMLRelationship(
		UMLNode from,
		UMLNode to,
		UMLRelationshipType type = UMLRelationshipType.Association,
		UMLRelationshipDirection direction = UMLRelationshipDirection.None
	)
	{
		From = from;
		To = to;
		Type = type;
		Direction = direction;
	}

	public UMLNode From { get; set; }
	public UMLNode To { get; set; }
	public UMLRelationshipType Type { get; set; }
	public UMLRelationshipDirection Direction { get; set; }

	/// <summary>Text drawn at the middle of the line; empty when there is none.</summary>
	public string Label { get; set; } = "";

	/// <summary>Multiplicity drawn next to the <c>From</c> node; empty when there is none.</summary>
	public string FromMultiplicity { get; set; } = "";

	/// <summary>Multiplicity drawn next to the <c>To</c> node; empty when there is none.</summary>
	public string ToMultiplicity { get; set; } = "";

	/// <summary>
	/// Zero-based index of the line the relationship was written on, or -1 when it
	/// did not come from source code.
	/// </summary>
	public int SourceLine { get; set; } = -1;

	public UMLRelationshipEnding FromEnding =>
		Direction.DecoratesFrom() ? Type.GetEnding() : UMLRelationshipEnding.None;

	public UMLRelationshipEnding ToEnding =>
		Direction.DecoratesTo() ? Type.GetEnding() : UMLRelationshipEnding.None;

	public bool IsDashed => Type.IsDashed();
}
