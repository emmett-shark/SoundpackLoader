﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundpackLoader;

public class SoundpackManager
{
    internal static List<Soundpack> soundpacks = new();

    // Extracted from GameController.loadSoundBundleResources
    internal static readonly Dictionary<string, SoundpackInfo> VANILLA_SOUNDPACK_INFO = new Dictionary<string, SoundpackInfo>
    {
        ["default"] = new SoundpackInfo(0, 1f),
        ["bass"] = new SoundpackInfo(1, 0.72f),
        ["muted"] = new SoundpackInfo(2, 0.34f),
        ["eightbit"] = new SoundpackInfo(3, 0.25f),
        ["club"] = new SoundpackInfo(4, 0.25f),
        ["fart"] = new SoundpackInfo(5, 0.75f)
    };

    internal static Soundpack _currentPack = new(); // initialized to vanilla:default once it's loaded
    public static Soundpack CurrentPack
    {
        get => _currentPack;
        set
        {
            var prevPack = _currentPack;
            _currentPack = value;
            if (!ReferenceEquals(prevPack, _currentPack))
            {
                OnSoundpackChanged(_currentPack, prevPack);
            }
        }
    }

    public static event EventHandler<SoundpackChangedEventArgs>? SoundpackChanged;

    public static IEnumerable<Soundpack> GetAllPacks() => soundpacks;
    public static IEnumerable<Soundpack> GetVanillaPacks() => GetAllPacks().Where(p => p.IsVanilla);
    public static IEnumerable<Soundpack> GetCustomPacks() => GetAllPacks().Where(p => !p.IsVanilla);
    public static Soundpack? FindPack(string nmspace, string name)
    {
        return soundpacks.FirstOrDefault(s => s.Namespace == nmspace && s.Name == name);
    }
    public static void AddPack(Soundpack pack) => soundpacks.Add(pack);
    public static bool RemovePack(Soundpack pack) => soundpacks.Remove(pack);

    public static Soundpack? RemovePack(string nmspace, string name)
    {
        int idx = soundpacks.FindIndex(p => p.Namespace == nmspace && p.Name == name);
        if (idx != -1)
        {
            var pack = soundpacks[idx];
            soundpacks.RemoveAt(idx);
            return pack;
        }
        return null;
    }

    /// <summary>
    /// Copies and returns a deep clone of the given Soundpack.
    /// </summary>
    /// <remarks>WARNING: The copy must be destroyed using <seealso cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> when finished, or huge memory leaks will occur.</remarks>
    /// <param name="pack">Pack to clone.</param>
    /// <returns>Deep clone of <paramref name="pack"/></returns>
    public static Soundpack ClonePack(Soundpack pack)
    {
        var clone = new Soundpack()
        {
            Name = pack.Name,
            Namespace = pack.Namespace,
            Directory = pack.Directory,
            VolumeModifier = pack.VolumeModifier,
        };
        var buf = new float[pack.Notes.Max(n => n.channels * n.samples)]; // reuse buffer for all clips, make it size of biggest clip
        for (int i = 0; i < pack.Notes.Length; ++i)
        {
            clone.Notes[i] = AudioUtil.CloneAudioClip(pack.Notes[i], buf);
        }
        return clone;
    }

    /// <summary>
    /// Changes the in-game <c>Soundpack</c> to the given one.
    /// </summary>
    /// <param name="gc">Reference to <c>GameController</c> (from Harmony patch)</param>
    /// <param name="soundpack">New <c>Soundpack</c> to use</param>
    public static void ChangePack(GameController gc, Soundpack soundpack)
    {
        CurrentPack = soundpack;
        if (soundpack.IsVanilla) return;
        gc.trombclips.tclips = soundpack.Notes;
        gc.trombvol_default = gc.trombvol_current = gc.currentnotesound.volume = soundpack.VolumeModifier;
    }

    internal static void OnSoundpackChanged(Soundpack newPack, Soundpack oldPack)
    {
        SoundpackChanged?.Invoke(null, new SoundpackChangedEventArgs(CurrentPack, oldPack));
    }
}
