using Xunit;

namespace GodoUML.Tests;

public class SequenceSelectionTests
{
	private static SequenceDiagram Parse(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	[Fact]
	public void SelectsOneThingAtATime()
	{
		SequenceDiagram diagram = Parse("participant A\nparticipant B\nA -> B\n");
		SequenceSelection selection = new();

		selection.SelectParticipant(diagram.Participants[0]);
		selection.SelectMessage(diagram, 0);

		Assert.Null(selection.GetParticipant(diagram));
		Assert.Equal(0, selection.GetMessageIndex(diagram));
	}

	[Fact]
	public void KeepsAParticipantAcrossAReparse()
	{
		SequenceSelection selection = new();
		selection.SelectParticipant(Parse("participant A\nparticipant B\n").Participants[1]);

		SequenceDiagram reparsed = Parse("participant B\nparticipant C\n");
		selection.Update(reparsed);

		Assert.Same(reparsed.Participants[0], selection.GetParticipant(reparsed));
	}

	[Fact]
	public void DropsAParticipantThatIsGone()
	{
		SequenceSelection selection = new();
		selection.SelectParticipant(Parse("participant A\nparticipant B\n").Participants[1]);

		SequenceDiagram reparsed = Parse("participant A\n");
		selection.Update(reparsed);

		Assert.True(selection.IsEmpty);
	}

	[Fact]
	public void FollowsAMessageWhenAnotherIsInsertedAboveIt()
	{
		SequenceSelection selection = new();
		selection.SelectMessage(Parse("participant A\nparticipant B\nA -> B : x\nB --> A\n"), 1);

		SequenceDiagram reparsed = Parse("participant A\nparticipant B\nA ->> B\nA -> B : x\nB --> A\n");
		selection.Update(reparsed);

		Assert.Equal(2, selection.GetMessageIndex(reparsed));
	}

	[Fact]
	public void TellsMessagesWrittenAlikeApartByTheirOrder()
	{
		SequenceDiagram diagram = Parse("participant A\nparticipant B\nA ->> B\nB ->> A\nA ->> B\n");
		SequenceSelection selection = new();

		selection.SelectMessage(diagram, 2);

		Assert.Equal(2, selection.GetMessageIndex(diagram));
	}

	[Fact]
	public void SelectsAMessageByItsIndexAfterTheEdit()
	{
		SequenceSelection selection = new();
		selection.SelectMessageAfterEdit(0);
		Assert.False(selection.IsEmpty);

		SequenceDiagram reparsed = Parse("participant A\nparticipant B\nB --> A\nA -> B\n");
		selection.Update(reparsed);

		Assert.Equal(0, selection.GetMessageIndex(reparsed));
		reparsed = Parse("participant A\nparticipant B\nA ->> A\nB --> A\nA -> B\n");
		selection.Update(reparsed);
		Assert.Equal(1, selection.GetMessageIndex(reparsed));
	}

	[Fact]
	public void DropsAMessageThatIsGone()
	{
		SequenceSelection selection = new();
		selection.SelectMessage(Parse("participant A\nparticipant B\nA -> B\n"), 0);

		SequenceDiagram reparsed = Parse("participant A\nparticipant B\nA ->> B\n");
		selection.Update(reparsed);

		Assert.True(selection.IsEmpty);
	}

	[Fact]
	public void ClearingSaysWhetherAnythingWasSelected()
	{
		SequenceSelection selection = new();
		Assert.False(selection.Clear());

		selection.SelectParticipant(Parse("participant A\n").Participants[0]);
		Assert.True(selection.Clear());
		Assert.True(selection.IsEmpty);
	}
}
