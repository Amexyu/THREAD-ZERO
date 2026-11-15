using UnityEditor;
using UnityEditor.SceneManagement;

namespace ThreadZero.Editor
{
    public static class DaggerReviewMenu
    {
        [MenuItem("Thread Zero/Review Starter Dagger (Unsaved)")]
        public static void Review()
        {
            if(EditorApplication.isPlaying)return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            SessionState.SetBool("ThreadZero.DaggerReview",true);
            EditorApplication.isPlaying=true;
        }
    }
}
