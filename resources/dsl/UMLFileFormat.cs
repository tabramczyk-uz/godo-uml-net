using System;

/// <summary>
/// GodoUML's own file format: a <c>.guml</c> file holds a diagram's source code
/// exactly as the code editor shows it. The source is the single source of
/// truth, so saving it loses nothing, and reading it back goes through the same
/// parser as typing it in.
/// </summary>
public static class UMLFileFormat
{
	public const string Extension = "guml";

	public const string Description = "GodoUML Diagram";

	/// <summary>The filter a file dialog lists <c>.guml</c> files under.</summary>
	public static string DialogFilter => $"*.{Extension} ; {Description}";

	/// <summary>
	/// <paramref name="path"/> with the <c>.guml</c> extension added, unless it
	/// already ends in it (in any letter case).
	/// </summary>
	public static string WithExtension(string path)
	{
		return path.EndsWith($".{Extension}", StringComparison.OrdinalIgnoreCase)
			? path
			: $"{path}.{Extension}";
	}

	/// <summary>
	/// <paramref name="text"/> with Windows and old Mac line endings turned into
	/// the plain <c>\n</c> the parser and the code writers split lines on, so a
	/// file edited outside GodoUML reads back the same.
	/// </summary>
	public static string NormalizeLineEndings(string text)
	{
		return text.Replace("\r\n", "\n").Replace('\r', '\n');
	}
}
