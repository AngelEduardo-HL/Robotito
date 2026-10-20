using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RobotitoAnimationTools
{
    private const string AnimationFolder = "Assets/Animaciones/RobotSaludo";
    private const string ClipPath = AnimationFolder + "/Saludo.anim";
    private const string ControllerPath = AnimationFolder + "/RobotSaludo.controller";
    private const float Duration = 5f;

    [MenuItem("Robotito/Ajustar baile y palma")]
    public static void UpdateDanceAndPalm()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Deten Play antes de editar las animaciones.");
        var scene = SceneManager.GetActiveScene();
        var dancer = scene.GetRootGameObjects().Single(r => r.name == "Robot pose Bailando");
        var greeting = scene.GetRootGameObjects().Single(r => r.name == "Robot Saludo");
        var wrist = greeting.transform.Find("Articulaciones/Cintura/Hombro_Saludo/Muneca_Saludo");
        Undo.RegisterFullObjectHierarchyUndo(dancer, "Animar baile");
        Undo.RegisterFullObjectHierarchyUndo(greeting, "Orientar palma");

        var palm = wrist.Find("Giro_Palma");
        if (palm == null)
        {
            var hand = wrist.Find("Mano_Saludo");
            palm = CreatePivot(wrist, "Giro_Palma", Vector3.zero);
            Undo.SetTransformParent(hand, palm, "Orientar palma");
        }
        palm.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        const string folder = "Assets/Animaciones/RobotBaile";
        const string path = folder + "/Baile.anim";
        const string controllerPath = folder + "/RobotBaile.controller";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null) clip.SampleAnimation(dancer, 0f);
        else
        {
            clip = new AnimationClip { name = "Baile", frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();

        var leftArm = FindBone(dancer, "LeftArm");
        var rightArm = FindBone(dancer, "RightArm");
        var leftForearm = FindBone(dancer, "LeftForeArm");
        var rightForearm = FindBone(dancer, "RightForeArm");
        Vector3 side = dancer.transform.right;
        Vector3 leftDirection = SkinCenter(dancer, FindBone(dancer, "LeftHand")) - leftArm.position;
        Vector3 rightDirection = SkinCenter(dancer, FindBone(dancer, "RightHand")) - rightArm.position;
        Vector3 leftAxis = side * Mathf.Sign(Vector3.Dot(Vector3.Cross(side, leftDirection), -dancer.transform.up));
        Vector3 rightAxis = side * Mathf.Sign(Vector3.Dot(Vector3.Cross(side, rightDirection), -dancer.transform.up));
        Vector3 hips = FindBone(dancer, "Hips").position;
        Vector3 leftOutward = Vector3.Project(leftArm.position - hips, side).normalized;
        Vector3 rightOutward = Vector3.Project(rightArm.position - hips, side).normalized;
        float leftYaw = Mathf.Sign(Vector3.Dot(Vector3.Cross(dancer.transform.up, leftDirection), leftOutward));
        float rightYaw = Mathf.Sign(Vector3.Dot(Vector3.Cross(dancer.transform.up, rightDirection), rightOutward));
        leftAxis = (leftAxis * Mathf.Cos(40f * Mathf.Deg2Rad) + dancer.transform.up * (leftYaw * Mathf.Sin(40f * Mathf.Deg2Rad))).normalized;
        rightAxis = (rightAxis * Mathf.Cos(40f * Mathf.Deg2Rad) + dancer.transform.up * (rightYaw * Mathf.Sin(40f * Mathf.Deg2Rad))).normalized;

        PoseCycle(clip, dancer.transform, leftArm, leftAxis, 170f, 1f, true);
        PoseCycle(clip, dancer.transform, rightArm, rightAxis, 170f, 1f, true);
        PoseCycle(clip, dancer.transform, leftForearm, leftAxis, 6f, 1f, true);
        PoseCycle(clip, dancer.transform, rightForearm, rightAxis, 6f, 1f, true);
        PoseCycle(clip, dancer.transform, FindBone(dancer, "Head"), dancer.transform.up, 16f);
        PoseCycle(clip, dancer.transform, FindBone(dancer, "Neck"), dancer.transform.right, 3f, 2f);
        NeckBounce(clip, dancer.transform, FindBone(dancer, "Neck"));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = false;
        settings.startTime = 0f;
        settings.stopTime = 2.4f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.EnsureQuaternionContinuity();

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
        controller.layers[0].stateMachine.defaultState.motion = clip;
        controller.layers[0].stateMachine.defaultState.writeDefaultValues = false;
        EditorUtility.SetDirty(controller.layers[0].stateMachine.defaultState);
        var animator = dancer.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(dancer);
        Undo.RecordObject(animator, "Asignar baile");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        foreach (var renderer in dancer.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Undo.RecordObject(renderer, "Actualizar baile visible");
            renderer.updateWhenOffscreen = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        clip.SampleAnimation(dancer, 0f);
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(controller);
        VerifyDanceAndPalm();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = dancer;
        Debug.Log("Baile y palma actualizados.");
    }

    private static Transform FindBone(GameObject robot, string name)
        => robot.GetComponentsInChildren<Transform>(true).Single(t => t.name == "mixamorig:" + name);

    private static Vector3 SkinCenter(GameObject robot, Transform bone)
    {
        Vector3 sum = Vector3.zero;
        float total = 0f;
        var mesh = new Mesh();
        try
        {
            foreach (var renderer in robot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index = System.Array.IndexOf(renderer.bones, bone);
                if (index < 0) continue;
                renderer.BakeMesh(mesh, true);
                var vertices = mesh.vertices;
                var weights = renderer.sharedMesh.boneWeights;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    float weight = (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f) +
                        (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
                    sum += renderer.transform.TransformPoint(vertices[i]) * weight;
                    total += weight;
                }
            }
        }
        finally { Object.DestroyImmediate(mesh); }
        if (total < 0.001f) throw new System.InvalidOperationException("Sin vertices para " + bone.name);
        return sum / total;
    }

    private static void PoseCycle(AnimationClip clip, Transform root, Transform bone, Vector3 worldAxis, float amplitude, float frequency = 1f, bool pendulum = false)
    {
        const float length = 2.4f;
        const int frames = 72;
        float step = length / frames;
        Vector3 axis = Quaternion.Inverse(bone.parent.rotation) * worldAxis;
        Quaternion rest = bone.localRotation;
        System.Func<float, Quaternion> sample = time =>
        {
            float phase = time * Mathf.PI * 2f * frequency / length;
            float motion = pendulum ? 0.5f - 0.5f * Mathf.Cos(phase) : Mathf.Sin(phase);
            return Quaternion.AngleAxis(amplitude * motion, axis) * rest;
        };
        for (int component = 0; component < 4; component++)
        {
            var keys = new Keyframe[frames + 1];
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = frame * step;
                float value = frame == frames ? rest[component] : sample(time)[component];
                float tangent = (sample(time + step)[component] - sample(time - step)[component]) / (2f * step);
                keys[frame] = new Keyframe(time, value, tangent, tangent);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(bone, root), typeof(Transform), "m_LocalRotation." + "xyzw"[component]),
                new AnimationCurve(keys));
        }
    }

    private static void NeckBounce(AnimationClip clip, Transform root, Transform neck)
    {
        const float length = 2.4f;
        const int frames = 72;
        float step = length / frames;
        Vector3 rest = neck.localPosition;
        Vector3 lift = neck.parent.InverseTransformVector(root.up * 0.05f);
        System.Func<float, Vector3> sample = time =>
            rest + lift * (0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 4f / length));
        for (int component = 0; component < 3; component++)
        {
            var keys = new Keyframe[frames + 1];
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = frame * step;
                float tangent = (sample(time + step)[component] - sample(time - step)[component]) / (2f * step);
                keys[frame] = new Keyframe(time, frame == frames ? rest[component] : sample(time)[component], tangent, tangent);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                AnimationUtility.CalculateTransformPath(neck, root), typeof(Transform), "m_LocalPosition." + "xyz"[component]),
                new AnimationCurve(keys));
        }
    }

    [MenuItem("Robotito/Verificar baile y palma")]
    public static void VerifyDanceAndPalm()
    {
        var scene = SceneManager.GetActiveScene();
        var dancer = scene.GetRootGameObjects().Single(r => r.name == "Robot pose Bailando");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animaciones/RobotBaile/Baile.anim");
        if (!AnimationUtility.GetAnimationClipSettings(clip).loopTime)
            throw new System.InvalidOperationException("El baile no esta en bucle.");
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (dancer.transform.Find(binding.path) == null)
                throw new System.InvalidOperationException("Falta una articulacion: " + binding.path);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (Mathf.Abs(curve.Evaluate(0f) - curve.Evaluate(2.4f)) > 0.0001f ||
                Mathf.Abs(curve.keys[0].outTangent - curve.keys[curve.length - 1].inTangent) > 0.001f)
                throw new System.InvalidOperationException("El baile tiene un salto al repetirse.");
        }
        var clone = Object.Instantiate(dancer);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.GetComponent<Animator>().enabled = false;
        try
        {
            var legs = new[] { "Hips", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
            var fixedPose = legs.Select(n => FindBone(clone, n).localRotation).ToArray();
            clip.SampleAnimation(clone, 0f);
            float neckRestHeight = FindBone(clone, "Neck").position.y;
            Vector3 left = SkinCenter(clone, FindBone(clone, "LeftHand"));
            Vector3 right = SkinCenter(clone, FindBone(clone, "RightHand"));
            Quaternion leftArm = FindBone(clone, "LeftArm").localRotation;
            Quaternion rightArm = FindBone(clone, "RightArm").localRotation;
            clip.SampleAnimation(clone, 1.2f);
            float leftTravel = Vector3.Dot(SkinCenter(clone, FindBone(clone, "LeftHand")) - left, dancer.transform.forward);
            float rightTravel = Vector3.Dot(SkinCenter(clone, FindBone(clone, "RightHand")) - right, dancer.transform.forward);
            if (Mathf.Abs(leftTravel) < 0.45f || Mathf.Abs(rightTravel) < 0.45f ||
                Mathf.Abs(leftTravel) > 1.7f || Mathf.Abs(rightTravel) > 1.7f || leftTravel * rightTravel >= 0f)
                throw new System.InvalidOperationException($"Revisar recorrido de los brazos: {leftTravel:F3}, {rightTravel:F3}");
            if (Quaternion.Angle(leftArm, FindBone(clone, "LeftArm").localRotation) < 160f ||
                Quaternion.Angle(rightArm, FindBone(clone, "RightArm").localRotation) < 160f)
                throw new System.InvalidOperationException("El arco de los hombros no es suficientemente amplio.");
            clip.SampleAnimation(clone, 0.6f);
            Quaternion head = FindBone(clone, "Head").localRotation;
            float neckLift = FindBone(clone, "Neck").position.y - neckRestHeight;
            if (neckLift < 0.04f || neckLift > 0.06f)
                throw new System.InvalidOperationException("Revisar el balanceo suave del cuello.");
            clip.SampleAnimation(clone, 1.8f);
            if (Quaternion.Angle(head, FindBone(clone, "Head").localRotation) < 30f)
                throw new System.InvalidOperationException("La cabeza no se mueve de lado a lado.");
            for (int i = 0; i < legs.Length; i++)
                if (Quaternion.Angle(fixedPose[i], FindBone(clone, legs[i]).localRotation) > 0.01f)
                    throw new System.InvalidOperationException("Cambio la pose de las piernas.");
            float lowestHand = float.PositiveInfinity;
            for (int frame = 0; frame <= 24; frame++)
            {
                clip.SampleAnimation(clone, frame * 0.1f);
                lowestHand = Mathf.Min(lowestHand, SkinCenter(clone, FindBone(clone, "LeftHand")).y,
                    SkinCenter(clone, FindBone(clone, "RightHand")).y);
            }
            if (lowestHand < 0.08f)
                throw new System.InvalidOperationException($"El arco de las manos toca el suelo: {lowestHand:F3} m");
            Debug.Log($"Baile verificado: manos {leftTravel:F2} / {rightTravel:F2} m; arco 170 grados; cuello {neckLift:F2} m; altura minima manos {lowestHand:F2} m; pose agachada conservada; ciclo continuo.");
        }
        finally { Object.DestroyImmediate(clone); }
        var greeting = scene.GetRootGameObjects().Single(r => r.name == "Robot Saludo");
        clone = Object.Instantiate(greeting);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.GetComponent<Animator>().enabled = false;
        try
        {
            var salute = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var palm = clone.transform.Find("Articulaciones/Cintura/Hombro_Saludo/Muneca_Saludo/Giro_Palma");
            var camera = scene.GetRootGameObjects().Select(r => r.GetComponent<Camera>()).First(c => c != null);
            foreach (float time in new[] { 1.2f, 1.45f, 1.75f, 2.35f, 2.95f, 3.25f })
            {
                salute.SampleAnimation(clone, time);
                if (Vector3.Dot(-palm.up, -camera.transform.forward) < 0.95f)
                    throw new System.InvalidOperationException("La palma no esta orientada hacia la camara.");
            }
        }
        finally { Object.DestroyImmediate(clone); }
        VerifyGreeting();
    }

    [MenuItem("Robotito/Crear saludo")]
    public static void CreateGreeting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Deten Play antes de preparar la animacion.");

        var scene = SceneManager.GetActiveScene();
        var robot = scene.GetRootGameObjects().SingleOrDefault(r => r.name == "Robot T pose");
        if (robot == null)
            throw new System.InvalidOperationException("Selecciona la escena que contiene Robot T pose.");
        var original = robot.transform.Find("ROBOT");
        if (original == null || robot.transform.Find("Articulaciones") != null || AssetDatabase.IsValidFolder(AnimationFolder))
            throw new System.InvalidOperationException("El saludo ya existe o falta la jerarquia original ROBOT.");

        var arms = original.Find("group2_pCube8").GetComponent<MeshFilter>();
        var legs = original.Find("group2_pCylinder6").GetComponent<MeshFilter>();
        // Leer y comprobar la geometria antes de cambiar la escena.
        var parts = FindConnectedParts(arms, robot.transform);
        if (parts.Count != 10 || original.GetComponentsInChildren<MeshRenderer>().Length != 8)
            throw new System.InvalidOperationException("La malla cambio. Revisa su estructura antes de separar las piezas.");
        foreach (var name in new[] { "pCube1", "pCube2", "pCylinder2", "pCylinder7", "pCylinder9", "pCylinder10" })
            if (original.Find(name)?.GetComponent<MeshFilter>() == null)
                throw new System.InvalidOperationException("Falta una pieza del modelo: " + name);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Preparar saludo del robot");
        Undo.RegisterFullObjectHierarchyUndo(robot, "Preparar saludo del robot");
        Directory.CreateDirectory(AnimationFolder + "/Mallas");
        AssetDatabase.Refresh();

        var controls = CreatePivot(robot.transform, "Articulaciones", Vector3.zero);
        var waist = CreatePivot(controls, "Cintura", new Vector3(0f, 0.049151f, 0f));
        var head = CreatePivot(waist, "Cabeza", new Vector3(0.000726f, 0.037f, 0f));
        var waveShoulder = CreatePivot(waist, "Hombro_Saludo", new Vector3(-0.016522f, 0.012816f, 0f));
        var otherShoulder = CreatePivot(waist, "Hombro_Apoyo", new Vector3(0.017456f, 0.012816f, 0f));
        var waveWrist = CreatePivot(waveShoulder, "Muneca_Saludo", new Vector3(-0.043f, 0.001172f, 0f));
        var otherWrist = CreatePivot(otherShoulder, "Muneca_Apoyo", new Vector3(0.042f, 0.001172f, 0f));

        CopyPart(legs, controls, "Piernas_y_pies");
        CopyPart(original.Find("pCylinder10").GetComponent<MeshFilter>(), waist, "Cuerpo_y_cuello");
        CopyPart(original.Find("pCube1").GetComponent<MeshFilter>(), head, "Cabeza_malla");
        CopyPart(original.Find("pCylinder2").GetComponent<MeshFilter>(), head, "Ojo_derecho");
        CopyPart(original.Find("pCylinder7").GetComponent<MeshFilter>(), head, "Ojo_izquierdo");
        CopyPart(original.Find("pCylinder9").GetComponent<MeshFilter>(), head, "Detalle_ojo");
        CopyPart(original.Find("pCube2").GetComponent<MeshFilter>(), waveShoulder, "Detalle_hombro");

        // Cada isla es una pieza rigida. Separarlas conserva todas sus caras y sus UV.
        foreach (bool negative in new[] { false, true })
        {
            foreach (bool hand in new[] { false, true })
            {
                var matching = parts.Where(p => (p.Bounds.center.x < 0f) == negative &&
                    (Mathf.Abs(p.Bounds.center.x) > 0.055f) == hand).ToArray();
                var parent = negative ? (hand ? waveWrist : waveShoulder) : (hand ? otherWrist : otherShoulder);
                string name = (hand ? "Mano" : "Brazo") + (negative ? "_Saludo" : "_Apoyo");
                var mesh = ExtractParts(arms, matching, name);
                AssetDatabase.CreateAsset(mesh, AnimationFolder + "/Mallas/" + name + ".asset");
                var copy = CopyPart(arms, parent, name);
                copy.sharedMesh = mesh;
            }
        }

        // El FBX queda dentro del objeto como referencia para recuperar la pose T.
        original.gameObject.SetActive(false);
        robot.name = "Robot Saludo";
        var feet = controls.Find("Piernas_y_pies").GetComponent<Renderer>();
        float groundY = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Plane")?.transform.position.y ?? 0f;
        robot.transform.position += Vector3.up * (groundY - feet.bounds.min.y);

        var clip = new AnimationClip { name = "Saludo", frameRate = 30f };
        Rotation(clip, robot.transform, waist,
            new[] { 0f, 0.5f, 1.2f, 2.3f, 3.3f, 4.2f, Duration },
            new[] { Vector3.zero, new Vector3(0, 0, -1), new Vector3(0, 0, 2), new Vector3(0, 0, 1), new Vector3(0, 0, 2), Vector3.zero, Vector3.zero });
        Rotation(clip, robot.transform, head,
            new[] { 0f, 0.5f, 1.2f, 1.65f, 2.1f, 2.8f, 3.4f, 4.2f, Duration },
            new[] { Vector3.zero, new Vector3(5, 0, 0), new Vector3(-3, -8, 6), new Vector3(5, -8, 6), new Vector3(0, -8, 6), new Vector3(2, -5, 4), new Vector3(0, -5, 4), Vector3.zero, Vector3.zero });
        RotationZ(clip, robot.transform, waveShoulder,
            new[] { 0f, 0.4f, 1.2f, 1.6f, 2.1f, 2.6f, 3.1f, 3.5f, 4.3f, Duration },
            new[] { 70f, 70f, -65f, -62f, -67f, -62f, -67f, -62f, 70f, 70f });
        RotationZ(clip, robot.transform, waveWrist,
            new[] { 0f, 1.15f, 1.45f, 1.75f, 2.05f, 2.35f, 2.65f, 2.95f, 3.25f, 3.55f, 4.3f, Duration },
            new[] { 0f, 0f, -22f, 22f, -22f, 22f, -22f, 22f, -22f, 0f, 0f, 0f });
        RotationZ(clip, robot.transform, otherShoulder,
            new[] { 0f, 0.5f, 1.2f, 3.4f, 4.3f, Duration },
            new[] { -70f, -72f, -65f, -65f, -70f, -70f });
        RotationZ(clip, robot.transform, otherWrist,
            new[] { 0f, 1.2f, 3.4f, 4.3f, Duration }, new[] { 0f, -5f, -5f, 0f, 0f });
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.startTime = 0f;
        settings.stopTime = Duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, ClipPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
        controller.layers[0].stateMachine.defaultState.writeDefaultValues = false;
        var animator = Undo.AddComponent<Animator>(robot);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        clip.SampleAnimation(robot, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = robot;
        Undo.CollapseUndoOperations(undoGroup);
        VerifyGreeting();
        Debug.Log("Saludo listo: selecciona Robot Saludo y pulsa Play. El clip editable esta en " + ClipPath);
    }

    [MenuItem("Robotito/Verificar saludo")]
    public static void VerifyGreeting()
    {
        var robot = SceneManager.GetActiveScene().GetRootGameObjects().Single(r => r.name == "Robot Saludo");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var bindings = AnimationUtility.GetCurveBindings(clip);
        if (!AnimationUtility.GetAnimationClipSettings(clip).loopTime || Mathf.Abs(clip.length - Duration) > 0.001f)
            throw new System.InvalidOperationException("El clip debe durar cinco segundos y repetirse.");
        if (robot.GetComponent<Animator>().runtimeAnimatorController != controller || controller.layers[0].stateMachine.defaultState.motion != clip)
            throw new System.InvalidOperationException("El Animator no reproduce Saludo como estado inicial.");
        foreach (var binding in bindings)
        {
            if (robot.transform.Find(binding.path) == null)
                throw new System.InvalidOperationException("Una curva apunta a una pieza inexistente: " + binding.path);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (Mathf.Abs(curve.Evaluate(0f) - curve.Evaluate(Duration)) > 0.001f)
                throw new System.InvalidOperationException("El inicio y el final del ciclo no coinciden.");
        }
        var clone = Object.Instantiate(robot);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.GetComponent<Animator>().enabled = false;
        try
        {
            var wrist = clone.transform.Find("Articulaciones/Cintura/Hombro_Saludo/Muneca_Saludo");
            var feet = clone.transform.Find("Articulaciones/Piernas_y_pies").GetComponent<Renderer>();
            clip.SampleAnimation(clone, 0f);
            Vector3 rest = wrist.position;
            float feetY = feet.bounds.min.y;
            clip.SampleAnimation(clone, 1.45f);
            if (wrist.position.y - rest.y < 1f || Mathf.Abs(feet.bounds.min.y - feetY) > 0.001f)
                throw new System.InvalidOperationException("Comprueba la elevacion de la mano y que los pies no se deslicen.");
            clip.SampleAnimation(clone, 1.75f);
            float swing = Mathf.DeltaAngle(-22f, wrist.localEulerAngles.z);
            if (Mathf.Abs(swing) < 40f)
                throw new System.InvalidOperationException("La muneca no esta oscilando.");
            Directory.CreateDirectory("Library/RobotitoAnimation");
            File.WriteAllText("Library/RobotitoAnimation/verificacion.txt",
                $"OK\nEscena: {robot.scene.path}\nClip: {ClipPath}\nDuracion: {clip.length:F3}s\nCurvas: {bindings.Length}\nLoop: true\nAnimator: conectado\nRutas: correctas\nInicio y final: iguales\nMano: se eleva\nMuneca: oscila\nPies: fijos\n");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static Transform CreatePivot(Transform parent, string name, Vector3 position)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, "Crear articulacion");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        return obj.transform;
    }

    private static MeshFilter CopyPart(MeshFilter source, Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        Undo.RegisterCreatedObjectUndo(obj, "Separar pieza del robot");
        obj.transform.SetParent(source.transform.parent, false);
        obj.transform.localPosition = source.transform.localPosition;
        obj.transform.localRotation = source.transform.localRotation;
        obj.transform.localScale = source.transform.localScale;
        obj.transform.SetParent(parent, true);
        var filter = obj.GetComponent<MeshFilter>();
        filter.sharedMesh = source.sharedMesh;
        var renderer = obj.GetComponent<MeshRenderer>();
        var original = source.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = original.sharedMaterials;
        renderer.shadowCastingMode = original.shadowCastingMode;
        renderer.receiveShadows = original.receiveShadows;
        return filter;
    }

    private static void RotationZ(AnimationClip clip, Transform root, Transform part, float[] times, float[] angles)
        => Rotation(clip, root, part, times, angles.Select(z => new Vector3(0, 0, z)).ToArray());

    private static void Rotation(AnimationClip clip, Transform root, Transform part, float[] times, Vector3[] angles)
    {
        string path = AnimationUtility.CalculateTransformPath(part, root);
        for (int axis = 0; axis < 3; axis++)
        {
            var curve = new AnimationCurve(times.Select((t, i) => new Keyframe(t, angles[i][axis], 0f, 0f)).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis]), curve);
        }
    }

    private sealed class MeshPart
    {
        public Bounds Bounds;
        public readonly HashSet<int> Vertices = new HashSet<int>();
    }

    private static List<MeshPart> FindConnectedParts(MeshFilter source, Transform root)
    {
        var vertices = source.sharedMesh.vertices;
        var triangles = source.sharedMesh.triangles;
        var parent = Enumerable.Range(0, vertices.Length).ToArray();
        System.Func<int, int> find = null;
        find = i => parent[i] == i ? i : parent[i] = find(parent[i]);
        var positions = new Dictionary<Vector3Int, int>();
        var matrix = root.worldToLocalMatrix * source.transform.localToWorldMatrix;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var key = Vector3Int.RoundToInt(vertices[i] * 10000000f);
            if (positions.TryGetValue(key, out int existing)) parent[find(i)] = find(existing);
            else positions.Add(key, i);
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            parent[find(triangles[i])] = find(triangles[i + 1]);
            parent[find(triangles[i])] = find(triangles[i + 2]);
        }
        return Enumerable.Range(0, vertices.Length).GroupBy(find).Select(group =>
        {
            var part = new MeshPart { Bounds = new Bounds(vertices[group.First()], Vector3.zero) };
            foreach (int i in group) { part.Vertices.Add(i); part.Bounds.Encapsulate(vertices[i]); }
            return part;
        }).ToList();
    }

    private static Mesh ExtractParts(MeshFilter source, MeshPart[] parts, string name)
    {
        var original = source.sharedMesh;
        var included = new HashSet<int>(parts.SelectMany(p => p.Vertices));
        int[] indices = included.OrderBy(i => i).ToArray();
        var remap = indices.Select((old, index) => new { old, index }).ToDictionary(p => p.old, p => p.index);
        var mesh = new Mesh { name = name, vertices = indices.Select(i => original.vertices[i]).ToArray() };
        if (original.normals.Length > 0) mesh.normals = indices.Select(i => original.normals[i]).ToArray();
        if (original.uv.Length > 0) mesh.uv = indices.Select(i => original.uv[i]).ToArray();
        if (original.tangents.Length > 0) mesh.tangents = indices.Select(i => original.tangents[i]).ToArray();
        mesh.subMeshCount = original.subMeshCount;
        for (int submesh = 0; submesh < original.subMeshCount; submesh++)
        {
            var triangles = original.GetTriangles(submesh);
            var output = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (!included.Contains(triangles[i])) continue;
                if (!included.Contains(triangles[i + 1]) || !included.Contains(triangles[i + 2]))
                    throw new System.InvalidOperationException("Una cara cruza dos piezas; no se puede separar sin cortarla.");
                output.Add(remap[triangles[i]]);
                output.Add(remap[triangles[i + 1]]);
                output.Add(remap[triangles[i + 2]]);
            }
            mesh.SetTriangles(output, submesh);
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    [MenuItem("Robotito/Inspeccionar escena")]
    public static void InspectScene()
    {
        var report = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        report.AppendLine("SCENE " + scene.path);
        foreach (var root in scene.GetRootGameObjects())
        {
            report.AppendLine("ROOT " + root.name);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(t, root.transform);
                report.AppendLine($"  {path} | localPos={t.localPosition.ToString("F6")} localEuler={t.localEulerAngles.ToString("F3")} localScale={t.localScale.ToString("F6")} worldPos={t.position.ToString("F4")}");
                foreach (var renderer in t.GetComponents<Renderer>())
                {
                    report.AppendLine($"    {renderer.GetType().Name} boundsCenter={renderer.bounds.center.ToString("F4")} boundsSize={renderer.bounds.size.ToString("F4")}");
                    if (renderer is SkinnedMeshRenderer skinned)
                        report.AppendLine("    BONES " + string.Join(", ", skinned.bones.Select(b => b == null ? "null" : b.name)));
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                        DescribeMesh(report, filter, root.transform);
                }
            }
        }
        foreach (var path in new[] { "Assets/Modelo3D/hgf.fbx", "Assets/Modelo3D/ROBOT.fbx" })
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                report.AppendLine($"IMPORTED CLIP {path}: {clip.name} {clip.length:F3}s {AnimationUtility.GetCurveBindings(clip).Length} curves");
        Directory.CreateDirectory("Library/RobotitoAnimation");
        File.WriteAllText("Library/RobotitoAnimation/estructura.txt", report.ToString());
        Debug.Log("Estructura guardada en Library/RobotitoAnimation/estructura.txt");
    }

    private static void DescribeMesh(StringBuilder report, MeshFilter filter, Transform root)
    {
        var mesh = filter.sharedMesh;
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var parent = Enumerable.Range(0, vertices.Length).ToArray();
        System.Func<int, int> find = null;
        find = i => parent[i] == i ? i : parent[i] = find(parent[i]);
        var positions = new Dictionary<string, int>();
        var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            var p = vertices[i];
            var key = $"{Mathf.RoundToInt(p.x * 10000000)}:{Mathf.RoundToInt(p.y * 10000000)}:{Mathf.RoundToInt(p.z * 10000000)}";
            if (positions.TryGetValue(key, out var existing)) parent[find(i)] = find(existing);
            else positions.Add(key, i);
        }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            parent[find(triangles[i])] = find(triangles[i + 1]);
            parent[find(triangles[i])] = find(triangles[i + 2]);
        }
        report.AppendLine($"    MESH {mesh.name} vertices={vertices.Length} submeshes={mesh.subMeshCount}");
        foreach (var group in Enumerable.Range(0, vertices.Length).GroupBy(find))
        {
            var bounds = new Bounds(vertices[group.First()], Vector3.zero);
            foreach (int i in group) bounds.Encapsulate(vertices[i]);
            report.AppendLine($"      ISLAND {group.Count()} vertices center={bounds.center.ToString("F6")} size={bounds.size.ToString("F6")}");
        }
    }
}
