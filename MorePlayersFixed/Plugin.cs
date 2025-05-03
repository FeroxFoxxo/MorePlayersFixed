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

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "feroxfoxxo.MorePlayersFixed";
        public const string modName = "MorePlayersFixed";
        public const string modVersion = "1.0.2";

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
            static bool Prefix(string ___RoomName)
            {
                if (string.IsNullOrEmpty(___RoomName))
                {
                    mls.LogError("RoomName is null or empty, using previous method!");
                    return true;
                }

                if (configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                Debug.Log($"MorePlayersFixed: Attempting to join a lobby with the name '{___RoomName}'.");

                TryJoiningRoomInternal(___RoomName);

                Debug.Log($"MorePlayersFixed: Joined lobby '{___RoomName}' with a maximum player count of '{configMaxPlayers.Value}'.");

                return false;
            }

            static void TryJoiningRoomInternal(string RoomName)
            {
                Debug.Log("Trying to join room: " + RoomName);
                Hashtable hashtable = new Hashtable();
                hashtable.Add("PASSWORD", Traverse.Create(DataDirector.instance).Field("networkPassword").GetValue());
                PhotonNetwork.LocalPlayer.SetCustomProperties(hashtable);
                RoomOptions roomOptions = new RoomOptions
                {
                    MaxPlayers = configMaxPlayers.Value,
                    IsVisible = false
                };
                Hashtable hashtable2 = new Hashtable();
                hashtable2.Add("PASSWORD", Traverse.Create(DataDirector.instance).Field("networkPassword").GetValue());
                roomOptions.CustomRoomProperties = hashtable2;
                PhotonNetwork.JoinOrCreateRoom(RoomName, roomOptions, TypedLobby.Default);
            }
        }

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.HostLobby))]
        public class HostLobbyPatch
        {
            static bool Prefix(bool _open, ref bool ___privateLobby)
            {
                if (configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                Debug.Log($"MorePlayersFixed: Hosting an '{(_open ? "open" : "unopen")}' lobby for '{configMaxPlayers.Value}' players.");

                HostLobbyInternal(_open, ref ___privateLobby);

                Debug.Log($"MorePlayersFixed: Started the '{(___privateLobby ? "private" : "public")}' lobby.");

                return false;
            }

            static void HostLobbyInternal(bool _open, ref bool privateLobby)
            {
                Debug.Log("Steam: Hosting lobby...");
                Lobby? lobby = SteamMatchmaking.CreateLobbyAsync(configMaxPlayers.Value).GetAwaiter().GetResult();
                if (!lobby.HasValue)
                {
                    Debug.LogError("Lobby created but not correctly instantiated.");
                }
                else if (_open)
                {
                    lobby.Value.SetPublic();
                    lobby.Value.SetJoinable(b: false);
                    privateLobby = false;
                }
                else
                {
                    lobby.Value.SetPrivate();
                    lobby.Value.SetFriendsOnly();
                    lobby.Value.SetJoinable(b: false);
                    privateLobby = true;
                }
            }
        }
    }
}
