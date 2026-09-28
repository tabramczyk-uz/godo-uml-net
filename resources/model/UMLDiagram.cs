using System.Collections.Generic;

/// <summary>
/// One parsed diagram: the nodes and the relationships between them, in the
/// order the source code declared them.
/// </summary>
public class UMLDiagram
{
	public List<UMLNode> Nodes { get; set; } = [];
	public List<UMLRelationship> Relationships { get; set; } = [];

	public UMLNode FindNode(string name)
	{
		return Nodes.Find(node => node.Name == name);
	}

	/// <summary>
	/// The first name of the form <c>Name</c>, <c>Name2</c>, <c>Name3</c>… that no
	/// node is using yet.
	/// </summary>
	public string GetUniqueNodeName(string baseName)
	{
		if (FindNode(baseName) == null)
		{
			return baseName;
		}

		for (int suffix = 2; ; suffix++)
		{
			string candidate = baseName + suffix;
			if (FindNode(candidate) == null)
			{
				return candidate;
			}
		}
	}

	/// <summary>
	/// What stands in the way of renaming <paramref name="node"/> to
	/// <paramref name="newName"/>: a name the language does not allow, or one
	/// another node already has. <c>null</c> when nothing does, which includes
	/// keeping the current name. Names are case-sensitive, as in the parser.
	/// </summary>
	public string DescribeRenameProblem(UMLNode node, string newName)
	{
		string invalid = UMLSyntax.DescribeInvalidNodeName(newName);
		if (invalid != null)
		{
			return invalid;
		}

		UMLNode other = FindNode(newName);
		return other != null && other != node
			? $"There is already a node named \"{newName}\"."
			: null;
	}
}
