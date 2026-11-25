# Layered layout, with layers decided only by hierarchical relationships

Class diagrams are auto-laid out with the layered (Sugiyama) method. Force-directed, orthogonal and tree layouts were considered and rejected. Force-directed ignores the UML convention of putting the parent above the child, and small edits can reshuffle it. Orthogonal layout is too large to implement for this project. Tree layouts cannot handle cycles at all. The layered method is what Graphviz `dot`, yEd, ELK Layered and PlantUML use for class diagrams, and its standard first step removes cycles. So supporting cyclic dependencies does not argue against it. Only hierarchical relationships (generalization, realization, aggregation, composition) decide which layer a node goes in. Associations and dependencies only pull connected nodes together within a layer. In real designs they often form cycles, and letting them decide the layers is what made cycle removal fire on ordinary diagrams.

## Consequences

- The layout must be deterministic: it depends only on the source. Any tuning is a constant in the code, never a user setting or the window size.
- Rows wrap and separate clusters are packed towards a fixed target aspect ratio (4:3). The vertical order of hierarchical relationships is never broken to meet the ratio.
- Cycle removal (reversing DFS back edges) stays, but now only matters for unusual input such as aggregation loops or invalid inheritance cycles.

## Amendment (2026-11-25): connected groups, radial association groups, packing

Putting every node that has no hierarchical relationship in the top row made diagrams of many unrelated or only associated classes one very wide row. Wrapping long rows onto further rows was rejected: the lines from a parent to a wrapped row would run through the row above it.

Instead the diagram is split into connected groups (see `CONTEXT.md`), each laid out on its own:

- A hierarchical group keeps the layered layout above, with rows never wrapped. Nodes attached to it only by associations belong to it and sit in its rows.
- An association group has no "above", so in it, and only in it, associations and dependencies decide placement: a radial tree around the node with the most relationships. Its direct neighbours sit on one ring, an arc below it while there are fewer than eight; further levels sit on larger rings, each inside its ancestor's wedge. Lines along the tree cannot cross by construction. Breadth-first levels have no cycles, so cycle removal never fires on these groups.
- Unconnected nodes go in a grid below everything else.

The groups are packed on shelves towards the 4:3 target. Every rule is a constant in the code, so the layout still depends only on the source.
