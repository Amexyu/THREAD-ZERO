using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThreadZero.Editor
{
    public static class BuildProject
    {
        [MenuItem("Thread Zero/Prepare Main Scene")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.tag="MainCamera";
            camera.backgroundColor=new Color(.945f,.945f,.92f); camera.orthographic=true;
            camera.gameObject.AddComponent<AudioListener>();
            new GameObject("Thread Zero").AddComponent<GameApp>();
            const string path="Assets/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene,path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            PlayerSettings.companyName="Ame"; PlayerSettings.productName="Thread Zero";
            PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true; PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();
            Debug.Log("THREAD_ZERO_PREPARE_OK");
        }
        [MenuItem("Thread Zero/Build Windows")]
        public static void Windows()
        {
            Prepare();
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Builds/ThreadZero"));
            Directory.CreateDirectory(directory);
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Scenes/Main.unity"}, locationPathName=Path.Combine(directory,"ThreadZero.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None });
            Debug.Log("THREAD_ZERO_BUILD_RESULT="+report.summary.result);
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed");
        }
        public static void Smoke()
        {
            var model=new GameModel(null,704);
            var combat=new CombatSession(model);
            for(int i=0;i<600;i++) combat.Tick(.1f);
            if(model.state.kills<1 || model.state.items.Count<4) throw new Exception("Battle smoke failed");
            string folder=Path.GetFullPath("../Artifacts/SmokeSave");
            var saves=CreateSave(folder);
            if(!saves.Save(model.state)) throw new Exception("Save failed");
            string uid=model.state.items[0].uid;
            var loaded=saves.Load();
            if(loaded==null || loaded.items[0].uid!=uid) throw new Exception("Save roundtrip failed");
            // Corrupt only the isolated test primary and verify fallback.
            saves.Save(model.state);
            File.WriteAllText(saves.path,"{broken}");
            var recovered=CreateSave(folder).Load();
            if(recovered==null || recovered.items[0].uid!=uid) throw new Exception("Backup recovery failed");
            Debug.Log("THREAD_ZERO_SMOKE_OK kills="+model.state.kills+" items="+model.state.items.Count);
        }
        private static SaveService CreateSave(string folder)
        { return new SaveService(folder, JsonUtility.FromJson<GameState>, data => JsonUtility.ToJson(data, true), message => Debug.LogWarning(message)); }
    }
}
