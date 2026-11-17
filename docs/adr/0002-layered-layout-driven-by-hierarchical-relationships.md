# Layered layout, with layers decided only by hierarchical relationships

Class diagrams are auto-laid out with the layered (Sugiyama) method. Force-directed, orthogonal and tree layouts were considered and rejected. Force-directed ignores the UML convention of putting the parent above the child, and small edits can reshuffle it. Orthogonal layout is too large to implement for this project. Tree layouts cannot handle cycles at all. The layered method is what Graphviz `dot`, yEd, ELK Layered and PlantUML use for class diagrams, and its standard first step removes cycles. So supporting cyclic dependencies does not argue against it. Only hierarchical relationships (generalization, realization, aggregation, composition) decide which layer a node goes in. Associations and dependencies only pull connected nodes together within a layer. In real designs they often form cycles, and letting them decide the layers is what made cycle removal fire on ordinary diagrams.

## Consequences

- The layout must be deterministic: it depends only on the source. Any tuning is a constant in the code, never a user setting or the window size.
- Rows wrap and separate clusters are packed towards a fixed target aspect ratio (4:3). The vertical order of hierarchical relationships is never broken to meet the ratio.
- Cycle removal (reversing DFS back edges) stays, but now only matters for unusual input such as aggregation loops or invalid inheritance cycles.
