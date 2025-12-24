using Database;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.Architecture;
using System.IO;

namespace ONI_MP.Networking.Packets.World
{
	/// <summary>
	/// Packet to sync story instance state changes from host to all clients.
	/// </summary>
	public class StoryStatePacket : IPacket
	{
		public string StoryId;
		public int WorldId;
		public int CurrentState; // StoryInstance.State enum value
		public float KeepsakeX;
		public float KeepsakeY;
		public float KeepsakeZ;

		// Flag to prevent sync loops when applying state
		public static bool IsApplying = false;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(StoryId ?? string.Empty);
			writer.Write(WorldId);
			writer.Write(CurrentState);
			writer.Write(KeepsakeX);
			writer.Write(KeepsakeY);
			writer.Write(KeepsakeZ);
		}

		public void Deserialize(BinaryReader reader)
		{
			StoryId = reader.ReadString();
			WorldId = reader.ReadInt32();
			CurrentState = reader.ReadInt32();
			KeepsakeX = reader.ReadSingle();
			KeepsakeY = reader.ReadSingle();
			KeepsakeZ = reader.ReadSingle();
		}

		public void OnDispatched()
		{
			if (MultiplayerSession.IsHost) return;

			IsApplying = true;
			try
			{
				ApplyStoryState();
			}
			finally
			{
				IsApplying = false;
			}
		}

		private void ApplyStoryState()
		{
			if (StoryManager.Instance == null)
			{
				DebugConsole.LogWarning("[StoryStatePacket] StoryManager.Instance is null");
				return;
			}

			Story story = null;
			try { story = Db.Get().Stories.Get(StoryId); } catch { }
			if (story == null)
			{
				DebugConsole.LogWarning($"[StoryStatePacket] Story not found: {StoryId}");
				return;
			}

			var storyInstance = StoryManager.Instance.GetStoryInstance(story.HashId);

			// Create story if it doesn't exist
			if (storyInstance == null && CurrentState >= 0)
			{
				storyInstance = StoryManager.Instance.CreateStory(story, WorldId);
				DebugConsole.Log($"[StoryStatePacket] Created story instance: {StoryId}");
			}

			if (storyInstance == null)
			{
				DebugConsole.LogWarning($"[StoryStatePacket] Failed to get/create story instance: {StoryId}");
				return;
			}

			// Apply state based on the current state value
			var state = (StoryInstance.State)CurrentState;
			
			if (storyInstance.CurrentState != state)
			{
				DebugConsole.Log($"[StoryStatePacket] Updating story '{StoryId}' state: {storyInstance.CurrentState} -> {state}");
				
				// For COMPLETE state, spawn keepsake at specified position
				if (state == StoryInstance.State.COMPLETE && storyInstance.CurrentState != StoryInstance.State.COMPLETE)
				{
					var keepsakePos = new UnityEngine.Vector3(KeepsakeX, KeepsakeY, KeepsakeZ);
					StoryManager.Instance.CompleteStoryEvent(story, keepsakePos);
				}
				else
				{
					// Directly set the state for non-complete states
					storyInstance.CurrentState = state;
				}
			}
		}
	}
}
