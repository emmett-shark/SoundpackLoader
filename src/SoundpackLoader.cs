using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using UnityEngine;
using System.IO;

namespace SoundpackLoader;

/// <summary>
/// Asynchronously loads custom soundpacks using Unity coroutines
/// </summary>
public class SoundpackLoader : MonoBehaviour
{
    /// <summary>
    /// Loads the metadata for a soundpack from the given directory.
    /// In the background, the note audio files will be loaded.
    /// </summary>
    /// <param name="dir">Directory to load soundpack from.</param>
    public IEnumerator LoadSoundpack(DirectoryInfo dir)
    {
        var jsonFile = dir.GetFiles("*.json").FirstOrDefault();
        if (jsonFile == null)
        {
            Plugin.Logger.LogWarning($"No JSON file in soundpack directory: {dir.FullName}");
            yield break;
        }

        // Load metadata from .json file
        var metadata = JsonUtil.ReadFile<SoundpackJsonMetadata>(jsonFile);
        if (metadata == null)
        {
            Plugin.Logger.LogWarning($"Failed to read JSON file: {jsonFile.FullName}");
            yield break;
        }

        if (metadata.SoundpackFormatRevision == -1)
        {
            Plugin.Logger.LogWarning($"Missing SoundpackFormatRevision number in soundpack {metadata.Namespace}:{metadata.Name}! (Located in {dir.Name})");
        }

        var soundpack = new Soundpack()
        {
            Name = metadata.Name,
            Namespace = metadata.Namespace,
            VolumeModifier = metadata.VolumeModifier,
            Directory = dir,
        };

        // Load notes from .ogg/.wav files
        StartCoroutine(LoadNoteAudioFilesCoroutine(soundpack));
        SoundpackManager.AddPack(soundpack);
        yield return null;
    }

    private IEnumerator GetAudioClipCoroutine(FileInfo fileInfo, Action<AudioClip> onSuccess, Action<string> onError)
    {
        Dictionary<string, AudioType> EXTENSION_TO_AUD_TYPE = new Dictionary<string, AudioType>()
        {
            ["wav"] = AudioType.WAV,
            ["mp3"] = AudioType.MPEG,
            ["ogg"] = AudioType.OGGVORBIS
        };

        var audType = EXTENSION_TO_AUD_TYPE.GetValueOrDefault(fileInfo.Extension.ToLower(), AudioType.UNKNOWN);

        string uri = @"file://" + fileInfo.FullName;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, audType))
        {
            ((DownloadHandlerAudioClip)www.downloadHandler).streamAudio = false;
            yield return www.SendWebRequest();

            if (www.error != null)
            {
                onError(www.error);
            }
            else
            {
                AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
                onSuccess(myClip);
            }

        }
    }

    private IEnumerator LoadNoteAudioFilesCoroutine(Soundpack soundpack)
    {
        var NOTE_NAMES = new string[] { "C1", "D1", "E1", "F1", "G1", "A1", "B1", "C2", "D2", "E2", "F2", "G2", "A2", "B2", "C3" };
        int numLoadedClips = 0;
        bool isWaiting = false;

        Plugin.Logger.LogInfo(soundpack.QualifiedName);

        // Load C1 into index 0, D1 into index 1, etc.
        for (int i = 0; i < NOTE_NAMES.Length; ++i)
        {
            if (isWaiting)
                yield return null;
            string noteName = NOTE_NAMES[i];
            var noteFile = soundpack.Directory.GetFiles($"*{noteName}.*").FirstOrDefault(); // matches "aaaaC1.wav", "D1.mp3", etc
            if (noteFile == null)
            {
                string err = $"Audio file not found for note {noteName} in soundpack {soundpack.QualifiedName}";
                Plugin.Logger.LogWarning(err);
                yield break;
            }

            int idx = i; // Capture i in lambda by copy instead of ref
            StartCoroutine(GetAudioClipCoroutine(
                noteFile,
                onSuccess: clip =>
                {
                    soundpack.Notes[idx] = clip;
                    if (NOTE_NAMES.Length == ++numLoadedClips)
                    {
                        // Done loading all notes, add soundpack
                        Plugin.Logger.LogInfo($"Successfully loaded soundpack: {soundpack.QualifiedName}");
                    }
                    isWaiting = false;
                },
                onError: err => 
                {
                    string errorMsg = $"Error loading note {noteName} in soundpack {soundpack.QualifiedName}: {err}";
                    Plugin.Logger.LogWarning(errorMsg);
                }
            ));
            isWaiting = true;

            yield return null;
        }

    }
}
