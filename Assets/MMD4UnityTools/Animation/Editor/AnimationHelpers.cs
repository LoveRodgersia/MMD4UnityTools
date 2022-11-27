using MMDExtensions.Tools;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MMDExtensions
{
    public class AnimationHelpers
    {
        #region VMD Methods

        /// <summary>
        /// Create camera animation assets
        /// </summary>
        [MenuItem("Assets/MMDExtensions/Animation/Create/Camera Animation From VMD")]
        public static void CreateCameraAnimation()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (Path.GetExtension(path).ToUpper().Contains("VMD"))
            {
                var stream = File.Open(path, FileMode.Open);

                var vmd = VMDParser.ParseVMD(stream);

                var orderedFrames = from frame in vmd.Cameras
                                    orderby frame.FrameIndex
                                    select frame;
                var animationClip = new AnimationClip()
                {
                    frameRate = 30,
                };

                var delta = 1 / animationClip.frameRate;
                var scale = 0.085f;//1.76f / 2f;

                var quaternions = from frame in orderedFrames
                                  select new
                                  {
                                      Time = frame.FrameIndex * delta,
                                      Quaternion = Quaternion.Euler(new Vector3(frame.XRotation * Mathf.Rad2Deg, frame.YRotation * Mathf.Rad2Deg, frame.ZRotation * Mathf.Rad2Deg)),
                                      OutTangent = Mathf.Lerp(-1, 1, frame.Curve.AY / 127),
                                  };

                var q = quaternions.First().Quaternion;

                var xPosition = from position in orderedFrames
                                select new Keyframe(position.FrameIndex * delta, position.XPosition * scale);
                var YPosition = from position in orderedFrames
                                select new Keyframe(position.FrameIndex * delta, position.YPosition * scale);
                var ZPosition = from position in orderedFrames
                                select new Keyframe(position.FrameIndex * delta, position.ZPosition * scale);
                var XRoation = from quaternion in quaternions
                               select new Keyframe(quaternion.Time, quaternion.Quaternion.x);
                var YRoation = from quaternion in quaternions
                               select new Keyframe(quaternion.Time, quaternion.Quaternion.y);
                var ZRoation = from quaternion in quaternions
                               select new Keyframe(quaternion.Time, quaternion.Quaternion.z);
                var WRoation = from quaternion in quaternions
                               select new Keyframe(quaternion.Time, quaternion.Quaternion.w);
                var fov = from frame in orderedFrames
                          select new Keyframe(frame.FrameIndex * delta, (float)frame.FOV);

                var xPostionCurve = new AnimationCurve(xPosition.ToArray());
                var yPostionCurve = new AnimationCurve(YPosition.ToArray());
                var zPostionCurve = new AnimationCurve(ZPosition.ToArray());
                var xRotationCurve = new AnimationCurve(XRoation.ToArray());
                var yRotationCurve = new AnimationCurve(YRoation.ToArray());
                var zRotationCurve = new AnimationCurve(ZRoation.ToArray());
                var wRotationCurve = new AnimationCurve(WRoation.ToArray());
                var fovCurve = new AnimationCurve(fov.ToArray());
                animationClip.SetCurve("", typeof(Transform), "localPosition.x", xPostionCurve);
                animationClip.SetCurve("", typeof(Transform), "localPosition.y", yPostionCurve);
                animationClip.SetCurve("", typeof(Transform), "localPosition.z", zPostionCurve);
                animationClip.SetCurve("", typeof(Transform), "localRotation.x", xRotationCurve);
                animationClip.SetCurve("", typeof(Transform), "localRotation.y", yRotationCurve);
                animationClip.SetCurve("", typeof(Transform), "localRotation.z", zRotationCurve);
                animationClip.SetCurve("", typeof(Transform), "localRotation.w", wRotationCurve);
                animationClip.SetCurve("", typeof(Camera), "field of view", fovCurve);

                AssetDatabase.CreateAsset(animationClip, path.Replace("vmd", "anim"));//"Assets/VMDCamera.anim");
            }
        }

        /// <summary>
        /// Create morph animation assets
        /// </summary>
        [MenuItem("Assets/MMDExtensions/Animation/Create/Create Morph Animation")]
        public static void CreateMorphAnimation()
        {
            System.GC.Collect();
            var path = AssetDatabase.GetAssetPath(Selection.GetFiltered<DefaultAsset>(SelectionMode.Assets).FirstOrDefault());

            if (Path.GetExtension(path).ToUpper().Contains("VMD"))
            {
                var saveFolder = Path.GetDirectoryName(path).Replace("\\", "/");
                var fileName = Path.GetFileNameWithoutExtension(path);

                var stream = File.Open(path, FileMode.Open);
                var vmd = VMDParser.ParseVMD(stream);
                var selected = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel).FirstOrDefault();
                var smrs = Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep);
                var animationClips = smrs.ToDictionary(x => x, _ => new AnimationClip() { frameRate = 30 });
                var delta = 1 / 30f;
                var keyframes = from keys in vmd.Morphs.ToLookup(k => k.MorphName, v => new Keyframe(v.FrameIndex * delta, v.Weight * 100))
                                select keys;

                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (var smr in smrs)
                    {
                        var animationClip = animationClips[smr];
                        foreach (var package in keyframes)
                        {
                            var name = package.Key;

                            var curve = new AnimationCurve(package.ToArray());

                            var mesh = smr.sharedMesh;
                            var bsCounts = mesh.blendShapeCount;
                            var blendShapeNames = Enumerable.Range(0, bsCounts).ToList().ConvertAll(index => mesh.GetBlendShapeName(index));
                            var registerName = blendShapeNames.Find(x => x.Split('.').Last() == name) ?? name;
                            animationClip.SetCurve(GetTransformPathRecursively(smr.transform, selected.transform), typeof(SkinnedMeshRenderer), $"blendShape.{registerName}", curve);
                        }
                        var pathToSave = $"{saveFolder}/{fileName} - {smr.name}.anim";
                        if (File.Exists(pathToSave))
                        {
                            AssetDatabase.DeleteAsset(pathToSave);
                        }
                        AssetDatabase.CreateAsset(animationClip, pathToSave);
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }
        }

        #endregion VMD Methods

        private static string GetTransformPathRecursively(Transform transform, Transform top = null)
        {
            var name = transform.name;
            if (transform.parent != null && transform.parent != top)
            {
                return $"{GetTransformPathRecursively(transform.parent)}/{name}";
            }

            return transform.name;
        }
    }
}