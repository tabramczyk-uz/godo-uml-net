using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Properties a node can carry on an indented line below its declaration.
/// </summary>
public enum UMLNodeProperty
{
	Position,
}

/// <summary>
/// Every pattern and keyword of the GodoUML language, in one place. This
/// describes the in-app language only — PlantUML has its own grammar over in
/// <see cref="PlantUMLSyntax"/>.
/// </summary>
public static partial class UMLSyntax
{
	public const string CommentPrefix = "//";
	public const string Indent = "\t";

	private const string CoordinateFormat = "0.###";

	private const string Identifier = "[a-zA-Z_][a-zA-Z0-9_]*";

	/// <summary>A type name, optionally generic and optionally an array.</summary>
	private const string TypeName = "[a-zA-Z_][a-zA-Z0-9_.]*(?:<[^<>]*>)?(?:\\[\\])*";

	private const string Visibility = "[+\\-#~]";

	// A relationship operator reads as an optional head decoration, an optional
	// line body and an optional tail decoration - with at least a body, or at
	// least one decoration, actually present.
	private const string OperatorHead = "(?:<\\||<<?|[oO]{1,2}|\\*)";
	private const string OperatorBody = "(?:-{1,2}|\\.{1,2})";
	private const string OperatorTail = "(?:\\|>|>>?|[oO]{1,2}|\\*)";
	private const string Operator =
		"(?:"
		+ OperatorHead
		+ "?"
		+ OperatorBody
		+ OperatorTail
		+ "?|"
		+ OperatorHead
		+ OperatorTail
		+ "?|"
		+ OperatorTail
		+ ")";

	private const string IdentifierPattern = "^" + Identifier + "$";
	private const string TypeNamePattern = "^" + TypeName + "$";
	private const string NodePattern = "^(" + Identifier + ")[ \\t]+(" + Identifier + ")$";
	private const string PropertyPattern = "^(" + Identifier + "):[ \\t]*(.+)$";
	private const string AttributePattern =
		"^(?:("
		+ Visibility
		+ ")[ \\t]*)?("
		+ Identifier
		+ ")(?:[ \\t]*:[ \\t]*("
		+ TypeName
		+ "))?$";
	private const string MethodPattern =
		"^(?:("
		+ Visibility
		+ ")[ \\t]*)?("
		+ Identifier
		+ ")[ \\t]*\\((.*)\\)(?:[ \\t]*:[ \\t]*("
		+ TypeName
		+ "))?$";
	private const string ArgumentPattern =
		"^(" + Identifier + ")(?:[ \\t]*:[ \\t]*(" + TypeName + "))?$";
	private const string RelationshipPattern =
		"^("
		+ Identifier
		+ ")[ \\t]*(?:\"([^\"]*)\"[ \\t]*)?("
		+ Operator
		+ ")[ \\t]*(?:\"([^\"]*)\"[ \\t]*)?("
		+ Identifier
		+ ")[ \\t]*(?::[ \\t]*(.*?))?[ \\t]*$";
	private const string OperatorPattern =
		"^(" + OperatorHead + ")?(" + OperatorBody + ")?(" + OperatorTail + ")?$";
	private const string PositionPattern =
		"^\\[\\s*([\\-+]?\\d*\\.?\\d+)\\s*,\\s*([\\-+]?\\d*\\.?\\d+)\\s*\\]$";

	private static readonly Dictionary<UMLNodeType, string> NodeTypeKeywords = new()
	{
		{ UMLNodeType.Node, "node" },
		{ UMLNodeType.Class, "class" },
		{ UMLNodeType.Interface, "interface" },
		{ UMLNodeType.AbstractClass, "abstract" },
		{ UMLNodeType.Enum, "enum" },
		{ UMLNodeType.UseCase, "usecase" },
		{ UMLNodeType.Actor, "actor" },
	};

	private static readonly Dictionary<UMLNodeProperty, string> NodePropertyKeywords = new()
	{
		{ UMLNodeProperty.Position, "position" },
	};

	private static readonly Dictionary<string, UMLNodeType> NodeTypesByKeyword = Invert(
		NodeTypeKeywords
	);
	private static readonly Dictionary<string, UMLNodeProperty> NodePropertiesByKeyword = Invert(
		NodePropertyKeywords
	);

	[GeneratedRegex(IdentifierPattern)]
	public static partial Regex IdentifierRegex();

	[GeneratedRegex(TypeNamePattern)]
	public static partial Regex TypeNameRegex();

	[GeneratedRegex(NodePattern)]
	public static partial Regex NodeRegex();

	[GeneratedRegex(PropertyPattern)]
	public static partial Regex PropertyRegex();

	[GeneratedRegex(AttributePattern)]
	public static partial Regex AttributeRegex();

	[GeneratedRegex(MethodPattern)]
	public static partial Regex MethodRegex();

	[GeneratedRegex(ArgumentPattern)]
	public static partial Regex ArgumentRegex();

	[GeneratedRegex(RelationshipPattern)]
	public static partial Regex RelationshipRegex();

	[GeneratedRegex(OperatorPattern)]
	public static partial Regex OperatorRegex();

	[GeneratedRegex(PositionPattern)]
	public static partial Regex PositionRegex();

	public static bool IsValidNodeName(string name)
	{
		return name != null && IdentifierRegex().IsMatch(name);
	}

	public static UMLNodeType GetNodeType(UMLNode node)
	{
		return node.Type;
	}

	public static string GetKeyword(UMLNodeType nodeType)
	{
		return NodeTypeKeywords[nodeType];
	}

	public static string GetKeyword(UMLNodeProperty property)
	{
		return NodePropertyKeywords[property];
	}

	public static bool TryGetNodeType(string keyword, out UMLNodeType nodeType)
	{
		return NodeTypesByKeyword.TryGetValue(keyword, out nodeType);
	}

	public static bool TryGetNodeProperty(string keyword, out UMLNodeProperty property)
	{
		return NodePropertiesByKeyword.TryGetValue(keyword, out property);
	}

	/// <summary>
	/// Reads a relationship operator such as <c>&lt;|--</c> as the UML
	/// relationship it draws. The strongest decoration on either side decides the
	/// type; the sides carrying it decide the direction.
	/// </summary>
	public static bool TryGetRelationship(
		string operatorText,
		out UMLRelationshipType type,
		out UMLRelationshipDirection direction
	)
	{
		type = UMLRelationshipType.Association;
		direction = UMLRelationshipDirection.None;

		Match match = OperatorRegex().Match(operatorText);
		if (!match.Success)
		{
			return false;
		}

		string head = match.Groups[1].Value;
		string body = match.Groups[2].Value;
		string tail = match.Groups[3].Value;
		if (head.Length == 0 && body.Length == 0 && tail.Length == 0)
		{
			return false;
		}

		UMLRelationshipEnding fromEnding = GetEnding(head);
		UMLRelationshipEnding toEnding = GetEnding(tail);
		UMLRelationshipEnding ending = fromEnding >= toEnding ? fromEnding : toEnding;
		bool isDashed = body.StartsWith('.');

		type = ending switch
		{
			UMLRelationshipEnding.HollowTriangle => isDashed
				? UMLRelationshipType.Realization
				: UMLRelationshipType.Generalization,
			UMLRelationshipEnding.HollowDiamond => UMLRelationshipType.Aggregation,
			UMLRelationshipEnding.FilledDiamond => UMLRelationshipType.Composition,
			_ => isDashed ? UMLRelationshipType.Dependency : UMLRelationshipType.Association,
		};

		bool decoratesFrom = ending != UMLRelationshipEnding.None && fromEnding == ending;
		bool decoratesTo = ending != UMLRelationshipEnding.None && toEnding == ending;
		direction = (decoratesFrom, decoratesTo) switch
		{
			(true, true) => UMLRelationshipDirection.Both,
			(true, false) => UMLRelationshipDirection.Backward,
			(false, true) => UMLRelationshipDirection.Forward,
			_ => UMLRelationshipDirection.None,
		};

		return true;
	}

	/// <summary>
	/// Spells out the operator that <see cref="TryGetRelationship"/> reads back as
	/// the same relationship.
	/// </summary>
	public static string GetOperator(UMLRelationshipType type, UMLRelationshipDirection direction)
	{
		string head = direction.DecoratesFrom() ? GetHeadSymbol(type) : string.Empty;
		string tail = direction.DecoratesTo() ? GetTailSymbol(type) : string.Empty;
		return head + (type.IsDashed() ? ".." : "--") + tail;
	}

	public static string FormatPosition(Vector2 position)
	{
		return $"[{FormatCoordinate(position.X)}, {FormatCoordinate(position.Y)}]";
	}

	public static string FormatCoordinate(float value)
	{
		return value.ToString(CoordinateFormat, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Removes a trailing comment and any trailing whitespace, keeping the
	/// leading indentation intact.
	/// </summary>
	public static string StripComment(string line)
	{
		SplitComment(line, out string code, out _);
		return code.TrimEnd();
	}

	/// <summary>
	/// Splits a line into its code part and its trailing comment, so rewrites can
	/// leave comments untouched.
	/// </summary>
	public static void SplitComment(string line, out string code, out string comment)
	{
		int commentIndex = line.IndexOf(CommentPrefix, StringComparison.Ordinal);
		if (commentIndex < 0)
		{
			code = line;
			comment = string.Empty;
			return;
		}

		code = line[..commentIndex];
		comment = line[commentIndex..];
	}

	/// <summary>
	/// Counts the leading tabs of a line. The result doubles as the offset of the
	/// line's content.
	/// </summary>
	public static int GetIndentation(string line)
	{
		int indentation = 0;
		while (indentation < line.Length && line[indentation] == '\t')
		{
			indentation += 1;
		}

		return indentation;
	}

	private static string GetHeadSymbol(UMLRelationshipType type)
	{
		return type switch
		{
			UMLRelationshipType.Generalization or UMLRelationshipType.Realization => "<|",
			UMLRelationshipType.Aggregation => "o",
			UMLRelationshipType.Composition => "*",
			_ => "<",
		};
	}

	private static string GetTailSymbol(UMLRelationshipType type)
	{
		return type switch
		{
			UMLRelationshipType.Generalization or UMLRelationshipType.Realization => "|>",
			UMLRelationshipType.Aggregation => "o",
			UMLRelationshipType.Composition => "*",
			_ => ">",
		};
	}

	private static UMLRelationshipEnding GetEnding(string decoration)
	{
		if (decoration.Length == 0)
		{
			return UMLRelationshipEnding.None;
		}

		if (decoration.Contains('|'))
		{
			return UMLRelationshipEnding.HollowTriangle;
		}

		return decoration[0] switch
		{
			'*' => UMLRelationshipEnding.FilledDiamond,
			'o' or 'O' => UMLRelationshipEnding.HollowDiamond,
			_ => UMLRelationshipEnding.OpenArrow,
		};
	}

	private static Dictionary<string, TKey> Invert<TKey>(Dictionary<TKey, string> keywords)
	{
		var inverted = new Dictionary<string, TKey>(keywords.Count, StringComparer.Ordinal);
		foreach (KeyValuePair<TKey, string> pair in keywords)
		{
			inverted.Add(pair.Value, pair.Key);
		}

		return inverted;
	}
}
