# SoundpackLoader

An early WIP mod for loading custom soundpacks into the game.

# For users
Install the mod by going to the releases tab on the right side. Download SoundpackLoader.dll and drop it in your plugins folder.
Upon launching the game, a CustomSoundpacks folder should appear in the BepInEx folder. Put your soundpacks here and relaunch.

# For modders

## API Overview
SoundpackLoader provides an API to conveniently control soundpacks in-game. Almost everything you need will be in the SoundpackManager class.

- Get a Soundpack instance using `SoundpackManager.FindPack`, `GetAllPacks`, `GetVanillaPacks`, and `GetCustomPacks`.
- Inside a song, use `SoundpackManager.ChangePack(GameController, Soundpack)` to change the currently-selected soundpack and update the sounds used by the GameController instance.
- To change the currently-selected soundpack without updating the sounds used by a GameController instance, use the `SoundpackManager.CurrentPack` setter property.
- To add/remove/load songs dynamically (besides the ones in CustomSoundpacks folder), use `SoundpackManager.AddPack`, `RemovePack`, and `LoadPack`.
- To do something whenever the soundpack changes, subscribe to `SoundpackManager.SoundpackChanged`. The event args provide the old and new packs.

## Setup
Add a reference to SoundpackLoader's DLL in your project's DLL. To do this, add another `<Reference>` in your .csproj file:
```
  <ItemGroup>
    <PackageReference Include="BepInEx.Analyzers" Version="1.*" PrivateAssets="all" />
    <PackageReference Include="BepInEx.Core" Version="5.*" />
    <PackageReference Include="BepInEx.PluginInfoProps" Version="1.*" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.2" />
    <PackageReference Include="TromboneChamp.GameLibs" Version="1.8.9.3" />

    <Reference Include="SoundpackLoader">
      <HintPath>$(GameFolder)\BepInEx\plugins\SoundpackLoader.dll</HintPath>
    </Reference>
  </ItemGroup>
  ```
  
## Code examples
Change the soundpack before every note (see https://github.com/crispyross/SoundpackChaos):

```
[HarmonyPatch(typeof(GameController))]
class GameControllerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameController.playNote))]
    static void BeforeNote(GameController __instance)
    {
        if (Plugin.Enabled.Value)
        {
            var packs = Plugin.IncludeVanilla.Value ? SoundpackManager.GetAllPacks() : SoundpackManager.GetCustomPacks();
            SoundpackManager.ChangePack(__instance, packs.GetRandom());
        }
    }
}
```

Shuffle the notes of each soundpack (see NoteShuffle (TODO upload to github)):

```
public class Plugin : BaseUnityPlugin {
    ...
    private void Awake() {
      ...
      SoundpackManager.SoundpackChanged += OnSoundpackChanged;
    }
    
    private void OnSoundpackChanged(object sender, SoundpackChangedEventArgs e)
    {
        if (backup != null)
        {
            for (int i = 0; i < backup.Notes.Length; i++)
            {
                Destroy(e.OldPack.Notes[i]);
                e.OldPack.Notes[i] = backup.Notes[i];
            }
        }
        backup = SoundpackManager.ClonePack(e.NewPack);
        if (Enabled.Value)
        {
            var notes = e.NewPack.Notes;
            for (int i = 0; i < 30; i++)
            {
                int a = rand.Next(notes.Length);
                int b = rand.Next(notes.Length);
                var temp = notes[a];
                notes[a] = notes[b];
                notes[b] = temp;
            }
        }

    }
