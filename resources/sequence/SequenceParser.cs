using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Turns sequence diagram source code into a <see cref="SequenceDiagram"/>. Every
/// line is a participant declaration or a message, and a participant must be
/// declared before any message uses it. Parsing stops at the first error.
/// The code must not carry a diagram header: remove it first with
/// <see cref="UMLDiagramHeader.RemoveFrom"/>, which keeps the line numbers. One
/// instance holds the state of one parse; use the static <see cref="Parse"/>
/// entry point.
/// </summary>
public sealed class SequenceParser
{
	private readonly SequenceDiagram diagram = new();
	private readonly Dictionary<string, SequenceParticipant> participantsByName = [];

	private int lineNumber;
	private string errorMessage;

	private SequenceParser() { }

	public static SequenceParseResult Parse(string code)
	{
		return new SequenceParser().Run(code);
	}

	private SequenceParseResult Run(string code)
	{
		string[] lines = code.Split('\n');
		for (lineNumber = 0; lineNumber < lines.Length; lineNumber++)
		{
			if (!ParseLine(UMLSyntax.StripComment(lines[lineNumber])))
			{
				return SequenceParseResult.Failure(errorMessage, lineNumber);
			}
		}

		diagram.Activations.AddRange(SequenceActivations.Derive(diagram.Messages));
		return SequenceParseResult.Success(diagram);
	}

	private bool ParseLine(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return true;
		}

		if (UMLSyntax.GetIndentation(line) > 0)
		{
			return Fail("Unexpected indentation");
		}

		Match declarationMatch = SequenceSyntax.DeclarationRegex().Match(line);
		if (declarationMatch.Success)
		{
			return AddParticipant(declarationMatch.Groups[1].Value, declarationMatch.Groups[2].Value);
		}

		Match messageMatch = SequenceSyntax.MessageRegex().Match(line);
		if (messageMatch.Success)
		{
			return AddMessage(messageMatch);
		}

		return Fail("Syntax error");
	}

	private bool AddParticipant(string keyword, string name)
	{
		if (!SequenceSyntax.TryGetParticipantKind(keyword, out SequenceParticipantKind kind))
		{
			return Fail($"Unknown declaration: {keyword}");
		}

		if (participantsByName.ContainsKey(name))
		{
			return Fail($"Duplicate participant: {name}");
		}

		SequenceParticipant participant = new(name, kind) { SourceLine = lineNumber };
		diagram.Participants.Add(participant);
		participantsByName.Add(name, participant);
		return true;
	}

	private bool AddMessage(Match match)
	{
		string fromName = match.Groups[1].Value;
		string toName = match.Groups[3].Value;

		if (!participantsByName.TryGetValue(fromName, out SequenceParticipant from))
		{
			return Fail($"Unknown participant: {fromName}");
		}

		if (!participantsByName.TryGetValue(toName, out SequenceParticipant to))
		{
			return Fail($"Unknown participant: {toName}");
		}

		if (!SequenceSyntax.TryGetMessageKind(match.Groups[2].Value, out SequenceMessageKind kind))
		{
			return Fail($"Unknown message operator: {match.Groups[2].Value}");
		}

		diagram.Messages.Add(
			new SequenceMessage(from, to, kind, match.Groups[4].Value) { SourceLine = lineNumber }
		);
		return true;
	}

	private bool Fail(string message)
	{
		errorMessage = message;
		return false;
	}
}
