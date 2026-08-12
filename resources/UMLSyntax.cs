using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum UMLNodeType
{
	Node,
	Class
}

public enum UMLNodeProperty
{
	Position
}

public static partial class UMLSyntax
{
	public const string CommentPrefix = "//";

	private const string Identifier = "[a-zA-Z_][a-zA-Z0-9_]*";
	private const string IdentifierPattern = "^" + Identifier + "$";
	private const string NodePattern = "^(" + Identifier + ")[ \\t]+(" + Identifier + ")$";
	private const string PropertyPattern = "^(" + Identifier + "):[ \\t]*(.+)$";
	private const string RelationshipPattern =
		"^(" + Identifier + ")\\s*(->|<-|[\\-\\.]{1,2}|[<>]{1,2}|[oO]{1,2})\\s*(" + Identifier + ")$";
	private const string PositionPattern = "^\\[\\s*([\\-+]?\\d*\\.?\\d+)\\s*,\\s*([\\-+]?\\d*\\.?\\d+)\\s*\\]$";

	private static readonly Dictionary<UMLNodeType, string> NodeTypeKeywords = new Dictionary<UMLNodeType, string>
	{
		{ UMLNodeType.Node, "node" },
		{ UMLNodeType.Class, "class" }
	};

	private static readonly Dictionary<UMLNodeProperty, string> NodePropertyKeywords = new Dictionary<UMLNodeProperty, string>
	{
		{ UMLNodeProperty.Position, "position" }
	};

	private static readonly Dictionary<string, UMLNodeType> NodeTypesByKeyword = Invert(NodeTypeKeywords);
	private static readonly Dictionary<string, UMLNodeProperty> NodePropertiesByKeyword = Invert(NodePropertyKeywords);

	[GeneratedRegex(IdentifierPattern)]
	public static partial Regex IdentifierRegex();

	[GeneratedRegex(NodePattern)]
	public static partial Regex NodeRegex();

	[GeneratedRegex(PropertyPattern)]
	public static partial Regex PropertyRegex();

	[GeneratedRegex(RelationshipPattern)]
	public static partial Regex RelationshipRegex();

	[GeneratedRegex(PositionPattern)]
	public static partial Regex PositionRegex();

	public static bool IsValidNodeName(string name)
	{
		return name != null && IdentifierRegex().IsMatch(name);
	}

	public static UMLNodeType GetNodeType(UMLNode node)
	{
		return node is UMLClass ? UMLNodeType.Class : UMLNodeType.Node;
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
	/// Removes a trailing comment and any trailing whitespace, keeping the leading indentation intact.
	/// </summary>
	public static string StripComment(string line)
	{
		SplitComment(line, out string code, out _);
		return code.TrimEnd();
	}

	/// <summary>
	/// Splits a line into its code part and its trailing comment, so rewrites can leave comments untouched.
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

		code = line.Substring(0, commentIndex);
		comment = line.Substring(commentIndex);
	}

	/// <summary>
	/// Counts the leading tabs of a line. The result doubles as the offset of the line's content.
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
