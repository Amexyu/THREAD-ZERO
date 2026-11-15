using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThreadZero.Editor
{
    public static class RigCaptureBatch
    {
        public static void Run()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/RigCapture"));
            Directory.CreateDirectory(output);
            for (int i = 0; i < 8; i++)
            {
                Texture2D frame = SkeletalActorView.CaptureWalkReview(i * Mathf.PI / 4f);
                if (frame == null) throw new Exception("Missing V08 body or h0 texture");
                File.WriteAllBytes(Path.Combine(output, "Walk_" + i.ToString("D2") + ".png"), frame.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(frame);
            }
            Debug.Log("THREAD_ZERO_RIG_CAPTURE_OK " + output);
        }
    }
}
