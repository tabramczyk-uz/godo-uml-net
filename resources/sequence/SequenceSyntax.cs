using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Every pattern and keyword of the sequence diagram language. It shares the
/// lexical rules of the class diagram language (<c>//</c> comments, tab
/// indentation, identifiers) through <see cref="UMLSyntax"/>, but none of its
/// grammar.
/// </summary>
public static partial class SequenceSyntax
{
	private const string Identifier = "[a-zA-Z_][a-zA-Z0-9_]*";

	// The longer operators come first, so `->>` and `-->` are never read as `->`
	// followed by a stray character.
	private const string Operator = "->>|-->|->";

	private const string DeclarationPattern = "^(" + Identifier + ")[ \t]+(" + Identifier + ")$";
	private const string MessagePattern =
		"^("
		+ Identifier
		+ ")[ \t]*("
		+ Operator
		+ ")[ \t]*("
		+ Identifier
		+ ")(?:[ \t]*:[ \t]*(.*))?$";

	private static readonly Dictionary<SequenceParticipantKind, string> ParticipantKindKeywords =
		new()
		{
			{ SequenceParticipantKind.Participant, "participant" },
			{ SequenceParticipantKind.Actor, "actor" },
		};

	private static readonly Dictionary<SequenceMessageKind, string> MessageOperators = new()
	{
		{ SequenceMessageKind.Synchronous, "->" },
		{ SequenceMessageKind.Asynchronous, "->>" },
		{ SequenceMessageKind.Reply, "-->" },
	};

	private static readonly Dictionary<string, SequenceParticipantKind> ParticipantKindsByKeyword =
		Invert(ParticipantKindKeywords);

	private static readonly Dictionary<string, SequenceMessageKind> MessageKindsByOperator =
		Invert(MessageOperators);

	// [GeneratedRegex] has the compiler write the body of each of these partial
	// methods, building the regex at compile time instead of at run time.
	/// <summary>A participant declaration: the kind keyword, then the name.</summary>
	[GeneratedRegex(DeclarationPattern)]
	public static partial Regex DeclarationRegex();

	/// <summary>A message: sender, operator, receiver and an optional <c>: label</c>.</summary>
	[GeneratedRegex(MessagePattern)]
	public static partial Regex MessageRegex();

	public static string GetKeyword(SequenceParticipantKind kind)
	{
		return ParticipantKindKeywords[kind];
	}

	public static bool TryGetParticipantKind(string keyword, out SequenceParticipantKind kind)
	{
		return ParticipantKindsByKeyword.TryGetValue(keyword, out kind);
	}

	public static string GetOperator(SequenceMessageKind kind)
	{
		return MessageOperators[kind];
	}

	public static bool TryGetMessageKind(string messageOperator, out SequenceMessageKind kind)
	{
		return MessageKindsByOperator.TryGetValue(messageOperator, out kind);
	}

	private static Dictionary<TValue, TKey> Invert<TKey, TValue>(Dictionary<TKey, TValue> table)
	{
		Dictionary<TValue, TKey> inverted = [];
		foreach ((TKey key, TValue value) in table)
		{
			inverted.Add(value, key);
		}

		return inverted;
	}
}
