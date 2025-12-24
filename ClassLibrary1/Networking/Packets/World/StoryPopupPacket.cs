using Database;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.Architecture;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ONI_MP.Networking.Packets.World
{
	/// <summary>
	/// Packet to sync story popups bidirectionally.
	/// Uses SenderId to avoid double popups on the original sender.
	/// </summary>
	public class StoryPopupPacket : IPacket
	{
		public CSteamID SenderId;
		public string Title;
		public string Description;
		public string ButtonText;
		public string AnimFileName;
		public int PopupType;
		public int WorldId;
		public float TargetX;
		public float TargetY;
		public float TargetZ;
		public List<string> MinionNames = new List<string>();

		// Flag to prevent sync loops
		public static bool IsApplying = false;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(SenderId.m_SteamID);
			writer.Write(Title ?? string.Empty);
			writer.Write(Description ?? string.Empty);
			writer.Write(ButtonText ?? string.Empty);
			writer.Write(AnimFileName ?? string.Empty);
			writer.Write(PopupType);
			writer.Write(WorldId);
			writer.Write(TargetX);
			writer.Write(TargetY);
			writer.Write(TargetZ);
			
			// Serialize minion names
			writer.Write(MinionNames?.Count ?? 0);
			if (MinionNames != null)
			{
				foreach (var name in MinionNames)
				{
					writer.Write(name ?? string.Empty);
				}
			}
		}

		public void Deserialize(BinaryReader reader)
		{
			SenderId = new CSteamID(reader.ReadUInt64());
			Title = reader.ReadString();
			Description = reader.ReadString();
			ButtonText = reader.ReadString();
			AnimFileName = reader.ReadString();
			PopupType = reader.ReadInt32();
			WorldId = reader.ReadInt32();
			TargetX = reader.ReadSingle();
			TargetY = reader.ReadSingle();
			TargetZ = reader.ReadSingle();
			
			// Deserialize minion names
			int minionCount = reader.ReadInt32();
			MinionNames = new List<string>(minionCount);
			for (int i = 0; i < minionCount; i++)
			{
				MinionNames.Add(reader.ReadString());
			}
		}

		public void OnDispatched()
		{
			IsApplying = true;
			try
			{
				// Show popup locally (unless we are the original sender - we already showed it)
				if (SenderId != MultiplayerSession.LocalSteamID)
				{
					ShowPopup();
				}

				// Host: rebroadcast to all except sender and self
				if (MultiplayerSession.IsHost)
				{
					var exclude = new HashSet<CSteamID> { SenderId, MultiplayerSession.LocalSteamID };
					PacketSender.SendToAllExcluding(this, exclude);
					DebugConsole.Log($"[StoryPopupPacket] Host rebroadcast to others: {Title}");
				}
			}
			finally
			{
				IsApplying = false;
			}
		}

		private void ShowPopup()
		{
			try
			{
				var popupType = (EventInfoDataHelper.PopupType)PopupType;
				
				// Look up minions by name
				GameObject[] minions = null;
				if (MinionNames != null && MinionNames.Count > 0)
				{
					var minionList = new List<GameObject>();
					foreach (var minionName in MinionNames)
					{
						// Find MinionIdentity with matching name
						foreach (var identity in global::Components.MinionIdentities.Items)
						{
							if (identity != null && identity.GetProperName() == minionName)
							{
								minionList.Add(identity.gameObject);
								break;
							}
						}
					}
					if (minionList.Count > 0)
					{
						minions = minionList.ToArray();
					}
				}
				
				var eventInfo = EventInfoDataHelper.GenerateStoryTraitData(
					Title,
					Description,
					ButtonText,
					AnimFileName,
					popupType,
					null,
					minions,
					null
				);

				EventInfoScreen.ShowPopup(eventInfo);
				DebugConsole.Log($"[StoryPopupPacket] Showed popup with {MinionNames?.Count ?? 0} minions: {Title}");
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogError($"[StoryPopupPacket] Failed to show popup: {ex.Message}");
			}
		}
	}
}
