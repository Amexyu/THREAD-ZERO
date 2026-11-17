using UnityEditor;
using UnityEditor.SceneManagement;

namespace ThreadZero.Editor
{
    public static class DaggerReviewMenu
    {
        [MenuItem("Thread Zero/Preview Starter Dagger on Play")]
        public static void ToggleAutoReview()
        {
            bool enabled=EditorPrefs.GetBool(GameApp.DaggerAutoReviewKey,false);
            EditorPrefs.SetBool(GameApp.DaggerAutoReviewKey,!enabled);
        }
        [MenuItem("Thread Zero/Preview Starter Dagger on Play",true)]
        public static bool AutoReviewState()
        {
            Menu.SetChecked("Thread Zero/Preview Starter Dagger on Play",EditorPrefs.GetBool(GameApp.DaggerAutoReviewKey,false));
            return true;
        }
        [MenuItem("Thread Zero/Review Starter Dagger (Unsaved)")]
        public static void Review()
        {
            if(EditorApplication.isPlaying)
            {
                SessionState.SetBool("ThreadZero.DaggerReview",true);
                EditorApplication.isPlaying=false;
                EditorApplication.update+=RestartReviewWhenStopped;
                return;
            }
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool("ThreadZero.DaggerReview",true);
            EditorApplication.isPlaying=true;
        }
        private static void RestartReviewWhenStopped()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=RestartReviewWhenStopped;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.isPlaying=true;
        }
    }
}
