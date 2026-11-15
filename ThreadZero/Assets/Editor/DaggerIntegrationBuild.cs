using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThreadZero.Editor
{
    public static class DaggerIntegrationBuild
    {
        public static void Windows()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string name in new[]{"Body","Weapon"})
            {
                var image=Resources.Load<Texture2D>("Art/V12/StarterDagger/"+name);
                if(image==null || image.width!=1536 || image.height!=768)throw new Exception("Dagger atlas import failed: "+name);
            }
            var m=new GameModel(null,1611);var dagger=m.Create("w1",Quality.Common,1);m.state.items.Add(dagger);m.Equip(dagger.uid);
            m.state.equipped[(int)Slot.Head]=null;
            if(!DaggerActorView.CanDraw(m))throw new Exception("Starter outfit renderer not selected");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Builds/ThreadZero"));Directory.CreateDirectory(folder);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=Path.Combine(folder,"ThreadZero.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Dagger integration build failed");
            Debug.Log("DAGGER_BUILD_OK "+folder);
        }
    }
}
