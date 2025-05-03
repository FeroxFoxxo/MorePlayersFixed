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
    using System.Reflection;
    using System;

    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "feroxfoxxo.MorePlayersFixed";
        public const string modName = "MorePlayersFixed";
        public const string modVersion = "1.0.0";

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
            static bool Prefix(ref string ___RoomName)
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

                TryJoiningRoomInternal(ref ___RoomName);
                return false;
            }

            static void TryJoiningRoomInternal(ref string ___RoomName)
            {
                Debug.Log("Trying to join room: " + ___RoomName);

                Hashtable hashtable = new()
                {
                    { "PASSWORD", GetNetworkPassword() }
                };

                PhotonNetwork.LocalPlayer.SetCustomProperties(hashtable);

                RoomOptions roomOptions = new()
                {
                    MaxPlayers = configMaxPlayers.Value,
                    IsVisible = false
                };

                Hashtable hashtable2 = new()
                {
                    { "PASSWORD", GetNetworkPassword() }
                };

                roomOptions.CustomRoomProperties = hashtable2;

                PhotonNetwork.JoinOrCreateRoom(___RoomName, roomOptions, TypedLobby.Default);
            }

            static string GetNetworkPassword()
            {
                DataDirector instance = DataDirector.instance;
                const string fieldName = "networkPassword";

                Type type = instance.GetType();

                FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldInfo != null)
                {
                    return (string)fieldInfo.GetValue(instance);
                }

                throw new MissingMemberException("Could not find internal member 'networkPassword'");
            }
        }

        [HarmonyPatch(typeof(SteamManager), "HostLobby")]
        public class HostLobbyPatch
        {
            static bool Prefix(ref bool ___privateLobby, bool _open)
            {
                if (configMaxPlayers.Value == 0)
                {
                    mls.LogError("The MaxPlayers config is null or empty, using previous method!");
                    return true;
                }

                ___privateLobby = HostLobbyInternal(_open).GetAwaiter().GetResult();

                return false;
            }

            static async Task<bool> HostLobbyInternal(bool _open)
            {
                Debug.Log("Steam: Hosting lobby...");
                Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(configMaxPlayers.Value);

                if (!lobby.HasValue)
                {
                    Debug.LogError("Lobby created but not correctly instantiated.");
                    return true;
                }
                else if (_open)
                {
                    lobby.Value.SetPublic();
                    lobby.Value.SetJoinable(b: false);
                    return false;
                }
                else
                {
                    lobby.Value.SetPrivate();
                    lobby.Value.SetFriendsOnly();
                    lobby.Value.SetJoinable(b: false);
                    return true;
                }
            }
        }
    }
}
