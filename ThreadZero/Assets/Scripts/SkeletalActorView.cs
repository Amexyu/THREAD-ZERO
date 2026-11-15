using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // The approved V08 drawing is one continuous textured skin on a shared bone mesh.
    // Walking uses the standing cell; authored attack cells receive small skeletal recoil.
    public static class SkeletalActorView
    {
        private const int Cell = 512, Grid = 16;
        private const int Torso = 0, Head = 1, ArmRear = 2, ForearmRear = 3,
            ArmFront = 4, ForearmFront = 5, LegRear = 6, ShinRear = 7,
            LegFront = 8, ShinFront = 9, FootRear = 10, FootFront = 11;
        private static readonly Vector2[] Rest = {
            new Vector2(256,258), new Vector2(269,185),
            new Vector2(211,225), new Vector2(191,286),
            new Vector2(291,224), new Vector2(313,287),
            new Vector2(232,338), new Vector2(222,400),
            new Vector2(281,338), new Vector2(289,400),
            new Vector2(220,448), new Vector2(300,448)
        };
        private static readonly int[] Parent = { -1, Torso, Torso, ArmRear, Torso, ArmFront,
            Torso, LegRear, Torso, LegFront, ShinRear, ShinFront };
        private static readonly Dictionary<int, Texture2D> atlases = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<string, Texture2D> heads = new Dictionary<string, Texture2D>();
        private static GameObject root;
        private static Transform[] bones;
        private static SkinnedMeshRenderer skin;
        private static SkinnedMeshRenderer headSkin;
        private static Mesh[] poseMeshes;
        private static Mesh[] headPoseMeshes;
        private static Camera camera;
        private static RenderTexture target;

        public static bool Draw(float feetX, float feetY, float canvas, int coatStyle,
            string headId, string weaponId, string backId, int frame, bool moving, float walkPhase,
            float attackProgress, Color tint)
        {
            if (coatStyle != 0 || weaponId != "w0" || backId != "b0" ||
                (headId != "_default" && headId != "h0")) return false;
            if (!Application.isPlaying) return false;
            if (root == null) Create();
            Texture2D atlas;
            if (!atlases.TryGetValue(coatStyle, out atlas))
            {
                atlas = Resources.Load<Texture2D>("Art/V08/AtlasesFace/Player_c" + coatStyle);
                atlases[coatStyle] = atlas;
            }
            if (atlas == null || skin == null || camera == null) return false;
            skin.sharedMaterial.mainTexture = atlas;
            skin.sharedMesh = poseMeshes[moving ? 0 : Mathf.Clamp(frame, 0, 5)];
            Texture2D head = null;
            if (!string.IsNullOrEmpty(headId) && headId != "_default")
            {
                if (!heads.TryGetValue(headId, out head))
                {
                    head = Resources.Load<Texture2D>("Art/V08/HeadEquipment/" + headId);
                    heads[headId] = head;
                }
                if (head == null) return false;
            }
            headSkin.enabled = head != null;
            if (head != null)
            {
                headSkin.sharedMesh = headPoseMeshes[moving ? 0 : Mathf.Clamp(frame, 0, 5)];
                headSkin.sharedMaterial.mainTexture = head;
            }
            ApplyPose(moving, walkPhase, attackProgress);
            if (Event.current.type == EventType.Repaint) camera.Render();
            float scale = canvas / Cell;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTexture(Ui.R(feetX - 256 * scale, feetY - 470 * scale, canvas, canvas), target, ScaleMode.StretchToFill, true);
            GUI.color = old;
            return true;
        }

        public static Texture2D CaptureWalkReview(float phase)
        {
            if (root == null) Create();
            Texture2D body = Resources.Load<Texture2D>("Art/V08/AtlasesFace/Player_c0");
            Texture2D hood = Resources.Load<Texture2D>("Art/V08/HeadEquipment/h0");
            if (body == null || hood == null) return null;
            skin.sharedMaterial.mainTexture = body;
            skin.sharedMesh = poseMeshes[0];
            headSkin.sharedMaterial.mainTexture = hood;
            headSkin.sharedMesh = headPoseMeshes[0];
            headSkin.enabled = true;
            ApplyPose(true, phase, 0);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static void ApplyPose(bool moving, float walkPhase, float attackProgress)
        {
            float swing = moving ? Mathf.Sin(walkPhase) : 0;
            float lift = moving ? Mathf.Max(0, Mathf.Cos(walkPhase)) : 0;
            float recoil = !moving && attackProgress > .28f && attackProgress < .48f
                ? Mathf.Sin((attackProgress - .28f) / .20f * Mathf.PI) : 0;
            float torsoAngle = swing * 1.5f - recoil * 2.5f;
            Set(Torso, torsoAngle);
            Set(Head, -swing * 2f + recoil * 1.5f);
            Set(ArmRear, -swing * 12f - recoil * 2f); Set(ForearmRear, -lift * 5f);
            Set(ArmFront, swing * 12f - recoil * 2f); Set(ForearmFront, -(1 - lift) * 5f);
            Set(LegRear, swing * 16f); Set(ShinRear, -Mathf.Max(0, swing) * 14f);
            Set(LegFront, -swing * 16f); Set(ShinFront, -Mathf.Max(0, -swing) * 14f);
            Set(FootRear, -(torsoAngle + swing * 16f - Mathf.Max(0, swing) * 14f));
            Set(FootFront, -(torsoAngle - swing * 16f - Mathf.Max(0, -swing) * 14f));
        }

        private static void Set(int index, float z)
        { bones[index].localRotation = Quaternion.Euler(0, 0, z); }

        private static void Create()
        {
            root = new GameObject("V08 Continuous Skin Rig");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.layer = 30;
            // Keep the capture rig outside the gameplay camera's view.
            root.transform.position = new Vector3(10000, 0, 0);
            bones = new Transform[Rest.Length];
            for (int i = 0; i < Rest.Length; i++)
            {
                Transform parent = Parent[i] < 0 ? root.transform : bones[Parent[i]];
                var bone = new GameObject("Bone " + i).transform;
                bone.SetParent(parent, false);
                bone.localPosition = ToWorld(Rest[i]) - (Parent[i] < 0 ? Vector3.zero : ToWorld(Rest[Parent[i]]));
                bone.gameObject.layer = 30;
                bones[i] = bone;
            }
            var shape = new GameObject("Continuous V08 Skin");
            shape.layer = 30; shape.transform.SetParent(root.transform, false);
            skin = shape.AddComponent<SkinnedMeshRenderer>();
            poseMeshes = new Mesh[6];
            headPoseMeshes = new Mesh[6];
            for (int frame = 0; frame < poseMeshes.Length; frame++)
            {
                poseMeshes[frame] = MakeMesh(shape.transform, frame, false);
                headPoseMeshes[frame] = MakeMesh(shape.transform, frame, true);
            }
            skin.sharedMesh = poseMeshes[0];
            skin.bones = bones; skin.rootBone = bones[Torso];
            skin.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            skin.sortingOrder = 0;
            skin.localBounds = new Bounds(Vector3.zero, new Vector3(900, 900, 20));
            skin.updateWhenOffscreen = true;
            var headObject = new GameObject("V08 Head Equipment Skin");
            headObject.layer = 30; headObject.transform.SetParent(root.transform, false);
            headSkin = headObject.AddComponent<SkinnedMeshRenderer>();
            headSkin.bones = bones; headSkin.rootBone = bones[Torso];
            headSkin.sharedMesh = headPoseMeshes[0];
            headSkin.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            headSkin.sortingOrder = 1;
            headSkin.localBounds = skin.localBounds;
            headSkin.updateWhenOffscreen = true;
            headSkin.enabled = false;

            target = new RenderTexture(Cell, Cell, 16, RenderTextureFormat.ARGB32);
            target.name = "V08 Skeletal Walk"; target.Create();
            var cameraObject = new GameObject("V08 Rig Capture Camera");
            cameraObject.layer = 30; cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 214, -10);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 256;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 30;
            camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.targetTexture = target;
        }

        private static Vector3 ToWorld(Vector2 pixel)
        { return new Vector3(pixel.x - 256, 470 - pixel.y, 0); }

        private static Mesh MakeMesh(Transform renderer, int frame, bool headOnly)
        {
            int side = Cell / Grid + 1, count = side * side;
            var vertices = new Vector3[count]; var uv = new Vector2[count];
            var weights = new BoneWeight[count]; var triangles = new List<int>();
            for (int row = 0; row < side; row++)
            for (int col = 0; col < side; col++)
            {
                int i = row * side + col; float x = col * Grid, y = row * Grid;
                vertices[i] = ToWorld(new Vector2(x, y));
                uv[i] = new Vector2((frame % 3 * Cell + x) / 1536f,
                    1 - (frame / 3 * Cell + y) / 1024f);
                weights[i] = Weight(x, y);
                if (row == side - 1 || col == side - 1) continue;
                // Source atlases contain stray opaque pixels outside the artwork.
                if (headOnly ? (col < 10 || col >= 22 || row >= 14)
                    : (col < 9 || col >= 22 || row < 1 || row >= 31)) continue;
                triangles.Add(i); triangles.Add(i+side); triangles.Add(i+1);
                triangles.Add(i+1); triangles.Add(i+side); triangles.Add(i+side+1);
            }
            var mesh = new Mesh { name = "Continuous V08 mesh", vertices = vertices, uv = uv, triangles = triangles.ToArray(),
                boneWeights = weights };
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * renderer.localToWorldMatrix;
            mesh.bindposes = bind; mesh.RecalculateBounds();
            return mesh;
        }

        private static BoneWeight Weight(float x, float y)
        {
            int a = Torso, b = Torso; float blend = 0;
            if (y < 205 && x > 175 && x < 350)
            { a = Head; b = Torso; blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 170) / 45f)); }
            else if (y > 325 && x > 155 && x < 350)
            {
                bool rear = x < 255; a = rear ? LegRear : LegFront;
                b = y > 440 ? (rear ? FootRear : FootFront) : y > 395 ? (rear ? ShinRear : ShinFront) : Torso;
                if (y > 440) { a = rear ? ShinRear : ShinFront;
                    blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 425) / 35f)); }
                else blend = y > 395 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 380) / 40f))
                    : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 325) / 35f));
            }
            else if (y > 205 && y < 355 && (x < 227 || x > 295))
            {
                bool rear = x < 227; a = rear ? ArmRear : ArmFront;
                b = y > 275 ? (rear ? ForearmRear : ForearmFront) : Torso;
                blend = y > 275 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 260) / 45f))
                    : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((y - 205) / 40f));
            }
            return new BoneWeight { boneIndex0 = a, weight0 = 1 - blend, boneIndex1 = b, weight1 = blend };
        }
    }
}
