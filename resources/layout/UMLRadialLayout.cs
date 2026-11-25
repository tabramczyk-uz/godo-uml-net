using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Lays out an association group, a connected group joined only by
/// associations and dependencies, as a radial tree. Nothing in such a group
/// belongs above anything else, so instead of rows its most connected node
/// goes in the middle with the others in rings around it (ADR-0002).
///
/// A breadth-first spanning tree from that root puts every level on exactly
/// one ring. The root's neighbours form an arc straight below it, one 45° step
/// each, closing into a full circle from eight neighbours on. Each of them owns
/// a wedge of the plane, and its descendants stay inside it, so the lines along
/// the tree never cross each other or run through a node. Lines that close a
/// cycle are not on the tree and are drawn straight wherever they fall.
///
/// Everything here is measured in cells, (1, 1) being one column and one row
/// of the layout's grid, so a circle here is an ellipse on the canvas
/// stretched to the shape of a cell. Two nodes at least one cell apart never
/// overlap, whatever the direction between them.
/// </summary>
public static class UMLRadialLayout
{
	/// <summary>
	/// The angle between neighbours of the root while they form an arc rather
	/// than a full circle.
	/// </summary>
	public const float ArcStepDegrees = 45.0f;

	/// <summary>Straight down: angles grow from the right towards the bottom.</summary>
	public const float BottomDegrees = 90.0f;

	/// <summary>
	/// How many neighbours of the root close the arc into a full circle:
	/// <see cref="ArcStepDegrees"/> times this is 360°.
	/// </summary>
	public const int FullCircle = 8;

	/// <summary>
	/// Returns where each node of <paramref name="group"/> goes, in cells,
	/// relative to the root at (0, 0). Only the relationships between nodes of
	/// the group count.
	/// </summary>
	public static Dictionary<UMLNode, Vector2> Layout(List<UMLNode> group, List<UMLRelationship> relationships)
	{
		List<int>[] neighbours = BuildNeighbours(group, relationships);
		int root = FindRoot(neighbours);
		(List<int>[] children, List<List<int>> levels) = BuildSpanningTree(neighbours, root);
		int[] sizes = CountSubtrees(children, levels);

		// In degrees, not kept to [0, 360): a wedge may straddle 0°.
		float[] angles = new float[group.Count];
		var wedges = new Wedge[group.Count];
		PlaceAroundRoot(children[root], sizes, angles, wedges);
		for (int level = 1; level < levels.Count - 1; level++)
		{
			foreach (int node in levels[level])
			{
				SpreadChildren(children[node], sizes, angles, wedges, wedges[node]);
			}
		}

		var positions = new Dictionary<UMLNode, Vector2> { [group[root]] = Vector2.Zero };
		float radius = 0.0f;
		for (int level = 1; level < levels.Count; level++)
		{
			radius = RingRadius(levels[level], angles, radius);
			foreach (int node in levels[level])
			{
				float angle = Mathf.DegToRad(angles[node]);
				positions[group[node]] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			}
		}

		return positions;
	}

	/// <summary>
	/// Each node's distinct neighbours within the group, as indices into it,
	/// in declaration order.
	/// </summary>
	private static List<int>[] BuildNeighbours(List<UMLNode> group, List<UMLRelationship> relationships)
	{
		var indices = new Dictionary<UMLNode, int>(group.Count);
		for (int i = 0; i < group.Count; i++)
		{
			indices[group[i]] = i;
		}

		var neighbours = new SortedSet<int>[group.Count];
		for (int i = 0; i < group.Count; i++)
		{
			neighbours[i] = [];
		}

		foreach (UMLRelationship relationship in relationships)
		{
			if (
				indices.TryGetValue(relationship.From, out int from)
				&& indices.TryGetValue(relationship.To, out int to)
				&& from != to
			)
			{
				neighbours[from].Add(to);
				neighbours[to].Add(from);
			}
		}

		return neighbours.Select(set => set.ToList()).ToArray();
	}

	/// <summary>The node with the most neighbours; the first declared wins a tie.</summary>
	private static int FindRoot(List<int>[] neighbours)
	{
		int root = 0;
		for (int node = 1; node < neighbours.Length; node++)
		{
			if (neighbours[node].Count > neighbours[root].Count)
			{
				root = node;
			}
		}

		return root;
	}

	/// <summary>
	/// A breadth-first search from <paramref name="root"/>, taking neighbours in
	/// declaration order: each node's children on the tree, and the nodes of
	/// each level, level 0 being the root alone.
	/// </summary>
	private static (List<int>[] Children, List<List<int>> Levels) BuildSpanningTree(
		List<int>[] neighbours,
		int root
	)
	{
		var children = new List<int>[neighbours.Length];
		for (int i = 0; i < neighbours.Length; i++)
		{
			children[i] = [];
		}

		bool[] reached = new bool[neighbours.Length];
		reached[root] = true;
		List<List<int>> levels = [[root]];

		while (true)
		{
			List<int> next = [];
			foreach (int node in levels[^1])
			{
				foreach (int neighbour in neighbours[node].Where(neighbour => !reached[neighbour]))
				{
					reached[neighbour] = true;
					children[node].Add(neighbour);
					next.Add(neighbour);
				}
			}

			if (next.Count == 0)
			{
				return (children, levels);
			}

			levels.Add(next);
		}
	}

	/// <summary>How many nodes each node's subtree holds, itself included.</summary>
	private static int[] CountSubtrees(List<int>[] children, List<List<int>> levels)
	{
		int[] sizes = new int[children.Length];
		for (int level = levels.Count - 1; level >= 0; level--)
		{
			foreach (int node in levels[level])
			{
				sizes[node] = 1 + children[node].Sum(child => sizes[child]);
			}
		}

		return sizes;
	}

	/// <summary>
	/// <paramref name="nodes"/> with the largest subtree first, ties in
	/// declaration order: the order in which they take the places nearest the
	/// middle of where they go.
	/// </summary>
	private static List<int> LargestFirst(List<int> nodes, int[] sizes)
	{
		return nodes.OrderByDescending(node => sizes[node]).ThenBy(node => node).ToList();
	}


	/// <summary>
	/// Puts the root's neighbours on the first ring. Fewer than
	/// <see cref="FullCircle"/> form an arc centred straight below the root,
	/// <see cref="ArcStepDegrees"/> apart; more share the whole circle evenly.
	/// The largest subtree takes the place nearest the bottom and the others
	/// alternate outwards from it, the left side (the larger angle) first.
	/// Each one's wedge is the slice as wide as the step between them, centred
	/// on it.
	/// </summary>
	private static void PlaceAroundRoot(List<int> neighbours, int[] sizes, float[] angles, Wedge[] wedges)
	{
		int count = neighbours.Count;
		float step = count < FullCircle ? ArcStepDegrees : 360.0f / count;
		float first = count < FullCircle ? BottomDegrees - (step * (count - 1) / 2.0f) : BottomDegrees;

		List<float> slots = Enumerable
			.Range(0, count)
			.Select(i => first + (step * i))
			.OrderBy(slot => Mathf.Abs(AngleBetween(slot, BottomDegrees)))
			.ThenByDescending(slot => AngleBetween(slot, BottomDegrees) > 0.0f)
			.ToList();

		List<int> order = LargestFirst(neighbours, sizes);
		for (int i = 0; i < count; i++)
		{
			angles[order[i]] = slots[i];
			wedges[order[i]] = new Wedge(slots[i] - (step / 2.0f), slots[i] + (step / 2.0f));
		}
	}

	/// <summary>
	/// Shares <paramref name="wedge"/> between <paramref name="children"/>, each
	/// getting a slice in proportion to its subtree, so that bigger subtrees get
	/// more room further out. The largest subtree takes the middle slice,
	/// pointing straight away from the root, and the others alternate to either
	/// side of it, the larger angle first. Each child sits in the middle of its
	/// slice, which becomes its own wedge.
	/// </summary>
	private static void SpreadChildren(
		List<int> children,
		int[] sizes,
		float[] angles,
		Wedge[] wedges,
		Wedge wedge
	)
	{
		// Built from the middle outwards, smallest angle first: the largest
		// subtree, then one to the larger-angle side, one to the other, and so on.
		List<int> order = LargestFirst(children, sizes);
		var fromSmallestAngle = new LinkedList<int>();
		for (int i = 0; i < order.Count; i++)
		{
			if (i % 2 == 0)
			{
				fromSmallestAngle.AddFirst(order[i]);
			}
			else
			{
				fromSmallestAngle.AddLast(order[i]);
			}
		}

		float total = children.Sum(child => sizes[child]);
		float start = wedge.From;
		foreach (int child in fromSmallestAngle)
		{
			float width = (wedge.To - wedge.From) * sizes[child] / total;
			wedges[child] = new Wedge(start, start + width);
			angles[child] = start + (width / 2.0f);
			start += width;
		}
	}

	/// <summary>
	/// The radius of a ring holding <paramref name="nodes"/>: at least one cell
	/// further out than the ring inside it, so no node is closer than that to
	/// any node of that ring, and large enough to keep every two nodes on the
	/// ring at least one cell apart. The straight distance between two points
	/// at angles <c>Δ</c> apart on a circle of radius <c>r</c> is
	/// <c>2r·sin(Δ/2)</c>.
	/// </summary>
	private static float RingRadius(List<int> nodes, float[] angles, float inner)
	{
		float radius = inner + 1.0f;
		for (int i = 0; i < nodes.Count; i++)
		{
			for (int j = i + 1; j < nodes.Count; j++)
			{
				float apart = Mathf.DegToRad(Mathf.Abs(AngleBetween(angles[nodes[i]], angles[nodes[j]])));
				radius = Mathf.Max(radius, 1.0f / (2.0f * Mathf.Sin(apart / 2.0f)));
			}
		}

		return radius;
	}

	/// <summary>
	/// The signed difference from <paramref name="to"/> to
	/// <paramref name="from"/> in degrees, in (-180, 180].
	/// </summary>
	private static float AngleBetween(float from, float to)
	{
		float difference = (from - to) % 360.0f;
		if (difference <= -180.0f)
		{
			difference += 360.0f;
		}
		else if (difference > 180.0f)
		{
			difference -= 360.0f;
		}

		return difference;
	}

	/// <summary>
	/// The slice of the plane, between two angles in degrees, that a node's
	/// descendants are laid out in.
	/// </summary>
	private readonly record struct Wedge(float From, float To);
}
