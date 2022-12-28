using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SoundpackLoader
{
    internal class AudioUtil
    {
        public static AudioClip CloneAudioClip(AudioClip clip)
        {
            var newClip = AudioClip.Create(clip.name, clip.samples, clip.channels, clip.frequency, false);
            var buf = new float[clip.samples * clip.channels];
            clip.GetData(buf, 0);
            newClip.SetData(buf, 0);
            return newClip;
        }
    }
}
