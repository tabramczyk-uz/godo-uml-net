using Xunit;

namespace GodoUML.Tests;

public class SequenceParserTests
{
	private static SequenceDiagram ParseOrFail(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.True(
			result.IsSuccess,
			$"Expected a successful parse but got: {result.ErrorMessage} on line {result.ErrorLineNumber}"
		);
		return result.Diagram;
	}

	private static SequenceParseResult ParseExpectingFailure(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.False(result.IsSuccess, "Expected the parse to fail");
		return result;
	}

	[Fact]
	public void EmptyCodeIsAnEmptyDiagram()
	{
		SequenceDiagram diagram = ParseOrFail("");

		Assert.Empty(diagram.Participants);
		Assert.Empty(diagram.Messages);
	}

	[Fact]
	public void ParticipantsKeepTheirDeclarationOrderKindAndLine()
	{
		SequenceDiagram diagram = ParseOrFail("participant Shop\n\nactor Customer\nparticipant Bank");

		Assert.Equal(["Shop", "Customer", "Bank"], diagram.Participants.ConvertAll(p => p.Name));
		Assert.Equal(
			[
				SequenceParticipantKind.Participant,
				SequenceParticipantKind.Actor,
				SequenceParticipantKind.Participant,
			],
			diagram.Participants.ConvertAll(p => p.Kind)
		);
		Assert.Equal([0, 2, 3], diagram.Participants.ConvertAll(p => p.SourceLine));
	}

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData("A -> B : pay", SequenceMessageKind.Synchronous)]
	[InlineData("A ->> B : pay", SequenceMessageKind.Asynchronous)]
	[InlineData("A --> B : pay", SequenceMessageKind.Reply)]
	[InlineData("A->B:pay", SequenceMessageKind.Synchronous)]
	[InlineData("A->>B:pay", SequenceMessageKind.Asynchronous)]
	[InlineData("A-->B:pay", SequenceMessageKind.Reply)]
	public void ReadsEachMessageKind(string message, SequenceMessageKind expected)
	{
		SequenceDiagram diagram = ParseOrFail("participant A\nparticipant B\n" + message);

		SequenceMessage parsed = Assert.Single(diagram.Messages);
		Assert.Equal(expected, parsed.Kind);
		Assert.Equal("A", parsed.From.Name);
		Assert.Equal("B", parsed.To.Name);
		Assert.Equal("pay", parsed.Label);
		Assert.Equal(2, parsed.SourceLine);
	}

	[Fact]
	public void MessagesPointAtTheDeclaredParticipants()
	{
		SequenceDiagram diagram = ParseOrFail("actor A\nparticipant B\nA -> B");

		SequenceMessage message = Assert.Single(diagram.Messages);
		Assert.Same(diagram.Participants[0], message.From);
		Assert.Same(diagram.Participants[1], message.To);
	}

	[Theory]
	[InlineData("A -> B", "")]
	[InlineData("A -> B :", "")]
	[InlineData("A -> B :   pay the bill  ", "pay the bill")]
	[InlineData("A -> B : total: 3", "total: 3")]
	[InlineData("A -> B : pay // ignored", "pay")]
	public void TheLabelIsOptionalAndTrimmed(string message, string expectedLabel)
	{
		SequenceDiagram diagram = ParseOrFail("participant A\nparticipant B\n" + message);

		Assert.Equal(expectedLabel, Assert.Single(diagram.Messages).Label);
	}

	[Theory]
	[InlineData("A -> A")]
	[InlineData("A -> A : recalculate")]
	[InlineData("A --> A")]
	public void AMessageToItselfIsASelfMessage(string message)
	{
		SequenceDiagram diagram = ParseOrFail("participant A\n" + message);

		SequenceMessage parsed = Assert.Single(diagram.Messages);
		Assert.True(parsed.IsSelfMessage);
		Assert.Same(parsed.From, parsed.To);
	}

	[Fact]
	public void AMessageBetweenTwoParticipantsIsNotASelfMessage()
	{
		SequenceDiagram diagram = ParseOrFail("participant A\nparticipant B\nA -> B");

		Assert.False(Assert.Single(diagram.Messages).IsSelfMessage);
	}

	[Fact]
	public void MessagesKeepSourceOrder()
	{
		SequenceDiagram diagram = ParseOrFail(
			"participant A\nparticipant B\nA -> B : first\nB --> A : second\n// note\nA ->> B : third"
		);

		Assert.Equal(["first", "second", "third"], diagram.Messages.ConvertAll(m => m.Label));
		Assert.Equal([2, 3, 5], diagram.Messages.ConvertAll(m => m.SourceLine));
	}

	[Fact]
	public void DeclarationsMayFollowMessagesOfOtherParticipants()
	{
		SequenceDiagram diagram = ParseOrFail(
			"participant A\nparticipant B\nA -> B\nparticipant C\nB -> C"
		);

		Assert.Equal(3, diagram.Participants.Count);
		Assert.Equal(2, diagram.Messages.Count);
	}

	[Fact]
	public void CommentsAndBlankLinesAreIgnored()
	{
		SequenceDiagram diagram = ParseOrFail(
			"// checkout\n\nparticipant A // the shop\n   \nparticipant B\nA -> B // pay"
		);

		Assert.Equal(2, diagram.Participants.Count);
		Assert.Single(diagram.Messages);
	}

	[Fact]
	public void ParsesTheBodyOfAFileWhoseHeaderWasRemoved()
	{
		string code = "// shop\ndiagram sequence\nparticipant A\nparticipant B\nA -> B";

		SequenceDiagram diagram = ParseOrFail(UMLDiagramHeader.Read(code).RemoveFrom(code));

		Assert.Equal(2, diagram.Participants[0].SourceLine);
		Assert.Equal(4, diagram.Messages[0].SourceLine);
	}

	[Theory]
	[InlineData("participant A\nA -> B", "Unknown participant: B", 1)]
	[InlineData("participant B\nA -> B", "Unknown participant: A", 1)]
	[InlineData("participant A\nA -> B\nparticipant B", "Unknown participant: B", 1)]
	[InlineData("participant A\nactor A", "Duplicate participant: A", 1)]
	[InlineData("participant A\nobject B", "Unknown declaration: object", 1)]
	[InlineData("participant A\n\tparticipant B", "Unexpected indentation", 1)]
	[InlineData("participant A\nparticipant", "Syntax error", 1)]
	[InlineData("participant A\nparticipant B\nA => B", "Syntax error", 2)]
	[InlineData("participant A\nparticipant B\nA -> B -> A", "Syntax error", 2)]
	[InlineData("diagram sequence\nparticipant A", "Unknown declaration: diagram", 0)]
	public void ReportsTheFirstErrorAndItsLine(string code, string expectedMessage, int expectedLine)
	{
		SequenceParseResult result = ParseExpectingFailure(code);

		Assert.Equal(expectedMessage, result.ErrorMessage);
		Assert.Equal(expectedLine, result.ErrorLineNumber);
	}

	[Fact]
	public void StopsAtTheFirstError()
	{
		SequenceParseResult result = ParseExpectingFailure("participant A\nA -> B\nA -> C");

		Assert.Equal("Unknown participant: B", result.ErrorMessage);
		Assert.Null(result.Diagram);
	}

	[Theory]
	[InlineData(SequenceMessageKind.Synchronous)]
	[InlineData(SequenceMessageKind.Asynchronous)]
	[InlineData(SequenceMessageKind.Reply)]
	public void GetOperatorIsReadBackAsTheSameMessageKind(SequenceMessageKind kind)
	{
		string messageOperator = SequenceSyntax.GetOperator(kind);

		Assert.True(SequenceSyntax.TryGetMessageKind(messageOperator, out SequenceMessageKind read));
		Assert.Equal(kind, read);
	}

	[Theory]
	[InlineData(SequenceParticipantKind.Participant)]
	[InlineData(SequenceParticipantKind.Actor)]
	public void GetKeywordIsReadBackAsTheSameParticipantKind(SequenceParticipantKind kind)
	{
		string keyword = SequenceSyntax.GetKeyword(kind);

		Assert.True(SequenceSyntax.TryGetParticipantKind(keyword, out SequenceParticipantKind read));
		Assert.Equal(kind, read);
	}
}
