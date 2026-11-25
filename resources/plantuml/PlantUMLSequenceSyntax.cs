using System.Collections.Generic;

/// <summary>
/// The slice of PlantUML's sequence diagram grammar the import and export
/// modules understand. It sits apart from <see cref="PlantUMLSyntax"/>, which
/// covers class diagrams, just as <see cref="SequenceSyntax"/> sits apart from
/// <see cref="UMLSyntax"/>; the lexical rules (comments, names, the start and
/// end markers) are shared through <see cref="PlantUMLSyntax"/>.
/// </summary>
public static class PlantUMLSequenceSyntax
{
	private static readonly Dictionary<SequenceParticipantKind, string> Keywords = new()
	{
		{ SequenceParticipantKind.Participant, "participant" },
		{ SequenceParticipantKind.Actor, "actor" },
	};

	// PlantUML draws `->` with a filled head, `->>` with an open one, and a
	// dashed `-->` as a return, which are exactly GodoUML's three kinds.
	private static readonly Dictionary<SequenceMessageKind, string> Arrows = new()
	{
		{ SequenceMessageKind.Synchronous, "->" },
		{ SequenceMessageKind.Asynchronous, "->>" },
		{ SequenceMessageKind.Reply, "-->" },
	};

	public static string GetKeyword(SequenceParticipantKind kind)
	{
		return Keywords[kind];
	}

	public static string GetArrow(SequenceMessageKind kind)
	{
		return Arrows[kind];
	}
}
