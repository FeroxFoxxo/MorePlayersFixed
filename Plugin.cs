namespace MorePlayers
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
    using System.Threading.Tasks;

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "feroxfoxxo.MorePlayersFixed";
        public const string modName = "MorePlayersFixed";
        public const string modVersion = "1.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

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
            static bool Prefix(ref string ___RoomName)
            {
                if(configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                if(NetworkConnect.instance != null)
                {
                    Debug.Log("Trying to join room: " + ___RoomName);
                    
                    Hashtable hashtable = new Hashtable();
                    hashtable.Add("PASSWORD", DataDirector.instance.networkPassword);
                    PhotonNetwork.LocalPlayer.SetCustomProperties(hashtable);
                    
                    RoomOptions roomOptions = new RoomOptions
                    {
                        MaxPlayers = configMaxPlayers.Value,
                        IsVisible = false
                    };

                    Hashtable hashtable2 = new Hashtable();
                    hashtable2.Add("PASSWORD", DataDirector.instance.networkPassword);
                    roomOptions.CustomRoomProperties = hashtable2;
                    PhotonNetwork.JoinOrCreateRoom(___RoomName, roomOptions, TypedLobby.Default);
                    
                    return false;
                }
                else
                {
                    mls.LogError("NetworkConnect instance is null, using previous method!");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(SteamManager), "HostLobby")]
        public class HostLobbyPatch
        {
            static bool Prefix(bool _open)
            {
                HostLobbyAsync(_open);
                return false;
            }

            static async void HostLobbyAsync(bool _open)
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
