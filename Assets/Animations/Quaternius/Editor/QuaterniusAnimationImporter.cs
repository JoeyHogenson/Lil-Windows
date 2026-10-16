using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// First-import setup for the Quaternius Universal Animation Library FBXs in this folder:
/// Humanoid rig (so clips retarget onto any Humanoid character), clean clip names,
/// looping for "_Loop" clips, and an extra looping lying-down pose.
/// Only runs when the FBX has no .meta yet, so manual changes in the Inspector stick.
/// </summary>
public class QuaterniusAnimationImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Animations/Quaternius/";

    private bool IsFirstImportOfOurs
    {
        get { return assetPath.StartsWith(Folder) && assetImporter.importSettingsMissing; }
    }

    void OnPreprocessModel()
    {
        if(!IsFirstImportOfOurs) return;

        ModelImporter importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
    }

    void OnPreprocessAnimation()
    {
        if(!IsFirstImportOfOurs) return;

        ModelImporter importer = (ModelImporter)assetImporter;
        List<ModelImporterClipAnimation> clips = new List<ModelImporterClipAnimation>();

        foreach(ModelImporterClipAnimation clip in importer.defaultClipAnimations)
        {
            //Blender exports takes as "Armature|Name"
            clip.name = clip.takeName.Replace("Armature|", "");
            clip.loopTime = clip.name.EndsWith("_Loop");
            BakeRootMotionIntoPose(clip);
            clips.Add(clip);

            //the library only ships the get-up motion; its first frame is the lying pose,
            //so a 1-frame loop of it gives a usable "lying down" idle
            if(clip.name == "LayToIdle")
            {
                ModelImporterClipAnimation lying = CopyClip(clip);
                lying.name = "Lay_Idle_Loop";
                lying.lastFrame = clip.firstFrame + 1;
                lying.loopTime = true;
                clips.Add(lying);
            }
        }

        importer.clipAnimations = clips.ToArray();
    }

    //these are the in-place (non "_RM") files; baking keeps characters from drifting or
    //sinking/floating (e.g. while sitting) when the Animator has Apply Root Motion on
    private static void BakeRootMotionIntoPose(ModelImporterClipAnimation clip)
    {
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = true;
        clip.keepOriginalPositionXZ = true;
    }

    private static ModelImporterClipAnimation CopyClip(ModelImporterClipAnimation source)
    {
        ModelImporterClipAnimation copy = new ModelImporterClipAnimation
        {
            takeName = source.takeName,
            firstFrame = source.firstFrame,
            lastFrame = source.lastFrame,
        };
        BakeRootMotionIntoPose(copy);
        return copy;
    }
}
