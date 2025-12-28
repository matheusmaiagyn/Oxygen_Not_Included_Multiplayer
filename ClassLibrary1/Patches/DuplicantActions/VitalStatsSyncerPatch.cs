using HarmonyLib;
using ONI_MP.Networking;
using ONI_MP.Networking.Synchronization;

namespace ONI_MP.Patches.DuplicantActions
{
	[HarmonyPatch(typeof(MinionIdentity), "OnSpawn")]
	public static class MinionIdentitySpawnPatch
	{
		public static void Postfix(MinionIdentity __instance)
		{
			if (!MultiplayerSession.IsHost) return;
			if (__instance.IsNullOrDestroyed()) return;

			// Attach VitalStatsSyncer if not present
			var syncer = __instance.gameObject.GetComponent<VitalStatsSyncer>();
			if (syncer == null)
			{
				__instance.gameObject.AddComponent<VitalStatsSyncer>();
			}
		}
	}
}
