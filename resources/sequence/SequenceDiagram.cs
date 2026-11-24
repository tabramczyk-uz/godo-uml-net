using System.Collections.Generic;

/// <summary>
/// The content of a sequence diagram: participants in declaration order and
/// messages in source order. It shares nothing with <see cref="UMLDiagram"/>;
/// each diagram kind has its own model (see ADR-0001).
/// </summary>
public sealed class SequenceDiagram
{
	public List<SequenceParticipant> Participants { get; } = [];
	public List<SequenceMessage> Messages { get; } = [];

	/// <summary>
	/// The activations the messages imply, filled in by the parser from
	/// <see cref="SequenceActivations.Derive"/>. They are never written in the source.
	/// </summary>
	public List<SequenceActivation> Activations { get; } = [];
}
