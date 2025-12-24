using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Networking;
using ONI_MP.Networking.Packets.World;
using UnityEngine;

namespace ONI_MP.Patches.World
{
	/// <summary>
	/// Patches for StoryManager to enable bidirectional story sync.
	/// - Host: Executes actions and broadcasts state to all clients
	/// - Client: Sends requests to host (blocks local execution)
	/// </summary>

	#region ForceCreateStory

	[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.ForceCreateStory))]
	public static class StoryManager_ForceCreateStory_Patch
	{
		public static bool Prefix(Database.Story story, int worldId)
		{
			// Skip if applying from network or not in multiplayer
			if (StoryStatePacket.IsApplying || !MultiplayerSession.InSession) return true;

			if (!MultiplayerSession.IsHost)
			{
				// Client: Send request to host
				var packet = new StoryActionRequestPacket
				{
					StoryId = story.Id,
					WorldId = worldId,
					Action = StoryActionRequestPacket.ActionType.Create
				};
				PacketSender.SendToHost(packet);
				DebugConsole.Log($"[StoryManager] Client requested ForceCreateStory: {story.Id}");
				return false; // Block local execution
			}

			return true; // Host: Execute normally
		}

		public static void Postfix(Database.Story story, int worldId)
		{
			// Host: Broadcast state after execution
			if (!MultiplayerSession.IsHost || StoryStatePacket.IsApplying) return;
			if (!MultiplayerSession.InSession) return;

			var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
			if (storyInstance == null) return;

			var packet = new StoryStatePacket
			{
				StoryId = story.Id,
				WorldId = storyInstance.worldId,
				CurrentState = (int)storyInstance.CurrentState
			};
			PacketSender.SendToAllClients(packet);
			DebugConsole.Log($"[StoryManager] Host broadcast ForceCreateStory: {story.Id}");
		}
	}

	#endregion

	#region DiscoverStoryEvent

	[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.DiscoverStoryEvent))]
	public static class StoryManager_DiscoverStoryEvent_Patch
	{
		public static bool Prefix(Database.Story story)
		{
			if (StoryStatePacket.IsApplying || !MultiplayerSession.InSession) return true;

			if (!MultiplayerSession.IsHost)
			{
				// Get worldId from existing story instance
				var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
				int worldId = storyInstance?.worldId ?? 0;

				var packet = new StoryActionRequestPacket
				{
					StoryId = story.Id,
					WorldId = worldId,
					Action = StoryActionRequestPacket.ActionType.Discover
				};
				PacketSender.SendToHost(packet);
				DebugConsole.Log($"[StoryManager] Client requested DiscoverStoryEvent: {story.Id}");
				return false;
			}

			return true;
		}

		public static void Postfix(Database.Story story)
		{
			if (!MultiplayerSession.IsHost || StoryStatePacket.IsApplying) return;
			if (!MultiplayerSession.InSession) return;

			var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
			if (storyInstance == null) return;

			var packet = new StoryStatePacket
			{
				StoryId = story.Id,
				WorldId = storyInstance.worldId,
				CurrentState = (int)storyInstance.CurrentState
			};
			PacketSender.SendToAllClients(packet);
			DebugConsole.Log($"[StoryManager] Host broadcast DiscoverStoryEvent: {story.Id}");
		}
	}

	#endregion

	#region BeginStoryEvent

	[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.BeginStoryEvent))]
	public static class StoryManager_BeginStoryEvent_Patch
	{
		public static bool Prefix(Database.Story story)
		{
			if (StoryStatePacket.IsApplying || !MultiplayerSession.InSession) return true;

			if (!MultiplayerSession.IsHost)
			{
				var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
				int worldId = storyInstance?.worldId ?? 0;

				var packet = new StoryActionRequestPacket
				{
					StoryId = story.Id,
					WorldId = worldId,
					Action = StoryActionRequestPacket.ActionType.Begin
				};
				PacketSender.SendToHost(packet);
				DebugConsole.Log($"[StoryManager] Client requested BeginStoryEvent: {story.Id}");
				return false;
			}

			return true;
		}

		public static void Postfix(Database.Story story)
		{
			if (!MultiplayerSession.IsHost || StoryStatePacket.IsApplying) return;
			if (!MultiplayerSession.InSession) return;

			var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
			if (storyInstance == null) return;

			var packet = new StoryStatePacket
			{
				StoryId = story.Id,
				WorldId = storyInstance.worldId,
				CurrentState = (int)storyInstance.CurrentState
			};
			PacketSender.SendToAllClients(packet);
			DebugConsole.Log($"[StoryManager] Host broadcast BeginStoryEvent: {story.Id}");
		}
	}

	#endregion

	#region CompleteStoryEvent (with keepsake position)

	[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.CompleteStoryEvent), typeof(Database.Story), typeof(Vector3))]
	public static class StoryManager_CompleteStoryEvent_Patch
	{
		public static bool Prefix(Database.Story story, Vector3 keepsakeSpawnPosition)
		{
			if (StoryStatePacket.IsApplying || !MultiplayerSession.InSession) return true;

			if (!MultiplayerSession.IsHost)
			{
				var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
				int worldId = storyInstance?.worldId ?? 0;

				var packet = new StoryActionRequestPacket
				{
					StoryId = story.Id,
					WorldId = worldId,
					Action = StoryActionRequestPacket.ActionType.Complete,
					KeepsakeX = keepsakeSpawnPosition.x,
					KeepsakeY = keepsakeSpawnPosition.y,
					KeepsakeZ = keepsakeSpawnPosition.z
				};
				PacketSender.SendToHost(packet);
				DebugConsole.Log($"[StoryManager] Client requested CompleteStoryEvent: {story.Id}");
				return false;
			}

			return true;
		}

		public static void Postfix(Database.Story story, Vector3 keepsakeSpawnPosition)
		{
			if (!MultiplayerSession.IsHost || StoryStatePacket.IsApplying) return;
			if (!MultiplayerSession.InSession) return;

			var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
			if (storyInstance == null) return;

			var packet = new StoryStatePacket
			{
				StoryId = story.Id,
				WorldId = storyInstance.worldId,
				CurrentState = (int)storyInstance.CurrentState,
				KeepsakeX = keepsakeSpawnPosition.x,
				KeepsakeY = keepsakeSpawnPosition.y,
				KeepsakeZ = keepsakeSpawnPosition.z
			};
			PacketSender.SendToAllClients(packet);
			DebugConsole.Log($"[StoryManager] Host broadcast CompleteStoryEvent: {story.Id}");
		}
	}

	#endregion

	#region CompleteStoryEvent (with focus sequence - for UI initiated)

	[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.CompleteStoryEvent), typeof(Database.Story), typeof(MonoBehaviour), typeof(FocusTargetSequence.Data))]
	public static class StoryManager_CompleteStoryEvent_Focus_Patch
	{
		public static bool Prefix(Database.Story story, MonoBehaviour keepsakeSpawnTarget, FocusTargetSequence.Data sequenceData)
		{
			if (StoryStatePacket.IsApplying || !MultiplayerSession.InSession) return true;

			if (!MultiplayerSession.IsHost)
			{
				var storyInstance = StoryManager.Instance?.GetStoryInstance(story.HashId);
				int worldId = storyInstance?.worldId ?? 0;

				var packet = new StoryActionRequestPacket
				{
					StoryId = story.Id,
					WorldId = worldId,
					Action = StoryActionRequestPacket.ActionType.Complete,
					KeepsakeX = sequenceData.Target.x,
					KeepsakeY = sequenceData.Target.y,
					KeepsakeZ = sequenceData.Target.z
				};
				PacketSender.SendToHost(packet);
				DebugConsole.Log($"[StoryManager] Client requested CompleteStoryEvent (focus): {story.Id}");
				return false;
			}

			return true;
		}

		// Note: Postfix not needed here as this overload will internally call the other CompleteStoryEvent
	}

	#endregion
}
