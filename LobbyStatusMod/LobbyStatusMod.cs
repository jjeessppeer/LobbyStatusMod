using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BepInEx;
using HarmonyLib;
using UnityEngine;

using Muse.Goi2.Entity;
using Newtonsoft.Json;

using System.IO;
using System.Net;

namespace LobbyStatusMod
{

    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class LobbyStatusMod : BaseUnityPlugin
    {
        public const string pluginGuid = "whereami.lobbystatus.mod";
        public const string pluginName = "Lobby Status Mod";
        public const string pluginVersion = "0.1";

        private const float _LobbyRefreshTimeout = 5;
        private float _LobbyRefreshTimerAcc = 0;

        public void Awake()
        {
            //FileLog.Log("PATCHING LOBBY STATUS MOD");
            // Console.WriteLine("\n___\n___\n___\n___PATCHING LOBBY STATUS MOD\n___\n___\n___\n___\n___\n___"); // 
            var harmony = new Harmony(pluginGuid);
            harmony.PatchAll();
        }

        public void Update()
        {
            _LobbyRefreshTimerAcc += Time.deltaTime;
            if (_LobbyRefreshTimerAcc < _LobbyRefreshTimeout) return;
            if (!LobbyStatus.Ready) return;
            _LobbyRefreshTimerAcc = 0;
            LobbyStatus.RequestRefresh();

        }
    }


    [HarmonyPatch]
    public static class LobbyStatus
    {
        private static bool _LoggedInComplete = false;
        public static bool Ready = false;

        public static List<LobbyData> LobbyDataList = new List<LobbyData>();

        private const string _UploadURL = "http://127.0.0.1:80";


        public static void PostLobbyList()
        {
            //FileLog.Log($"Uploading lobby list...");
            UploadDataPacket packet = new UploadDataPacket()
            {
                APIKey = "123123",
                LobbyDataList = LobbyDataList
            };

            //FileLog.Log($"Creating request...");
            var request = (HttpWebRequest)WebRequest.Create($"{_UploadURL}/update_list");
            var postData = JsonConvert.SerializeObject(packet);
            //FileLog.Log($"{postData}");
            var data = Encoding.ASCII.GetBytes(postData);
            request.Method = "POST";
            request.Timeout = 1000;
            request.ContentType = "application/json";
            request.ContentLength = data.Length;

            //FileLog.Log($"Writing stream...");
            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            catch (System.Net.WebException e)
            {
                //FileLog.Log($"Upload failed. Server unresponsive.\n{e.ToString()}");
            }
            
            //FileLog.Log($"Writing stream...");
            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                ////FileLog.Log($"Response: {responseString}");
            }
            catch (System.Net.WebException e)
            {
                //FileLog.Log($"Upload failed. No response.\n{e.ToString()}");
            }
            //FileLog.Log($"Upload complete.");
        }

        public static void RequestRefresh()
        {
            CharCustomActions.GetFriendsMatches(); // Request match list refresh.
        }

        // Go to menu when login is finished
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UILauncherMainPanel), "EnablePlayButton")]
        private static void LauncherReady()
        {
            if (_LoggedInComplete) return;
            _LoggedInComplete = true;
            //FileLog.Log("UILauncherMainPanel EnablePlayButton");
            UILauncherMainPanel.ForceCloseWithoutCallback();

            //FileLog.Log("Going to match view.");
            UIManager.UIViewMatchList();

            Ready = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UINewMatchList), "HandleMatchListUpdate")]
        private static void MatchListDataUpdated(UINewMatchList __instance, ref List<MatchEntity> ___matches)
        {
            //FileLog.Log("Match list updated.");

            //FileLog.Log("Matches: ");
            //LobbyDataList.Clear();
            foreach (MatchEntity match in ___matches)
            {
                LobbyData lobbyData = new LobbyData()
                {
                    MapName = match.Map.NameText.En,
                    MaxPlayers = match.MaxPlayerCount,
                    CurrentPlayers = match.PlayerCount,
                    CurrentPilots = match.CaptainCount
                };
                LobbyDataList.Add(lobbyData);
                //FileLog.Log($"Match list entry: {JsonConvert.SerializeObject(lobbyData)}");
            }

            PostLobbyList();

        }
    }

    public class UploadDataPacket
    {
        public string APIKey;
        public List<LobbyData> LobbyDataList;
    }

    public class LobbyData
    {
        public string MapName;
        public int MaxPlayers;
        public int CurrentPlayers;
        public int CurrentPilots;
    }
}
