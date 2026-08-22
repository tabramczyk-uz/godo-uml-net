using System.Collections.Generic;
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

	private const int OrderingSweeps = 4;

	public static void Apply(UMLDiagram diagram)
	{
		if (diagram.Nodes.Count == 0)
		{
			return;
		}

		Dictionary<UMLNode, int> indices = IndexNodes(diagram.Nodes);
		List<(int From, int To)> edges = BuildEdges(diagram, indices);
		int[] layers = AssignLayers(diagram.Nodes.Count, edges);
		List<List<int>> rows = GroupIntoRows(diagram.Nodes.Count, layers);

		OrderRows(rows, edges, layers);
		PlaceNodes(diagram.Nodes, rows);
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
		UMLDiagram diagram,
		Dictionary<UMLNode, int> indices
	)
	{
		List<(int From, int To)> edges = [];

		foreach (UMLRelationship relationship in diagram.Relationships)
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

	private static void PlaceNodes(List<UMLNode> nodes, List<List<int>> rows)
	{
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
				nodes[rows[row][column]].Position = new Vector2(
					Margin + offset + (column * ColumnSpacing),
					Margin + (row * RowSpacing)
				);
			}
		}
	}
}
