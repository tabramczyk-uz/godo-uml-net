using System.Collections.Generic;
using Godot;

/// <summary>
/// Where each auto-positioned node sits on the canvas when nothing pushes it,
/// by name. The layout is worked out afresh on every parse, and shifts whenever
/// a node is added; remembering the spots keeps the nodes already shown still
/// while the user types. They are given up when they would mislead:
/// <list type="bullet">
/// <item>A node that is deleted or pinned loses its spot, so one coming back
/// under that name, or unpinned by Reset Position, is placed by the current
/// layout rather than where it once was.</item>
/// <item>When the relationships between auto-positioned nodes gain one the
/// layout did not have, every spot is dropped and the nodes take the fresh
/// layout, since the rows it chose may no longer fit. Losing a relationship,
/// as when a node is deleted or pinned, leaves the spots as they are.</item>
/// </list>
/// </summary>
public class UMLLayoutMemory
{
	private readonly Dictionary<string, Vector2> restPositions = [];

	/// <summary>
	/// The relationships between auto-positioned nodes at the last update, as
	/// above/below pairs of names.
	/// </summary>
	private HashSet<(string Above, string Below)> edges = [];

	/// <summary>
	/// Takes in a freshly laid out diagram, whose auto-positioned nodes carry
	/// the positions <see cref="UMLAutoLayout"/> gave them this time.
	/// </summary>
	public void Update(UMLDiagram diagram)
	{
		HashSet<(string Above, string Below)> newEdges = GetEdges(diagram);
		if (!newEdges.IsSubsetOf(edges))
		{
			restPositions.Clear();
		}

		edges = newEdges;

		var autoPositioned = new HashSet<string>();
		foreach (UMLNode node in diagram.Nodes)
		{
			if (node.IsAutoPositioned)
			{
				autoPositioned.Add(node.Name);
				restPositions.TryAdd(node.Name, node.Position.Value);
			}
		}

		foreach (string name in new List<string>(restPositions.Keys))
		{
			if (!autoPositioned.Contains(name))
			{
				restPositions.Remove(name);
			}
		}
	}

	public Vector2 GetRestPosition(UMLNode node)
	{
		return restPositions[node.Name];
	}

	/// <summary>
	/// Gives a node's spot, and its relationships, to its new name as well, so
	/// a rename does not move anything. The old name keeps them too, since the
	/// rename may yet be refused; the next <see cref="Update"/> drops whichever
	/// name the code no longer uses. A spot already held under the new name is
	/// left alone.
	/// </summary>
	public void Rename(string oldName, string newName)
	{
		if (restPositions.TryGetValue(oldName, out Vector2 position))
		{
			restPositions.TryAdd(newName, position);
		}

		foreach ((string above, string below) in new List<(string, string)>(edges))
		{
			edges.Add((above == oldName ? newName : above, below == oldName ? newName : below));
		}
	}

	/// <summary>
	/// Forgets everything, so the next diagram is laid out afresh instead of
	/// inheriting spots from one that merely used the same node names.
	/// </summary>
	public void Clear()
	{
		restPositions.Clear();
		edges = [];
	}

	private static HashSet<(string Above, string Below)> GetEdges(UMLDiagram diagram)
	{
		var result = new HashSet<(string Above, string Below)>();
		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			(UMLNode above, UMLNode below) = UMLAutoLayout.Orient(relationship);
			if (above != below && above.IsAutoPositioned && below.IsAutoPositioned)
			{
				result.Add((above.Name, below.Name));
			}
		}

		return result;
	}
}
