using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SoundpackLoader;


internal record MetaData
{
    public const string PLUGIN_NAME = "SoundpackLoader";
    public const string PLUGIN_GUID = "org.crispykevin.soundpackloader";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(MetaData.PLUGIN_GUID, MetaData.PLUGIN_NAME, MetaData.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger = null!;
    public static SoundpackLoader Loader = null!;
    public static ConfigEntry<KeyCode> CycleForwards = null!;
    public static ConfigEntry<KeyCode> CycleBackwards = null!;
    public static int Idx = 2; // As of right now soundpackloader stores the vanilla soundpacks from asset bundles in file name order, so default is idx 2

    public Plugin()
    {
        Logger = base.Logger;
    }

    private void Awake()
    {
        CycleForwards = Config.Bind("General", "CycleForwards", KeyCode.PageUp);
        CycleBackwards = Config.Bind("General", "CycleBackwards", KeyCode.PageDown);
        new Harmony(MetaData.PLUGIN_GUID).PatchAll();
        
        Logger.LogInfo($"Plugin {MetaData.PLUGIN_GUID} v{MetaData.PLUGIN_VERSION} is loaded!");
        SoundpackManager.SoundpackChanged += (_, e) => Logger.LogInfo($"{e.OldPack} -> {e.NewPack}");

    }

    void Start()
    {
        Loader = this.gameObject.AddComponent<SoundpackLoader>();

        LoadVanillaSoundpacks();
        LoadCustomSoundpacks();
    }

    void LoadVanillaSoundpacks()
    {
        // Extracted from GameController.loadSoundBundleResources
        var VANILLA_VOLUME_MODIFIERS = new Dictionary<string, float>
        {
            ["default"] = 1.0f,
            ["bass"] = 0.72f,
            ["muted"] = 0.34f,
            ["eightbit"] = 0.25f,
            ["club"] = 0.25f,
            ["fart"] = 0.75f,
        };

        Logger.LogInfo("Loading vanilla soundpacks...");
        var info = new DirectoryInfo(Path.Combine(Application.streamingAssetsPath, "soundpacks"));
        var bundles = info.GetFiles()
            .Where(file => file.Extension == "") // ignore .manifest files
            .Select(file => new
            {
                Name = file.Name.Substring("soundpack".Length),
                BundlePath = file.FullName
            }); // ["soundpackbass", "soundpackdefault"] turns into [{Name: "bass", BundlePath: @"C:\Whatever"}, etc.]

        // Must do this sequentially or Unity will error out
        foreach (var b in bundles)
        {
            var bundle = AssetBundle.LoadFromFile(b.BundlePath);
            if (bundle == null)
            {
                Logger.LogWarning($"Failed to load asset bundle: {b.Name}");
                continue;
            }
            var gamePack = bundle
                .LoadAsset<GameObject>("soundpack" + b.Name)
                .GetComponent<AudioClipsTromb>();

            var soundpackCopy = new Soundpack()
            {
                Name = b.Name,
                Namespace = "vanilla",
                VolumeModifier = VANILLA_VOLUME_MODIFIERS.GetValueOrDefault(b.Name, 1.0f),
                Directory = info
            };

            // Copy vanilla audio clips to new ones
            var buf = new float[gamePack.tclips.Max(clip => clip.samples * clip.channels)];
            for (int i = 0; i < gamePack.tclips.Length; i++)
            {
                var origClip = gamePack.tclips[i];
                soundpackCopy.Notes[i] = AudioUtil.CloneAudioClip(origClip, buf);
            }
            SoundpackManager.AddPack(soundpackCopy);

            bundle.Unload(true);
        }

        var defPack = SoundpackManager.FindPack("vanilla", "default");
        if (defPack != null)
            SoundpackManager.CurrentPack = defPack;
        else
            Logger.LogWarning("Default soundpack not found, very bad things will happen");
    }

    void LoadCustomSoundpacks()
    {
        Logger.LogInfo("Loading custom soundpacks...");
        var dirs = new DirectoryInfo(Path.Combine(Paths.BepInExRootPath, "CustomSoundpacks"))
            .EnumerateDirectories();

        foreach (var dir in dirs)
            SoundpackManager.LoadPack(dir);
    }

}


[HarmonyPatch(typeof(GameController))]
internal class GameControllerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.Update))]
    static void PostUpdate(GameController __instance)
    {
        var self = __instance;
        if (Input.GetKeyDown(Plugin.CycleForwards.Value)) {
            if (++Plugin.Idx >= SoundpackManager.GetAllPacks().Count())
                Plugin.Idx = 0;
            var pack = SoundpackManager.GetAllPacks().ElementAt(Plugin.Idx);
            self.ChangePack(pack);
            Plugin.Logger.LogInfo(Plugin.Idx);
        }
        if (Input.GetKeyDown(Plugin.CycleBackwards.Value))
        {
            if (--Plugin.Idx < 0)
                Plugin.Idx = SoundpackManager.GetAllPacks().Count() - 1;
            var pack = SoundpackManager.GetAllPacks().ElementAt(Plugin.Idx);
            self.ChangePack(pack);
            Plugin.Logger.LogInfo(Plugin.Idx);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameController.Start))]
    static void PreStart(GameController __instance)
    {
        var self = __instance;
        int soundsetIdx = self.soundset;
        var SOUNDSETS = new string[] { "default", "bass", "muted", "eightbit", "club", "fart" };
        SoundpackManager.CurrentPack = SoundpackManager.FindPack("vanilla", SOUNDSETS[soundsetIdx]);
    }
}