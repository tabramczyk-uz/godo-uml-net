using System.Collections.Generic;
using Xunit;

namespace GodoUML.Tests;

public class SequenceCodeWriterTests
{
	private const string Header = "diagram sequence\n";

	/// <summary>Parses editor text the way the app will: header first, then the body.</summary>
	private static SequenceDiagram Parse(string code)
	{
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		Assert.True(header.IsSuccess, header.ErrorMessage);
		SequenceParseResult result = SequenceParser.Parse(header.RemoveFrom(code));
		Assert.True(
			result.IsSuccess,
			$"Expected a successful parse but got: {result.ErrorMessage} on line {result.ErrorLineNumber}"
		);
		return result.Diagram;
	}

	private static List<string> ParticipantNames(string code)
	{
		return Parse(code).Participants.ConvertAll(p => p.Name);
	}

	private static SequenceParticipant Participant(SequenceDiagram diagram, string name)
	{
		return diagram.Participants.Find(p => p.Name == name);
	}

	[Fact]
	public void AddsAParticipantBelowTheLastDeclaration()
	{
		string code = Header + "participant A\nparticipant B\nA -> B\n";

		string added = SequenceCodeWriter.AddParticipant(code, SequenceParticipantKind.Actor, "C");

		Assert.Equal(Header + "participant A\nparticipant B\nactor C\nA -> B\n", added);
		Assert.Equal(["A", "B", "C"], ParticipantNames(added));
	}

	[Fact]
	public void AddsTheFirstParticipantAtTheEnd()
	{
		string code = Header + "// a shop\n";

		string added = SequenceCodeWriter.AddParticipant(
			code,
			SequenceParticipantKind.Participant,
			"Shop"
		);

		Assert.Equal(Header + "// a shop\nparticipant Shop\n", added);
	}

	[Fact]
	public void AddingToCodeWithoutAFinalNewlineAppendsALine()
	{
		string added = SequenceCodeWriter.AddParticipant(
			"diagram sequence",
			SequenceParticipantKind.Participant,
			"A"
		);

		Assert.Equal("diagram sequence\nparticipant A", added);
	}

	[Fact]
	public void RenamesTheDeclarationAndEveryMessage()
	{
		string code = Header + "participant A\nparticipant B\nA -> B : ask\nB --> A\nA -> A";
		SequenceDiagram diagram = Parse(code);

		string renamed = SequenceCodeWriter.RenameParticipant(code, diagram.Participants[0], "C");

		Assert.Equal(
			Header + "participant C\nparticipant B\nC -> B : ask\nB --> C\nC -> C",
			renamed
		);
	}

	[Fact]
	public void RenamingKeepsCommentsSpacingAndLongerNames()
	{
		string code =
			Header + "actor  A   // the user\nparticipant AB\nA->>AB:go // first\nAB  -->  A";
		SequenceDiagram diagram = Parse(code);

		string renamed = SequenceCodeWriter.RenameParticipant(code, diagram.Participants[0], "User");

		Assert.Equal(
			Header
				+ "actor  User   // the user\nparticipant AB\nUser->>AB:go // first\nAB  -->  User",
			renamed
		);
	}

	[Fact]
	public void RenamingLeavesTheDiagramHeaderAlone()
	{
		string code = Header + "participant sequence\nparticipant B\nsequence -> B";
		SequenceDiagram diagram = Parse(code);

		string renamed = SequenceCodeWriter.RenameParticipant(code, diagram.Participants[0], "A");

		Assert.Equal(Header + "participant A\nparticipant B\nA -> B", renamed);
	}

	[Fact]
	public void RemovesAParticipantWithAllItsMessages()
	{
		string code =
			Header
			+ "participant A // first\nparticipant B\nparticipant C\n\n"
			+ "A -> B\nB -> C : keep\nC --> A\nB -> B";
		SequenceDiagram diagram = Parse(code);

		string removed = SequenceCodeWriter.RemoveParticipant(code, diagram.Participants[0]);

		Assert.Equal(Header + "participant B\nparticipant C\n\nB -> C : keep\nB -> B", removed);
	}

	[Fact]
	public void RemovingAParticipantThatIsNotThereChangesNothing()
	{
		string code = Header + "participant A";

		string removed = SequenceCodeWriter.RemoveParticipant(
			code,
			new SequenceParticipant("B", SequenceParticipantKind.Participant)
		);

		Assert.Equal(code, removed);
	}

	[Fact]
	public void FindsAParticipantByNameWhenItsSourceLineIsStale()
	{
		SequenceDiagram diagram = Parse(Header + "participant A\nparticipant B\nA -> B");
		string code = Header + "// moved down\nparticipant A\nparticipant B\nA -> B";

		string removed = SequenceCodeWriter.RemoveParticipant(code, diagram.Participants[1]);

		Assert.Equal(Header + "// moved down\nparticipant A", removed);
	}

	[Fact]
	public void MovingAParticipantLeftPutsItsDeclarationAboveTheOneItPasses()
	{
		string code = Header + "participant A\nparticipant B\nactor C // me\nA -> C";
		SequenceDiagram diagram = Parse(code);

		string moved = SequenceCodeWriter.MoveParticipant(
			code,
			diagram,
			Participant(diagram, "C"),
			0
		);

		Assert.Equal(Header + "actor C // me\nparticipant A\nparticipant B\nA -> C", moved);
	}

	[Fact]
	public void MovingAParticipantRightComesToMovingItsLineDown()
	{
		string code = Header + "participant A\nparticipant B\nparticipant C\nA -> C";
		SequenceDiagram diagram = Parse(code);

		string moved = SequenceCodeWriter.MoveParticipant(
			code,
			diagram,
			Participant(diagram, "A"),
			1
		);

		Assert.Equal(Header + "participant B\nparticipant A\nparticipant C\nA -> C", moved);
	}

	[Fact]
	public void MovingAParticipantRightNeverPutsItBelowItsMessages()
	{
		string code = Header + "participant A\nparticipant B\nA -> B\nparticipant C\nB -> C";
		SequenceDiagram diagram = Parse(code);

		string moved = SequenceCodeWriter.MoveParticipant(
			code,
			diagram,
			Participant(diagram, "A"),
			2
		);

		Assert.Equal(
			Header + "participant B\nparticipant C\nparticipant A\nA -> B\nB -> C",
			moved
		);
		Assert.Equal(["B", "C", "A"], ParticipantNames(moved));
	}

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData(1)]
	[InlineData(-1)]
	[InlineData(2)]
	public void MovingAParticipantToItsOwnOrAMissingIndexChangesNothing(int newIndex)
	{
		string code = Header + "participant A\nparticipant B";
		SequenceDiagram diagram = Parse(code);

		string moved = SequenceCodeWriter.MoveParticipant(
			code,
			diagram,
			diagram.Participants[1],
			newIndex
		);

		Assert.Equal(code, moved);
	}
}
