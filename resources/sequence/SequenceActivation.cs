/// <summary>
/// The span of a participant's lifeline during which it handles a synchronous
/// message, from the call until its reply. Activations are derived from the
/// messages by <see cref="SequenceActivations"/> and never written in the source.
/// </summary>
public sealed class SequenceActivation
{
	public SequenceActivation(
		SequenceParticipant participant,
		SequenceParticipant caller,
		int startMessageIndex,
		int depth
	)
	{
		Participant = participant;
		Caller = caller;
		StartMessageIndex = startMessageIndex;
		Depth = depth;
	}

	/// <summary>The participant whose lifeline is active: the call's receiver.</summary>
	public SequenceParticipant Participant { get; }

	/// <summary>The call's sender, the one participant whose reply can close it.</summary>
	public SequenceParticipant Caller { get; }

	/// <summary>Index into the diagram's messages of the call that opened it.</summary>
	public int StartMessageIndex { get; }

	/// <summary>
	/// Index into the diagram's messages of the reply that closed it, or -1 when
	/// no reply came and the activation runs to the end of the diagram.
	/// </summary>
	public int EndMessageIndex { get; set; } = -1;

	/// <summary>
	/// How deeply it is nested on its participant's lifeline: 0 for an activation
	/// opened while the lifeline was idle, one more than the deepest activation
	/// still open on that lifeline otherwise.
	/// </summary>
	public int Depth { get; }

	public bool RunsToEnd => EndMessageIndex < 0;
}
