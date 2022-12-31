using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
    public static int SelectedIdx = 2; // As of right now soundpackloader stores the vanilla soundpacks from asset bundles in file name order, so default is idx 2

    public Plugin()
    {
        Logger = base.Logger;
    }

    private void Awake()
    {
        CycleForwards = Config.Bind("General", "CycleForwards", KeyCode.PageDown);
        CycleBackwards = Config.Bind("General", "CycleBackwards", KeyCode.PageUp);
        new Harmony(MetaData.PLUGIN_GUID).PatchAll();
        
        Logger.LogInfo($"Plugin {MetaData.PLUGIN_GUID} v{MetaData.PLUGIN_VERSION} is loaded!");
        SoundpackManager.SoundpackChanged += (_, e) => Logger.LogInfo($"{e.NewPack} (from {e.OldPack})");
    }

    void Start()
    {
        Loader = this.gameObject.AddComponent<SoundpackLoader>();

        LoadVanillaSoundpacks();
        LoadCustomSoundpacks();

        SoundpackManager.SoundpackChanged += (_, e) =>
        {
            int idx = SoundpackManager.soundpacks.IndexOf(e.NewPack);
            if (idx != -1)
                SelectedIdx = idx;
        };
    }

    void Update()
    {
        if (Input.GetKeyDown(CycleForwards.Value))
        {
            if (++SelectedIdx >= SoundpackManager.soundpacks.Count)
                SelectedIdx = 0;
            var pack = SoundpackManager.soundpacks[SelectedIdx];
            SoundpackManager.CurrentPack = pack;
            DebugUtil.Dump(SelectedIdx);
        }
        if (Input.GetKeyDown(CycleBackwards.Value))
        {
            if (--SelectedIdx < 0)
                SelectedIdx = SoundpackManager.soundpacks.Count - 1;
            var pack = SoundpackManager.soundpacks[SelectedIdx];
            SoundpackManager.CurrentPack = pack;
            DebugUtil.Dump(SelectedIdx);
        }
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
            SoundpackManager._currentPack = defPack;
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
    static EventHandler<SoundpackChangedEventArgs>? OnSoundpackChanged = null;
    static GameObject? TrombClipsHolder = null;

    static IEnumerator EmptyIEnumerator()
    {
        yield break;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameController.loadSoundBundleResources))]
    static bool PatchOutSoundBundleResources(GameController __instance, ref IEnumerator __result)
    {
        __result = EmptyIEnumerator();
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.Start))]
    static void AfterStart(GameController __instance)
    {
        OnSoundpackChanged = (_, e) => SoundpackManager.ChangePack(__instance, e.NewPack);
        SoundpackManager.SoundpackChanged += OnSoundpackChanged;

        TrombClipsHolder = new GameObject();
        __instance.trombclips = TrombClipsHolder.AddComponent<AudioClipsTromb>();

        SoundpackManager.ChangePack(__instance, SoundpackManager.CurrentPack);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameController.unloadBundles))]
    static void AfterUnloadBundles(GameController __instance)
    {
        Plugin.Logger.LogInfo("AfterUnloadBundles");
        SoundpackManager.SoundpackChanged -= OnSoundpackChanged;
        OnSoundpackChanged = null;
        GameObject.Destroy(TrombClipsHolder);
        TrombClipsHolder = null;
    }

}

[HarmonyPatch(typeof(CharSelectController))]
internal class CharSelectControllerPatch
{
    static GameObject soundpackTextHolder = null!;
    static Text soundpackText = null!;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController.Start))]
    static void AfterStart(CharSelectController __instance)
    {
        Plugin.Logger.LogInfo("AfterStart");

        GameObject characterNameTextHolder = __instance.allcharacterportraits[0].transform.GetChild(4).gameObject; // wtf??
        soundpackTextHolder = UnityEngine.Object.Instantiate(characterNameTextHolder, Vector3.zero, Quaternion.identity, GameObject.Find("Canvas").transform);
        soundpackTextHolder.SetActive(true);

        soundpackText = soundpackTextHolder.GetComponent<Text>();
        //if (GlobalVariables.chosen_character >= 4)
        //    DebugUtil.Dump(soundpackText);

        soundpackText.text = SoundpackManager.CurrentPack.Name;
        soundpackText.transform.GetChild(0).GetComponent<Text>().text = SoundpackManager.CurrentPack.Name; // set shadow text
        soundpackText.rectTransform.anchorMin = new Vector2(0, 0);
        soundpackText.rectTransform.anchorMax = new Vector2(1, 0);
        soundpackText.rectTransform.offsetMin = new Vector2(0, -20 + 10);
        soundpackText.rectTransform.offsetMax = new Vector2(0, 20 + 10);
        soundpackText.alignment = TextAnchor.LowerCenter;

        SoundpackManager.SoundpackChanged += OnSoundpackChanged;
    }

    static void OnSoundpackChanged(object? sender, SoundpackChangedEventArgs e)
    {
        soundpackText.text = e.NewPack.Name;
        soundpackText.transform.GetChild(0).GetComponent<Text>().text = e.NewPack.Name; // set shadow text
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController.navAway))]
    static void OnNavStart(CharSelectController __instance)
    {
        Plugin.Logger.LogInfo("Cleaning up char select screen");
        soundpackTextHolder.SetActive(false);
        GameObject.Destroy(soundpackTextHolder);
        SoundpackManager.SoundpackChanged -= OnSoundpackChanged;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController.selectSound))]
    static void AfterSelectSound(CharSelectController __instance, int soundindex, bool selected)
    {
        if (selected)
        {
            string name = SoundpackManager.VANILLA_SOUNDPACK_NAMES[soundindex];
            var selectedPack = SoundpackManager.soundpacks.FirstOrDefault(x => x.IsVanilla && x.Name == name);
            if (selectedPack != null)
                SoundpackManager.CurrentPack = selectedPack;
        }
    }
}