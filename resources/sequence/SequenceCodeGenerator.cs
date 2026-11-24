using System.Text;

/// <summary>
/// Writes a whole <see cref="SequenceDiagram"/> out as source code, starting
/// with its diagram header so it reads back as a sequence diagram.
///
/// This is the opposite of <see cref="SequenceCodeWriter"/>, which edits code
/// that already exists. Generating from scratch is only right when there is no
/// source text yet: a new file or an import.
/// </summary>
public static class SequenceCodeGenerator
{
	public static string Generate(SequenceDiagram diagram)
	{
		var code = new StringBuilder();
		code.Append(UMLDiagramHeader.Format(UMLDiagramKind.Sequence)).Append('\n');

		if (diagram.Participants.Count > 0)
		{
			code.Append('\n');
		}

		foreach (SequenceParticipant participant in diagram.Participants)
		{
			code.Append(SequenceSyntax.FormatDeclaration(participant.Kind, participant.Name))
				.Append('\n');
		}

		if (diagram.Messages.Count > 0)
		{
			code.Append('\n');
		}

		foreach (SequenceMessage message in diagram.Messages)
		{
			code.Append(
					SequenceSyntax.FormatMessage(
						message.From.Name,
						message.To.Name,
						message.Kind,
						message.Label
					)
				)
				.Append('\n');
		}

		return code.ToString();
	}
}
