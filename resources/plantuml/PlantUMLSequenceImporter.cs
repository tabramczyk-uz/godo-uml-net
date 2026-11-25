using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Reads a PlantUML sequence diagram into a <see cref="SequenceDiagram"/>. It is
/// the sequence counterpart of <see cref="PlantUMLImporter"/> and, like it, an
/// independent front end that shares only the model with the GodoUML language.
///
/// The supported subset is participant declarations (every PlantUML shape
/// becomes a participant, except <c>actor</c>) and one-way messages. PlantUML
/// creates a participant on its first mention, so one that is only ever named
/// by a message is declared at that point, in the order PlantUML would place
/// it. Activation statements are skipped, because GodoUML derives activations
/// from the messages; fragments, creation and destruction, and anything else
/// it cannot read are reported as warnings.
///
/// The same pass decides whether the source is a sequence diagram at all
/// (<see cref="PlantUMLSequenceImportResult.IsSequenceDiagram"/>). PlantUML
/// itself reads a diagram as a sequence diagram whenever it can, so an arrow
/// such as <c>A -&gt; B</c> belongs to one unless something in the file only
/// a class or use case diagram has, such as <c>class A</c>,
/// <c>A &lt;|-- B</c> or a <c>{</c> block, says otherwise.
/// </summary>
public sealed class PlantUMLSequenceImporter
{
	private readonly List<string> names = [];
	private readonly List<SequenceParticipantKind> kinds = [];
	private readonly Dictionary<string, int> participantsByPlantName = new(StringComparer.Ordinal);
	private readonly HashSet<string> usedIdentifiers = new(StringComparer.Ordinal);
	private readonly List<(int From, int To, SequenceMessageKind Kind, string Label)> messages = [];
	private readonly List<string> warnings = [];

	private bool inBlockComment;
	private bool inNote;
	private bool hasSequenceStatement;
	private bool hasClassStatement;
	private int lineNumber;

	private PlantUMLSequenceImporter() { }

	public static PlantUMLSequenceImportResult Import(string source)
	{
		return new PlantUMLSequenceImporter().Run(source);
	}

	private PlantUMLSequenceImportResult Run(string source)
	{
		string[] lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		for (lineNumber = 0; lineNumber < lines.Length; lineNumber++)
		{
			ReadLine(lines[lineNumber].Trim());
		}

		return new PlantUMLSequenceImportResult(
			BuildDiagram(),
			warnings,
			hasSequenceStatement && !hasClassStatement
		);
	}

	/// <summary>
	/// Hands the line to the first handler that recognises it, in order of
	/// precedence, as <see cref="PlantUMLImporter"/> does.
	/// </summary>
	private void ReadLine(string line)
	{
		bool consumed =
			ContinueBlockComment(line)
			|| ContinueNote(line)
			|| StartBlockComment(line)
			|| line.StartsWith(PlantUMLSyntax.CommentPrefix, StringComparison.Ordinal)
			|| line.Length == 0
			// A declaration or a message is read before notes and directives
			// are skipped, so a participant called Note or Title is never
			// mistaken for one.
			|| ReadDeclaration(line)
			|| ReadMessage(line)
			|| StartNote(line)
			|| PlantUMLSyntax.IgnoredRegex().IsMatch(line)
			|| SkipSequenceStatement(line);

		if (consumed)
		{
			return;
		}

		if (PlantUMLSequenceSyntax.UnsupportedRegex().IsMatch(line))
		{
			hasSequenceStatement = true;
			Warn(line, "unsupported sequence diagram statement");
			return;
		}

		hasClassStatement |= IsClassStatement(line);
		Warn(line, "unsupported statement");
	}

	private bool ContinueBlockComment(string line)
	{
		if (!inBlockComment)
		{
			return false;
		}

		inBlockComment = !line.Contains(PlantUMLSyntax.BlockCommentEnd, StringComparison.Ordinal);
		return true;
	}

	private bool StartBlockComment(string line)
	{
		if (!line.StartsWith(PlantUMLSyntax.BlockCommentStart, StringComparison.Ordinal))
		{
			return false;
		}

		inBlockComment = !line.Contains(PlantUMLSyntax.BlockCommentEnd, StringComparison.Ordinal);
		return true;
	}

	private bool ContinueNote(string line)
	{
		if (!inNote)
		{
			return false;
		}

		inNote = !PlantUMLSyntax.NoteEndRegex().IsMatch(line);
		return true;
	}

	private bool StartNote(string line)
	{
		if (!PlantUMLSyntax.NoteRegex().IsMatch(line))
		{
			return false;
		}

		inNote = !line.Contains(':', StringComparison.Ordinal);
		return true;
	}

	private bool SkipSequenceStatement(string line)
	{
		if (!PlantUMLSequenceSyntax.SkippedRegex().IsMatch(line))
		{
			return false;
		}

		hasSequenceStatement = true;
		return true;
	}

	/// <summary>
	/// Whether the line is one only a class or use case diagram has: a
	/// declaration GodoUML's sequence diagrams have no keyword for, an arrow
	/// that is not a message, or a block in braces.
	/// </summary>
	private static bool IsClassStatement(string line)
	{
		return PlantUMLSyntax.DeclarationRegex().IsMatch(line)
			|| PlantUMLSyntax.RelationshipRegex().IsMatch(line)
			|| line.EndsWith('{');
	}

	private bool ReadDeclaration(string line)
	{
		Match match = PlantUMLSequenceSyntax.DeclarationRegex().Match(line);
		if (!match.Success)
		{
			return false;
		}

		string keyword = match.Groups["keyword"].Value;
		string name = match.Groups["name"].Value;
		string alias = match.Groups["alias"].Success ? match.Groups["alias"].Value : null;

		// In `X as Y` either side may be the label; the one in quotes is, and
		// the other is the name messages use.
		if (alias != null && alias.StartsWith('"') && !name.StartsWith('"'))
		{
			(name, alias) = (alias, name);
		}

		string displayName = PlantUMLSyntax.UnwrapName(name, out _);
		string plantName = alias == null ? displayName : PlantUMLSyntax.UnwrapName(alias, out _);

		int index = GetOrCreateParticipant(plantName);
		participantsByPlantName.TryAdd(displayName, index);
		kinds[index] = PlantUMLSequenceSyntax.GetParticipantKind(keyword);

		// `participant` is the one keyword only a sequence diagram has; the
		// others name shapes a use case or component diagram draws too.
		hasSequenceStatement |=
			keyword == PlantUMLSequenceSyntax.GetKeyword(SequenceParticipantKind.Participant);
		return true;
	}

	private bool ReadMessage(string line)
	{
		Match match = PlantUMLSequenceSyntax.MessageRegex().Match(line);
		if (
			!match.Success
			|| !PlantUMLSequenceSyntax.TryGetMessageKind(
				match.Groups["head"].Value,
				match.Groups["body"].Value,
				match.Groups["tail"].Value,
				out SequenceMessageKind kind,
				out bool isReversed
			)
		)
		{
			return false;
		}

		int left = GetOrCreateParticipant(PlantUMLSyntax.UnwrapName(match.Groups["from"].Value, out _));
		int right = GetOrCreateParticipant(PlantUMLSyntax.UnwrapName(match.Groups["to"].Value, out _));
		string label = PlantUMLSequenceSyntax.CleanLabel(match.Groups["label"].Value);

		messages.Add(isReversed ? (right, left, kind, label) : (left, right, kind, label));
		hasSequenceStatement = true;
		return true;
	}

	/// <summary>
	/// Looks up a participant by the name PlantUML knows it by, creating it as a
	/// plain participant on its first mention, as PlantUML does.
	/// </summary>
	private int GetOrCreateParticipant(string plantName)
	{
		if (participantsByPlantName.TryGetValue(plantName, out int existing))
		{
			return existing;
		}

		names.Add(MakeUniqueIdentifier(PlantUMLSyntax.ToIdentifier(plantName)));
		kinds.Add(SequenceParticipantKind.Participant);
		participantsByPlantName[plantName] = names.Count - 1;
		return names.Count - 1;
	}

	private string MakeUniqueIdentifier(string identifier)
	{
		string unique = identifier;
		for (int suffix = 2; !usedIdentifiers.Add(unique); suffix++)
		{
			unique = identifier + suffix;
		}

		return unique;
	}

	/// <summary>
	/// Builds the diagram once every line is read, since a declaration may
	/// change the kind of a participant a message has already created.
	/// </summary>
	private SequenceDiagram BuildDiagram()
	{
		var diagram = new SequenceDiagram();
		for (int i = 0; i < names.Count; i++)
		{
			diagram.Participants.Add(new SequenceParticipant(names[i], kinds[i]));
		}

		foreach ((int from, int to, SequenceMessageKind kind, string label) in messages)
		{
			diagram.Messages.Add(
				new SequenceMessage(diagram.Participants[from], diagram.Participants[to], kind, label)
			);
		}

		diagram.Activations.AddRange(SequenceActivations.Derive(diagram.Messages));
		return diagram;
	}

	private void Warn(string line, string reason)
	{
		warnings.Add($"Line {lineNumber + 1}: {reason} ({line})");
	}
}
