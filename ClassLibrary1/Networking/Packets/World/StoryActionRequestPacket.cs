using Database;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.Architecture;
using System.IO;

namespace ONI_MP.Networking.Packets.World
{
	/// <summary>
	/// Packet for clients to request story actions from host.
	/// The host will execute the action and broadcast results via StoryStatePacket.
	/// </summary>
	public class StoryActionRequestPacket : IPacket
	{
		public enum ActionType : byte
		{
			Create = 0,
			Discover = 1,
			Begin = 2,
			Complete = 3
		}

		public string StoryId;
		public int WorldId;
		public ActionType Action;
		public float KeepsakeX;
		public float KeepsakeY;
		public float KeepsakeZ;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(StoryId ?? string.Empty);
			writer.Write(WorldId);
			writer.Write((byte)Action);
			writer.Write(KeepsakeX);
			writer.Write(KeepsakeY);
			writer.Write(KeepsakeZ);
		}

		public void Deserialize(BinaryReader reader)
		{
			StoryId = reader.ReadString();
			WorldId = reader.ReadInt32();
			Action = (ActionType)reader.ReadByte();
			KeepsakeX = reader.ReadSingle();
			KeepsakeY = reader.ReadSingle();
			KeepsakeZ = reader.ReadSingle();
		}

		public void OnDispatched()
		{
			// Only host should process action requests
			if (!MultiplayerSession.IsHost) return;

			ExecuteAction();
		}

		private void ExecuteAction()
		{
			if (StoryManager.Instance == null)
			{
				DebugConsole.LogWarning("[StoryActionRequestPacket] StoryManager.Instance is null");
				return;
			}

			Story story = null;
			try { story = Db.Get().Stories.Get(StoryId); } catch { }
			if (story == null)
			{
				DebugConsole.LogWarning($"[StoryActionRequestPacket] Story not found: {StoryId}");
				return;
			}

			DebugConsole.Log($"[StoryActionRequestPacket] Executing action {Action} for story '{StoryId}'");

			// Set applying flag to let patches know this is from network
			StoryStatePacket.IsApplying = true;
			try
			{
				switch (Action)
				{
					case ActionType.Create:
						StoryManager.Instance.ForceCreateStory(story, WorldId);
						break;

					case ActionType.Discover:
						StoryManager.Instance.DiscoverStoryEvent(story);
						break;

					case ActionType.Begin:
						StoryManager.Instance.BeginStoryEvent(story);
						break;

					case ActionType.Complete:
						var keepsakePos = new UnityEngine.Vector3(KeepsakeX, KeepsakeY, KeepsakeZ);
						StoryManager.Instance.CompleteStoryEvent(story, keepsakePos);
						break;
				}
			}
			finally
			{
				StoryStatePacket.IsApplying = false;
			}

			// Now broadcast the updated state to all clients
			BroadcastStoryState(story);
		}

		private void BroadcastStoryState(Database.Story story)
		{
			var storyInstance = StoryManager.Instance.GetStoryInstance(story.HashId);
			if (storyInstance == null) return;

			var packet = new StoryStatePacket
			{
				StoryId = story.Id,
				WorldId = storyInstance.worldId,
				CurrentState = (int)storyInstance.CurrentState,
				KeepsakeX = KeepsakeX,
				KeepsakeY = KeepsakeY,
				KeepsakeZ = KeepsakeZ
			};

			PacketSender.SendToAllClients(packet);
		}
	}
}
