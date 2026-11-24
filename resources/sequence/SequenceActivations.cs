using System;
using System.Collections.Generic;

/// <summary>
/// Derives a sequence diagram's activations from its messages, walking them in
/// order. None of the rules can fail, so a diagram that parses always has its
/// activations:
/// <list type="bullet">
/// <item>A synchronous message opens an activation on its receiver.</item>
/// <item>A reply closes the most recently opened activation that is still open
/// on the reply's sender and was called by the reply's receiver.</item>
/// <item>An activation opened while others are open on the same lifeline nests
/// inside them.</item>
/// <item>A call that is never replied to runs to the end of the diagram.</item>
/// <item>A reply with no matching open call closes nothing.</item>
/// <item>Asynchronous messages neither open nor close anything.</item>
/// </list>
/// </summary>
public static class SequenceActivations
{
	/// <returns>The activations, in the order their calls were sent.</returns>
	public static List<SequenceActivation> Derive(IReadOnlyList<SequenceMessage> messages)
	{
		List<SequenceActivation> activations = [];
		List<SequenceActivation> open = [];

		for (int index = 0; index < messages.Count; index++)
		{
			SequenceMessage message = messages[index];
			switch (message.Kind)
			{
				case SequenceMessageKind.Synchronous:
					SequenceActivation opened = new(
						message.To,
						message.From,
						index,
						GetNextDepth(open, message.To)
					);
					activations.Add(opened);
					open.Add(opened);
					break;
				case SequenceMessageKind.Reply:
					Close(open, message, index);
					break;
			}
		}

		return activations;
	}

	private static int GetNextDepth(List<SequenceActivation> open, SequenceParticipant participant)
	{
		int depth = 0;
		foreach (SequenceActivation activation in open)
		{
			if (activation.Participant == participant)
			{
				depth = Math.Max(depth, activation.Depth + 1);
			}
		}

		return depth;
	}

	private static void Close(List<SequenceActivation> open, SequenceMessage reply, int index)
	{
		for (int i = open.Count - 1; i >= 0; i--)
		{
			if (open[i].Participant == reply.From && open[i].Caller == reply.To)
			{
				open[i].EndMessageIndex = index;
				open.RemoveAt(i);
				return;
			}
		}
	}
}
