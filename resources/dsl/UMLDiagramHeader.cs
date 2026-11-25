using System;
using System.Collections.Generic;

/// <summary>
/// The optional first line of a diagram's source that names its diagram kind,
/// such as <c>diagram sequence</c>. <see cref="Read"/> runs before any kind's
/// own parser, to decide which one to run. A diagram without a header is a
/// class diagram, so files written before headers existed read as before.
/// </summary>
public sealed class UMLDiagramHeader
{
	public const string Keyword = "diagram";

	private static readonly Dictionary<UMLDiagramKind, string> KindKeywords = new()
	{
		{ UMLDiagramKind.Class, "class" },
		{ UMLDiagramKind.Sequence, "sequence" },
	};

	private static readonly Dictionary<string, UMLDiagramKind> KindsByKeyword = Invert(KindKeywords);

	private static readonly char[] Whitespace = [' ', '\t'];

	private UMLDiagramHeader(
		UMLDiagramKind kind,
		int lineNumber,
		string errorMessage,
		int errorLineNumber
	)
	{
		Kind = kind;
		LineNumber = lineNumber;
		ErrorMessage = errorMessage;
		ErrorLineNumber = errorLineNumber;
	}

	/// <summary>The kind the header names, or <see cref="UMLDiagramKind.Class"/> without one.</summary>
	public UMLDiagramKind Kind { get; }

	/// <summary>The line the header is on, or -1 when the diagram has none.</summary>
	public int LineNumber { get; }

	public string ErrorMessage { get; }
	public int ErrorLineNumber { get; }

	public bool IsSuccess => ErrorMessage == null;
	public bool IsPresent => LineNumber >= 0;

	public static string GetKeyword(UMLDiagramKind kind)
	{
		return KindKeywords[kind];
	}

	/// <summary>Spells out the header line that names <paramref name="kind"/>.</summary>
	public static string Format(UMLDiagramKind kind)
	{
		return $"{Keyword} {GetKeyword(kind)}";
	}

	/// <summary>
	/// The code a new, empty diagram of <paramref name="kind"/> starts with:
	/// nothing for a class diagram, which needs no header, and otherwise just
	/// the header line.
	/// </summary>
	public static string NewDiagramCode(UMLDiagramKind kind)
	{
		return kind == UMLDiagramKind.Class ? string.Empty : Format(kind);
	}

	/// <summary>
	/// Reads the header from the first line that is not blank or only a
	/// comment. That line is a header when its first word is
	/// <see cref="Keyword"/> at the start of the line; otherwise the diagram has
	/// no header. A header must name exactly one known kind, or the read fails
	/// on that line.
	/// </summary>
	public static UMLDiagramHeader Read(string code)
	{
		string[] lines = code.Split('\n');
		for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
		{
			string line = UMLSyntax.StripComment(lines[lineNumber]);
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			return ReadLine(line, lineNumber);
		}

		return None();
	}

	/// <summary>
	/// Blanks the header line out of <paramref name="code"/>, leaving every other
	/// line, and so every line number, as it was. A kind's own parser can then
	/// run on the result without knowing about headers.
	/// </summary>
	public string RemoveFrom(string code)
	{
		if (!IsPresent)
		{
			return code;
		}

		string[] lines = code.Split('\n');
		lines[LineNumber] = string.Empty;
		return string.Join('\n', lines);
	}

	private static UMLDiagramHeader ReadLine(string line, int lineNumber)
	{
		string[] words = line.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
		if (UMLSyntax.GetIndentation(line) > 0 || words[0] != Keyword)
		{
			return None();
		}

		if (words.Length == 1)
		{
			return Failure("Missing diagram kind", lineNumber);
		}

		if (words.Length > 2)
		{
			return Failure("A diagram header names exactly one kind", lineNumber);
		}

		if (!KindsByKeyword.TryGetValue(words[1], out UMLDiagramKind kind))
		{
			return Failure($"Unknown diagram kind: {words[1]}", lineNumber);
		}

		return new UMLDiagramHeader(kind, lineNumber, null, -1);
	}

	private static UMLDiagramHeader None()
	{
		return new UMLDiagramHeader(UMLDiagramKind.Class, -1, null, -1);
	}

	private static UMLDiagramHeader Failure(string errorMessage, int errorLineNumber)
	{
		return new UMLDiagramHeader(UMLDiagramKind.Class, -1, errorMessage, errorLineNumber);
	}

	private static Dictionary<string, UMLDiagramKind> Invert(
		Dictionary<UMLDiagramKind, string> keywords
	)
	{
		Dictionary<string, UMLDiagramKind> inverted = [];
		foreach ((UMLDiagramKind kind, string keyword) in keywords)
		{
			inverted.Add(keyword, kind);
		}

		return inverted;
	}
}
