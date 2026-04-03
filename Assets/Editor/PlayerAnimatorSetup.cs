#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// One-time setup: builds sprite AnimationClips + Animator Controller from <c>PlayerSpritesheet.png</c>.
/// Use menu: <b>Tools → Player → Generate Animator From PlayerSpritesheet</b>
/// </summary>
public static class PlayerAnimatorSetup
{
    const string SpritesheetPath = "Assets/Sprites/Player/PlayerSpritesheet.png";
    const string OutputFolder = "Assets/Animations/Player";

    [MenuItem("Tools/Player/Generate Animator From PlayerSpritesheet")]
    public static void Generate()
    {
        if (!AssetDatabase.LoadAssetAtPath<Texture2D>(SpritesheetPath))
        {
            Debug.LogError($"[PlayerAnimatorSetup] Missing texture at {SpritesheetPath}");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Animations", "Player");

        var sprites = LoadSpritesOrdered(SpritesheetPath);
        if (sprites.Count < 8)
        {
            Debug.LogError($"[PlayerAnimatorSetup] Expected sliced sprites on {SpritesheetPath}, found {sprites.Count}.");
            return;
        }

        float idleFps = 8f;
        float runFps = 12f;
        float attackFps = 14f;

        // Rows on a typical 4-column sheet: idle ~0–3, run ~4–7, attack ~8–11, last frame(s) for roll/death
        var idleFrames = TakeRange(sprites, 0, 4);
        var runFrames = TakeRange(sprites, 4, 4);
        var attackFrames = TakeRange(sprites, 8, 4);
        if (attackFrames.Count < 2)
            attackFrames = TakeRange(sprites, Mathf.Max(0, sprites.Count - 4), 4);

        AnimationClip idleClip = BuildSpriteClip("Player_Idle", idleFrames, idleFps, loop: true);
        AnimationClip runClip = BuildSpriteClip("Player_Run", runFrames, runFps, loop: true);
        AnimationClip attackClip = BuildSpriteClip("Player_Attack", attackFrames, attackFps, loop: false);
        AnimationClip rollClip = BuildSpriteClip("Player_Roll", runFrames, runFps * 1.25f, loop: true);

        SaveClip($"{OutputFolder}/Player_Idle.anim", idleClip);
        SaveClip($"{OutputFolder}/Player_Run.anim", runClip);
        SaveClip($"{OutputFolder}/Player_Attack.anim", attackClip);
        SaveClip($"{OutputFolder}/Player_Roll.anim", rollClip);

        string controllerPath = $"{OutputFolder}/Player.controller";
        BuildController(controllerPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PlayerAnimatorSetup] Created clips + {controllerPath}. Assign controller on Player's Animator (same object as Sprite Renderer).");
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

    static void BuildController(string path)
    {
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputFolder}/Player_Idle.anim");
        AnimationClip run = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputFolder}/Player_Run.anim");
        AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputFolder}/Player_Attack.anim");
        AnimationClip roll = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputFolder}/Player_Roll.anim");

        if (idle == null || run == null || attack == null || roll == null)
        {
            Debug.LogError("[PlayerAnimatorSetup] Clips missing; abort controller.");
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
