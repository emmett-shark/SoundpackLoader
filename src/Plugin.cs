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

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
internal class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger = null!;
    public static SoundpackLoader Loader = null!;
    public static ConfigEntry<KeyCode> CycleForwards = null!;
    public static ConfigEntry<KeyCode> CycleBackwards = null!;
    public static ConfigEntry<int> SelectedIdx = null!;

    public Plugin()
    {
        Logger = base.Logger;
    }

    private void Awake()
    {
        CycleForwards = Config.Bind("General", "CycleForwards", KeyCode.PageDown);
        CycleBackwards = Config.Bind("General", "CycleBackwards", KeyCode.PageUp);
        SelectedIdx = Config.Bind("General", "Soundpack Index", 0);
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} is loaded!");
        SoundpackManager.SoundpackChanged += (_, e) => Logger.LogInfo($"{SelectedIdx.Value}. {e.NewPack}");
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
                SelectedIdx.Value = idx;
        };
    }

    void LoadVanillaSoundpacks()
    {
        Logger.LogInfo("Loading vanilla soundpacks...");
        var info = new DirectoryInfo(Path.Combine(Application.streamingAssetsPath, "soundpacks"));
        // ["soundpackbass", "soundpackdefault"] turns into [{Name: "bass", BundlePath: @"C:\Whatever"}, etc.]
        var bundles = info.GetFiles()
            .Where(file => file.Extension == "") // ignore .manifest files
            .Select(file => new
            {
                Name = file.Name.Substring("soundpack".Length),
                BundlePath = file.FullName
            })
            .OrderBy(i => SoundpackManager.VANILLA_SOUNDPACK_INFO.ContainsKey(i.Name)
                ? SoundpackManager.VANILLA_SOUNDPACK_INFO[i.Name].order : int.MaxValue);

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
                VolumeModifier = SoundpackManager.VANILLA_SOUNDPACK_INFO.ContainsKey(b.Name)
                    ? SoundpackManager.VANILLA_SOUNDPACK_INFO[b.Name].volume : 1.0f,
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
        __instance.fixAudioMixerStuff();
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

[HarmonyPatch(typeof(CharSelectController_new))]
internal class CharSelectControllerPatch
{
    static GameObject soundpackTextHolder = null!;
    static Text soundpackText = null!;
    static int soundpackIndex = 0;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CharSelectController_new.Start))]
    static void BeforeStart()
    {
        soundpackIndex = Plugin.SelectedIdx.Value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController_new.Start))]
    static void AfterStart(CharSelectController_new __instance)
    {
        Plugin.Logger.LogInfo("AfterStart");
        __instance.chooseSoundPack(soundpackIndex);

        GameObject sfxNameTextHolder = __instance.all_sfx_buttons[0].img_text.gameObject; // Finds an existing text object that can be copied for the plugin UI.
        soundpackTextHolder = UnityEngine.Object.Instantiate(sfxNameTextHolder, Vector3.zero, Quaternion.identity, GameObject.Find("Canvas").transform);
        soundpackTextHolder.SetActive(true);

        soundpackText = soundpackTextHolder.GetComponent<Text>();

        string name = SoundpackManager.CurrentPack.Name;
        soundpackText.text = name;
        soundpackText.transform.GetChild(0).GetComponent<Text>().text = name; // set shadow text
        soundpackText.rectTransform.anchorMin = new Vector2(0.7f, 1);
        soundpackText.rectTransform.anchorMax = new Vector2(1, 1);
        soundpackText.rectTransform.offsetMin = new Vector2(0, -75);
        soundpackText.rectTransform.offsetMax = new Vector2(0, -75);
        soundpackText.rectTransform.localScale = new Vector3(0.6f, 0.6f, 1);
        soundpackText.alignment = TextAnchor.LowerCenter;

        SoundpackManager.SoundpackChanged += OnSoundpackChanged;
    }

    static void OnSoundpackChanged(object? sender, SoundpackChangedEventArgs e)
    {
        soundpackText.text = e.NewPack.Name;
        soundpackText.transform.GetChild(0).GetComponent<Text>().text = e.NewPack.Name; // set shadow text
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController_new.Update))]
    static void Update(CharSelectController_new __instance)
    {
        if (Input.GetKeyDown(Plugin.CycleForwards.Value))
        {
            if (++Plugin.SelectedIdx.Value >= SoundpackManager.soundpacks.Count)
                Plugin.SelectedIdx.Value = 0;
            __instance.chooseSoundPack(Plugin.SelectedIdx.Value);
        }
        if (Input.GetKeyDown(Plugin.CycleBackwards.Value))
        {
            if (--Plugin.SelectedIdx.Value < 0)
                Plugin.SelectedIdx.Value = SoundpackManager.soundpacks.Count - 1;
            __instance.chooseSoundPack(Plugin.SelectedIdx.Value);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(CharSelectController_new.loadNextScene))]
    static void OnLeaveSelectScreen(CharSelectController_new __instance)
    {
        Plugin.Logger.LogInfo("Cleaning up char select screen");
        soundpackTextHolder.SetActive(false);
        GameObject.Destroy(soundpackTextHolder);
        SoundpackManager.SoundpackChanged -= OnSoundpackChanged;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CharSelectController_new.chooseSoundPack))]
    static bool BeforeChooseSoundPack(CharSelectController_new __instance, int sfx_choice)
    {
        SoundpackManager.CurrentPack = SoundpackManager.soundpacks[sfx_choice];
        for (int i = 0; i < __instance.all_sfx_buttons.Length; i++)
        {
            if (i != sfx_choice)
            {
                __instance.all_sfx_buttons[i].deselectBtn();
            }
        }
        return sfx_choice < __instance.all_sfx_buttons.Length;
    }
}
