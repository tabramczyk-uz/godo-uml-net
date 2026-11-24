using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Applies edits made on the sequence canvas back to the source code, touching
/// as little of it as possible: each edit inserts, removes or moves whole
/// lines, or swaps a name inside them, and leaves comments, blank lines and
/// spacing alone. The code is the whole editor text, diagram header included.
/// Every method is a pure function of the code it is handed, and returns it
/// unchanged when the edit does not apply.
///
/// A participant must be declared above every message that uses it. Edits keep
/// that true by only ever moving a declaration up, never down past a message:
/// where a line would have to go below a declaration it needs, that
/// declaration is lifted above it instead.
/// </summary>
public static class SequenceCodeWriter
{
	/// <summary>
	/// Declares a new participant below the last declaration, so it becomes the
	/// rightmost one, or at the end of the code when there is none yet.
	/// </summary>
	public static string AddParticipant(string code, SequenceParticipantKind kind, string name)
	{
		string[] lines = code.Split('\n');
		string declaration = SequenceSyntax.FormatDeclaration(kind, name);

		int lastDeclaration = -1;
		for (int i = 0; i < lines.Length; i++)
		{
			if (TryMatchDeclaration(lines[i], out _))
			{
				lastDeclaration = i;
			}
		}

		int insertAt = lastDeclaration >= 0 ? lastDeclaration + 1 : GetEndOfCode(lines);
		return Rebuild(lines, [], insertAt, [declaration]);
	}

	/// <summary>
	/// Renames a participant in its declaration and in every message that uses
	/// it. Names that merely contain the old name are left alone.
	/// </summary>
	public static string RenameParticipant(
		string code,
		SequenceParticipant participant,
		string newName
	)
	{
		string[] lines = code.Split('\n');

		for (int i = 0; i < lines.Length; i++)
		{
			UMLSyntax.SplitComment(lines[i], out string codePart, out string comment);

			if (TryMatchDeclaration(lines[i], out Match declaration))
			{
				if (declaration.Groups[2].Value == participant.Name)
				{
					lines[i] = ReplaceGroup(codePart, declaration.Groups[2], newName) + comment;
				}

				continue;
			}

			if (!TryMatchMessage(lines[i], out Match message))
			{
				continue;
			}

			// The receiver is replaced first so the sender's index stays valid.
			string renamedLine = codePart;
			if (message.Groups[3].Value == participant.Name)
			{
				renamedLine = ReplaceGroup(renamedLine, message.Groups[3], newName);
			}

			if (message.Groups[1].Value == participant.Name)
			{
				renamedLine = ReplaceGroup(renamedLine, message.Groups[1], newName);
			}

			lines[i] = renamedLine + comment;
		}

		return string.Join("\n", lines);
	}

	/// <summary>
	/// Removes a participant's declaration and every message it sends or
	/// receives, in one edit, so it is a single undo step.
	/// </summary>
	public static string RemoveParticipant(string code, SequenceParticipant participant)
	{
		string[] lines = code.Split('\n');

		int declarationLine = FindDeclarationLine(lines, participant);
		if (declarationLine == -1)
		{
			return code;
		}

		var removed = new HashSet<int> { declarationLine };
		for (int i = 0; i < lines.Length; i++)
		{
			if (
				TryMatchMessage(lines[i], out Match message)
				&& (
					message.Groups[1].Value == participant.Name
					|| message.Groups[3].Value == participant.Name
				)
			)
			{
				removed.Add(i);
			}
		}

		return Rebuild(lines, removed, -1, []);
	}

	/// <summary>
	/// Reorders the declarations so <paramref name="participant"/> becomes the
	/// one at <paramref name="newIndex"/> (left to right, counting from 0).
	/// Moving left, its declaration goes above the one now at that index.
	/// Moving right, the declarations it passes are lifted above it instead,
	/// since its own could not go below a message that uses it. When the
	/// declarations stand together, both come to the same as moving one line.
	/// </summary>
	public static string MoveParticipant(
		string code,
		SequenceDiagram diagram,
		SequenceParticipant participant,
		int newIndex
	)
	{
		int oldIndex = diagram.Participants.IndexOf(participant);
		if (
			oldIndex == -1
			|| newIndex == oldIndex
			|| newIndex < 0
			|| newIndex >= diagram.Participants.Count
		)
		{
			return code;
		}

		string[] lines = code.Split('\n');
		int first = Math.Min(oldIndex, newIndex);
		int last = Math.Max(oldIndex, newIndex);

		// The participants that move up: the one dragged left, or the ones a
		// participant dragged right passes over.
		int liftFrom = newIndex < oldIndex ? oldIndex : first + 1;
		int liftTo = newIndex < oldIndex ? oldIndex : last;

		int insertAt = FindDeclarationLine(lines, diagram.Participants[first]);
		if (insertAt == -1)
		{
			return code;
		}

		var removed = new HashSet<int>();
		var lifted = new List<string>();
		for (int index = liftFrom; index <= liftTo; index++)
		{
			int line = FindDeclarationLine(lines, diagram.Participants[index]);
			if (line == -1)
			{
				return code;
			}

			removed.Add(line);
			lifted.Add(lines[line]);
		}

		return Rebuild(lines, removed, insertAt, lifted);
	}

	/// <summary>
	/// Writes a new message so it becomes the one at <paramref name="index"/>
	/// in the diagram's messages: above the message now at that index, or below
	/// the last one when <paramref name="index"/> is their count. When either
	/// participant is declared further down, the declarations in between are
	/// lifted above the new message, keeping their order.
	/// </summary>
	public static string InsertMessage(
		string code,
		SequenceDiagram diagram,
		int index,
		SequenceParticipant from,
		SequenceParticipant to,
		SequenceMessageKind kind,
		string label = ""
	)
	{
		if (index < 0 || index > diagram.Messages.Count)
		{
			return code;
		}

		string[] lines = code.Split('\n');
		string message = SequenceSyntax.FormatMessage(from.Name, to.Name, kind, label);
		return PlaceMessage(lines, diagram.Messages, index, from, to, message, -1) ?? code;
	}

	/// <summary>Removes the line the message was written on.</summary>
	public static string RemoveMessage(string code, SequenceMessage message)
	{
		string[] lines = code.Split('\n');
		int line = FindMessageLine(lines, message);
		return line == -1 ? code : Rebuild(lines, [line], -1, []);
	}

	/// <summary>
	/// Moves a message's line, comment and all, so it becomes the one at
	/// <paramref name="newIndex"/> in the diagram's messages. Declarations are
	/// lifted above it as in <see cref="InsertMessage"/>.
	/// </summary>
	public static string MoveMessage(
		string code,
		SequenceDiagram diagram,
		SequenceMessage message,
		int newIndex
	)
	{
		int oldIndex = diagram.Messages.IndexOf(message);
		if (
			oldIndex == -1
			|| newIndex == oldIndex
			|| newIndex < 0
			|| newIndex >= diagram.Messages.Count
		)
		{
			return code;
		}

		string[] lines = code.Split('\n');
		int line = FindMessageLine(lines, message);
		if (line == -1)
		{
			return code;
		}

		var others = new List<SequenceMessage>(diagram.Messages);
		others.RemoveAt(oldIndex);
		return PlaceMessage(lines, others, newIndex, message.From, message.To, lines[line], line)
			?? code;
	}

	/// <summary>
	/// Joins the lines back up without the <paramref name="removed"/> ones and
	/// with <paramref name="inserted"/> placed just before line
	/// <paramref name="insertAt"/>, all numbered as in <paramref name="lines"/>.
	/// An <paramref name="insertAt"/> of <c>lines.Length</c> appends; -1 inserts
	/// nothing.
	/// </summary>
	private static string Rebuild(
		string[] lines,
		HashSet<int> removed,
		int insertAt,
		List<string> inserted
	)
	{
		var result = new List<string>(lines.Length + inserted.Count);
		for (int i = 0; i <= lines.Length; i++)
		{
			if (i == insertAt)
			{
				result.AddRange(inserted);
			}

			if (i < lines.Length && !removed.Contains(i))
			{
				result.Add(lines[i]);
			}
		}

		return string.Join("\n", result);
	}

	/// <summary>
	/// Where a line goes to be the last one: before the empty string that
	/// splitting leaves after a final newline, so the newline stays last.
	/// </summary>
	private static int GetEndOfCode(string[] lines)
	{
		// `^n` counts n back from the end: `lines[^1]` is the last line.
		return lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
	}

	/// <summary>
	/// Puts <paramref name="messageLine"/> where it becomes the message at
	/// <paramref name="index"/> among <paramref name="messages"/>, removing
	/// <paramref name="oldLine"/> (-1 for none) in the same pass. Returns null
	/// when a line it needs cannot be found.
	/// </summary>
	private static string PlaceMessage(
		string[] lines,
		List<SequenceMessage> messages,
		int index,
		SequenceParticipant from,
		SequenceParticipant to,
		string messageLine,
		int oldLine
	)
	{
		int insertAt;
		if (index < messages.Count)
		{
			insertAt = FindMessageLine(lines, messages[index]);
		}
		else if (messages.Count > 0)
		{
			int lastLine = FindMessageLine(lines, messages[^1]);
			insertAt = lastLine == -1 ? -1 : lastLine + 1;
		}
		else
		{
			insertAt = GetEndOfCode(lines);
		}

		int fromLine = FindDeclarationLine(lines, from);
		int toLine = FindDeclarationLine(lines, to);
		if (insertAt == -1 || fromLine == -1 || toLine == -1)
		{
			return null;
		}

		var removed = new HashSet<int>();
		if (oldLine != -1)
		{
			removed.Add(oldLine);
		}

		var inserted = new List<string>();
		int liftTo = Math.Max(fromLine, toLine);
		for (int i = insertAt; i <= liftTo; i++)
		{
			if (TryMatchDeclaration(lines[i], out _))
			{
				removed.Add(i);
				inserted.Add(lines[i]);
			}
		}

		inserted.Add(messageLine);
		return Rebuild(lines, removed, insertAt, inserted);
	}

	private static int FindDeclarationLine(string[] lines, SequenceParticipant participant)
	{
		if (IsDeclarationOf(lines, participant.SourceLine, participant))
		{
			return participant.SourceLine;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			if (IsDeclarationOf(lines, i, participant))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsDeclarationOf(string[] lines, int line, SequenceParticipant participant)
	{
		return line >= 0
			&& line < lines.Length
			&& TryMatchDeclaration(lines[line], out Match match)
			&& match.Groups[2].Value == participant.Name;
	}

	/// <summary>
	/// The message's line: the one it was parsed from if that still holds it,
	/// otherwise the first line with the same sender, operator, receiver and label.
	/// </summary>
	private static int FindMessageLine(string[] lines, SequenceMessage message)
	{
		if (IsMessageLine(lines, message.SourceLine, message))
		{
			return message.SourceLine;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			if (IsMessageLine(lines, i, message))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool IsMessageLine(string[] lines, int line, SequenceMessage message)
	{
		return line >= 0
			&& line < lines.Length
			&& TryMatchMessage(lines[line], out Match match)
			&& match.Groups[1].Value == message.From.Name
			&& match.Groups[2].Value == SequenceSyntax.GetOperator(message.Kind)
			&& match.Groups[3].Value == message.To.Name
			&& match.Groups[4].Value == message.Label;
	}

	/// <summary>
	/// Matches a participant declaration. The diagram header has the same shape
	/// (two words), so the first word must be a participant keyword.
	/// </summary>
	private static bool TryMatchDeclaration(string line, out Match match)
	{
		match = SequenceSyntax.DeclarationRegex().Match(UMLSyntax.StripComment(line));
		return match.Success && SequenceSyntax.TryGetParticipantKind(match.Groups[1].Value, out _);
	}

	private static bool TryMatchMessage(string line, out Match match)
	{
		match = SequenceSyntax.MessageRegex().Match(UMLSyntax.StripComment(line));
		return match.Success;
	}

	private static string ReplaceGroup(string line, Group group, string replacement)
	{
		// `text[a..b]` is the substring from index a up to, not including, b; a
		// missing a or b means the start or the end.
		return line[..group.Index] + replacement + line[(group.Index + group.Length)..];
	}
}
