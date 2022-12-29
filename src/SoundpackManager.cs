using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

public class SoundpackManager
{
    private static List<Soundpack> soundpacks = new();

    public static Soundpack CurrentPack { get; internal set; } = new();
    public static event EventHandler<SoundpackChangedEventArgs> SoundpackChanged;

    public static IEnumerable<Soundpack> GetAllPacks() => soundpacks;
    public static IEnumerable<Soundpack> GetVanillaPacks() => GetAllPacks().Where(p => p.IsVanilla);
    public static IEnumerable<Soundpack> GetCustomPacks() => GetAllPacks().Where(p => !p.IsVanilla);
    public static Soundpack? FindPack(string nmspace, string name)
    {
        return soundpacks.FirstOrDefault(s => s.Namespace == nmspace && s.Name == name);
    }
    public static void AddPack(Soundpack pack) => soundpacks.Add(pack);
    public static bool RemovePack(Soundpack pack) => soundpacks.Remove(pack);

    /// <summary>
    /// <para>Begins loading the soundpack in the given directory.</para>
    /// <para>On success, the pack is added to the custom pack registry. On failure, no action is taken.</para>
    /// <para>For more control, see <seealso cref="SoundpackLoader.LoadSoundpack"/></para>
    /// </summary>
    /// <param name="dir"></param>
    public static void LoadPack(DirectoryInfo dir)
    {
        Plugin.Loader.LoadSoundpack(dir, AddPack, err => Plugin.Logger.LogWarning($"Failed to load pack from {dir.Name}: {err}"));
    }

    /// <summary>
    /// See <see cref="LoadPack(DirectoryInfo)"/>.
    /// </summary>
    public static void LoadPack(string directoryPath)
    {
        LoadPack(new DirectoryInfo(directoryPath));
    }
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
        gc.trombclips.tclips = soundpack.Notes;
        gc.trombvol_default = gc.trombvol_current = gc.currentnotesound.volume = soundpack.VolumeModifier;
        var oldPack = CurrentPack;
        CurrentPack = soundpack;
        SoundpackChanged?.Invoke(null, new SoundpackChangedEventArgs(CurrentPack, oldPack));
    }
}
