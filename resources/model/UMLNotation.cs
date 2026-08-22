using System.Collections.Generic;
using System.Text;

/// <summary>
/// Standard UML surface notation for class members — <c>+ name : Type</c> and
/// <c>+ name(argument : Type) : Type</c>. The canvas and the PlantUML exporter
/// both render members through here so they cannot drift apart.
/// </summary>
public static class UMLNotation
{
	public const char PublicSymbol = '+';
	public const char PrivateSymbol = '-';
	public const char ProtectedSymbol = '#';
	public const char PackageSymbol = '~';

	private static readonly Dictionary<UMLVisibility, char> VisibilitySymbols = new()
	{
		{ UMLVisibility.Public, PublicSymbol },
		{ UMLVisibility.Private, PrivateSymbol },
		{ UMLVisibility.Protected, ProtectedSymbol },
		{ UMLVisibility.Package, PackageSymbol },
	};

	private static readonly Dictionary<char, UMLVisibility> VisibilitiesBySymbol = Invert();

	/// <summary>Every character that can open a member line.</summary>
	public static readonly char[] Symbols =
	[
		PublicSymbol,
		PrivateSymbol,
		ProtectedSymbol,
		PackageSymbol,
	];

	public static string GetSymbol(UMLVisibility visibility)
	{
		return VisibilitySymbols.TryGetValue(visibility, out char symbol)
			? symbol.ToString()
			: string.Empty;
	}

	public static UMLVisibility GetVisibility(string symbol)
	{
		return symbol != null
			&& symbol.Length == 1
			&& VisibilitiesBySymbol.TryGetValue(symbol[0], out UMLVisibility visibility)
			? visibility
			: UMLVisibility.Unknown;
	}

	public static string Format(UMLAttribute attribute)
	{
		return Prefix(attribute.Visibility) + attribute.Name + TypeSuffix(attribute.Type);
	}

	public static string Format(UMLMethod method)
	{
		var signature = new StringBuilder();
		signature.Append(Prefix(method.Visibility)).Append(method.Name).Append('(');

		for (int i = 0; i < method.Arguments.Count; i++)
		{
			if (i > 0)
			{
				signature.Append(", ");
			}

			UMLMethodArgument argument = method.Arguments[i];
			signature.Append(argument.Name).Append(TypeSuffix(argument.Type));
		}

		return signature.Append(')').Append(TypeSuffix(method.ReturnType)).ToString();
	}

	private static string Prefix(UMLVisibility visibility)
	{
		string symbol = GetSymbol(visibility);
		return symbol.Length == 0 ? string.Empty : symbol + " ";
	}

	private static string TypeSuffix(string type)
	{
		return string.IsNullOrEmpty(type) ? string.Empty : " : " + type;
	}

	private static Dictionary<char, UMLVisibility> Invert()
	{
		var inverted = new Dictionary<char, UMLVisibility>(VisibilitySymbols.Count);
		foreach (KeyValuePair<UMLVisibility, char> pair in VisibilitySymbols)
		{
			inverted.Add(pair.Value, pair.Key);
		}

		return inverted;
	}
}
