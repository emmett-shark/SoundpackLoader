using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

public static class VanillaExtensions
{
    public static void ChangeSoundpack(this GameController self, Soundpack soundpack)
    {
        self.trombclips.tclips = soundpack.Notes;
        self.trombvol_default = self.trombvol_current = self.currentnotesound.volume = soundpack.VolumeModifier;
        DebugUtil.Dump(self.currentnotesound.volume);
        // self.StartCoroutine(self.loadSoundBundleResources());
    }
}
