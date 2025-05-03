namespace MorePlayersFixed
{
    using BepInEx;
    using BepInEx.Configuration;
    using BepInEx.Logging;
    using HarmonyLib;
    using Photon.Pun;
    using Photon.Realtime;
    using Steamworks.Data;
    using Steamworks;
    using UnityEngine;
    using ExitGames.Client.Photon;
    using System.Threading.Tasks;

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "feroxfoxxo.MorePlayersFixed";
        public const string modName = "MorePlayersFixed";
        public const string modVersion = "1.0.4";

        private readonly Harmony harmony = new(modGUID);

        public static ConfigEntry<int> configMaxPlayers;

        public static ManualLogSource mls;

        void Awake()
        {
            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
            mls.LogInfo($"{modGUID} is now awake!");

            configMaxPlayers = Config.Bind
            (
                "General",
                "MaxPlayersFixed",
                10,
                "The max amount of players allowed in a server"
            );

            harmony.PatchAll(typeof(TryJoiningRoomPatch));
            harmony.PatchAll(typeof(HostLobbyPatch));
        }

        [HarmonyPatch(typeof(NetworkConnect), "TryJoiningRoom")]
        public class TryJoiningRoomPatch
        {
            const string NetworkPasswordVariable = "networkPassword";

            static string GetNetworkPassword() => (string)Traverse.Create(DataDirector.instance).Field(NetworkPasswordVariable).GetValue();

            static bool Prefix(string ___RoomName)
            {
                if (string.IsNullOrEmpty(___RoomName))
                {
                    mls.LogError("RoomName is null or empty, using previous method!");
                    return true;
                }

                TryJoiningRoomWrapper(___RoomName);

                return false;
            }

            static void TryJoiningRoomWrapper(string RoomName)
            {
                Debug.Log($"MorePlayersFixed: Attempting to join a lobby with the name '{RoomName}' and password '{GetNetworkPassword()}'.");

                TryJoiningRoomInternal(RoomName);

                Debug.Log($"MorePlayersFixed: Joined lobby!");
            }

            static void TryJoiningRoomInternal(string RoomName)
            {
                Debug.Log("Trying to join room: " + RoomName);
                Hashtable hashtable = new Hashtable();
                hashtable.Add("PASSWORD", GetNetworkPassword());
                PhotonNetwork.LocalPlayer.SetCustomProperties(hashtable);
                RoomOptions roomOptions = new RoomOptions
                {
                    MaxPlayers = configMaxPlayers.Value,
                    IsVisible = false
                };
                Hashtable hashtable2 = new Hashtable();
                hashtable2.Add("PASSWORD", GetNetworkPassword());
                roomOptions.CustomRoomProperties = hashtable2;
                PhotonNetwork.JoinOrCreateRoom(RoomName, roomOptions, TypedLobby.Default);
            }
        }

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.HostLobby))]
        public class HostLobbyPatch
        {
            const string PrivateLobbyVariable = "privateLobby";

            static bool GetPrivateLobby(SteamManager instance) => (bool)Traverse.Create(instance).Field(PrivateLobbyVariable).GetValue();
            static void SetPrivateLobby(SteamManager instance, bool isPrivate) => Traverse.Create(instance).Field(PrivateLobbyVariable).SetValue(isPrivate);

            static bool Prefix(bool _open, SteamManager __instance)
            {
                if (configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                HostLobbyWrapper(_open, __instance);

                return false;
            }

            static async void HostLobbyWrapper(bool _open, SteamManager __instance)
            {
                Debug.Log($"MorePlayersFixed: Hosting an '{(_open ? "open" : "unopen")}' lobby for '{configMaxPlayers.Value}' players.");

                await HostLobbyInternal(_open, __instance);

                Debug.Log($"MorePlayersFixed: Started the {(GetPrivateLobby(__instance) ? "private" : "public")} lobby.");
            }

            static async Task HostLobbyInternal(bool _open, SteamManager __instance)
            {
                Debug.Log("Steam: Hosting lobby...");
                Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(configMaxPlayers.Value);
                if (!lobby.HasValue)
                {
                    Debug.LogError("Lobby created but not correctly instantiated.");
                }
                else if (_open)
                {
                    lobby.Value.SetPublic();
                    lobby.Value.SetJoinable(b: false);
                    SetPrivateLobby(__instance, false);
                }
                else
                {
                    lobby.Value.SetPrivate();
                    lobby.Value.SetFriendsOnly();
                    lobby.Value.SetJoinable(b: false);
                    SetPrivateLobby(__instance, true);
                }
            }
        }
    }
}
