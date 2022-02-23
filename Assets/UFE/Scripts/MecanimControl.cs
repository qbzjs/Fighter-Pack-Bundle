﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class AnimationData {
	public AnimationClip clip;
	public string clipName;
    public float speed = 1;
	public float transitionDuration = -1;
	public WrapMode wrapMode;
	public bool applyRootMotion;
	[HideInInspector] public int timesPlayed = 0;
	[HideInInspector] public float secondsPlayed = 0;
	[HideInInspector] public float length = 0;
    [HideInInspector] public float originalSpeed = 1;
    [HideInInspector] public float normalizedSpeed = 1;
    [HideInInspector] public float normalizedTime = 1;
	[HideInInspector] public int stateHash;
	[HideInInspector] public string stateName;
}

[RequireComponent (typeof (Animator))]
public class MecanimControl : MonoBehaviour {

	public AnimationData defaultAnimation = new AnimationData();
	public AnimationData[] animations = new AnimationData[0];
	public bool debugMode = false;
	public bool alwaysPlay = false;
	public bool overrideRootMotion = false;
    public bool overrideAnimatorUpdate = false;
	public float defaultTransitionDuration = 0.15f;
	public WrapMode defaultWrapMode = WrapMode.Loop;

	private Animator animator;

	private int state1Hash;
	private int state2Hash;
	
	private RuntimeAnimatorController controller1;
	private RuntimeAnimatorController controller2;
	private RuntimeAnimatorController controller3;
	private RuntimeAnimatorController controller4;

	private AnimationData currentAnimationData;
	private bool currentMirror;

	public delegate void AnimEvent(AnimationData animationData);
	public static event AnimEvent OnAnimationBegin;
	public static event AnimEvent OnAnimationEnd;
	public static event AnimEvent OnAnimationLoop;
	
	// UNITY METHODS
	void Awake () {
		animator = gameObject.GetComponent<Animator>();
		controller1 = (RuntimeAnimatorController) Resources.Load("controller1");
		controller2 = (RuntimeAnimatorController) Resources.Load("controller2");
		controller3 = (RuntimeAnimatorController) Resources.Load("controller3");
		controller4 = (RuntimeAnimatorController) Resources.Load("controller4");
		
		foreach(AnimationData animData in animations) {
			if (animData.wrapMode == WrapMode.Default) animData.wrapMode = defaultWrapMode;
			animData.clip.wrapMode = animData.wrapMode;
		}

	}
	
	void Start(){
		if (defaultAnimation.clip == null && animations.Length > 0){
			SetDefaultClip(animations[0].clip, "Default", animations[0].speed, animations[0].wrapMode, false);
		}
		
		if (defaultAnimation.clip != null){
			foreach(AnimationData animData in animations) {
				if (animData.clip == defaultAnimation.clip)
					defaultAnimation.clip = (AnimationClip) Instantiate(defaultAnimation.clip);
			}
			AnimatorOverrideController overrideController = new AnimatorOverrideController();
			overrideController.runtimeAnimatorController = controller1;
			
			currentAnimationData = defaultAnimation;
			currentAnimationData.stateName = "State2";
			currentAnimationData.length = currentAnimationData.clip.length;

			overrideController["State1"] = currentAnimationData.clip;
			overrideController["State2"] = currentAnimationData.clip;

			animator.runtimeAnimatorController = overrideController;
			animator.Play("State2", 0, 0);

			if (overrideRootMotion) animator.applyRootMotion = currentAnimationData.applyRootMotion;
			SetSpeed(currentAnimationData.speed);
		}
	}
	
	public void DoFixedUpdate(){
        //WrapMode emulator
        if (overrideAnimatorUpdate) {
            animator.enabled = false;
            animator.Update(Time.fixedDeltaTime);
        }

        if (currentAnimationData == null || currentAnimationData.clip == null) return;


        currentAnimationData.secondsPlayed += (Time.fixedDeltaTime * animator.speed);
        if (currentAnimationData.secondsPlayed > currentAnimationData.length) {
            currentAnimationData.secondsPlayed = currentAnimationData.length;
        }
        currentAnimationData.normalizedTime = currentAnimationData.secondsPlayed / currentAnimationData.length;

		if (currentAnimationData.secondsPlayed == currentAnimationData.length){
			if (currentAnimationData.clip.wrapMode == WrapMode.Loop || currentAnimationData.clip.wrapMode == WrapMode.PingPong) {
				if (MecanimControl.OnAnimationLoop != null) MecanimControl.OnAnimationLoop(currentAnimationData);
				currentAnimationData.timesPlayed ++;
				
				if (currentAnimationData.clip.wrapMode == WrapMode.Loop) {
					SetCurrentClipPosition(0);
				}
				
				if (currentAnimationData.clip.wrapMode == WrapMode.PingPong) {
					SetSpeed(currentAnimationData.clipName, -currentAnimationData.speed);
					SetCurrentClipPosition(0);
				}
				
			}else if (currentAnimationData.timesPlayed == 0) {
				if (MecanimControl.OnAnimationEnd != null) MecanimControl.OnAnimationEnd(currentAnimationData);
				currentAnimationData.timesPlayed = 1;
				
				if (currentAnimationData.clip.wrapMode == WrapMode.Once && alwaysPlay) {
					Play(defaultAnimation, currentMirror);
				}else if (!alwaysPlay){
					animator.speed = 0;
				}
			}
		}
	}
	
	void OnGUI(){
		//Toggle debug mode to see the live data in action
		if (debugMode) {
			GUI.Box (new Rect (Screen.width - 340,40,340,400), "Animation Data");
			GUI.BeginGroup(new Rect (Screen.width - 330,60,400,400));{
				
				AnimatorClipInfo[] animationInfoArray = animator.GetCurrentAnimatorClipInfo(0);
				foreach (AnimatorClipInfo animationInfo in animationInfoArray){
					AnimatorStateInfo animatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
					GUILayout.Label(animationInfo.clip.name);
					GUILayout.Label("-Wrap Mode: "+ animationInfo.clip.wrapMode);
					GUILayout.Label("-Is Playing: "+ IsPlaying(animationInfo.clip));
					GUILayout.Label("-Blend Weight: "+ animationInfo.weight);
					GUILayout.Label("-Normalized Time: "+ animatorStateInfo.normalizedTime);
					GUILayout.Label("-Length: "+ animationInfo.clip.length);
					GUILayout.Label("----");
				}

                GUILayout.Label("Global Speed Speed: " + GetSpeed().ToString());

				GUILayout.Label("--Current Animation Data--");
                GUILayout.Label("-Clip Name: " + currentAnimationData.clipName);
                GUILayout.Label("-Speed: " + GetSpeed(currentAnimationData));
				GUILayout.Label("-Normalized Speed: "+ GetNormalizedSpeed(currentAnimationData));
				GUILayout.Label("-Times Played: "+ currentAnimationData.timesPlayed);
				GUILayout.Label("-Seconds Played: "+ currentAnimationData.secondsPlayed);
                GUILayout.Label("-Normalized Time: " + currentAnimationData.normalizedTime);
				GUILayout.Label("-Lengh: "+ currentAnimationData.length);
			}GUI.EndGroup();
		}
	}
	


	// MECANIM CONTROL METHODS
	public void RemoveClip(string name) {
		List<AnimationData> animationDataList = new List<AnimationData>(animations);
		animationDataList.Remove(GetAnimationData(name));
		animations = animationDataList.ToArray();
	}

	public void RemoveClip(AnimationClip clip) {
		List<AnimationData> animationDataList = new List<AnimationData>(animations);
		animationDataList.Remove(GetAnimationData(clip));
		animations = animationDataList.ToArray();
	}
	
	public void SetDefaultClip(AnimationClip clip, string name, float speed, WrapMode wrapMode, bool mirror) {
		defaultAnimation.clip = (AnimationClip) Instantiate(clip);
		defaultAnimation.clip.wrapMode = wrapMode;
		defaultAnimation.clipName = name;
		defaultAnimation.speed = speed;
		defaultAnimation.originalSpeed = speed;
		defaultAnimation.transitionDuration = -1;
		defaultAnimation.wrapMode = wrapMode;
	}
	
	public void AddClip(AnimationClip clip, string newName) {
		AddClip(clip, newName, 1, defaultWrapMode);
	}

	public void AddClip(AnimationClip clip, string newName, float speed, WrapMode wrapMode) {
		if (GetAnimationData(newName) != null) Debug.LogWarning("An animation with the name '"+ newName +"' already exists.");
		AnimationData animData = new AnimationData();
		animData.clip = (AnimationClip) Instantiate(clip);
		if (wrapMode == WrapMode.Default) wrapMode = defaultWrapMode;
		animData.clip.wrapMode = wrapMode;
		animData.clip.name = newName;
		animData.clipName = newName;
        animData.speed = speed;
        animData.originalSpeed = speed;
		animData.length = clip.length;
		animData.wrapMode = wrapMode;

		List<AnimationData> animationDataList = new List<AnimationData>(animations);
		animationDataList.Add(animData);
		animations = animationDataList.ToArray();
	}

	public AnimationData GetAnimationData(string clipName){
		foreach(AnimationData animData in animations){
			if (animData.clipName == clipName){
				return animData;
			}
		}
		if (clipName == defaultAnimation.clipName) return defaultAnimation;
		return null;
	}

	public AnimationData GetAnimationData(AnimationClip clip){
		foreach(AnimationData animData in animations){
			if (animData.clip == clip){
				return animData;
			}
		}
		if (clip == defaultAnimation.clip) return defaultAnimation;
		return null;
	}
	
	public void CrossFade(string clipName, float blendingTime){
		CrossFade(clipName, blendingTime, 0, currentMirror);
	}

	public void CrossFade(string clipName, float blendingTime, float normalizedTime, bool mirror){
		_playAnimation(GetAnimationData(clipName), blendingTime, normalizedTime, mirror);
	}
	
	public void CrossFade(AnimationData animationData, float blendingTime, float normalizedTime, bool mirror){
		_playAnimation(animationData, blendingTime, normalizedTime, mirror);
	}

	public void Play(string clipName, float blendingTime, float normalizedTime, bool mirror){
		_playAnimation(GetAnimationData(clipName), blendingTime, normalizedTime, mirror);
	}
	
	public void Play(AnimationClip clip, float blendingTime, float normalizedTime, bool mirror){
		_playAnimation(GetAnimationData(clip), blendingTime, normalizedTime, mirror);
	}

	public void Play(string clipName, bool mirror){
		_playAnimation(GetAnimationData(clipName), 0, 0, mirror);
	}

	public void Play(string clipName){
		_playAnimation(GetAnimationData(clipName), 0, 0, currentMirror);
	}
	
	public void Play(AnimationClip clip, bool mirror){
		_playAnimation(GetAnimationData(clip), 0, 0, mirror);
	}

	public void Play(AnimationClip clip){
		_playAnimation(GetAnimationData(clip), 0, 0, currentMirror);
	}

	public void Play(AnimationData animationData, bool mirror){
		_playAnimation(animationData, animationData.transitionDuration, 0, mirror);
	}

	public void Play(AnimationData animationData){
		_playAnimation(animationData, animationData.transitionDuration, 0, currentMirror);
	}
	
	public void Play(AnimationData animationData, float blendingTime, float normalizedTime, bool mirror){
		_playAnimation(animationData, blendingTime, normalizedTime, mirror);
	}

	public void Play(){
		animator.speed = Mathf.Abs(currentAnimationData.speed);
	}

    //The overrite machine. Creates an overrideController, replace its core animations and restate it back in
	private void _playAnimation(AnimationData targetAnimationData, float blendingTime, float normalizedTime, bool mirror){
		if (targetAnimationData == null || targetAnimationData.clip == null) return;
		AnimatorOverrideController overrideController = new AnimatorOverrideController();

        float newAnimatorSpeed = Mathf.Abs(targetAnimationData.originalSpeed);
		float currentNormalizedTime = GetCurrentClipPosition();

        currentMirror = mirror;
		if (mirror){
            if (targetAnimationData.originalSpeed >= 0) {
				overrideController.runtimeAnimatorController = controller2;
			}else{
				overrideController.runtimeAnimatorController = controller4;
			}
		}else{
            if (targetAnimationData.originalSpeed >= 0) {
				overrideController.runtimeAnimatorController = controller1;
			}else{
				overrideController.runtimeAnimatorController = controller3;
			}
		}
		
		if (currentAnimationData != null) overrideController["State1"] = currentAnimationData.clip;
		overrideController["State2"] = targetAnimationData.clip;
		 

		if (blendingTime == -1) blendingTime = currentAnimationData.transitionDuration;
		if (blendingTime == -1) blendingTime = defaultTransitionDuration;

		if (blendingTime <= 0 || currentAnimationData == null){
            animator.runtimeAnimatorController = overrideController;
            animator.Play("State2", 0, normalizedTime);

		}else {
			animator.runtimeAnimatorController = overrideController;
			currentAnimationData.stateName = "State1";
			SetCurrentClipPosition(currentNormalizedTime);

            animator.Update(0);
			animator.CrossFade("State2", blendingTime/newAnimatorSpeed, 0, normalizedTime);
		}


		targetAnimationData.secondsPlayed = (normalizedTime * targetAnimationData.clip.length) / newAnimatorSpeed;
		targetAnimationData.length = targetAnimationData.clip.length;
        targetAnimationData.normalizedTime = normalizedTime;

		if (overrideRootMotion) animator.applyRootMotion = targetAnimationData.applyRootMotion;
        SetSpeed(targetAnimationData.originalSpeed);

        if (currentAnimationData != null) {
            currentAnimationData.speed = currentAnimationData.originalSpeed;
            currentAnimationData.normalizedSpeed = 1;
            currentAnimationData.timesPlayed = 0;
        }

		currentAnimationData = targetAnimationData;
		currentAnimationData.stateName = "State2";

		if (MecanimControl.OnAnimationBegin != null) MecanimControl.OnAnimationBegin(currentAnimationData);
	}
	
	public bool IsPlaying(string clipName){
		return IsPlaying(GetAnimationData(clipName));
	}
	
	public bool IsPlaying(string clipName, float weight){
		return IsPlaying(GetAnimationData(clipName), weight);
	}
	
	public bool IsPlaying(AnimationClip clip){
		return IsPlaying(GetAnimationData(clip));
	}
	
	public bool IsPlaying(AnimationClip clip, float weight){
		return IsPlaying(GetAnimationData(clip), weight);
	}

    public bool IsPlaying(AnimationData animData) {
        return (currentAnimationData == animData);
    }

	public bool IsPlaying(AnimationData animData, float weight){
		if (animData == null) return false;
		if (currentAnimationData == null) return false;
		if (currentAnimationData == animData && animData.wrapMode == WrapMode.Once && animData.timesPlayed > 0) return false;
		if (currentAnimationData == animData && animData.wrapMode == WrapMode.ClampForever) return true;
		if (currentAnimationData == animData) return true;

		AnimatorClipInfo[] animationInfoArray = animator.GetCurrentAnimatorClipInfo(0);
		foreach (AnimatorClipInfo animationInfo in animationInfoArray){
			if (animData.clip == animationInfo.clip && animationInfo.weight >= weight) return true;
		}
		return false;
	}
	
	public string GetCurrentClipName(){
		return currentAnimationData.clipName;
	}
	
	public AnimationData GetCurrentAnimationData(){
		return currentAnimationData;
	}
	
	public int GetCurrentClipPlayCount(){
		return currentAnimationData.timesPlayed;
	}
	
	public float GetCurrentClipTime(){
		return currentAnimationData.secondsPlayed;
	}

	public float GetCurrentClipLength(){
		return currentAnimationData.length;
	}

	public void SetCurrentClipPosition(float normalizedTime){
		SetCurrentClipPosition(normalizedTime, false);
	}

	public void SetCurrentClipPosition(float normalizedTime, bool pause){
        normalizedTime = Mathf.Clamp01(normalizedTime);
		currentAnimationData.secondsPlayed = normalizedTime * currentAnimationData.length;
        currentAnimationData.normalizedTime = normalizedTime;

        animator.Play(currentAnimationData.stateName, 0, normalizedTime);
        animator.Update(0);

		if (pause) Pause();
	}

	public float GetCurrentClipPosition(){
		if (currentAnimationData == null) return 0;
		return currentAnimationData.secondsPlayed/currentAnimationData.length;
	}
	
	public void Stop(){
		Play(defaultAnimation.clip, defaultTransitionDuration, 0, currentMirror);
	}
	
	public void Pause(){
		animator.speed = 0;
	}
	
    public void SetSpeed(AnimationClip clip, float speed) {
        SetSpeed(GetAnimationData(clip), speed);
    }

    public void SetSpeed(string clipName, float speed) {
        SetSpeed(GetAnimationData(clipName), speed);
    }

    public void SetSpeed(AnimationData animData, float speed) {
        if (animData != null) {
            animData.normalizedSpeed = speed / animData.originalSpeed;
            animData.speed = speed;
            if (IsPlaying(animData)) SetSpeed(speed);
        }
    }

    public void SetNormalizedSpeed(AnimationClip clip, float normalizedSpeed) {
        SetNormalizedSpeed(GetAnimationData(clip), normalizedSpeed);
    }

    public void SetNormalizedSpeed(string clipName, float normalizedSpeed) {
        SetNormalizedSpeed(GetAnimationData(clipName), normalizedSpeed);
    }

    public void SetNormalizedSpeed(AnimationData animData, float normalizedSpeed) {
        animData.normalizedSpeed = normalizedSpeed;
        animData.speed = animData.originalSpeed * animData.normalizedSpeed;
        if (IsPlaying(animData)) SetSpeed(animData.speed);
    }

	public void SetSpeed(float speed){
		animator.speed = Mathf.Abs(speed);
	}
	
	public void RestoreSpeed(){
		SetSpeed(currentAnimationData.speed);
	}
	
	public void Rewind(){
		SetSpeed(-currentAnimationData.speed);
	}

	public void SetWrapMode(WrapMode wrapMode){
		defaultWrapMode = wrapMode;
	}
	
	public void SetWrapMode(AnimationData animationData, WrapMode wrapMode){
		animationData.wrapMode = wrapMode;
		animationData.clip.wrapMode = wrapMode;
	}

	public void SetWrapMode(AnimationClip clip, WrapMode wrapMode){
		AnimationData animData = GetAnimationData(clip);
		animData.wrapMode = wrapMode;
		animData.clip.wrapMode = wrapMode;
	}

	public void SetWrapMode(string clipName, WrapMode wrapMode){
		AnimationData animData = GetAnimationData(clipName);
		animData.wrapMode = wrapMode;
		animData.clip.wrapMode = wrapMode;
	}

    public float GetSpeed(AnimationClip clip) {
        return GetSpeed(GetAnimationData(clip));
	}

    public float GetSpeed(string clipName) {
        return GetSpeed(GetAnimationData(clipName));
	}

    public float GetSpeed(AnimationData animData) {
        return animData.speed;
    }

    public float GetSpeed() {
        return animator.speed;
    }

    public float GetNormalizedSpeed(AnimationClip clip) {
        return GetNormalizedSpeed(GetAnimationData(clip));
    }

    public float GetNormalizedSpeed(string clipName) {
        return GetNormalizedSpeed(GetAnimationData(clipName));
    }

    public float GetNormalizedSpeed(AnimationData animData) {
        return animData.normalizedSpeed;
	}
	
	public bool GetMirror(){
		return currentMirror;
	}

	public void SetMirror(bool toggle){
		SetMirror(toggle, 0, false);
	}
	
	public void SetMirror(bool toggle, float blendingTime){
		SetMirror(toggle, blendingTime, false);
	}

	public void SetMirror(bool toggle, float blendingTime, bool forceMirror){
		if (currentMirror == toggle && !forceMirror) return;
		
		if (blendingTime == 0) blendingTime = defaultTransitionDuration;
		_playAnimation(currentAnimationData, blendingTime, GetCurrentClipPosition(), toggle);
	}
}