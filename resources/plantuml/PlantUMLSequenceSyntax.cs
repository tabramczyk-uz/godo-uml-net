using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// The slice of PlantUML's sequence diagram grammar the import and export
/// modules understand. It sits apart from <see cref="PlantUMLSyntax"/>, which
/// covers class diagrams, just as <see cref="SequenceSyntax"/> sits apart from
/// <see cref="UMLSyntax"/>; the lexical rules (comments, names, the start and
/// end markers) are shared through <see cref="PlantUMLSyntax"/>.
/// </summary>
public static partial class PlantUMLSequenceSyntax
{
	private const string Name = "(?:\"[^\"]*\"|[A-Za-z_][A-Za-z0-9_.]*)";

	private const string DeclarationPattern =
		"^(?<keyword>participant|actor|boundary|control|entity|database|collections|queue)"
		+ "[ \\t]+(?<name>"
		+ Name
		+ ")"
		+ "(?:[ \\t]+as[ \\t]+(?<alias>"
		+ Name
		+ "))?"
		+ "(?:[ \\t]*<<[^>]*>>)?"
		+ "(?:[ \\t]+order[ \\t]+-?[0-9]+)?"
		+ "(?:[ \\t]*#[A-Za-z0-9_]+)?[ \\t]*$";

	// A message arrow points one way: a head on the left (`<-`, `<<-`) or a tail
	// on the right (`->`, `->>`). One dash is a solid line, two a dashed one, and
	// a colour may sit between them (`-[#red]>`).
	private const string MessagePattern =
		"^(?<from>"
		+ Name
		+ ")[ \\t]*(?<head><<?)?(?<body>-(?:\\[[^\\]]*\\])?-?)(?<tail>>>?)?[ \\t]*(?<to>"
		+ Name
		+ ")[ \\t]*(?::[ \\t]*(?<label>.*?))?[ \\t]*$";

	// Sequence diagram statements GodoUML has nothing to show for, but whose
	// absence changes no message: activations are derived from the messages,
	// and boxes only frame participants.
	private const string SkippedPattern = "^(?:activate|deactivate|autoactivate|box)\\b";

	// Statements only a sequence diagram has, which GodoUML cannot represent:
	// fragments, references, creation and destruction, returns, dividers,
	// delays and spacing.
	private const string UnsupportedPattern =
		"^(?:(?:alt|else|opt|loop|par|break|critical|group|end|ref|create|destroy|return|newpage)\\b"
		+ "|\\.\\.\\.|\\|\\|\\||==)";

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

	// [GeneratedRegex] has the compiler write the body of each of these partial
	// methods, building the regex at compile time instead of at run time.
	/// <summary>A participant declaration: keyword, name, and an optional alias.</summary>
	[GeneratedRegex(DeclarationPattern)]
	public static partial Regex DeclarationRegex();

	/// <summary>A message: sender, arrow, receiver and an optional <c>: label</c>.</summary>
	[GeneratedRegex(MessagePattern)]
	public static partial Regex MessageRegex();

	[GeneratedRegex(SkippedPattern, RegexOptions.IgnoreCase)]
	public static partial Regex SkippedRegex();

	[GeneratedRegex(UnsupportedPattern, RegexOptions.IgnoreCase)]
	public static partial Regex UnsupportedRegex();

	public static string GetKeyword(SequenceParticipantKind kind)
	{
		return Keywords[kind];
	}

	/// <summary>
	/// Which participant kind a declaration keyword becomes. PlantUML's other
	/// shapes (boundary, database, ...) are all drawn as plain participants.
	/// </summary>
	public static SequenceParticipantKind GetParticipantKind(string keyword)
	{
		return keyword == Keywords[SequenceParticipantKind.Actor]
			? SequenceParticipantKind.Actor
			: SequenceParticipantKind.Participant;
	}

	public static string GetArrow(SequenceMessageKind kind)
	{
		return Arrows[kind];
	}

	/// <summary>
	/// Reads a message arrow back as the kind of message it draws, and whether
	/// it points from right to left. An arrow with both a head and a tail, or
	/// neither, is not a message GodoUML can show.
	/// </summary>
	public static bool TryGetMessageKind(
		string head,
		string body,
		string tail,
		out SequenceMessageKind kind,
		out bool isReversed
	)
	{
		kind = SequenceMessageKind.Synchronous;
		isReversed = head.Length > 0;
		if (head.Length > 0 == tail.Length > 0)
		{
			return false;
		}

		string decoration = isReversed ? head : tail;
		if (body.Length > 1 && body[^1] == '-')
		{
			kind = SequenceMessageKind.Reply;
		}
		else if (decoration.Length == 2)
		{
			kind = SequenceMessageKind.Asynchronous;
		}

		return true;
	}

	/// <summary>
	/// Makes a PlantUML label safe to write as a GodoUML message label, where
	/// <c>//</c> would start a comment and cut the label short.
	/// </summary>
	public static string CleanLabel(string label)
	{
		string cleaned = label.Trim();
		while (cleaned.Contains("//", StringComparison.Ordinal))
		{
			cleaned = cleaned.Replace("//", "/", StringComparison.Ordinal);
		}

		return cleaned;
	}
}
