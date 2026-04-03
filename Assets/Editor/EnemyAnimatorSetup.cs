#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds sprite clips + Animator Controllers from enemy sheets (same 4×N layout as <see cref="PlayerAnimatorSetup"/>).
/// Menu: <b>Tools → Enemy → Generate Animators From Enemy Spritesheets</b>
/// </summary>
public static class EnemyAnimatorSetup
{
    struct EnemySheet
    {
        public string Id;
        public string TexturePath;
    }

    static readonly EnemySheet[] Sheets =
    {
        new EnemySheet { Id = "Watcher", TexturePath = "Assets/Sprites/Enemy/WatcherSpritesheet.png" },
        new EnemySheet { Id = "Bulwark", TexturePath = "Assets/Sprites/Enemy/BulwarkSpritesheet.png" },
        new EnemySheet { Id = "Sprinter", TexturePath = "Assets/Sprites/Enemy/SprinterSpritesheet.png" },
    };

    const string OutputRoot = "Assets/Animations/Enemy";

    [MenuItem("Tools/Enemy/Generate Animators From Enemy Spritesheets")]
    public static void GenerateAll()
    {
        foreach (var s in Sheets)
            GenerateOne(s);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EnemyAnimatorSetup] Done. Controllers are under " + OutputRoot);
    }

    static void GenerateOne(EnemySheet sheet)
    {
        if (!AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.TexturePath))
        {
            Debug.LogError($"[EnemyAnimatorSetup] Missing texture: {sheet.TexturePath}");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(OutputRoot))
            AssetDatabase.CreateFolder("Assets/Animations", "Enemy");

        string folder = $"{OutputRoot}/{sheet.Id}";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(OutputRoot, sheet.Id);

        var sprites = LoadSpritesOrdered(sheet.TexturePath);
        if (sprites.Count < 8)
        {
            Debug.LogError($"[EnemyAnimatorSetup] {sheet.Id}: need at least 8 sliced sprites, found {sprites.Count}.");
            return;
        }

        float idleFps = 8f;
        float runFps = 12f;
        float attackFps = 14f;

        var idleFrames = TakeRange(sprites, 0, 4);
        var runFrames = TakeRange(sprites, 4, 4);
        var attackFrames = TakeRange(sprites, 8, 4);
        if (attackFrames.Count < 2)
            attackFrames = TakeRange(sprites, Mathf.Max(0, sprites.Count - 4), 4);

        string prefix = $"{sheet.Id}_";
        SaveClip($"{folder}/{prefix}Idle.anim", BuildSpriteClip($"{prefix}Idle", idleFrames, idleFps, loop: true));
        SaveClip($"{folder}/{prefix}Run.anim", BuildSpriteClip($"{prefix}Run", runFrames, runFps, loop: true));
        SaveClip($"{folder}/{prefix}Attack.anim", BuildSpriteClip($"{prefix}Attack", attackFrames, attackFps, loop: false));
        SaveClip($"{folder}/{prefix}Roll.anim", BuildSpriteClip($"{prefix}Roll", runFrames, runFps * 1.25f, loop: true));

        BuildController($"{folder}/{sheet.Id}.controller", folder, prefix);
    }

    static List<Sprite> LoadSpritesOrdered(string texturePath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        var list = new List<Sprite>();
        foreach (Object o in all)
        {
            if (o is Sprite s)
                list.Add(s);
        }

        var rx = new Regex(@"_(\d+)$", RegexOptions.RightToLeft);
        list.Sort((a, b) =>
        {
            var ma = rx.Match(a.name);
            var mb = rx.Match(b.name);
            int na = ma.Success ? int.Parse(ma.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            int nb = mb.Success ? int.Parse(mb.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            return na.CompareTo(nb);
        });
        return list;
    }

    static List<Sprite> TakeRange(List<Sprite> sprites, int start, int count)
    {
        var r = new List<Sprite>();
        for (int i = 0; i < count && start + i < sprites.Count; i++)
            r.Add(sprites[start + i]);
        return r;
    }

    static AnimationClip BuildSpriteClip(string name, List<Sprite> frames, float fps, bool loop)
    {
        var clip = new AnimationClip { name = name };
        if (frames.Count == 0)
            return clip;

        float dt = 1f / Mathf.Max(1f, fps);
        var keys = new ObjectReferenceKeyframe[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * dt, value = frames[i] };

        var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    static void SaveClip(string path, AnimationClip clip)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
        }
        else
            AssetDatabase.CreateAsset(clip, path);
    }

    static void BuildController(string path, string clipFolder, string prefix)
    {
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{clipFolder}/{prefix}Idle.anim");
        AnimationClip run = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{clipFolder}/{prefix}Run.anim");
        AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{clipFolder}/{prefix}Attack.anim");
        AnimationClip roll = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{clipFolder}/{prefix}Roll.anim");

        if (idle == null || run == null || attack == null || roll == null)
        {
            Debug.LogError($"[EnemyAnimatorSetup] Missing clips for {prefix}; abort controller.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path))
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Rolling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState stIdle = sm.AddState("Idle");
        stIdle.motion = idle;
        AnimatorState stRun = sm.AddState("Run");
        stRun.motion = run;
        AnimatorState stAttack = sm.AddState("Attack");
        stAttack.motion = attack;
        AnimatorState stRoll = sm.AddState("Roll");
        stRoll.motion = roll;

        sm.defaultState = stIdle;

        AddInstantTransition(stIdle, stRun, AnimatorConditionMode.Greater, 0.08f, "Speed");
        AddInstantTransition(stRun, stIdle, AnimatorConditionMode.Less, 0.08f, "Speed");

        AnimatorStateTransition anyAttack = sm.AddAnyStateTransition(stAttack);
        anyAttack.hasExitTime = false;
        anyAttack.duration = 0.05f;
        anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        AnimatorStateTransition atkRun = stAttack.AddTransition(stRun);
        atkRun.hasExitTime = true;
        atkRun.exitTime = 0.95f;
        atkRun.duration = 0.08f;
        atkRun.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");

        AnimatorStateTransition atkIdle = stAttack.AddTransition(stIdle);
        atkIdle.hasExitTime = true;
        atkIdle.exitTime = 0.95f;
        atkIdle.duration = 0.08f;
        atkIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

        AnimatorStateTransition anyRoll = sm.AddAnyStateTransition(stRoll);
        anyRoll.hasExitTime = false;
        anyRoll.duration = 0.03f;
        anyRoll.AddCondition(AnimatorConditionMode.If, 0, "Rolling");

        AnimatorStateTransition rollToRun = stRoll.AddTransition(stRun);
        rollToRun.hasExitTime = false;
        rollToRun.duration = 0.05f;
        rollToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "Rolling");
        rollToRun.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");

        AnimatorStateTransition rollToIdle = stRoll.AddTransition(stIdle);
        rollToIdle.hasExitTime = false;
        rollToIdle.duration = 0.05f;
        rollToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Rolling");
        rollToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

        EditorUtility.SetDirty(controller);
    }

    static void AddInstantTransition(
        AnimatorState from,
        AnimatorState to,
        AnimatorConditionMode mode,
        float threshold,
        string param)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.08f;
        t.AddCondition(mode, threshold, param);
    }
}
#endif
