using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Where everything of a sequence diagram sits on its canvas, and what a point
/// picks. Participants stand left to right in declaration order, each heading
/// a lifeline, and messages run top to bottom in source order; the order is
/// the whole layout, so nothing here is ever written to the source. It is a
/// plain class, not a node, so it runs in the tests without Godot. All
/// positions are in diagram coordinates: the first head's left side is at
/// x = 0, and the top of an actor's head at y = 0.
/// </summary>
public class SequenceLayout
{
	/// <summary>
	/// The height of an actor's head, a stick figure with the name underneath.
	/// Every lifeline starts this far down, so the heads stand on one line.
	/// </summary>
	public const float HeadHeight = 64.0f;

	/// <summary>The height of a plain participant's head, a box with the name in it.</summary>
	public const float BoxHeight = 40.0f;

	/// <summary>The width of an activation bar.</summary>
	public const float ActivationWidth = 12.0f;

	/// <summary>How far right a self-message's loop reaches from its lifeline.</summary>
	public const float SelfMessageWidth = 32.0f;

	/// <summary>How far below where it leaves a self-message's loop comes back.</summary>
	public const float SelfMessageDrop = 20.0f;

	private const float MinHeadWidth = 80.0f;
	private const float HeadPadding = 12.0f;

	/// <summary>The least room between two neighbouring heads.</summary>
	private const float HeadGap = 40.0f;

	/// <summary>The least distance between two neighbouring lifelines.</summary>
	private const float MinLifelineGap = 140.0f;

	/// <summary>The room between the heads and the first message.</summary>
	private const float FirstMessageOffset = 40.0f;

	/// <summary>The room between one message and the next.</summary>
	private const float MessageSpacing = 40.0f;

	/// <summary>How far a label stands off its arrow.</summary>
	private const float LabelGap = 4.0f;

	/// <summary>The least room left on each side of a label between its two lifelines.</summary>
	private const float LabelPadding = 16.0f;

	private readonly SequenceDiagram diagram;
	private readonly float[] lifelineXs;
	private readonly float[] headWidths;
	private readonly float[] messageYs;
	private readonly Rect2[] activationRects;
	private readonly Vector2[][] messagePaths;
	private readonly Rect2[] labelRects;

	/// <param name="measure">The size of a piece of text as the canvas draws it.</param>
	public SequenceLayout(SequenceDiagram diagram, Func<string, Vector2> measure)
	{
		this.diagram = diagram;

		int participantCount = diagram.Participants.Count;
		int messageCount = diagram.Messages.Count;
		headWidths = new float[participantCount];
		lifelineXs = new float[participantCount];
		messageYs = new float[messageCount];
		activationRects = new Rect2[diagram.Activations.Count];
		messagePaths = new Vector2[messageCount][];
		labelRects = new Rect2[messageCount];

		for (int i = 0; i < participantCount; i++)
		{
			headWidths[i] = Math.Max(MinHeadWidth, measure(diagram.Participants[i].Name).X + 2.0f * HeadPadding);
		}

		PlaceLifelines(measure);
		PlaceMessageRows();
		PlaceActivations();
		for (int n = 0; n < messageCount; n++)
		{
			messagePaths[n] = TracePath(n);
			labelRects[n] = PlaceLabel(n, measure(diagram.Messages[n].Label));
		}
	}

	/// <summary>Where the lifelines end, below the last message.</summary>
	public float LifelineBottom { get; private set; }

	/// <summary>The box around everything drawn, or <c>null</c> for an empty diagram.</summary>
	public Rect2? Bounds
	{
		get
		{
			if (diagram.Participants.Count == 0)
			{
				return null;
			}

			Rect2 bounds = GetHeadRect(0);
			for (int i = 1; i < diagram.Participants.Count; i++)
			{
				bounds = bounds.Merge(GetHeadRect(i));
			}

			bounds = bounds.Expand(new Vector2(bounds.Position.X, LifelineBottom));
			foreach (Rect2 label in labelRects)
			{
				if (label.Size.X > 0.0f)
				{
					bounds = bounds.Merge(label);
				}
			}

			foreach (Vector2[] path in messagePaths)
			{
				foreach (Vector2 point in path)
				{
					bounds = bounds.Expand(point);
				}
			}

			return bounds;
		}
	}

	/// <summary>The x of the participant's lifeline, through the middle of its head.</summary>
	public float GetLifelineX(int participantIndex)
	{
		return lifelineXs[participantIndex];
	}

	/// <summary>
	/// The participant's head above its lifeline, name included: a box for a
	/// plain participant, a stick figure for an actor. Every head ends where
	/// the lifelines start.
	/// </summary>
	public Rect2 GetHeadRect(int participantIndex)
	{
		float width = headWidths[participantIndex];
		float height = diagram.Participants[participantIndex].Kind == SequenceParticipantKind.Actor
			? HeadHeight
			: BoxHeight;
		return new Rect2(lifelineXs[participantIndex] - width / 2.0f, HeadHeight - height, width, height);
	}

	/// <summary>The y at which the message leaves its sender.</summary>
	public float GetMessageY(int messageIndex)
	{
		return messageYs[messageIndex];
	}

	/// <summary>
	/// The points the message's arrow runs through, from the sender to the
	/// arrowhead at the receiver: two for a straight arrow, four for a
	/// self-message's loop.
	/// </summary>
	public IReadOnlyList<Vector2> GetMessagePath(int messageIndex)
	{
		return messagePaths[messageIndex];
	}

	/// <summary>
	/// The box the message's label is drawn in: centred above a straight
	/// arrow, or to the right of a self-message's loop. Empty for no label.
	/// </summary>
	public Rect2 GetLabelRect(int messageIndex)
	{
		return labelRects[messageIndex];
	}

	/// <summary>The bar of the activation at the same index in the diagram's activations.</summary>
	public Rect2 GetActivationRect(int activationIndex)
	{
		return activationRects[activationIndex];
	}

	/// <summary>The participant whose head contains <paramref name="point"/>, or -1.</summary>
	public int GetParticipantAt(Vector2 point)
	{
		for (int i = 0; i < diagram.Participants.Count; i++)
		{
			if (GetHeadRect(i).HasPoint(point))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// The participant whose head contains <paramref name="point"/>, or whose
	/// lifeline passes closest to it within <paramref name="maxDistance"/>
	/// sideways, or -1.
	/// </summary>
	public int GetLifelineAt(Vector2 point, float maxDistance)
	{
		int head = GetParticipantAt(point);
		if (head >= 0)
		{
			return head;
		}

		if (point.Y < HeadHeight || point.Y > LifelineBottom)
		{
			return -1;
		}

		int closest = -1;
		float closestDistance = maxDistance;
		for (int i = 0; i < lifelineXs.Length; i++)
		{
			float distance = Math.Abs(point.X - lifelineXs[i]);
			if (distance <= closestDistance)
			{
				closest = i;
				closestDistance = distance;
			}
		}

		return closest;
	}

	/// <summary>
	/// The message whose label contains <paramref name="point"/>, or whose
	/// arrow passes closest to it within <paramref name="maxDistance"/>, or -1.
	/// </summary>
	public int GetMessageAt(Vector2 point, float maxDistance)
	{
		int closest = -1;
		float closestDistance = maxDistance;
		for (int n = 0; n < messagePaths.Length; n++)
		{
			float distance = labelRects[n].HasPoint(point) ? 0.0f : DistanceToPath(point, messagePaths[n]);
			if (distance <= closestDistance)
			{
				closest = n;
				closestDistance = distance;
			}
		}

		return closest;
	}

	/// <summary>
	/// The index a new message drawn at height <paramref name="y"/> takes: just
	/// below every message that leaves above it.
	/// </summary>
	public int GetInsertIndex(float y)
	{
		int index = 0;
		while (index < messageYs.Length && messageYs[index] < y)
		{
			index++;
		}

		return index;
	}

	/// <summary>
	/// Spaces the lifelines so neighbouring heads keep apart and every label
	/// fits between the lifelines its message joins, or beside its
	/// self-message's loop. Each lifeline is placed once, from the left, so a
	/// long label only pushes the lifelines right of it.
	/// </summary>
	private void PlaceLifelines(Func<string, Vector2> measure)
	{
		for (int k = 0; k < lifelineXs.Length; k++)
		{
			if (k == 0)
			{
				lifelineXs[0] = headWidths[0] / 2.0f;
				continue;
			}

			float x = lifelineXs[k - 1]
				+ Math.Max(MinLifelineGap, (headWidths[k - 1] + headWidths[k]) / 2.0f + HeadGap);
			foreach (SequenceMessage message in diagram.Messages)
			{
				float labelWidth = measure(message.Label).X;
				int from = diagram.Participants.IndexOf(message.From);
				int to = diagram.Participants.IndexOf(message.To);
				if (message.IsSelfMessage && from == k - 1)
				{
					x = Math.Max(x, lifelineXs[k - 1] + SelfMessageWidth + LabelGap + labelWidth + LabelPadding);
				}
				else if (!message.IsSelfMessage && Math.Max(from, to) == k)
				{
					x = Math.Max(x, lifelineXs[Math.Min(from, to)] + labelWidth + 2.0f * LabelPadding);
				}
			}

			lifelineXs[k] = x;
		}
	}

	/// <summary>
	/// Gives each message its row, a self-message a taller one for its loop,
	/// and ends the lifelines one row below the last.
	/// </summary>
	private void PlaceMessageRows()
	{
		float y = HeadHeight + FirstMessageOffset;
		for (int n = 0; n < messageYs.Length; n++)
		{
			messageYs[n] = y;
			y += MessageSpacing + (diagram.Messages[n].IsSelfMessage ? SelfMessageDrop : 0.0f);
		}

		LifelineBottom = y;
	}

	/// <summary>
	/// Runs each activation's bar from its call down to its reply, or to the
	/// end of the lifeline, each level of nesting half a bar further right.
	/// A self-call's bar starts where its loop comes back.
	/// </summary>
	private void PlaceActivations()
	{
		for (int a = 0; a < activationRects.Length; a++)
		{
			SequenceActivation activation = diagram.Activations[a];
			int participant = diagram.Participants.IndexOf(activation.Participant);
			float left = lifelineXs[participant] - ActivationWidth / 2.0f + activation.Depth * ActivationWidth / 2.0f;

			float top = messageYs[activation.StartMessageIndex];
			if (diagram.Messages[activation.StartMessageIndex].IsSelfMessage)
			{
				top += SelfMessageDrop;
			}

			float bottom = activation.RunsToEnd ? LifelineBottom : messageYs[activation.EndMessageIndex];
			activationRects[a] = new Rect2(left, top, ActivationWidth, bottom - top);
		}
	}

	/// <summary>
	/// The arrow's points, each end on the side of the participant's
	/// innermost activation bar facing the other end, or on the bare lifeline.
	/// </summary>
	private Vector2[] TracePath(int messageIndex)
	{
		SequenceMessage message = diagram.Messages[messageIndex];
		int from = diagram.Participants.IndexOf(message.From);
		int to = diagram.Participants.IndexOf(message.To);
		float y = messageYs[messageIndex];

		if (message.IsSelfMessage)
		{
			float returnY = y + SelfMessageDrop;
			float loopX = lifelineXs[from] + ActivationWidth / 2.0f + SelfMessageWidth;
			return
			[
				new Vector2(GetEdgeX(from, y, true), y),
				new Vector2(loopX, y),
				new Vector2(loopX, returnY),
				new Vector2(GetEdgeX(from, returnY, true), returnY),
			];
		}

		bool rightward = lifelineXs[to] > lifelineXs[from];
		return
		[
			new Vector2(GetEdgeX(from, y, rightward), y),
			new Vector2(GetEdgeX(to, y, !rightward), y),
		];
	}

	/// <summary>
	/// The x at which an arrow meets the participant at height
	/// <paramref name="y"/>: the right or left side of its innermost activation
	/// bar there, or its lifeline when none is active.
	/// </summary>
	private float GetEdgeX(int participantIndex, float y, bool rightSide)
	{
		int deepest = -1;
		Rect2 bar = default;
		for (int a = 0; a < activationRects.Length; a++)
		{
			SequenceActivation activation = diagram.Activations[a];
			Rect2 rect = activationRects[a];
			bool covers = rect.Position.Y <= y && y <= rect.End.Y;
			if (
				covers
				&& diagram.Participants.IndexOf(activation.Participant) == participantIndex
				&& activation.Depth > deepest
			)
			{
				deepest = activation.Depth;
				bar = rect;
			}
		}

		if (deepest < 0)
		{
			return lifelineXs[participantIndex];
		}

		return rightSide ? bar.End.X : bar.Position.X;
	}

	private Rect2 PlaceLabel(int messageIndex, Vector2 size)
	{
		if (diagram.Messages[messageIndex].Label.Length == 0)
		{
			return default;
		}

		Vector2[] path = messagePaths[messageIndex];
		if (diagram.Messages[messageIndex].IsSelfMessage)
		{
			float middleY = (path[1].Y + path[2].Y) / 2.0f;
			return new Rect2(path[1].X + LabelGap, middleY - size.Y / 2.0f, size);
		}

		float centerX = (path[0].X + path[1].X) / 2.0f;
		return new Rect2(centerX - size.X / 2.0f, path[0].Y - LabelGap - size.Y, size);
	}

	private static float DistanceToPath(Vector2 point, Vector2[] path)
	{
		float distance = float.MaxValue;
		for (int i = 1; i < path.Length; i++)
		{
			distance = Math.Min(distance, UMLGeometry.DistanceToSegment(point, path[i - 1], path[i]));
		}

		return distance;
	}
}
