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
        SoundpackManager.ChangeSoundpack(self, soundpack);
    }
}
