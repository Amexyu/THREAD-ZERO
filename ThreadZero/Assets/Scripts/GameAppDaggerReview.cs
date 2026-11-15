using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private bool daggerReview;
        private static bool DaggerReviewRequested()
        {
#if UNITY_EDITOR
            if(UnityEditor.SessionState.GetBool("ThreadZero.DaggerReview",false))
            {UnityEditor.SessionState.SetBool("ThreadZero.DaggerReview",false);return true;}
#endif
            return Array.IndexOf(Environment.GetCommandLineArgs(),"-dagger-review")>=0;
        }
        private void PrepareDaggerReviewModel()
        {
            Item dagger=model.Create("w1",Quality.Common,1);model.state.items.Add(dagger);model.Unlock("w1");model.Equip(dagger.uid);
            model.state.equipped[(int)Slot.Head]=null;model.state.appearance=new string[4];
        }
        private void BeginDaggerReviewCapture()
        {
            combat.OnDaggerHit=(index,damage)=>Debug.Log("DAGGER_HIT "+index+" age="+combat.DaggerAge+" damage="+damage);
            Debug.Log("DAGGER_REVIEW: isolated unsaved model; w1 Common, c0, default head, b0");
            string[] args=Environment.GetCommandLineArgs();int pos=Array.IndexOf(args,"-dagger-capture");
            if(pos>=0 && pos+1<args.Length) StartCoroutine(CaptureDaggerReview(args[pos+1]));
        }
        private IEnumerator CaptureDaggerReview(string folder)
        {
            Directory.CreateDirectory(folder);int next=0;float age=0;
            float[] moments={.24f,.33f,.52f};
            while(age<4)
            {
                yield return new WaitForEndOfFrame();age+=Time.unscaledDeltaTime;
                if(next<moments.Length && combat.DaggerCombo && combat.DaggerActive && combat.DaggerAge>=moments[next])
                {
                    string path=Path.Combine(folder,"Hit"+(next+1)+".png");
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log("DAGGER_CAPTURE "+next+" age="+combat.DaggerAge+" frame="+DaggerSkill.Frame(combat.DaggerAge,true));next++;
                }
                if(next==3 && age>2)break;
            }
            Debug.Log("DAGGER_CAPTURE_DONE count="+next);Application.Quit(next==3?0:2);
        }
    }
}
