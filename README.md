# SoundpackLoader

An early WIP mod for loading custom soundpacks into the game.

# For users
Download SoundpackLoader.dll from [here](https://github.com/emmett-shark/SoundpackLoader/releases/latest) and drop it in your plugins folder.
Upon launching the game, a CustomSoundpacks folder should appear in the BepInEx folder. Put your soundpacks there and relaunch.

The current interface is a bit rough -- on the "Choose Yer Tromboner" screen, the selected soundpack is written at the top of the screen. Cycle through soundpacks using PageUp and PageDown. You can also cycle through soundpacks while playing a song. The keys can be changed in the config text file in `TromboneChamp\BepInEx\config\org.crispykevin.soundpackloader.cfg`.

### Changelog
v0.2.2
- Fix so trombone sound can happen if you skip the character select screen

### Soundpack structure
Making a soundpack is simple. It's just fifteen audio files of the notes of the C major scale, from C1 to C3. Additionally, some information about the pack is given in a special text format (JSON).

![Screenshot 2023-01-06 141928](https://user-images.githubusercontent.com/77177424/211094213-84b04b01-8e01-4ba5-b91f-d371ab09d66b.png)

Currently, not much is in the .json file:


| Attribute     | Description   | Required?  |
| ------------- | ------------- | ---------- |
| SoundpackFormatRevision  | Indicates which version of the soundpack specification this soundpack was designed for. Right now, always set this to 1.  | true |
| Name | Name of the soundpack  | true |
| Namespace | Another name/identifier used in case there's two soundpacks with the same name. | true |
| VolumeModifier | Multiplier to adjust the trombone volume while using the soundpack (0.0-1.0) | false, default = 1.0 |

```
{
    "SoundpackFormatRevision": 1,
    "Name": "cello-lower",
    "Namespace": "crispykevin",
    "VolumeModifier": 1.0
}
```

Other notes:
- A soundpack must have fifteen audio files, each matching `"*{noteName}.*"` (file glob, not regex) (e.g. "my_cool_C2.wav" is acceptable)
- Currently, .ogg and .wav audio files are supported.

# For modders

### API Overview
SoundpackLoader provides an API to conveniently control soundpacks in-game. Almost everything you need will be in the SoundpackManager class.

- Get a Soundpack instance using `SoundpackManager.FindPack`, `GetAllPacks`, `GetVanillaPacks`, and `GetCustomPacks`.
- Inside a song, use `SoundpackManager.ChangePack(GameController, Soundpack)` to change the currently-selected soundpack and update the sounds used by the GameController instance.
- To change the currently-selected soundpack without updating the sounds used by a GameController instance, use the `SoundpackManager.CurrentPack` setter property.
- To add/remove/load songs dynamically (besides the ones in CustomSoundpacks folder), use `SoundpackManager.AddPack`, `RemovePack`, and `LoadPack`.
- To do something whenever the soundpack changes, subscribe to `SoundpackManager.SoundpackChanged`. The event args provide the old and new packs.

### Setup
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
  
### Code examples
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
