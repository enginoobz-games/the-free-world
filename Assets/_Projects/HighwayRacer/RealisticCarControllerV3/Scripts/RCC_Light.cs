﻿//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2021 BoneCracker Games
// http://www.bonecrackergames.com
// Buğra Özdoğanlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;

/// <summary>
/// General lighting system for vehicles. It has all kind of lights such as Headlight, Brake Light, Indicator Light, Reverse Light.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller/Light/RCC Light")]
public class RCC_Light : RCC_Core {
  // Getting an Instance of Main Shared RCC Settings.

  #region RCC Settings Instance

  private RCC_Settings RCCSettingsInstance;

  private RCC_Settings RCCSettings {
    get {
      if (RCCSettingsInstance == null) {
        RCCSettingsInstance = RCC_Settings.Instance;
        return RCCSettingsInstance;
      }

      return RCCSettingsInstance;
    }
  }

  #endregion

  internal RCC_CarControllerV3 carController;
  private Light _light;
  private Projector projector;
  private LensFlare lensFlare;
  private TrailRenderer trail;
  private Camera mainCamera;

  public float defaultIntensity = 0f;
  public float flareBrightness = 1.5f;
  private float finalFlareBrightness;

  public LightType lightType;

  public enum LightType {
    HeadLight,
    BrakeLight,
    ReverseLight,
    Indicator,
    ParkLight,
    HighBeamHeadLight,
    External
  };

  public float inertia = 1f;
  public Flare flare;

  public int refreshRate = 30;
  private float refreshTimer = 0f;

  private bool parkLightFound = false;
  private bool highBeamLightFound = false;

  // For Indicators.
  private RCC_CarControllerV3.IndicatorsOn indicatorsOn;
  private AudioSource indicatorSound;

  public AudioClip indicatorClip {
    get { return RCCSettings.indicatorClip; }
  }

  private void Start() {
    // Getting main car controller if light is attached to vehicle.
    carController = GetComponentInParent<RCC_CarControllerV3>();

    // Do not init the light if it's not attached to the vehicle (Used for trailers. Trailers have not main car controller script. Assigning car controller of the light when trailer is attached/detached.).
    if (carController)
      Init();
  }

  public void Init() {
    _light = GetComponent<Light>();
    _light.enabled = true;
    lensFlare = GetComponent<LensFlare>();
    trail = GetComponentInChildren<TrailRenderer>();

    if (lensFlare) {
      if (_light.flare != null)
        _light.flare = null;
    }

    if (RCCSettings.useLightProjectorForLightingEffect) {
      projector = GetComponent<Projector>();
      if (projector == null) {
        projector = ((GameObject) Instantiate(RCCSettings.projector, transform.position, transform.rotation))
          .GetComponent<Projector>();
        projector.transform.SetParent(transform, true);
      }

      projector.ignoreLayers = RCCSettings.projectorIgnoreLayer;
      if (lightType != LightType.HeadLight)
        projector.transform.localRotation = Quaternion.Euler(20f, transform.localPosition.z > 0f ? 0f : 180f, 0f);
      Material newMaterial = new Material(projector.material);
      projector.material = newMaterial;
    }

    if (RCCSettings.useLightsAsVertexLights) {
      _light.renderMode = LightRenderMode.ForceVertex;
      _light.cullingMask = 0;
    }
    else {
      if (lightType == LightType.HeadLight)
        _light.renderMode = LightRenderMode.ForcePixel;
      else
        _light.renderMode = LightRenderMode.ForceVertex;
    }

    if (lightType == LightType.Indicator) {
      if (!carController.transform.Find("All Audio Sources/Indicator Sound AudioSource"))
        indicatorSound = NewAudioSource(RCCSettings.audioMixer, carController.gameObject, "Indicator Sound AudioSource",
          1f, 3f, 1, indicatorClip, false, false, false);
      else
        indicatorSound = carController.transform.Find("All Audio Sources/Indicator Sound AudioSource")
          .GetComponent<AudioSource>();
    }

    RCC_Light[] allLights = carController.GetComponentsInChildren<RCC_Light>();

    for (int i = 0; i < allLights.Length; i++) {
      if (allLights[i].lightType == LightType.ParkLight)
        parkLightFound = true;

      if (allLights[i].lightType == LightType.HighBeamHeadLight)
        highBeamLightFound = true;
    }

    CheckRotation();
    CheckLensFlare();
  }

  void OnEnable() {
    if (!_light)
      _light = GetComponent<Light>();

    if (defaultIntensity == 0)
      defaultIntensity = _light.intensity;

    _light.intensity = 0f;
  }

  void Update() {
    if (!carController)
      return;

    if (RCCSettings.useLightProjectorForLightingEffect)
      Projectors();

    if (lensFlare)
      LensFlare();

    if (trail)
      Trail();

    switch (lightType) {
      case LightType.HeadLight:
        if (highBeamLightFound) {
          Lighting(carController.lowBeamHeadLightsOn ? defaultIntensity : 0f, 50f, 90f);
        }
        else {
          Lighting(carController.lowBeamHeadLightsOn ? defaultIntensity : 0f, 50f, 90f);

          if (!carController.lowBeamHeadLightsOn && !carController.highBeamHeadLightsOn)
            Lighting(0f);
          if (carController.lowBeamHeadLightsOn && !carController.highBeamHeadLightsOn) {
            Lighting(defaultIntensity, 50f, 90f);
            transform.localEulerAngles = new Vector3(10f, 0f, 0f);
          }
          else if (carController.highBeamHeadLightsOn) {
            Lighting(defaultIntensity, 100f, 45f);
            transform.localEulerAngles = new Vector3(0f, 0f, 0f);
          }
        }

        break;

      case LightType.BrakeLight:

        if (parkLightFound)
          Lighting(carController.brakeInput >= .1f ? defaultIntensity : 0f);
        else
          Lighting(carController.brakeInput >= .1f ? defaultIntensity : !carController.lowBeamHeadLightsOn ? 0f : .25f);
        break;

      case LightType.ReverseLight:
        Lighting(carController.direction == -1 ? defaultIntensity : 0f);
        break;

      case LightType.ParkLight:
        Lighting((!carController.lowBeamHeadLightsOn ? 0f : defaultIntensity));
        break;

      case LightType.Indicator:
        indicatorsOn = carController.indicatorsOn;
        Indicators();
        break;

      case LightType.HighBeamHeadLight:
        Lighting(carController.highBeamHeadLightsOn ? defaultIntensity : 0f, 200f, 45f);
        break;
    }
  }

  void Lighting(float input) {
    if (input >= .05f)
      _light.intensity = Mathf.Lerp(_light.intensity, input, Time.deltaTime * inertia * 20f);
    else
      _light.intensity = 0f;
  }

  void Lighting(float input, float range, float spotAngle) {
    if (input >= .05f)
      _light.intensity = Mathf.Lerp(_light.intensity, input, Time.deltaTime * inertia * 20f);
    else
      _light.intensity = 0f;

    _light.range = range;
    _light.spotAngle = spotAngle;
  }

  void Indicators() {
    Vector3 relativePos = carController.transform.InverseTransformPoint(transform.position);

    switch (indicatorsOn) {
      case RCC_CarControllerV3.IndicatorsOn.Left:

        if (relativePos.x > 0f) {
          Lighting(0);
          break;
        }

        if (carController.indicatorTimer >= .5f) {
          Lighting(0);
          if (indicatorSound.isPlaying)
            indicatorSound.Stop();
        }
        else {
          Lighting(defaultIntensity);
          if (!indicatorSound.isPlaying && carController.indicatorTimer <= .05f)
            indicatorSound.Play();
        }

        if (carController.indicatorTimer >= 1f)
          carController.indicatorTimer = 0f;
        break;

      case RCC_CarControllerV3.IndicatorsOn.Right:

        if (relativePos.x < 0f) {
          Lighting(0);
          break;
        }

        if (carController.indicatorTimer >= .5f) {
          Lighting(0);
          if (indicatorSound.isPlaying)
            indicatorSound.Stop();
        }
        else {
          Lighting(defaultIntensity);
          if (!indicatorSound.isPlaying && carController.indicatorTimer <= .05f)
            indicatorSound.Play();
        }

        if (carController.indicatorTimer >= 1f)
          carController.indicatorTimer = 0f;
        break;

      case RCC_CarControllerV3.IndicatorsOn.All:

        if (carController.indicatorTimer >= .5f) {
          Lighting(0);
          if (indicatorSound.isPlaying)
            indicatorSound.Stop();
        }
        else {
          Lighting(defaultIntensity);
          if (!indicatorSound.isPlaying && carController.indicatorTimer <= .05f)
            indicatorSound.Play();
        }

        if (carController.indicatorTimer >= 1f)
          carController.indicatorTimer = 0f;
        break;

      case RCC_CarControllerV3.IndicatorsOn.Off:

        Lighting(0);
        carController.indicatorTimer = 0f;
        break;
    }
  }

  private void Projectors() {
    if (!_light.enabled) {
      projector.enabled = false;
      return;
    }
    else {
      projector.enabled = true;
    }

    projector.material.color = _light.color * (_light.intensity / 5f);

    projector.farClipPlane = Mathf.Lerp(10f, 40f, (_light.range - 50) / 150f);
    projector.fieldOfView = Mathf.Lerp(40f, 30f, (_light.range - 50) / 150f);
  }

  private void LensFlare() {
    if (refreshTimer > (1f / refreshRate)) {
      refreshTimer = 0f;

      if (!mainCamera)
        mainCamera = RCC_SceneManager.Instance.activeMainCamera;

      if (!mainCamera)
        return;

      float distanceTocam = Vector3.Distance(transform.position, mainCamera.transform.position);
      float angle = 1f;

      if (lightType != LightType.External)
        angle = Vector3.Angle(transform.forward, mainCamera.transform.position - transform.position);

      if (angle != 0)
        finalFlareBrightness = flareBrightness * (4f / distanceTocam) * ((300f - (3f * angle)) / 300f) / 3f;

      lensFlare.brightness = finalFlareBrightness * _light.intensity;
      lensFlare.color = _light.color;
    }

    refreshTimer += Time.deltaTime;
  }

  private void Trail() {
    trail.emitting = _light.intensity > .1f ? true : false;
    trail.startColor = _light.color;
  }

  private void CheckRotation() {
    Vector3 relativePos = carController.transform.InverseTransformPoint(transform.position);

    if (relativePos.z > 0f) {
      if (Mathf.Abs(transform.localRotation.y) > .5f)
        transform.localRotation = Quaternion.identity;
    }
    else {
      if (Mathf.Abs(transform.localRotation.y) < .5f)
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }
  }

  private void CheckLensFlare() {
    if (transform.GetComponent<LensFlare>() == null) {
      gameObject.AddComponent<LensFlare>();
      LensFlare lf = gameObject.GetComponent<LensFlare>();
      lf.brightness = 0f;
      lf.color = Color.white;
      lf.fadeSpeed = 20f;
    }

    if (gameObject.GetComponent<LensFlare>().flare == null)
      gameObject.GetComponent<LensFlare>().flare = flare;

    gameObject.GetComponent<Light>().flare = null;
  }

  void Reset() {
    CheckRotation();
    CheckLensFlare();
  }
}