using Xunit;

namespace GodoUML.Tests;

public class SequenceCodeGeneratorTests
{
	private static SequenceDiagram ParseBody(string code)
	{
		UMLDiagramHeader header = UMLDiagramHeader.Read(code);
		Assert.True(header.IsSuccess, header.ErrorMessage);
		Assert.Equal(UMLDiagramKind.Sequence, header.Kind);
		SequenceParseResult result = SequenceParser.Parse(header.RemoveFrom(code));
		Assert.True(result.IsSuccess, result.ErrorMessage);
		return result.Diagram;
	}

	[Fact]
	public void AnEmptyDiagramIsJustTheHeader()
	{
		Assert.Equal("diagram sequence\n", SequenceCodeGenerator.Generate(new SequenceDiagram()));
	}

	[Fact]
	public void WritesTheHeaderTheParticipantsAndThenTheMessages()
	{
		var diagram = new SequenceDiagram();
		var customer = new SequenceParticipant("Customer", SequenceParticipantKind.Actor);
		var shop = new SequenceParticipant("Shop", SequenceParticipantKind.Participant);
		diagram.Participants.AddRange([customer, shop]);
		diagram.Messages.AddRange(
			[
				new SequenceMessage(customer, shop, SequenceMessageKind.Synchronous, "order"),
				new SequenceMessage(shop, shop, SequenceMessageKind.Asynchronous),
				new SequenceMessage(shop, customer, SequenceMessageKind.Reply, "receipt"),
			]
		);

		string code = SequenceCodeGenerator.Generate(diagram);

		Assert.Equal(
			"diagram sequence\n\nactor Customer\nparticipant Shop\n\n"
				+ "Customer -> Shop : order\nShop ->> Shop\nShop --> Customer : receipt\n",
			code
		);
	}

	[Fact]
	public void GeneratedCodeReadsBackAsTheSameDiagram()
	{
		string source =
			"diagram sequence\n// comments are not kept\nparticipant A\nA -> A\nactor B\n"
			+ "A ->> B : go\nB --> A : done";
		SequenceDiagram original = ParseBody(source);

		SequenceDiagram copy = ParseBody(SequenceCodeGenerator.Generate(original));

		Assert.Equal(
			original.Participants.ConvertAll(p => (p.Name, p.Kind)),
			copy.Participants.ConvertAll(p => (p.Name, p.Kind))
		);
		Assert.Equal(
			original.Messages.ConvertAll(m => (m.From.Name, m.To.Name, m.Kind, m.Label)),
			copy.Messages.ConvertAll(m => (m.From.Name, m.To.Name, m.Kind, m.Label))
		);
	}
}
