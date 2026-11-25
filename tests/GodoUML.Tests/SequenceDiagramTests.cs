using Xunit;

namespace GodoUML.Tests;

public class SequenceDiagramTests
{
	private static SequenceDiagram Parse(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	private static readonly SequenceDiagram Shop = Parse("actor User\nparticipant Shop\nUser -> Shop : order\n");

	private static SequenceParticipant User => Shop.Participants[0];

	[Fact]
	public void RejectsRenamingToANameAnotherParticipantHas()
	{
		Assert.Equal(
			"There is already a participant named \"Shop\".",
			Shop.DescribeRenameProblem(User, "Shop")
		);
	}

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData("Customer")]
	[InlineData("User")]
	[InlineData("shop")]
	public void AllowsAFreeNameTheSameNameOrADifferentCase(string newName)
	{
		Assert.Null(Shop.DescribeRenameProblem(User, newName));
	}

	[Theory]
	[InlineData("")]
	[InlineData("My User")]
	[InlineData("2User")]
	public void ReportsANameTheLanguageDoesNotAllow(string newName)
	{
		Assert.Equal(
			UMLSyntax.DescribeInvalidNodeName(newName),
			Shop.DescribeRenameProblem(User, newName)
		);
	}
}
