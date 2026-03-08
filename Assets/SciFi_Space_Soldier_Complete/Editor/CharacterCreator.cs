#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;

public class CharacterCreator : ScriptableWizard
{
    public enum CHR
    {
        Player, AIRanged, AIMelee
    }

    public CHR Type = CHR.Player;

    public GameObject CharacterMesh; //To Create

    public string generic_avatar_r_hand = "Bip001 R Hand";
    public string generic_avatar_l_hand = "Bip001 L Hand";
    public string generic_avatar_head = "Bip001 Head";
    public string generic_avatar_torso = "Bip001 Spine1";

    public bool useRootMotion = false;
    public bool overrideAnimController = false;
    public UnityEditor.Animations.AnimatorController overridedAnimatorController; //to use as reference

    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AnimatorController; //to use as reference
    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AnimatorControllerRM; //to use as reference
    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AIMeleeAnimController; //to use as reference
    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AIRangedAnimController; //to use as reference
    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AIMeleeAnimControllerRM; //to use as reference
    [HideInInspector]
    public UnityEditor.Animations.AnimatorController AIRangedAnimControllerRM; //to use as reference

    [HideInInspector]
    public UnityEngine.Audio.AudioMixerGroup secondaryAudioGroup;
    public UnityEngine.Audio.AudioMixerGroup weaponAudioGroup;

    [MenuItem("PolygonR/Create Character Wizard...")]
    static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<CharacterCreator>("PolygonR : Create Character", "Create Player");
    }

    void OnWizardUpdate()
    {
        helpString = "Set the Character Mesh FBX or Prefab to create a new Character Actor";
        if (CharacterMesh == null)
        {
            errorString = "you MUST assign a character Mesh";
            isValid = false;
        }
        else if (overrideAnimController && overridedAnimatorController == null)
        {
            errorString = "Warning: Animator override is NOT RECOMMENDED. You should check provided Animators and provide an Animator including the same parameters. Also, you MUST assign an animator controller to override if you want to continue.";
            isValid = false;
        }
        else if (CharacterMesh.GetComponent<Animator>().isHuman == false)
        {
          
            errorString = "Warning: Humanoid Avatar NOT FOUND. Animations will not work correctly. Using generic avatar variables";
            isValid = true;
            
        }
        else
        {
            errorString = "";
            isValid = true;
        }
    }

    void OnWizardCreate()
    {
        CreateCharacter();

    }

    void SetParentAndPosition(Transform target, Transform source)
    {
        if (source != null)
        {
            target.transform.SetParent(source);
            target.transform.position = source.position;
        }
        else
        {
            Debug.LogError(source + " Not found, the tool can´t assign " + target + " to the correct position. Check naming please");
        }
    }

    void NotFoundError(Transform source)
    {
        Debug.LogError(source + " Not found, Check naming please");
    }

    
    //Breadth-first search
    public Transform FindDeepChild(Transform aParent, string aName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(aParent);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.name == aName)
                return c;
            foreach (Transform t in c)
                queue.Enqueue(t);
        }
        return null;
    }

    /*
    //Depth-first search
    public static Transform FindDeepChild(this Transform aParent, string aName)
    {
        foreach(Transform child in aParent)
        {
            if(child.name == aName )
                return child;
            var result = child.FindDeepChild(aName);
            if (result != null)
                return result;
        }
        return null;
    }
    */
    

    void CreateCharacter()

    {
        //Create new character and parent it
        GameObject characterGO = Instantiate(CharacterMesh, Vector3.zero, Quaternion.identity) as GameObject;
        
        if (characterGO.GetComponent<Animator>() == null)
            characterGO.AddComponent<Animator>();
        Animator charAnimator = characterGO.GetComponent<Animator>();

        //SetTag and layers
        /*
        if (Type == CHR.Player)
        {
            characterGO.name = CharacterMesh.name + "_Player";
            characterGO.tag = "Player";
            characterGO.layer = LayerMask.NameToLayer("PlayerCharacter");
        }
        else if (Type == CHR.EnemyMelee || Type == CHR.EnemyRanged)
        {
            characterGO.name = CharacterMesh.name + "_Enemy";
            characterGO.tag = "Enemy";
            characterGO.layer = LayerMask.NameToLayer("Character");
        }
        else if (Type == CHR.FriendlyAI)
        {
            characterGO.name = CharacterMesh.name + "_AIPlayer";
            characterGO.tag = "AIPlayer";
            characterGO.layer = LayerMask.NameToLayer("Character");
        }
        else if (Type == CHR.NeutralAI)
        {
            characterGO.name = CharacterMesh.name + "_AINeutral";
            characterGO.tag = "AINeutral";
            characterGO.layer = LayerMask.NameToLayer("Character");
        }*/

        //Look for Weapon Grip Node
        GameObject WeaponGrip = GameObject.Find("Weapon_R") as GameObject;
        if (WeaponGrip != null)
        {
            WeaponGrip.name = "Weapon_R_OLD";
            DestroyImmediate(WeaponGrip);
        }
  
        GameObject WeaponGripL = GameObject.Find("Weapon_L") as GameObject;
        if (WeaponGripL != null)
        {
            WeaponGripL.name = "Weapon_L_OLD";
            DestroyImmediate(WeaponGripL);
        }

        //Assign components
        if (characterGO.GetComponent<CapsuleCollider>() == null)
            characterGO.AddComponent<CapsuleCollider>();

        if (characterGO.GetComponent<Rigidbody>() == null)
            characterGO.AddComponent<Rigidbody>();

        if (characterGO.GetComponent<AudioSource>() == null)
            characterGO.AddComponent<AudioSource>();

        if (characterGO.GetComponent<PrActorUtils>() == null)
            characterGO.AddComponent<PrActorUtils>();

        //Set Basic components
        AudioSource audioC = characterGO.GetComponent<AudioSource>();
        if (secondaryAudioGroup)
            audioC.outputAudioMixerGroup = secondaryAudioGroup;

        //Collider
        CapsuleCollider cap = characterGO.GetComponent<CapsuleCollider>();
        cap.radius = 0.3f;
        cap.height = 2.0f;
        cap.center = new Vector3(0, 1, 0);
        cap.direction = 1;

        //RigidBody
        Rigidbody rigid = characterGO.GetComponent<Rigidbody>();
        rigid.useGravity = true;
        rigid.mass = 100;
        rigid.linearDamping = 0.5f;
        rigid.angularDamping = 0.05f;
        rigid.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        PrActorUtils charActorUtil = characterGO.GetComponent<PrActorUtils>();
        charActorUtil.Type = PrActorUtils.AT.player;

        //Create Aim IK Node
        GameObject aimIK = new GameObject("Weapon_IKAim");
        aimIK.transform.localEulerAngles = new Vector3(90, 0, 0);
         GameObject weaponR = new GameObject("Weapon_R");
        AudioSource weaponAudio = weaponR.AddComponent<AudioSource>();
        if (weaponAudioGroup)
            weaponAudio.outputAudioMixerGroup = weaponAudioGroup;

        weaponR.AddComponent<PrWeaponNodeHelper>();
        GameObject weaponL = new GameObject("Weapon_L");
        weaponL.AddComponent<PrWeaponNodeHelper>();
        weaponL.GetComponent<PrWeaponNodeHelper>().useGrenade = true;

        weaponR.transform.SetParent(aimIK.transform);
        aimIK.transform.SetParent(characterGO.transform);
        weaponL.transform.SetParent(characterGO.transform);

        //Set Animator references
        if (charAnimator)
        {
            //Check if it´s humanoid or generic
            if (charAnimator.avatar.isHuman)
            {
                charActorUtil.BurnAndFrozenVFXParent = charAnimator.GetBoneTransform(HumanBodyBones.Chest);
                charActorUtil.EarAndEyesPosition = charAnimator.GetBoneTransform(HumanBodyBones.Head);

                aimIK.transform.SetParent(charAnimator.GetBoneTransform(HumanBodyBones.RightHand));
                aimIK.transform.position = charAnimator.GetBoneTransform(HumanBodyBones.RightHand).position;

                weaponL.transform.SetParent(charAnimator.GetBoneTransform(HumanBodyBones.LeftHand));
                weaponL.transform.position = charAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position;

                Debug.Log("Humanoid rig detected and bones assigned correctly");
            }
            else
            {
                Debug.Log("Humanoid Avatar NOT FOUND. Starting to use generic avatar names defined in the tool");

                Transform r_hand = FindDeepChild(characterGO.transform, generic_avatar_r_hand);

                //Transform r_hand = characterGO.Find(generic_avatar_r_hand);
                Transform l_hand = FindDeepChild(characterGO.transform, generic_avatar_l_hand);
                Transform head = FindDeepChild(characterGO.transform, generic_avatar_head);
                Transform spine = FindDeepChild(characterGO.transform, generic_avatar_torso);


                SetParentAndPosition(aimIK.transform, r_hand);
                SetParentAndPosition(weaponL.transform, l_hand);

                if (spine != null)
                {
                    charActorUtil.BurnAndFrozenVFXParent = spine;
                }
                else
                {
                    NotFoundError(spine);
                }
                if (head != null)
                {
                    charActorUtil.EarAndEyesPosition = head;
                }
                else
                {
                    NotFoundError(head);
                }
            }
        }
        else
        {
            charActorUtil.BurnAndFrozenVFXParent = characterGO.transform;
            charActorUtil.EarAndEyesPosition = characterGO.transform;
        }

        charActorUtil.WeaponL = weaponL.transform;
        charActorUtil.WeaponR = weaponR.transform;
        
        if (overrideAnimController)
            charAnimator.runtimeAnimatorController = overridedAnimatorController;
        else
        {
            if (Type == CHR.Player)
            {
                if (useRootMotion)
                {
                    charAnimator.runtimeAnimatorController = AnimatorController;
                }
                else
                {
                    charAnimator.runtimeAnimatorController = AnimatorControllerRM;
                }
            }
            else if (Type == CHR.AIRanged)
            {
                if (useRootMotion)
                {
                    charAnimator.runtimeAnimatorController = AIRangedAnimControllerRM;
                }
                else
                {
                    charAnimator.runtimeAnimatorController = AIRangedAnimController;
                }
            }
            else if (Type == CHR.AIMelee)
            {
                if (useRootMotion)
                {
                    charAnimator.runtimeAnimatorController = AIMeleeAnimControllerRM;
                }
                else
                {
                    charAnimator.runtimeAnimatorController = AIMeleeAnimController;
                }
            }
        }

        
    }
    
    void CopyComponents(Component Source, Component Target )
    {
        ComponentUtility.CopyComponent(Source);
        ComponentUtility.PasteComponentValues(Target);
    }

    void CopyComponents(Component Source, GameObject Target)
    {
        ComponentUtility.CopyComponent(Source);
        ComponentUtility.PasteComponentAsNew(Target);
    }
    
}
#endif