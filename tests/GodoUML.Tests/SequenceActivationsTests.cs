using System.Collections.Generic;
using Xunit;

namespace GodoUML.Tests;

public class SequenceActivationsTests
{
	/// <summary>
	/// Parses the code and spells each activation out as
	/// <c>Participant@depth start-end</c>, with message indices and <c>end</c>
	/// for one that runs to the end of the diagram.
	/// </summary>
	private static List<string> DescribeActivations(string code)
	{
		SequenceParseResult result = SequenceParser.Parse(code);
		Assert.True(
			result.IsSuccess,
			$"Expected a successful parse but got: {result.ErrorMessage} on line {result.ErrorLineNumber}"
		);

		return result.Diagram.Activations.ConvertAll(activation =>
			$"{activation.Participant.Name}@{activation.Depth} "
			+ $"{activation.StartMessageIndex}-{(activation.RunsToEnd ? "end" : activation.EndMessageIndex)}"
		);
	}

	private const string ThreeParticipants = "participant A\nparticipant B\nparticipant C\n";

	[Fact]
	public void ADiagramWithoutMessagesHasNoActivations()
	{
		Assert.Empty(DescribeActivations("participant A\nparticipant B"));
	}

	[Fact]
	public void ASynchronousMessageOpensAnActivationOnItsReceiver()
	{
		SequenceDiagram diagram = SequenceParser.Parse(ThreeParticipants + "A -> B\nB --> A").Diagram;

		SequenceActivation activation = Assert.Single(diagram.Activations);
		Assert.Same(diagram.Participants[1], activation.Participant);
		Assert.Same(diagram.Participants[0], activation.Caller);
	}

	[Fact]
	public void TheReplyFromTheReceiverToTheCallerClosesIt()
	{
		Assert.Equal(["B@0 0-2"], DescribeActivations(ThreeParticipants + "A -> B\nA ->> C\nB --> A"));
	}

	[Theory]
	[InlineData("A -> B\nB --> C")]
	[InlineData("A -> B\nC --> A")]
	[InlineData("A -> B\nA --> B")]
	public void RepliesFromOrToAnyoneElseLeaveItOpen(string messages)
	{
		Assert.Equal(["B@0 0-end"], DescribeActivations(ThreeParticipants + messages));
	}

	[Fact]
	public void OnlyTheNextMatchingReplyClosesIt()
	{
		Assert.Equal(
			["B@0 0-1", "B@0 2-end"],
			DescribeActivations(ThreeParticipants + "A -> B\nB --> A\nA -> B\nC --> A")
		);
	}

	[Fact]
	public void ACallWithNoReplyRunsToTheEndOfTheDiagram()
	{
		Assert.Equal(["B@0 0-end"], DescribeActivations(ThreeParticipants + "A -> B\nA ->> C"));
		Assert.Equal(
			["B@0 0-end", "C@0 1-2"],
			DescribeActivations(ThreeParticipants + "A -> B\nB -> C\nC --> B")
		);
	}

	[Fact]
	public void AsynchronousMessagesNeitherOpenNorClose()
	{
		Assert.Equal(
			["B@0 0-3"],
			DescribeActivations(ThreeParticipants + "A -> B\nA ->> B\nB ->> A\nB --> A")
		);
		Assert.Empty(DescribeActivations(ThreeParticipants + "A ->> B\nB ->> C"));
	}

	[Fact]
	public void AReplyWithNoOpenCallIsKeptButChangesNothing()
	{
		string code = ThreeParticipants + "B --> A\nA -> B\nC --> A";
		SequenceParseResult result = SequenceParser.Parse(code);

		Assert.True(result.IsSuccess, result.ErrorMessage);
		Assert.Equal(3, result.Diagram.Messages.Count);
		Assert.Equal(SequenceMessageKind.Reply, result.Diagram.Messages[0].Kind);
		Assert.Equal(["B@0 1-end"], DescribeActivations(code));
	}

	[Fact]
	public void CallsMadeDuringAnActivationNestInsideIt()
	{
		Assert.Equal(
			["B@0 0-3", "C@0 1-2"],
			DescribeActivations(ThreeParticipants + "A -> B\nB -> C\nC --> B\nB --> A")
		);
	}

	[Fact]
	public void ASynchronousSelfMessageNestsOnItsOwnLifeline()
	{
		Assert.Equal(
			["B@0 0-3", "B@1 1-2"],
			DescribeActivations(ThreeParticipants + "A -> B\nB -> B\nB --> B\nB --> A")
		);
	}

	[Fact]
	public void ACallbackNestsOnTheCallersLifeline()
	{
		Assert.Equal(
			["A@0 0-5", "B@0 1-4", "A@1 2-3"],
			DescribeActivations(
				"actor U\nparticipant A\nparticipant B\nU -> A\nA -> B\nB -> A\nA --> B\nB --> A\nA --> U"
			)
		);
	}

	[Fact]
	public void AReplyClosesTheLatestOpenCallFromTheSameCaller()
	{
		Assert.Equal(
			["B@0 0-end", "B@1 1-2"],
			DescribeActivations(ThreeParticipants + "A -> B\nA -> B\nB --> A")
		);
	}

	[Fact]
	public void ANewActivationNestsDeeperThanEveryOpenOneOnItsLifeline()
	{
		// A's call on B closes before C's, which is still open at depth 1; the next
		// call on B must not be drawn on top of it at depth 1 again.
		Assert.Equal(
			["B@0 0-2", "B@1 1-end", "B@2 3-end"],
			DescribeActivations(ThreeParticipants + "A -> B\nC -> B\nB --> A\nA -> B")
		);
	}

	[Fact]
	public void ActivationsAreListedInTheOrderTheirCallsWereSent()
	{
		Assert.Equal(
			["B@0 0-3", "C@0 1-2", "C@0 4-end"],
			DescribeActivations(ThreeParticipants + "A -> B\nB -> C\nC --> B\nB --> A\nA -> C")
		);
	}
}
