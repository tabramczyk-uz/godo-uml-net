using System.Text;

/// <summary>
/// Walks a <see cref="SequenceDiagram"/> out to a PlantUML sequence diagram.
/// Every participant is declared, in order, before the first message, so
/// PlantUML places them left to right as GodoUML does. Activations are left
/// out: GodoUML derives them from the messages, and so does a re-import.
/// </summary>
public static class PlantUMLSequenceExporter
{
	public static string Export(SequenceDiagram diagram)
	{
		var output = new StringBuilder();
		output.Append(PlantUMLSyntax.Start).Append('\n').Append('\n');

		foreach (SequenceParticipant participant in diagram.Participants)
		{
			output
				.Append(PlantUMLSequenceSyntax.GetKeyword(participant.Kind))
				.Append(' ')
				.Append(participant.Name)
				.Append('\n');
		}

		if (diagram.Participants.Count > 0 && diagram.Messages.Count > 0)
		{
			output.Append('\n');
		}

		foreach (SequenceMessage message in diagram.Messages)
		{
			WriteMessage(output, message);
		}

		return output.Append('\n').Append(PlantUMLSyntax.End).Append('\n').ToString();
	}

	private static void WriteMessage(StringBuilder output, SequenceMessage message)
	{
		output
			.Append(message.From.Name)
			.Append(' ')
			.Append(PlantUMLSequenceSyntax.GetArrow(message.Kind))
			.Append(' ')
			.Append(message.To.Name);

		if (message.Label.Length > 0)
		{
			output.Append(" : ").Append(message.Label);
		}

		output.Append('\n');
	}
}
