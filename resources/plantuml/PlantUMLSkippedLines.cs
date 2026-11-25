using System.Collections.Generic;
using System.Text;

/// <summary>
/// Tells the user which lines of a PlantUML import were left out. Every diagram
/// kind's import reports its skipped lines the same way.
/// </summary>
public static class PlantUMLSkippedLines
{
	/// <summary>
	/// Lists at most <paramref name="maxListed"/> of the
	/// <paramref name="warnings"/>, or returns <c>null</c> when there are none.
	/// </summary>
	public static string Describe(IReadOnlyList<string> warnings, int maxListed)
	{
		if (warnings.Count == 0)
		{
			return null;
		}

		string lines = warnings.Count == 1 ? "1 line" : $"{warnings.Count} lines";
		var description = new StringBuilder($"{lines} could not be imported and will be left out:");

		for (int i = 0; i < warnings.Count && i < maxListed; i++)
		{
			description.Append('\n').Append(warnings[i]);
		}

		if (warnings.Count > maxListed)
		{
			description.Append($"\n...and {warnings.Count - maxListed} more.");
		}

		return description.ToString();
	}
}
