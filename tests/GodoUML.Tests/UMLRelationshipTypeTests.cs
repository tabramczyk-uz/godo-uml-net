using System.Collections.Generic;
using Xunit;

namespace GodoUML.Tests;

public class UMLRelationshipTypeTests
{
	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData(UMLRelationshipEnding.None, UMLRelationshipEnding.None, false, UMLRelationshipType.Association, UMLRelationshipDirection.None)]
	[InlineData(UMLRelationshipEnding.None, UMLRelationshipEnding.None, true, UMLRelationshipType.Dependency, UMLRelationshipDirection.None)]
	[InlineData(UMLRelationshipEnding.None, UMLRelationshipEnding.OpenArrow, false, UMLRelationshipType.Association, UMLRelationshipDirection.Forward)]
	[InlineData(UMLRelationshipEnding.OpenArrow, UMLRelationshipEnding.None, true, UMLRelationshipType.Dependency, UMLRelationshipDirection.Backward)]
	[InlineData(UMLRelationshipEnding.OpenArrow, UMLRelationshipEnding.OpenArrow, false, UMLRelationshipType.Association, UMLRelationshipDirection.Both)]
	[InlineData(UMLRelationshipEnding.HollowTriangle, UMLRelationshipEnding.None, false, UMLRelationshipType.Generalization, UMLRelationshipDirection.Backward)]
	[InlineData(UMLRelationshipEnding.None, UMLRelationshipEnding.HollowTriangle, true, UMLRelationshipType.Realization, UMLRelationshipDirection.Forward)]
	[InlineData(UMLRelationshipEnding.HollowDiamond, UMLRelationshipEnding.None, false, UMLRelationshipType.Aggregation, UMLRelationshipDirection.Backward)]
	[InlineData(UMLRelationshipEnding.None, UMLRelationshipEnding.FilledDiamond, false, UMLRelationshipType.Composition, UMLRelationshipDirection.Forward)]
	public void ReadsTheRelationshipFromItsEndings(
		UMLRelationshipEnding fromEnding,
		UMLRelationshipEnding toEnding,
		bool isDashed,
		UMLRelationshipType expectedType,
		UMLRelationshipDirection expectedDirection
	)
	{
		UMLRelationshipTypeExtensions.FromEndings(
			fromEnding,
			toEnding,
			isDashed,
			out UMLRelationshipType type,
			out UMLRelationshipDirection direction
		);

		Assert.Equal(expectedType, type);
		Assert.Equal(expectedDirection, direction);
	}

	[Theory]
	[InlineData(UMLRelationshipEnding.HollowTriangle, UMLRelationshipEnding.OpenArrow, UMLRelationshipType.Generalization, UMLRelationshipDirection.Backward)]
	[InlineData(UMLRelationshipEnding.OpenArrow, UMLRelationshipEnding.FilledDiamond, UMLRelationshipType.Composition, UMLRelationshipDirection.Forward)]
	[InlineData(UMLRelationshipEnding.FilledDiamond, UMLRelationshipEnding.HollowDiamond, UMLRelationshipType.Composition, UMLRelationshipDirection.Backward)]
	[InlineData(UMLRelationshipEnding.HollowTriangle, UMLRelationshipEnding.HollowDiamond, UMLRelationshipType.Aggregation, UMLRelationshipDirection.Forward)]
	public void LetsTheStrongerEndingDecideWhenTheEndsDiffer(
		UMLRelationshipEnding fromEnding,
		UMLRelationshipEnding toEnding,
		UMLRelationshipType expectedType,
		UMLRelationshipDirection expectedDirection
	)
	{
		UMLRelationshipTypeExtensions.FromEndings(
			fromEnding,
			toEnding,
			false,
			out UMLRelationshipType type,
			out UMLRelationshipDirection direction
		);

		Assert.Equal(expectedType, type);
		Assert.Equal(expectedDirection, direction);
	}

	[Theory]
	[InlineData(UMLRelationshipEnding.HollowDiamond, UMLRelationshipType.Aggregation)]
	[InlineData(UMLRelationshipEnding.FilledDiamond, UMLRelationshipType.Composition)]
	public void IgnoresADashedLineOnADiamond(UMLRelationshipEnding ending, UMLRelationshipType expectedType)
	{
		UMLRelationshipTypeExtensions.FromEndings(
			ending,
			ending,
			true,
			out UMLRelationshipType type,
			out UMLRelationshipDirection direction
		);

		Assert.Equal(expectedType, type);
		Assert.Equal(UMLRelationshipDirection.Both, direction);
	}

	/// <summary>
	/// Every relationship a line can draw. Only associations and dependencies can
	/// go undecorated: with no ending, the others would read back as one of those.
	/// </summary>
	public static IEnumerable<object[]> EveryRelationship()
	{
		foreach (UMLRelationshipType type in System.Enum.GetValues<UMLRelationshipType>())
		{
			foreach (UMLRelationshipDirection direction in System.Enum.GetValues<UMLRelationshipDirection>())
			{
				bool canBeUndecorated = type is UMLRelationshipType.Association or UMLRelationshipType.Dependency;
				if (direction != UMLRelationshipDirection.None || canBeUndecorated)
				{
					yield return [type, direction];
				}
			}
		}
	}

	[Theory]
	[MemberData(nameof(EveryRelationship))]
	public void ReadsBackTheEndingsEveryRelationshipIsDrawnWith(
		UMLRelationshipType type,
		UMLRelationshipDirection direction
	)
	{
		UMLRelationshipEnding fromEnding = direction.DecoratesFrom() ? type.GetEnding() : UMLRelationshipEnding.None;
		UMLRelationshipEnding toEnding = direction.DecoratesTo() ? type.GetEnding() : UMLRelationshipEnding.None;

		UMLRelationshipTypeExtensions.FromEndings(
			fromEnding,
			toEnding,
			type.IsDashed(),
			out UMLRelationshipType readType,
			out UMLRelationshipDirection readDirection
		);

		Assert.Equal(type, readType);
		Assert.Equal(direction, readDirection);
	}
}
