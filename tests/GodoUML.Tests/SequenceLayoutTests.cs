using System.Collections.Generic;
using Godot;
using Xunit;

namespace GodoUML.Tests;

/// <summary>
/// Where <see cref="SequenceLayout"/> puts participants, messages and
/// activations, and what a point picks. Drawing needs Godot and is checked by
/// hand.
/// </summary>
public class SequenceLayoutTests
{
	/// <summary>Every character 10 wide and every line 16 high.</summary>
	private static Vector2 Measure(string text)
	{
		return new Vector2(text.Length * 10.0f, 16.0f);
	}

	private static SequenceDiagram Parse(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	private static SequenceLayout Lay(string code)
	{
		return new SequenceLayout(Parse(code), Measure);
	}

	[Fact]
	public void PutsParticipantsLeftToRightInDeclarationOrder()
	{
		SequenceLayout layout = Lay("participant B\nactor A\nparticipant C\n");

		Assert.True(layout.GetLifelineX(0) < layout.GetLifelineX(1));
		Assert.True(layout.GetLifelineX(1) < layout.GetLifelineX(2));
		Assert.Equal(0.0f, layout.GetHeadRect(0).Position.X);
		Assert.Equal(layout.GetHeadRect(1).GetCenter().X, layout.GetLifelineX(1));
	}

	[Fact]
	public void StandsEveryHeadOnTheLineWhereTheLifelinesStart()
	{
		SequenceLayout layout = Lay("participant A\nactor B\n");

		Assert.Equal(SequenceLayout.HeadHeight, layout.GetHeadRect(0).End.Y);
		Assert.Equal(SequenceLayout.HeadHeight, layout.GetHeadRect(1).End.Y);
		Assert.Equal(SequenceLayout.BoxHeight, layout.GetHeadRect(0).Size.Y);
		Assert.Equal(SequenceLayout.HeadHeight, layout.GetHeadRect(1).Size.Y);
	}

	[Fact]
	public void KeepsLongNamesFromOverlapping()
	{
		SequenceLayout layout = Lay("participant AVeryLongParticipantName\nparticipant AnotherVeryLongName\n");

		Assert.False(layout.GetHeadRect(0).Intersects(layout.GetHeadRect(1)));
	}

	[Fact]
	public void WidensTheGapForALongLabel()
	{
		string label = new('x', 40);
		SequenceLayout plain = Lay("participant A\nparticipant B\nA -> B\n");
		SequenceLayout labelled = Lay($"participant A\nparticipant B\nA -> B : {label}\n");

		Assert.True(labelled.GetLifelineX(1) > plain.GetLifelineX(1));
		Assert.True(labelled.GetLifelineX(1) - labelled.GetLifelineX(0) >= Measure(label).X);
	}

	[Fact]
	public void LeavesRoomForASelfMessageLabelBeforeTheNextLifeline()
	{
		string label = new('x', 30);
		SequenceLayout layout = Lay($"participant A\nparticipant B\nA -> A : {label}\n");

		Assert.True(layout.GetLabelRect(0).End.X < layout.GetLifelineX(1));
	}

	[Fact]
	public void PutsMessagesTopToBottomInSourceOrder()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nB -> A\nA -> B\nA ->> B\n");

		Assert.True(layout.GetMessageY(0) > SequenceLayout.HeadHeight);
		Assert.True(layout.GetMessageY(0) < layout.GetMessageY(1));
		Assert.True(layout.GetMessageY(1) < layout.GetMessageY(2));
		Assert.True(layout.LifelineBottom > layout.GetMessageY(2));
	}

	[Fact]
	public void GivesASelfMessageATallerRow()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA ->> B\nA ->> A\nA ->> B\nA ->> B\n");

		float plainRow = layout.GetMessageY(3) - layout.GetMessageY(2);
		float selfRow = layout.GetMessageY(2) - layout.GetMessageY(1);
		Assert.Equal(plainRow + SequenceLayout.SelfMessageDrop, selfRow);
	}

	[Fact]
	public void RunsAMessageFromItsSenderToItsReceiver()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nB ->> A\n");

		IReadOnlyList<Vector2> path = layout.GetMessagePath(0);
		Assert.Equal(2, path.Count);
		Assert.Equal(layout.GetLifelineX(1), path[0].X);
		Assert.Equal(layout.GetLifelineX(0), path[1].X);
		Assert.Equal(layout.GetMessageY(0), path[0].Y);
		Assert.Equal(layout.GetMessageY(0), path[1].Y);
	}

	[Fact]
	public void LoopsASelfMessageBackToItsLifelineFurtherDown()
	{
		SequenceLayout layout = Lay("participant A\nA ->> A\n");

		IReadOnlyList<Vector2> path = layout.GetMessagePath(0);
		Assert.Equal(4, path.Count);
		Assert.Equal(layout.GetLifelineX(0), path[0].X);
		Assert.Equal(layout.GetLifelineX(0), path[3].X);
		Assert.True(path[1].X > path[0].X);
		Assert.Equal(path[0].Y + SequenceLayout.SelfMessageDrop, path[3].Y);
	}

	[Fact]
	public void RunsAnActivationFromItsCallToItsReply()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\nB --> A\n");

		Rect2 bar = layout.GetActivationRect(0);
		Assert.Equal(layout.GetMessageY(0), bar.Position.Y);
		Assert.Equal(layout.GetMessageY(1), bar.End.Y);
		Assert.Equal(layout.GetLifelineX(1), bar.GetCenter().X);
		Assert.Equal(SequenceLayout.ActivationWidth, bar.Size.X);
	}

	[Fact]
	public void RunsAnUnansweredActivationToTheEndOfTheLifeline()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\n");

		Assert.Equal(layout.LifelineBottom, layout.GetActivationRect(0).End.Y);
	}

	[Fact]
	public void ShiftsANestedActivationRight()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\nB -> B\n");

		Rect2 outer = layout.GetActivationRect(0);
		Rect2 inner = layout.GetActivationRect(1);
		Assert.Equal(outer.Position.X + SequenceLayout.ActivationWidth / 2.0f, inner.Position.X);
		Assert.Equal(layout.GetMessageY(1) + SequenceLayout.SelfMessageDrop, inner.Position.Y);
	}

	[Fact]
	public void EndsArrowsAtTheActivationBars()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\nB --> A\n");

		Rect2 bar = layout.GetActivationRect(0);
		Assert.Equal(bar.Position.X, layout.GetMessagePath(0)[1].X);
		Assert.Equal(bar.Position.X, layout.GetMessagePath(1)[0].X);
		Assert.Equal(layout.GetLifelineX(0), layout.GetMessagePath(1)[1].X);
	}

	[Fact]
	public void CentresALabelAboveItsArrow()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B : order\n");

		Rect2 label = layout.GetLabelRect(0);
		IReadOnlyList<Vector2> path = layout.GetMessagePath(0);
		Assert.Equal((path[0].X + path[1].X) / 2.0f, label.GetCenter().X, 3);
		Assert.True(label.End.Y < path[0].Y);
		Assert.Equal(Measure("order"), label.Size);
	}

	[Fact]
	public void BoundsCoverTheHeadsAndLifelines()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\n");

		Rect2 bounds = layout.Bounds.Value;
		Assert.True(bounds.Encloses(layout.GetHeadRect(0)));
		Assert.True(bounds.Encloses(layout.GetHeadRect(1)));
		Assert.Equal(layout.LifelineBottom, bounds.End.Y);
	}

	[Fact]
	public void AnEmptyDiagramHasNoBounds()
	{
		Assert.Null(Lay("").Bounds);
	}

	[Fact]
	public void PicksAParticipantByItsHead()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\n");

		Assert.Equal(1, layout.GetParticipantAt(layout.GetHeadRect(1).GetCenter()));
		Assert.Equal(-1, layout.GetParticipantAt(new Vector2(layout.GetLifelineX(1), layout.LifelineBottom - 1.0f)));
	}

	[Fact]
	public void PicksALifelineNearTheClick()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\n");
		float y = layout.LifelineBottom - 1.0f;

		Assert.Equal(1, layout.GetLifelineAt(new Vector2(layout.GetLifelineX(1) + 5.0f, y), 6.0f));
		Assert.Equal(-1, layout.GetLifelineAt(new Vector2(layout.GetLifelineX(1) + 7.0f, y), 6.0f));
		Assert.Equal(-1, layout.GetLifelineAt(new Vector2(layout.GetLifelineX(1), layout.LifelineBottom + 1.0f), 6.0f));
		Assert.Equal(0, layout.GetLifelineAt(layout.GetHeadRect(0).GetCenter(), 6.0f));
	}

	[Fact]
	public void PicksAMessageByItsArrowOrItsLabel()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B : first\nB --> A\n");
		float middleX = (layout.GetLifelineX(0) + layout.GetLifelineX(1)) / 2.0f;

		Assert.Equal(1, layout.GetMessageAt(new Vector2(middleX, layout.GetMessageY(1) + 3.0f), 6.0f));
		Assert.Equal(0, layout.GetMessageAt(layout.GetLabelRect(0).GetCenter(), 6.0f));
		Assert.Equal(-1, layout.GetMessageAt(new Vector2(middleX, layout.LifelineBottom), 6.0f));
	}

	[Fact]
	public void PicksASelfMessageByItsLoop()
	{
		SequenceLayout layout = Lay("participant A\nA ->> A\n");
		IReadOnlyList<Vector2> path = layout.GetMessagePath(0);

		Assert.Equal(0, layout.GetMessageAt((path[1] + path[2]) / 2.0f, 6.0f));
	}

	// [Theory] runs the test once per [InlineData] row, passing the row's values
	// as the arguments.
	[Theory]
	[InlineData(0.0f, 0)]
	[InlineData(1000.0f, 2)]
	public void InsertsANewMessageByHeight(float y, int expected)
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\nB --> A\n");

		Assert.Equal(expected, layout.GetInsertIndex(y));
	}

	[Fact]
	public void InsertsANewMessageBetweenTheMessagesAroundIt()
	{
		SequenceLayout layout = Lay("participant A\nparticipant B\nA -> B\nB --> A\n");
		float between = (layout.GetMessageY(0) + layout.GetMessageY(1)) / 2.0f;

		Assert.Equal(1, layout.GetInsertIndex(between));
	}
}
