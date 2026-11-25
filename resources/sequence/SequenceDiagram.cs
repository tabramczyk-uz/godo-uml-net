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

	/// <summary>
	/// What stands in the way of renaming <paramref name="participant"/> to
	/// <paramref name="newName"/>: a name the language does not allow, or one
	/// another participant already has. <c>null</c> when nothing does, which
	/// includes keeping the current name. Names are case-sensitive, as in the
	/// parser, and follow the same identifier rule as class diagram nodes.
	/// </summary>
	public string DescribeRenameProblem(SequenceParticipant participant, string newName)
	{
		string invalid = UMLSyntax.DescribeInvalidNodeName(newName);
		if (invalid != null)
		{
			return invalid;
		}

		SequenceParticipant other = Participants.Find(p => p.Name == newName);
		return other != null && other != participant
			? $"There is already a participant named \"{newName}\"."
			: null;
	}
}
