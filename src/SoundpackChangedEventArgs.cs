using System;

namespace SoundpackLoader;

public class SoundpackChangedEventArgs : EventArgs
{
    public Soundpack NewPack { get; }
    public Soundpack OldPack { get; }
    internal SoundpackChangedEventArgs(Soundpack newPack, Soundpack oldPack)
    {
        NewPack = newPack; 
        OldPack = oldPack;
    }
}
