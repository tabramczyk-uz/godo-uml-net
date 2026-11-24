using System.Collections.Generic;

/// <summary>
/// The selected nodes of the class canvas, kept by name, so a selection
/// outlives the re-parse after every edit, which builds new nodes and rebuilds
/// all the containers.
/// </summary>
public class NodeSelection
{
	private readonly HashSet<string> names = [];

	public bool Contains(UMLNode node)
	{
		return names.Contains(node.Name);
	}

	public void Add(UMLNode node)
	{
		names.Add(node.Name);
	}

	/// <summary>
	/// Selects a node pressed on. Without <paramref name="addToSelection"/>,
	/// the rest of the selection is dropped, unless the node is already part
	/// of it, so the whole selection can still be dragged.
	/// </summary>
	public void Press(UMLNode node, bool addToSelection)
	{
		if (!addToSelection && !Contains(node))
		{
			names.Clear();
		}

		names.Add(node.Name);
	}

	/// <summary>
	/// Selects exactly the nodes named, such as ones about to be pasted,
	/// which only exist once the code has been re-parsed.
	/// </summary>
	public void Replace(IEnumerable<string> newNames)
	{
		names.Clear();
		names.UnionWith(newNames);
	}

	/// <summary>Deselects everything. Returns whether anything was selected.</summary>
	public bool Clear()
	{
		if (names.Count == 0)
		{
			return false;
		}

		names.Clear();
		return true;
	}

	/// <summary>Drops the names of nodes <paramref name="diagram"/> no longer has.</summary>
	public void RemoveMissing(UMLDiagram diagram)
	{
		names.RemoveWhere(name => diagram.FindNode(name) == null);
	}
}
