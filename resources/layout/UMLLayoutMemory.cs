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
/// <item>When the relationships between auto-positioned nodes change, every
/// spot is dropped and the nodes take the fresh layout, since the rows it
/// chose may no longer fit. That covers a relationship added, removed or
/// turned around, and a node deleted along with its relationships. Pinning a
/// node does not count: its relationships are still there, the layout merely
/// stops placing it, and the rest stay where they are.</item>
/// </list>
/// </summary>
public class UMLLayoutMemory
{
	private readonly Dictionary<string, Vector2> restPositions = [];

	/// <summary>
	/// The relationships between auto-positioned nodes at the last update, as
	/// above/below pairs of names: what the layout itself works from.
	/// </summary>
	private HashSet<(string Above, string Below)> layoutEdges = [];

	/// <summary>Every relationship at the last update, in the same form.</summary>
	private HashSet<(string Above, string Below)> allEdges = [];

	/// <summary>
	/// Renames made on the canvas since the last update, old name to new. They
	/// may yet be refused, so they are only applied once the code shows them.
	/// </summary>
	private readonly Dictionary<string, string> pendingRenames = [];

	/// <summary>
	/// Takes in a freshly laid out diagram, whose auto-positioned nodes carry
	/// the positions <see cref="UMLAutoLayout"/> gave them this time.
	/// </summary>
	public void Update(UMLDiagram diagram)
	{
		ApplyRenames(diagram);

		HashSet<(string Above, string Below)> newAllEdges = GetEdges(diagram, autoPositionedOnly: false);
		HashSet<(string Above, string Below)> newLayoutEdges = GetEdges(diagram, autoPositionedOnly: true);

		bool gained = !newLayoutEdges.IsSubsetOf(layoutEdges);
		bool removed = false;
		foreach ((string Above, string Below) edge in layoutEdges)
		{
			// Gone from the layout and from the diagram: not merely pinned.
			if (!newLayoutEdges.Contains(edge) && !newAllEdges.Contains(edge))
			{
				removed = true;
				break;
			}
		}

		if (gained || removed)
		{
			restPositions.Clear();
		}

		layoutEdges = newLayoutEdges;
		allEdges = newAllEdges;

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
	/// Notes a rename made on the canvas, so that once the code carries it the
	/// node keeps its spot and its relationships do not count as changed. A
	/// refused rename never reaches the code, and is dropped at the next
	/// <see cref="Update"/>.
	/// </summary>
	public void Rename(string oldName, string newName)
	{
		pendingRenames[oldName] = newName;
	}

	/// <summary>
	/// Forgets everything, so the next diagram is laid out afresh instead of
	/// inheriting spots from one that merely used the same node names.
	/// </summary>
	public void Clear()
	{
		restPositions.Clear();
		layoutEdges = [];
		allEdges = [];
		pendingRenames.Clear();
	}

	/// <summary>
	/// Carries the spots and relationships of each canvas rename over to the
	/// new name, if the diagram shows the rename went through.
	/// </summary>
	private void ApplyRenames(UMLDiagram diagram)
	{
		foreach ((string oldName, string newName) in pendingRenames)
		{
			if (diagram.FindNode(newName) == null || diagram.FindNode(oldName) != null)
			{
				continue;
			}

			if (restPositions.Remove(oldName, out Vector2 position))
			{
				restPositions[newName] = position;
			}

			layoutEdges = RenameIn(layoutEdges, oldName, newName);
			allEdges = RenameIn(allEdges, oldName, newName);
		}

		pendingRenames.Clear();
	}

	private static HashSet<(string Above, string Below)> RenameIn(
		HashSet<(string Above, string Below)> edges,
		string oldName,
		string newName
	)
	{
		var renamed = new HashSet<(string Above, string Below)>();
		foreach ((string above, string below) in edges)
		{
			renamed.Add((above == oldName ? newName : above, below == oldName ? newName : below));
		}

		return renamed;
	}

	private static HashSet<(string Above, string Below)> GetEdges(UMLDiagram diagram, bool autoPositionedOnly)
	{
		var result = new HashSet<(string Above, string Below)>();
		foreach (UMLRelationship relationship in diagram.Relationships)
		{
			(UMLNode above, UMLNode below) = UMLAutoLayout.Orient(relationship);
			if (above == below)
			{
				continue;
			}

			if (!autoPositionedOnly || (above.IsAutoPositioned && below.IsAutoPositioned))
			{
				result.Add((above.Name, below.Name));
			}
		}

		return result;
	}
}
