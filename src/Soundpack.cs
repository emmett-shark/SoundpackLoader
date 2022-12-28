using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SoundpackLoader;

public class Soundpack
{
    internal Soundpack() { }

    public string Name { get; init; } = "Custom Soundpack";
    public string Namespace { get; init; } = "unknown";
    public float VolumeModifier { get; init; } = 1.0f;

    public DirectoryInfo Directory { get; init; } = null!;
    public AudioClip[] Notes { get; init; } = new AudioClip[15];

    public bool IsVanilla => Namespace == "vanilla";
    public string QualifiedName => Namespace + ":" + Name;
}

internal class SoundpackJsonMetadata
{
    public string Name { get; set; } = "Custom Soundpack";
    public string Namespace { get; set; } = "unknown";
    public float VolumeModifier { get; set; } = 1.0f;
    public int SoundpackFormatRevision = -1;
}