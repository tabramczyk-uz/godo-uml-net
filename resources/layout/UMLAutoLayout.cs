using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Places the nodes of a diagram that arrived without coordinates, which is what
/// every PlantUML file that GodoUML did not write itself looks like.
///
/// It is a layered layout in the Sugiyama spirit: hierarchical relationships are
/// read as "this one belongs above that one", nodes are pushed as far down as
/// those relationships require, and each row is then ordered, by every
/// relationship, to keep the lines between rows as untangled as the barycentre
/// rule can manage.
/// </summary>
public static class UMLAutoLayout
{
	public const float ColumnSpacing = 240.0f;
	public const float RowSpacing = 170.0f;
	public const float Margin = 40.0f;

	/// <summary>
	/// The footprint assumed for a node when keeping auto-placed nodes clear of
	/// fixed ones; the model does not know the size of the containers.
	/// </summary>
	public static readonly Vector2 EstimatedNodeSize = new(160.0f, 100.0f);
	public const float ClearanceBuffer = 20.0f;

	private const int OrderingSweeps = 4;
	private const int SeparationPasses = 32;
	private const float OverlapTolerance = 0.01f;

	public static void Apply(UMLDiagram diagram)
	{
		Assign(Layout(diagram.Nodes, diagram.Relationships));
	}

	/// <summary>
	/// Places the nodes that have no position. The layout is worked out for every
	/// node, positioned or not, and the positioned ones just don't take their
	/// cells. So pinning a node, or resetting its position, leaves the cells of
	/// all the others where they were. A node may land on top of a positioned
	/// one; the class canvas pushes it aside, the same way it does while a node
	/// is dragged over it, and <see cref="SeparateFromPositioned"/> does the same
	/// for callers whose positions end up in the source.
	/// </summary>
	public static void ApplyToUnpositioned(UMLDiagram diagram)
	{
		if (diagram.Nodes.All(node => node.Position != null))
		{
			return;
		}

		Dictionary<UMLNode, Vector2> positions = Layout(diagram.Nodes, diagram.Relationships);
		Assign(positions.Where(entry => entry.Key.Position == null).ToDictionary());
	}

	/// <summary>
	/// Pushes the auto-positioned nodes off the positioned ones and off each
	/// other, as the class canvas would, taking every node to be
	/// <see cref="EstimatedNodeSize"/>. For positions that will be written into
	/// the source, such as an import or a paste, where no canvas push follows.
	/// </summary>
	public static void SeparateFromPositioned(UMLDiagram diagram)
	{
		List<UMLNode> movable = [];
		List<Rect2> restBoxes = [];
		List<Rect2> fixedBoxes = [];
		foreach (UMLNode node in diagram.Nodes)
		{
			var box = new Rect2(node.Position.Value, EstimatedNodeSize);
			if (node.IsAutoPositioned)
			{
				movable.Add(node);
				restBoxes.Add(box);
			}
			else
			{
				fixedBoxes.Add(box);
			}
		}

		Vector2[] separated = Separate(restBoxes, fixedBoxes);
		for (int i = 0; i < movable.Count; i++)
		{
			movable[i].Position = separated[i];
		}
	}

	/// <summary>
	/// Computes where <paramref name="nodes"/> would go on the layout's grid.
	/// Nothing is mutated.
	/// </summary>
	public static Dictionary<UMLNode, Vector2> Layout(List<UMLNode> nodes, List<UMLRelationship> relationships)
	{
		if (nodes.Count == 0)
		{
			return [];
		}

		Dictionary<UMLNode, int> indices = IndexNodes(nodes);
		(List<(int From, int To)> hierarchy, List<(int From, int To)> pulls) = BuildEdges(relationships, indices);
		List<(int From, int To)> edges = BreakCycles(nodes.Count, hierarchy);
		int[] layers = AssignLayers(nodes.Count, edges);
		(edges, layers) = SplitLongEdges(edges, layers);
		List<List<int>> rows = GroupIntoRows(layers.Length, layers);

		OrderRows(rows, [.. edges, .. pulls], layers);
		return PlaceNodes(nodes, rows);
	}

	/// <summary>
	/// Pushes the <paramref name="movable"/> boxes out of each other and out of the
	/// <paramref name="fixedBoxes"/>, like a force field around every box: an
	/// overlapping box is shoved along the line between the two centres until
	/// <paramref name="clearance"/> separates them. Fixed boxes never move and push
	/// with full force; two movable boxes split the push between them. Pushes can
	/// cascade, so the pass repeats until nothing overlaps or the pass budget runs
	/// out. Returns the new top-left corners of <paramref name="movable"/>, in order.
	/// </summary>
	public static Vector2[] Separate(
		IReadOnlyList<Rect2> movable,
		IReadOnlyList<Rect2> fixedBoxes,
		float clearance = ClearanceBuffer
	)
	{
		Rect2[] boxes = [.. movable];

		for (int pass = 0; pass < SeparationPasses; pass++)
		{
			bool moved = false;

			for (int i = 0; i < boxes.Length; i++)
			{
				for (int j = i + 1; j < boxes.Length; j++)
				{
					Vector2 push = Repulsion(boxes[i], boxes[j], clearance, Vector2.Right);
					if (push != Vector2.Zero)
					{
						boxes[i].Position -= push / 2.0f;
						boxes[j].Position += push / 2.0f;
						moved = true;
					}
				}
			}

			// Fixed boxes go last so that, if the budget runs out, the leftover
			// overlaps are between movable boxes rather than over a fixed one.
			for (int i = 0; i < boxes.Length; i++)
			{
				foreach (Rect2 fixedBox in fixedBoxes)
				{
					Vector2 push = Repulsion(fixedBox, boxes[i], clearance, Vector2.Down);
					if (push != Vector2.Zero)
					{
						boxes[i].Position += push;
						moved = true;
					}
				}
			}

			if (!moved)
			{
				break;
			}
		}

		return boxes.Select(box => box.Position).ToArray();
	}

	/// <summary>
	/// How far <paramref name="target"/> has to move, directly away from the centre
	/// of <paramref name="source"/>, for the two to be <paramref name="clearance"/>
	/// apart; zero when they already are. Boxes sharing a centre are pushed along
	/// <paramref name="fallback"/>.
	/// </summary>
	private static Vector2 Repulsion(Rect2 source, Rect2 target, float clearance, Vector2 fallback)
	{
		Vector2 between = target.GetCenter() - source.GetCenter();
		Vector2 overlap = ((source.Size + target.Size) / 2.0f) + (Vector2.One * clearance) - between.Abs();
		if (overlap.X <= OverlapTolerance || overlap.Y <= OverlapTolerance)
		{
			return Vector2.Zero;
		}

		Vector2 direction = between.LengthSquared() > OverlapTolerance ? between.Normalized() : fallback;

		// Travelling along the direction, the boxes separate as soon as either
		// axis clears, so the push is the shorter of the two distances.
		float distance = float.PositiveInfinity;
		if (Mathf.Abs(direction.X) > Mathf.Epsilon)
		{
			distance = overlap.X / Mathf.Abs(direction.X);
		}

		if (Mathf.Abs(direction.Y) > Mathf.Epsilon)
		{
			distance = Mathf.Min(distance, overlap.Y / Mathf.Abs(direction.Y));
		}

		return direction * distance;
	}

	private static void Assign(Dictionary<UMLNode, Vector2> positions)
	{
		foreach ((UMLNode node, Vector2 position) in positions)
		{
			node.Position = position;
			node.IsAutoPositioned = true;
		}
	}

	private static Dictionary<UMLNode, int> IndexNodes(List<UMLNode> nodes)
	{
		var indices = new Dictionary<UMLNode, int>(nodes.Count);
		for (int i = 0; i < nodes.Count; i++)
		{
			indices[nodes[i]] = i;
		}

		return indices;
	}

	/// <summary>
	/// Sorts the relationships between the laid-out nodes into two kinds of edge.
	/// A hierarchical relationship becomes an "above/below" edge that decides the
	/// layers: a generalization points from the general classifier to the
	/// specific one, so superclasses end up on top, and an aggregation points from
	/// the whole to its parts. Associations and dependencies only become
	/// <c>Pulls</c>, which draw their two nodes together when a row is ordered
	/// but never move a node to another row: in real designs they often form
	/// cycles, and letting them decide the layers made cycle removal fire on
	/// ordinary diagrams (see ADR-0002).
	/// </summary>
	private static (List<(int From, int To)> Hierarchy, List<(int From, int To)> Pulls) BuildEdges(
		List<UMLRelationship> relationships,
		Dictionary<UMLNode, int> indices
	)
	{
		List<(int From, int To)> hierarchy = [];
		List<(int From, int To)> pulls = [];

		foreach (UMLRelationship relationship in relationships)
		{
			if (
				!indices.TryGetValue(relationship.From, out int from)
				|| !indices.TryGetValue(relationship.To, out int to)
				|| from == to
			)
			{
				continue;
			}

			if (Orient(relationship) is (UMLNode above, UMLNode below))
			{
				hierarchy.Add((indices[above], indices[below]));
			}
			else
			{
				pulls.Add((from, to));
			}
		}

		return (hierarchy, pulls);
	}

	/// <summary>
	/// The relationship as the layout reads it: which of its two nodes belongs
	/// above the other. Null for an association or dependency, which leaves the
	/// rows alone.
	/// </summary>
	public static (UMLNode Above, UMLNode Below)? Orient(UMLRelationship relationship)
	{
		if (!relationship.Type.IsHierarchical())
		{
			return null;
		}

		return relationship.Direction.DecoratesTo()
			? (relationship.To, relationship.From)
			: (relationship.From, relationship.To);
	}

	/// <summary>
	/// Turns around every edge that closes a cycle, so the layering sees a graph
	/// without any. Layering a cycle as it stands pushes its nodes down without
	/// end, which left them far below the rest under a stack of empty rows.
	///
	/// A depth-first search from each node in declaration order marks the nodes
	/// on its current path; an edge back to one of them is what closes a cycle.
	/// Turning it around keeps it in the graph, so the row ordering still pulls
	/// its two nodes together. In a cycle, the node declared first ends up on
	/// top, and walking the search in declaration order keeps the result
	/// independent of anything but the source.
	/// </summary>
	private static List<(int From, int To)> BreakCycles(int nodeCount, List<(int From, int To)> edges)
	{
		List<int>[] outgoing = new List<int>[nodeCount];
		for (int i = 0; i < nodeCount; i++)
		{
			outgoing[i] = [];
		}

		for (int i = 0; i < edges.Count; i++)
		{
			outgoing[edges[i].From].Add(i);
		}

		var state = new VisitState[nodeCount];
		var acyclic = new List<(int From, int To)>(edges);

		// A local function: it is visible only inside BreakCycles and reads and
		// writes its variables (state, outgoing, acyclic) directly, so the
		// recursion doesn't have to pass them along.
		void Visit(int node)
		{
			state[node] = VisitState.OnPath;
			foreach (int edge in outgoing[node])
			{
				int next = edges[edge].To;
				if (state[next] == VisitState.OnPath)
				{
					acyclic[edge] = (next, node);
				}
				else if (state[next] == VisitState.Unvisited)
				{
					Visit(next);
				}
			}

			state[node] = VisitState.Done;
		}

		for (int node = 0; node < nodeCount; node++)
		{
			if (state[node] == VisitState.Unvisited)
			{
				Visit(node);
			}
		}

		return acyclic;
	}

	private enum VisitState
	{
		Unvisited,
		OnPath,
		Done,
	}

	/// <summary>
	/// Longest-path layering. The edges arrive without cycles, but the passes
	/// are still capped at one per node as a guard against looping forever.
	/// </summary>
	private static int[] AssignLayers(int nodeCount, List<(int From, int To)> edges)
	{
		int[] layers = new int[nodeCount];

		for (int pass = 0; pass < nodeCount; pass++)
		{
			bool changed = false;
			foreach ((int from, int to) in edges)
			{
				if (layers[to] < layers[from] + 1)
				{
					layers[to] = layers[from] + 1;
					changed = true;
				}
			}

			if (!changed)
			{
				break;
			}
		}

		return layers;
	}

	/// <summary>
	/// Breaks every edge that skips rows into a chain through a placeholder in
	/// each row it crosses. Lines are drawn straight, so an edge from row 0 to
	/// row 2 would otherwise run through whatever sits below its upper end in
	/// row 1; the placeholder takes a cell there, and the row ordering keeps
	/// the nodes of that row out of its way. Placeholders are numbered on from
	/// the real nodes, and the returned layers cover them too. They are ordered
	/// like nodes but never placed.
	/// </summary>
	private static (List<(int From, int To)> Edges, int[] Layers) SplitLongEdges(
		List<(int From, int To)> edges,
		int[] layers
	)
	{
		List<int> allLayers = [.. layers];
		List<(int From, int To)> split = [];

		foreach ((int from, int to) in edges)
		{
			int previous = from;
			for (int layer = layers[from] + 1; layer < layers[to]; layer++)
			{
				int placeholder = allLayers.Count;
				allLayers.Add(layer);
				split.Add((previous, placeholder));
				previous = placeholder;
			}

			split.Add((previous, to));
		}

		return (split, allLayers.ToArray());
	}

	private static List<List<int>> GroupIntoRows(int nodeCount, int[] layers)
	{
		int rowCount = 0;
		foreach (int layer in layers)
		{
			rowCount = layer + 1 > rowCount ? layer + 1 : rowCount;
		}

		List<List<int>> rows = new(rowCount);
		for (int i = 0; i < rowCount; i++)
		{
			rows.Add([]);
		}

		for (int node = 0; node < nodeCount; node++)
		{
			rows[layers[node]].Add(node);
		}

		return rows;
	}

	/// <summary>
	/// Barycentre sweeps: a node drifts towards the average position of the nodes
	/// it is connected to in the row above, then in the row below, until the rows
	/// settle. Ties keep declaration order, so the result never depends on
	/// dictionary iteration order.
	/// </summary>
	private static void OrderRows(List<List<int>> rows, List<(int From, int To)> edges, int[] layers)
	{
		List<int>[] neighbours = BuildNeighbours(layers.Length, edges);
		int[] positions = new int[layers.Length];
		UpdatePositions(rows, positions);

		for (int sweep = 0; sweep < OrderingSweeps; sweep++)
		{
			bool downwards = sweep % 2 == 0;
			for (int row = 0; row < rows.Count; row++)
			{
				int index = downwards ? row : rows.Count - 1 - row;
				SortRow(rows[index], neighbours, positions, layers, downwards ? index - 1 : index + 1);
				UpdatePositions(rows, positions);
			}
		}
	}

	private static List<int>[] BuildNeighbours(int nodeCount, List<(int From, int To)> edges)
	{
		List<int>[] neighbours = new List<int>[nodeCount];
		for (int i = 0; i < nodeCount; i++)
		{
			neighbours[i] = [];
		}

		foreach ((int from, int to) in edges)
		{
			neighbours[from].Add(to);
			neighbours[to].Add(from);
		}

		return neighbours;
	}

	private static void SortRow(
		List<int> row,
		List<int>[] neighbours,
		int[] positions,
		int[] layers,
		int referenceLayer
	)
	{
		if (row.Count < 2 || referenceLayer < 0)
		{
			return;
		}

		var barycentres = new Dictionary<int, float>(row.Count);
		foreach (int node in row)
		{
			int count = 0;
			float sum = 0.0f;
			foreach (int neighbour in neighbours[node])
			{
				if (layers[neighbour] == referenceLayer)
				{
					sum += positions[neighbour];
					count += 1;
				}
			}

			barycentres[node] = count == 0 ? positions[node] : sum / count;
		}

		row.Sort(
			(left, right) =>
			{
				int comparison = barycentres[left].CompareTo(barycentres[right]);
				return comparison != 0 ? comparison : positions[left].CompareTo(positions[right]);
			}
		);
	}

	private static void UpdatePositions(List<List<int>> rows, int[] positions)
	{
		foreach (List<int> row in rows)
		{
			for (int i = 0; i < row.Count; i++)
			{
				positions[row[i]] = i;
			}
		}
	}

	private static Dictionary<UMLNode, Vector2> PlaceNodes(List<UMLNode> nodes, List<List<int>> rows)
	{
		var positions = new Dictionary<UMLNode, Vector2>(nodes.Count);

		int widestRow = 0;
		foreach (List<int> row in rows)
		{
			widestRow = row.Count > widestRow ? row.Count : widestRow;
		}

		for (int row = 0; row < rows.Count; row++)
		{
			float offset = (widestRow - rows[row].Count) * ColumnSpacing / 2.0f;
			for (int column = 0; column < rows[row].Count; column++)
			{
				// A placeholder from SplitLongEdges only keeps its cell free.
				int node = rows[row][column];
				if (node < nodes.Count)
				{
					positions[nodes[node]] = CellPosition(offset, column, row);
				}
			}
		}

		return positions;
	}

	private static Vector2 CellPosition(float offset, int column, int row)
	{
		return new Vector2(Margin + offset + (column * ColumnSpacing), Margin + (row * RowSpacing));
	}
}
