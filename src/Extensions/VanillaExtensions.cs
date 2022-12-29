using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoundpackLoader;

public static class VanillaExtensions
{
    public static void ChangePack(this GameController self, Soundpack soundpack)
    {
        SoundpackManager.ChangePack(self, soundpack);
    }
}
