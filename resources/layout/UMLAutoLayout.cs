using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Places the nodes of a diagram that arrived without coordinates, which is what
/// every PlantUML file that GodoUML did not write itself looks like.
///
/// It is a layered layout in the Sugiyama spirit: relationships are read as
/// "this one belongs above that one", nodes are pushed as far down as their
/// relationships require, and each row is then ordered to keep the lines between
/// rows as untangled as the barycentre rule can manage.
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
		Assign(Layout(diagram.Nodes, diagram.Relationships, []));
	}

	public static void ApplyToUnpositioned(UMLDiagram diagram)
	{
		List<UMLNode> unpositionedNodes = diagram.Nodes.Where(n => n.Position == null).ToList();
		if (unpositionedNodes.Count == 0)
		{
			return;
		}

		List<Vector2> obstacles = diagram
			.Nodes.Where(n => n.Position != null)
			.Select(n => n.Position.Value)
			.ToList();

		Assign(Layout(unpositionedNodes, diagram.Relationships, obstacles));
	}

	/// <summary>
	/// Computes where <paramref name="nodes"/> would go, keeping clear of the fixed
	/// nodes whose top-left corners are in <paramref name="obstacles"/>. Nothing is
	/// mutated.
	/// </summary>
	public static Dictionary<UMLNode, Vector2> Layout(
		List<UMLNode> nodes,
		List<UMLRelationship> relationships,
		IReadOnlyList<Vector2> obstacles
	)
	{
		if (nodes.Count == 0)
		{
			return [];
		}

		Dictionary<UMLNode, int> indices = IndexNodes(nodes);
		List<(int From, int To)> edges = BuildEdges(relationships, indices);
		int[] layers = AssignLayers(nodes.Count, edges);
		List<List<int>> rows = GroupIntoRows(nodes.Count, layers);

		OrderRows(rows, edges, layers);
		return PlaceNodes(nodes, rows, obstacles);
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
	/// Turns relationships into "above/below" edges. A generalization points from
	/// the general classifier to the specific one, so superclasses end up on top;
	/// an aggregation points from the whole to its parts; anything else keeps the
	/// order it was written in.
	/// </summary>
	private static List<(int From, int To)> BuildEdges(
		List<UMLRelationship> relationships,
		Dictionary<UMLNode, int> indices
	)
	{
		List<(int From, int To)> edges = [];

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

			edges.Add(PointsAtParent(relationship) ? (to, from) : (from, to));
		}

		return edges;
	}

	private static bool PointsAtParent(UMLRelationship relationship)
	{
		bool marksContainer = relationship.Type
			is UMLRelationshipType.Generalization
				or UMLRelationshipType.Realization
				or UMLRelationshipType.Aggregation
				or UMLRelationshipType.Composition;

		return marksContainer && relationship.Direction.DecoratesTo();
	}

	/// <summary>
	/// Longest-path layering, relaxed at most once per node so a cycle in the
	/// diagram cannot spin here forever.
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

	private static Dictionary<UMLNode, Vector2> PlaceNodes(
		List<UMLNode> nodes,
		List<List<int>> rows,
		IReadOnlyList<Vector2> obstacles
	)
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
			int column = 0;
			foreach (int node in rows[row])
			{
				Vector2 candidate = CellPosition(offset, column, row);
				while (Collides(candidate, obstacles))
				{
					column++;
					candidate = CellPosition(offset, column, row);
				}

				positions[nodes[node]] = candidate;
				column++;
			}
		}

		return positions;
	}

	private static Vector2 CellPosition(float offset, int column, int row)
	{
		return new Vector2(Margin + offset + (column * ColumnSpacing), Margin + (row * RowSpacing));
	}

	private static bool Collides(Vector2 candidate, IReadOnlyList<Vector2> obstacles)
	{
		Rect2 box = new Rect2(candidate, EstimatedNodeSize).Grow(ClearanceBuffer);
		foreach (Vector2 obstacle in obstacles)
		{
			if (box.Intersects(new Rect2(obstacle, EstimatedNodeSize)))
			{
				return true;
			}
		}

		return false;
	}
}
