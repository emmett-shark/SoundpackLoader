using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SoundpackLoader
{
    internal class AudioUtil
    {
        public static AudioClip CloneAudioClip(AudioClip clip, float[]? buf = null)
        {
            var newClip = AudioClip.Create(clip.name, clip.samples, clip.channels, clip.frequency, false);
            if (buf == null || buf.Length < (clip.samples * clip.channels))
                buf = new float[clip.samples * clip.channels];

            // Since Unity has poor API design and doesn't let you specify the number of samples in SetData/GetData,
            // I call an internal private method. Otherwise, it spams console with warnings about buffer size

            var functionCallParams = new object[] { clip, buf, clip.samples, 0 };

            //private static extern bool GetData(AudioClip clip, [Out] float[] data, int numSamples, int samplesOffset);
            typeof(AudioClip).CallStaticMethod<bool>("GetData", functionCallParams);

            functionCallParams[0] = newClip;
            // private static extern bool SetData(AudioClip clip, float[] data, int numsamples, int samplesOffset);
            typeof(AudioClip).CallStaticMethod<bool>("SetData", functionCallParams);

            return newClip;
        }
    }
}
