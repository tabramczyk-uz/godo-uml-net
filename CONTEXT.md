# GodoUML

A UML designer in which a text DSL and a visual canvas are two equivalent views of the same diagram.

## Language

**Diagram**:
The whole content of one `.guml` file. A file holds exactly one diagram, and a diagram has exactly one diagram kind.
_Avoid_: document (that is the open file and its saved state), model

**Diagram kind**:
Which family of UML diagram a diagram is, e.g. a sequence diagram. It decides which elements the diagram may contain and how they are laid out.
_Avoid_: diagram type (clashes with node type and relationship type)

**Diagram header**:
The optional first line of a diagram's source that names its diagram kind (e.g. `diagram sequence`). A diagram without one is a class diagram.
_Avoid_: preamble, directive

**Class diagram**:
The diagram kind made of freely placed nodes joined by relationships. It deliberately does not police which node types appear together, so use cases and actors are allowed alongside classifiers.
_Avoid_: structure diagram, free diagram

**Hierarchical relationship**:
A generalization, realization, aggregation or composition: a relationship whose one end is the parent or whole of the other. Only these decide which node sits above which.
_Avoid_: structural relationship, containment

**Connected group**:
The nodes of a class diagram that are joined to each other through relationships of any type, directly or via other nodes. The auto-layout places each group on its own and then packs the groups side by side.
_Avoid_: cluster, component, island

**Hierarchical group**:
A connected group that contains at least one hierarchical relationship. It is laid out in rows, parents above children.

**Association group**:
A connected group joined only by associations and dependencies. It is laid out radially around its most connected node.
_Avoid_: flat group

**Unconnected node**:
A node with no relationship to any other node. Unconnected nodes are laid out in a grid below all the connected groups.
_Avoid_: isolated node, orphan

**Sequence diagram**:
A diagram kind made of participants placed left to right and messages between them, ordered top to bottom by time.

**Participant**:
An entity in a sequence diagram that sends and receives messages, drawn as a lifeline; it may be shown as an actor.
_Avoid_: lifeline (that is how a participant is drawn), object

**Message**:
A single communication from one participant to another (or to itself) in a sequence diagram: synchronous, asynchronous, or a reply.
_Avoid_: call, signal, relationship

**Self-message**:
A message whose sender and receiver are the same participant.

**Activation**:
The span of a participant's lifeline during which it is handling a synchronous message, from the call until its reply.
_Avoid_: execution specification, focus of control
