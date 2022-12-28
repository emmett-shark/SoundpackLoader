using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using System.IO;
using BepInEx.Logging;
using Newtonsoft.Json;
using System.Threading;

namespace SoundpackLoader;

public class SoundpackLoader : MonoBehaviour
{
    /**
     * Loads the metadata for a soundpack from the given directory.
     * In the background, the note audio files will be loaded, and upon completion,
     *    the onLoadCompleted callback will be called.
     * Returns: A partially-loaded soundpack (contains no note audio clips!)
     */
    public Soundpack? LoadSoundpack(DirectoryInfo dir, Action<Soundpack>? onLoadCompleted = null, Action<string>? onLoadFailed = null)
    {
        var jsonFile = dir.GetFiles("*.json").FirstOrDefault();
        if (jsonFile == null)
        {
            Plugin.Logger.LogWarning($"No JSON file in soundpack directory: {dir.FullName}");
            return null;
        }

        // Load metadata from .json file
        var metadata = JsonUtil.ReadFile<SoundpackJsonMetadata>(jsonFile);
        if (metadata == null)
            return null;

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
        StartCoroutine(LoadNoteAudioFilesCoroutine(soundpack, onLoadCompleted, onLoadFailed));
        return soundpack;
    }

    private IEnumerator GetAudioClipCoroutine(string path, Action<AudioClip> onSuccess, Action<string> onError)
    {
        return GetAudioClipCoroutine(new FileInfo(path), onSuccess, onError);
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

        string uri = @"file:\\" + fileInfo.FullName;
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

    private IEnumerator LoadNoteAudioFilesCoroutine(Soundpack soundpack, Action<Soundpack>? onCompleted, Action<string>? onFailed)
    {
        var NOTE_NAMES = new string[] { "C1", "D1", "E1", "F1", "G1", "A1", "B1", "C2", "D2", "E2", "F2", "G2", "A2", "B2", "C3" };
        int numLoadedClips = 0;
        bool isWaiting = false;
        string? errorMsg = null;

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
                if (onFailed != null)
                    onFailed(err);
                Plugin.Logger.LogWarning(err);
                yield break;
            }

            StartCoroutine(GetAudioClipCoroutine(
                noteFile,
                onSuccess: clip =>
                {
                    Plugin.Logger.LogInfo($"Putting note {noteName} into idx {i}");
                    soundpack.Notes[i] = clip;
                    if (NOTE_NAMES.Length == ++numLoadedClips)
                    {
                        // Done loading all notes, add soundpack
                        Plugin.Logger.LogInfo($"Successfully loaded soundpack: {soundpack.Namespace}:{soundpack.Name}");
                        if (onCompleted != null)
                            onCompleted(soundpack);
                    }
                    isWaiting = false;
                },
                onError: err => 
                {
                    string errorMsg = $"Error loading note {noteName} in soundpack {soundpack.QualifiedName}: {err}";
                    if (onFailed != null)
                        onFailed(errorMsg);
                    Plugin.Logger.LogWarning(errorMsg);
                }
            ));
            isWaiting = true;

            if (errorMsg != null)
                yield break;
            yield return null;
        }

    }
}
