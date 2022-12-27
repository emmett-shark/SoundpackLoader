using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace SoundpackLoader;


public record MetaData
{
    public const string PLUGIN_NAME = "SoundpackLoader";
    public const string PLUGIN_GUID = "org.crispykevin.soundpackloader";
    public const string PLUGIN_VERSION = "1.0.0";
}

[BepInPlugin(MetaData.PLUGIN_GUID, MetaData.PLUGIN_NAME, MetaData.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger = null!;
    public static List<Soundpack> Soundpacks = new();
    public static SoundpackLoader Loader = null!;

    public Plugin()
    {
        Logger = base.Logger;
    }


    private void Awake()
    {
        new Harmony(MetaData.PLUGIN_GUID).PatchAll();
        
        Logger.LogInfo($"Plugin {MetaData.PLUGIN_GUID} v{MetaData.PLUGIN_VERSION} is loaded!");

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
            // DebugUtil.Dump(soundpack, LogLevel.Info, $"soundpack '{b.Name}'");
            // DebugUtil.Dump(soundpack.GetComponent<AudioClipsTromb>());

            var soundpackCopy = new Soundpack()
            {
                Name = b.Name,
                Namespace = "vanilla",
                VolumeModifier = VANILLA_VOLUME_MODIFIERS.GetValueOrDefault(b.Name, 1.0f),
                Directory = info
            };

            // Copy vanilla audio clips to new ones
            for (int i = 0; i < gamePack.tclips.Length; i++)
            {
                var origClip = gamePack.tclips[i];
                var newClip = AudioClip.Create(origClip.name, origClip.samples, origClip.channels, origClip.frequency, false);
                var buf = new float[origClip.samples * origClip.channels];
                origClip.GetData(buf, 0);
                newClip.SetData(buf, 0);
                soundpackCopy.Notes[i] = newClip;
            }
            Plugin.Soundpacks.Add(soundpackCopy);

            bundle.Unload(true);
        }

    }

    void LoadCustomSoundpacks()
    {
        Plugin.Logger.LogInfo("Loading custom soundpacks...");
        var dirs = new DirectoryInfo(Path.Combine(Paths.BepInExRootPath, "CustomSoundpacks"))
            .EnumerateDirectories();
        foreach (var dir in dirs)
        {
            // Load soundpack, then as a callback add it to Soundpacks when done loading
            Loader.LoadSoundpack(dir, Soundpacks.Add);
        }
    }

}


[HarmonyPatch(typeof(GameController))]
class GameControllerPatch
{

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.Update))]
    static void PostUpdate(GameController __instance)
    {
        var self = __instance;
        if (Input.GetKeyDown(KeyCode.F5)) {
            self.ChangeSoundpack(Plugin.Soundpacks.GetRandom());
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            self.ChangeSoundpack(Plugin.Soundpacks.Where(x => !x.IsVanilla).GetRandom());
        }
    }


}