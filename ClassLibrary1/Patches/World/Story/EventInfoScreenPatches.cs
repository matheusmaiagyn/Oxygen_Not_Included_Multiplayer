using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Networking;
using ONI_MP.Networking.Packets.World;

namespace ONI_MP.Patches.World.Story
{
	/// <summary>
	/// Patches for EventInfoScreen to sync story popups bidirectionally.
	/// - Host shows popup -> broadcasts to all clients
	/// - Client shows popup -> sends to host -> host broadcasts to all
	/// </summary>

	[HarmonyPatch(typeof(EventInfoScreen), nameof(EventInfoScreen.ShowPopup))]
	public static class EventInfoScreen_ShowPopup_Patch
	{
		public static void Postfix(EventInfoData eventInfoData)
		{
			// Skip if applying from network or not in session
			if (StoryPopupPacket.IsApplying) return;
			if (!MultiplayerSession.InSession) return;

			// Skip if eventInfoData is null
			if (eventInfoData == null) return;

			try
			{
				var packet = BuildPopupPacket(eventInfoData);
				if (packet == null) return;

				if (MultiplayerSession.IsHost)
				{
					// Host: Broadcast to all clients
					PacketSender.SendToAllClients(packet);
					DebugConsole.Log($"[EventInfoScreen] Host broadcast popup: {eventInfoData.title}");
				}
				else
				{
					// Client: Send to host (who will show it and broadcast to other clients)
					PacketSender.SendToHost(packet);
					DebugConsole.Log($"[EventInfoScreen] Client sent popup to host: {eventInfoData.title}");
				}
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogError($"[EventInfoScreen] Failed to sync popup: {ex.Message}");
			}
		}

		private static StoryPopupPacket BuildPopupPacket(EventInfoData eventInfoData)
		{
			// Get the animation file name (stored as HashedString internally)
			// HashedString.ToString() returns hash, not original name
			// So we need to look it up from Assets.Anims
			string animFileName = "";
			try
			{
				var animField = typeof(EventInfoData).GetField("animFileName");
				if (animField != null)
				{
					var hashedString = (HashedString)animField.GetValue(eventInfoData);
					if (hashedString.IsValid)
					{
						// Search Assets.Anims to find the animation with matching hash
						foreach (var anim in Assets.Anims)
						{
							if (anim != null && anim.name != null)
							{
								var nameHash = new HashedString(anim.name);
								if (nameHash == hashedString)
								{
									animFileName = anim.name;
									break;
								}
							}
						}
						
						if (string.IsNullOrEmpty(animFileName))
						{
							DebugConsole.LogWarning($"[EventInfoScreen] Could not find anim for hash: {hashedString.HashValue}");
						}
					}
				}
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning($"[EventInfoScreen] Failed to get animFileName: {ex.Message}");
			}

			// Determine popup type
			int popupType = 0;
			try
			{
				var popupTypeField = typeof(EventInfoData).GetField("popupType");
				if (popupTypeField != null)
				{
					popupType = (int)popupTypeField.GetValue(eventInfoData);
				}
			}
			catch { }

			// Get click focus position if available
			float targetX = 0, targetY = 0, targetZ = 0;
			int worldId = 0;
			try
			{
				if (eventInfoData.clickFocus != null)
				{
					var pos = eventInfoData.clickFocus.position;
					targetX = pos.x;
					targetY = pos.y;
					targetZ = pos.z;
					
					var world = eventInfoData.clickFocus.gameObject?.GetMyWorld();
					if (world != null)
					{
						worldId = world.id;
					}
				}
			}
			catch { }

			// Extract minion names from EventInfoData if available
			var minionNames = new System.Collections.Generic.List<string>();
			try
			{
				// Try to find minions field - it might be named differently
				var minionsField = typeof(EventInfoData).GetField("minions");
				if (minionsField == null)
				{
					// Log all fields to see what's available
					DebugConsole.LogWarning("[EventInfoScreen] 'minions' field not found. Available fields:");
					foreach (var field in typeof(EventInfoData).GetFields())
					{
						DebugConsole.Log($"  - {field.Name}: {field.FieldType.Name}");
					}
				}
				else
				{
					var minions = minionsField.GetValue(eventInfoData) as UnityEngine.GameObject[];
					if (minions != null && minions.Length > 0)
					{
						DebugConsole.Log($"[EventInfoScreen] Found {minions.Length} minions");
						foreach (var minion in minions)
						{
							if (minion != null)
							{
								var identity = minion.GetComponent<MinionIdentity>();
								if (identity != null)
								{
									var name = identity.GetProperName();
									minionNames.Add(name);
									DebugConsole.Log($"[EventInfoScreen] Added minion: {name}");
								}
							}
						}
					}
					else
					{
						DebugConsole.LogWarning("[EventInfoScreen] minions field is null or empty");
					}
				}
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogError($"[EventInfoScreen] Minion extraction error: {ex.Message}");
			}

			return new StoryPopupPacket
			{
				SenderId = MultiplayerSession.LocalSteamID,
				Title = eventInfoData.title ?? "",
				Description = eventInfoData.description ?? "",
				ButtonText = eventInfoData.options?.Count > 0 ? eventInfoData.options[0].mainText ?? "" : "",
				AnimFileName = animFileName,
				PopupType = popupType,
				WorldId = worldId,
				TargetX = targetX,
				TargetY = targetY,
				TargetZ = targetZ,
				MinionNames = minionNames
			};
		}
	}
}
