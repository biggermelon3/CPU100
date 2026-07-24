using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CPU100PlayerAnimationBuilder
{
    const string PlayerArt = "Assets/CPU100/Art/Player";
    const string AnimationFolder = "Assets/CPU100/Animations";
    const string PlayerAnimationFolder = AnimationFolder + "/Player";
    const string ControllerPath = PlayerAnimationFolder + "/Player.controller";
    const string PlayerPrefabPath = "Assets/CPU100/Prefabs/Characters/Player.prefab";
    const float PlayerVisualScale = 1.6f;

    [InitializeOnLoadMethod]
    static void BuildAutomaticallyWhenMissing()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Transform prefabVisual = prefab != null ? prefab.transform.Find("Visual") : null;
            bool needsVisualMigration = prefab != null &&
                (prefabVisual == null ||
                 !Mathf.Approximately(prefabVisual.localScale.x, PlayerVisualScale));
            if (EditorApplication.isPlayingOrWillChangePlaymode || prefab == null ||
                (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null &&
                 !needsVisualMigration))
                return;

            Build();
        };
    }

    [MenuItem("Tools/CPU 100/Build Player Animation Controller")]
    public static void Build()
    {
        EnsureFolder("Assets/CPU100", "Animations");
        EnsureFolder(AnimationFolder, "Player");
        PrepareSpriteImports();

        AnimationClip idle = BuildClip("Player_Idle", 6f, true, new[]
        {
            PlayerArt + "/Idle/Idle1.png", PlayerArt + "/Idle/Idle2.png", PlayerArt + "/Idle/Idle3.png"
        });
        AnimationClip walk = BuildClip("Player_Walk", 12f, true, new[]
        {
            PlayerArt + "/Walk/Walk1.png", PlayerArt + "/Walk/Walk2.png",
            PlayerArt + "/Walk/Walk3.png", PlayerArt + "/Walk/Walk4.png",
            PlayerArt + "/Walk/Walk5.png", PlayerArt + "/Walk/Walk6.png"
        });
        AnimationClip jump = BuildClip("Player_Jump", 10f, false, new[]
        {
            PlayerArt + "/Jump/Jump1.png", PlayerArt + "/Jump/Jump2.png",
            PlayerArt + "/Jump/Jump3.png", PlayerArt + "/Jump/Jump4.png"
        });
        AnimationClip plane = BuildClip("Player_PaperPlane", 1f, true,
            new[] { PlayerArt + "/Ability/Plane.png" });
        AnimationClip poison = BuildClip("Player_PoisonIdle", 1f, true,
            new[] { PlayerArt + "/Hazard/PoisonIdle.png" });

        if (idle == null || walk == null || jump == null || plane == null || poison == null)
        {
            Debug.LogError("[CPU100 Animation] Build stopped because one or more player sprites could not be loaded.");
            return;
        }

        AnimatorController controller = BuildController(idle, walk, jump, plane, poison);
        BindPlayerPrefab(controller);
        BindOpenScenePlayer(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CPU100 Animation] Player animations and controller are ready at " + ControllerPath);
    }

    static AnimationClip BuildClip(string name, float frameRate, bool loop, string[] spritePaths)
    {
        var sprites = new List<Sprite>();
        foreach (string path in spritePaths)
        {
            Sprite sprite = LoadSprite(path);
            if (sprite == null)
            {
                Debug.LogError("[CPU100 Animation] Missing sprite: " + path);
                return null;
            }
            sprites.Add(sprite);
        }

        string clipPath = PlayerAnimationFolder + "/" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        clip.name = name;
        clip.frameRate = frameRate;
        var keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        var binding = new EditorCurveBinding
        {
            path = "Visual",
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorController BuildController(params AnimationClip[] clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states)
            machine.RemoveState(child.state);

        string[] names = { "Idle", "Walk", "Jump", "PaperPlane", "PoisonIdle" };
        for (int i = 0; i < names.Length; i++)
        {
            AnimatorState state = machine.AddState(names[i]);
            state.motion = clips[i];
            state.writeDefaultValues = true;
            if (i == 0)
                machine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void BindPlayerPrefab(RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
            return;

        ConfigurePlayer(root, controller);
        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void BindOpenScenePlayer(RuntimeAnimatorController controller)
    {
        PlayerController2D player = Object.FindFirstObjectByType<PlayerController2D>();
        if (player == null)
            return;

        ConfigurePlayer(player.gameObject, controller);
        EditorUtility.SetDirty(player.gameObject);
        if (player.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
    }

    static void ConfigurePlayer(GameObject player, RuntimeAnimatorController controller)
    {
        PlaceholderVisual placeholder = player.GetComponent<PlaceholderVisual>();
        if (placeholder != null)
            Object.DestroyImmediate(placeholder);

        Transform visual = player.transform.Find("Visual");
        if (visual == null)
        {
            GameObject visualObject = new GameObject("Visual");
            visual = visualObject.transform;
            visual.SetParent(player.transform, false);
        }
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one * PlayerVisualScale;

        SpriteRenderer oldRenderer = player.GetComponent<SpriteRenderer>();
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        if (oldRenderer != null)
        {
            renderer.sharedMaterials = oldRenderer.sharedMaterials;
            renderer.sortingLayerID = oldRenderer.sortingLayerID;
            renderer.sortingOrder = oldRenderer.sortingOrder;
            Object.DestroyImmediate(oldRenderer);
        }
        renderer.sprite = LoadSprite(PlayerArt + "/Idle/Idle1.png");
        renderer.color = Color.white;
        renderer.sortingOrder = 50;
        AlignVisualBottomToCollider(player, visual, renderer);

        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
            animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        if (player.GetComponent<PlayerAnimationController>() == null)
            player.AddComponent<PlayerAnimationController>();
    }

    static void AlignVisualBottomToCollider(GameObject player, Transform visual, SpriteRenderer renderer)
    {
        CapsuleCollider2D capsule = player.GetComponent<CapsuleCollider2D>();
        if (capsule == null || renderer.sprite == null)
        {
            visual.localPosition = Vector3.zero;
            return;
        }

        float colliderBottom = capsule.offset.y - capsule.size.y * 0.5f;
        float scaledSpriteBottom = renderer.sprite.bounds.min.y * visual.localScale.y;
        visual.localPosition = new Vector3(0f, colliderBottom - scaledSpriteBottom, 0f);
    }

    static Sprite LoadSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Sprite largest = null;
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                if (largest == null || sprite.rect.width * sprite.rect.height >
                    largest.rect.width * largest.rect.height)
                    largest = sprite;
            }
        }
        return largest;
    }

    static void PrepareSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PlayerArt });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 2400f) ||
                           importer.maxTextureSize != 1024 ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 2400f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
