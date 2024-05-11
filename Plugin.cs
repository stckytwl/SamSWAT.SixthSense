using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Aki.Common.Utils;
using Aki.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.Networking;

namespace SamSWAT.SixthSense
{
    [BepInPlugin("com.samswat.sixthsense", "SamSWAT.SixthSense", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static AudioClip AudioClip;
        internal static ConfigEntry<bool> PluginEnabled;
        internal static ConfigEntry<float> Cooldown;
        internal static ConfigEntry<int> Volume;

        private void Awake()
        {
            PluginEnabled = Config.Bind(
                "Main Settings",
                "Plugin on/off",
                true,
                "");

            Cooldown = Config.Bind(
                "Main Settings",
                "Sound cooldown",
                5f,
                "Time between sound playback in seconds");

            Volume = Config.Bind(
                "Main Settings",
                "Sound volume",
                100,
                new ConfigDescription("How loud the sound will be, percents",
                    new AcceptableValueRange<int>(0, 100)));

            new Patch().Enable();

            var directory = Assembly.GetExecutingAssembly().Location.GetDirectory() + @"\";
            var uri = directory + "audio.ogg"; 
            LoadAudioClip(uri);
        }

        // Yoinked from AmandsHitmarker
        private async void LoadAudioClip(string path)
        {
            AudioClip = await RequestAudioClip(path);
        }

        private async Task<AudioClip> RequestAudioClip(string path)
        {
            var audioType = AudioType.OGGVORBIS;

            var uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
            var sendWeb = uwr.SendWebRequest();

            while (!sendWeb.isDone)
                await Task.Yield();

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Logger.LogError("SamSWAT SixthSense: Failed to fetch audio clip. ");
                return null;
            }

            var audioclip = DownloadHandlerAudioClip.GetContent(uwr);
            return audioclip;
        }
    }

    public class Patch : ModulePatch
    {
        private static float _nextTime;

        protected override MethodBase GetTargetMethod()
        {
            return typeof(EnemyInfo).GetMethod("SetVisible", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        public static void PatchPostfix(EnemyInfo __instance, bool value)
        {
            if (!Plugin.PluginEnabled.Value || Plugin.AudioClip == null || Time.time < _nextTime) return;

            var person = (Player)__instance.Person;

            if (!value || !person.GetPlayer.IsYourPlayer) return;

            var betterAudio = Singleton<BetterAudio>.Instance;
            var audioSourceGroupType = BetterAudio.AudioSourceGroupType.Nonspatial;
            betterAudio.PlayNonspatial(Plugin.AudioClip, audioSourceGroupType, 0.0f, Plugin.Volume.Value / 100f);
            _nextTime = Time.time + Plugin.Cooldown.Value;
        }
    }
}