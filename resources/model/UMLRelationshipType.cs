/// <summary>
/// The UML relationships the language can spell out. What each one looks like on
/// the canvas follows from <see cref="UMLRelationshipTypeExtensions.GetEnding"/>
/// and <see cref="UMLRelationshipTypeExtensions.IsDashed"/>, so the renderer and
/// the PlantUML exporter never disagree about it.
/// </summary>
public enum UMLRelationshipType
{
	/// <summary>Solid line, optionally with an open arrow head.</summary>
	Association,

	/// <summary>Dashed line with an open arrow head.</summary>
	Dependency,

	/// <summary>Solid line with a hollow diamond at the whole end.</summary>
	Aggregation,

	/// <summary>Solid line with a filled diamond at the whole end.</summary>
	Composition,

	/// <summary>Solid line with a hollow triangle at the general end.</summary>
	Generalization,

	/// <summary>Dashed line with a hollow triangle at the interface end.</summary>
	Realization,
}

/// <summary>
/// Which ends of a relationship carry its decoration. <see cref="Forward"/>
/// decorates the <c>To</c> end, <see cref="Backward"/> the <c>From</c> end.
/// </summary>
public enum UMLRelationshipDirection
{
	None,
	Forward,
	Backward,
	Both,
}

/// <summary>
/// The shape drawn where a relationship meets a node.
/// </summary>
public enum UMLRelationshipEnding
{
	None,
	OpenArrow,
	HollowTriangle,
	HollowDiamond,
	FilledDiamond,
}

public static class UMLRelationshipTypeExtensions
{
	public static UMLRelationshipEnding GetEnding(this UMLRelationshipType type)
	{
		return type switch
		{
			UMLRelationshipType.Aggregation => UMLRelationshipEnding.HollowDiamond,
			UMLRelationshipType.Composition => UMLRelationshipEnding.FilledDiamond,
			UMLRelationshipType.Generalization => UMLRelationshipEnding.HollowTriangle,
			UMLRelationshipType.Realization => UMLRelationshipEnding.HollowTriangle,
			_ => UMLRelationshipEnding.OpenArrow,
		};
	}

	public static bool IsDashed(this UMLRelationshipType type)
	{
		return type is UMLRelationshipType.Dependency or UMLRelationshipType.Realization;
	}

	/// <summary>
	/// Whether one end of the relationship is the parent or whole of the other:
	/// generalization, realization, aggregation and composition. The decorated
	/// end is that parent or whole, which is what lets the auto-layout put it
	/// above the other node.
	/// </summary>
	public static bool IsHierarchical(this UMLRelationshipType type)
	{
		return type
			is UMLRelationshipType.Generalization
				or UMLRelationshipType.Realization
				or UMLRelationshipType.Aggregation
				or UMLRelationshipType.Composition;
	}

	/// <summary>
	/// Reads the relationship a line draws from the endings at its two ends and
	/// whether it is dashed: the inverse of <see cref="GetEnding"/> and
	/// <see cref="IsDashed"/>. The strongest ending decides the type; the ends
	/// carrying it decide the direction. Each grammar turns its own arrow
	/// spelling into endings and then reads them here, so the GodoUML language
	/// and PlantUML agree on what an arrow means.
	/// </summary>
	public static void FromEndings(
		UMLRelationshipEnding fromEnding,
		UMLRelationshipEnding toEnding,
		bool isDashed,
		out UMLRelationshipType type,
		out UMLRelationshipDirection direction
	)
	{
		UMLRelationshipEnding ending = fromEnding >= toEnding ? fromEnding : toEnding;

		type = ending switch
		{
			UMLRelationshipEnding.HollowTriangle => isDashed
				? UMLRelationshipType.Realization
				: UMLRelationshipType.Generalization,
			UMLRelationshipEnding.HollowDiamond => UMLRelationshipType.Aggregation,
			UMLRelationshipEnding.FilledDiamond => UMLRelationshipType.Composition,
			_ => isDashed ? UMLRelationshipType.Dependency : UMLRelationshipType.Association,
		};

		bool decoratesFrom = ending != UMLRelationshipEnding.None && fromEnding == ending;
		bool decoratesTo = ending != UMLRelationshipEnding.None && toEnding == ending;
		direction = (decoratesFrom, decoratesTo) switch
		{
			(true, true) => UMLRelationshipDirection.Both,
			(true, false) => UMLRelationshipDirection.Backward,
			(false, true) => UMLRelationshipDirection.Forward,
			_ => UMLRelationshipDirection.None,
		};
	}

	public static UMLRelationshipDirection Flip(this UMLRelationshipDirection direction)
	{
		return direction switch
		{
			UMLRelationshipDirection.Forward => UMLRelationshipDirection.Backward,
			UMLRelationshipDirection.Backward => UMLRelationshipDirection.Forward,
			_ => direction,
		};
	}

	public static bool DecoratesFrom(this UMLRelationshipDirection direction)
	{
		return direction is UMLRelationshipDirection.Backward or UMLRelationshipDirection.Both;
	}

	public static bool DecoratesTo(this UMLRelationshipDirection direction)
	{
		return direction is UMLRelationshipDirection.Forward or UMLRelationshipDirection.Both;
	}
}
