using System;
using DV.UI.Requests;
using UnityEngine;
using UnityEngine.XR;
using VRTK;
using VRUtilitiesMod.UMM;

namespace VRUtilitiesMod {
    
    public class CameraZoomVR : MonoBehaviour
    {
        private CameraZoomTunnel tunnelOverlay;
        Loader.VRUtilitiesModSettings.CameraZoomVR Settings;

        bool leftControlerInitialized;
        bool rightControlerInitialized;
        private Transform controllerLeft;
        private Transform controllerRight;
        private VRTK_ControllerEvents controllerEventsLeft;
        private VRTK_ControllerEvents controllerEventsRight;
        private ComfortTunnelOverlay LocomotionTunnelOverlay;

        private float normalFOV = 1f;
        private float currentZoomVelocity;

        private bool disableZoomForced;
        private bool isZoomPressed;
        private RequestSystem requestSystem = new RequestSystem(1f, true, false);

        private void Start()
        {
            if (!VRManager.IsVREnabled())
            {
                Debug.LogError("Not in VR. CameraZoomVR Destroying script", this);
                UnityEngine.Object.Destroy(this);
                return;
            }

            Settings = UMM.Loader.Settings.CameraZoom;

            requestSystem.ValueChanged += delegate (float value)
            {
                disableZoomForced = value == 0f;
            };

            if (TransmogrifyControllers.IsControllerReadyLeft)
            {
                OnControlsSet(SDK_BaseController.ControllerHand.Left);
            }
            if (TransmogrifyControllers.IsControllerReadyRight)
            {
                OnControlsSet(SDK_BaseController.ControllerHand.Right);
            }
            if (!TransmogrifyControllers.IsControllerReadyLeft || !TransmogrifyControllers.IsControllerReadyRight)
            {
                SetupDeviceSpecificControls.DeviceSpecificControlsSet.Register(new Action<SDK_BaseController.ControllerHand>(OnControlsSet));
            }
            tunnelOverlay = PlayerManager.ActiveCamera.gameObject.AddComponent<CameraZoomTunnel>();
        }

        private void OnControlsSet(SDK_BaseController.ControllerHand hand)
        {
            VRTK_ControllerReference controllerReferenceForHand = VRTK_DeviceFinder.GetControllerReferenceForHand(hand);
            if (hand == SDK_BaseController.ControllerHand.Left)
            {
                controllerLeft = VRTK_DeviceFinder.GetControllerLeftHand(true).transform;
                controllerEventsLeft = controllerLeft.GetComponentInChildren<VRTK_ControllerEvents>();
                leftControlerInitialized = true;
            }
            else if (hand == SDK_BaseController.ControllerHand.Right)
            {
                controllerRight = VRTK_DeviceFinder.GetControllerRightHand(true).transform;
                controllerEventsRight = controllerRight.GetComponentInChildren<VRTK_ControllerEvents>();
                rightControlerInitialized = true;
            }
            else
            {
                Debug.LogError("Controller not initialized properly. Given hand must be left or right.", this);
            }

            if (controllerLeft != null && controllerRight != null)
            {
                SetupDeviceSpecificControls.DeviceSpecificControlsSet.Unregister(new Action<SDK_BaseController.ControllerHand>(OnControlsSet));
            }
        }

        private void OnDestroy()
        {
            XRDevice.fovZoomFactor = 1f;
            if (UnloadWatcher.isUnloading || UnloadWatcher.isQuitting) return;
            UnityEngine.Object.DestroyImmediate(tunnelOverlay);
        }

        public void RequestZoomDisable(object caller, float value, int priority = 0)
        {
            requestSystem.RequestValue(caller, value, priority);
        }

        public void RemoveZoomDisableRequest(object caller)
        {
            requestSystem.RemoveValue(caller);
        }

        private void Update()
        {
            if (!Settings.ZoomEnabled
                || (Settings.LeftRight == Loader.ControllerSide.Left ? !leftControlerInitialized : !rightControlerInitialized)
                || (Settings.Button == VRTK_ControllerEvents.ButtonAlias.Undefined
                    && Settings.Axis == VRTK_ControllerEvents.Vector2AxisAlias.Undefined))
            {
                return;
            }
            if (!disableZoomForced && IsZoomPressed())
            {
                isZoomPressed = true;
            }
            else if (isZoomPressed && (!KeyBindings.zoomKeys.IsPressed() || disableZoomForced))
            {
                isZoomPressed = false;
            }
            if (disableZoomForced)
            {
                if (XRDevice.fovZoomFactor < normalFOV)
                {
                    XRDevice.fovZoomFactor = normalFOV;
                    //effectSize = normalEffectSize;
                    //tunnelOverlay.SetEffect(effectSize, Settings.ComfortTunnerFeather);
                }
                return;
            }

            if (Time.deltaTime == 0) return;
            if (isZoomPressed && XRDevice.fovZoomFactor < Settings.ZoomFactor)
            {
                if (Mathf.Approximately(XRDevice.fovZoomFactor, Settings.ZoomFactor)) XRDevice.fovZoomFactor = Settings.ZoomFactor;
                XRDevice.fovZoomFactor = Mathf.SmoothDamp(XRDevice.fovZoomFactor, Settings.ZoomFactor, ref currentZoomVelocity, Settings.ZoomTime);
            }
            if (!isZoomPressed && XRDevice.fovZoomFactor > normalFOV)
            {
                if (Mathf.Approximately(XRDevice.fovZoomFactor, normalFOV)) XRDevice.fovZoomFactor = normalFOV;
                XRDevice.fovZoomFactor = Mathf.SmoothDamp(XRDevice.fovZoomFactor, normalFOV, ref currentZoomVelocity, Settings.ZoomTime);
            }
        }

        private bool IsZoomPressed()
        {
            VRTK_ControllerEvents controllerEvents = Settings.LeftRight == Loader.ControllerSide.Left ? controllerEventsLeft : controllerEventsRight;
            if (Settings.Button != VRTK_ControllerEvents.ButtonAlias.Undefined)
            {
                return controllerEvents.IsButtonPressed(Settings.Button);
            }
            else if (Settings.Axis != VRTK_ControllerEvents.Vector2AxisAlias.Undefined)
            {
                return controllerEvents.GetAxis(Settings.Axis).y > 0.75f;
            }
            return false;
        }
    }

    public class CameraZoomTunnel : MonoBehaviour
    {
        Loader.VRUtilitiesModSettings.CameraZoomVR Settings;

        protected virtual void Awake()
        {
            matCameraEffect = new Material(Resources.Load<Material>("TunnelOverlay_v2"));
            shaderPropertyColor = Shader.PropertyToID("_Color");
            shaderPropertyAV = Shader.PropertyToID("_AngularVelocity");
            shaderPropertyFeather = Shader.PropertyToID("_FeatherSize");
            shaderPropertySkyboxTexture = Shader.PropertyToID("_SecondarySkyBox");

            shaderPropertyFarPlaneMultiplier = Shader.PropertyToID("_FarPlaneMultiplier");
            shaderPropertyRadiusMultiplier = Shader.PropertyToID("_RadiusMultiplier");
            
            Settings = UMM.Loader.Settings.CameraZoom;
        }

        protected virtual void OnEnable()
        {
            headset = VRTK_DeviceFinder.HeadsetCamera();
            if (headset != null)
            {
                headsetCamera = headset.GetComponent<Camera>();
            }
            playarea = VRTK_DeviceFinder.PlayAreaTransform();
            originalAngularVelocity = matCameraEffect.GetFloat(shaderPropertyAV);
            originalFeatherSize = matCameraEffect.GetFloat(shaderPropertyFeather);
            originalColor = matCameraEffect.GetColor(shaderPropertyColor);
            originalFarPlaneMulti = matCameraEffect.GetFloat(shaderPropertyFarPlaneMultiplier);
            originalRadiusMultiplier = matCameraEffect.GetFloat(shaderPropertyRadiusMultiplier);
            CheckSkyboxTexture();
            if (effectSkybox != null)
            {
                originalSkyboxTexture = matCameraEffect.GetTexture(shaderPropertySkyboxTexture);
                matCameraEffect.SetTexture("_SecondarySkyBox", effectSkybox);
            }

            SetShaderFeather(effectColor, 0, Settings.ComfortTunnerFeather);
        }

        protected virtual void OnDisable()
        {
            headset = null;
            headsetCamera = null;
            playarea = null;

            matCameraEffect.SetTexture("_SecondarySkyBox", originalSkyboxTexture);
            originalSkyboxTexture = null;
            SetShaderFeather(originalColor, originalAngularVelocity, originalFeatherSize);
            matCameraEffect.SetColor(shaderPropertyColor, originalColor);

            if (createEffectSkybox)
            {
                effectSkybox = null;
                createEffectSkybox = false;
            }
        }

        protected virtual void OnDestroy()
        {
            VRTK_SDKManager.AttemptRemoveBehaviourToToggleOnLoadedSetupChange(this);
        }

        protected virtual void LateUpdate()
        {
            float fovZoomFactor = XRDevice.fovZoomFactor;

            if (!Settings.TunnelEnabled || fovZoomFactor == 0 || Time.deltaTime == 0) return;

            if (fovZoomFactor > lastFov)
            {
                if (Mathf.Approximately(targetEffectAmount, Settings.ComfortTunnelSize)) targetEffectAmount = Settings.ComfortTunnelSize;
                targetEffectAmount = Mathf.SmoothDamp(targetEffectAmount, Settings.ComfortTunnelSize, ref currentTunnelZoomVelocity, Settings.ZoomTime / 4);
            } 
            else if (fovZoomFactor < lastFov)
            {
                if (Mathf.Approximately(targetEffectAmount, normalEffectAmount)) targetEffectAmount = normalEffectAmount;
                targetEffectAmount = Mathf.SmoothDamp(targetEffectAmount, normalEffectAmount, ref currentTunnelZoomVelocity, Settings.ZoomTime);
            }

            lastFov = fovZoomFactor;
            SetShaderFeather(effectColor, targetEffectAmount, Settings.ComfortTunnerFeather);
            
            if (effectSkybox != null)
            {
                matCameraEffect.SetMatrixArray("_EyeToWorld", new Matrix4x4[]
                {
                headsetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse,
                headsetCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse
                });
                Matrix4x4[] eyeProjection = new Matrix4x4[]
                {
                headsetCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left),
                headsetCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right)
                };
                eyeProjection[0] = headsetCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                eyeProjection[1] = headsetCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                eyeProjection[0] = GL.GetGPUProjectionMatrix(eyeProjection[0], true).inverse;
                eyeProjection[1] = GL.GetGPUProjectionMatrix(eyeProjection[1], true).inverse;
                eyeProjection[0][1, 1] *= -1f;
                eyeProjection[1][1, 1] *= -1f;

                matCameraEffect.SetMatrixArray("_EyeProjection", eyeProjection);
            }
        }

        protected virtual void SetShaderFeather(Color givenTunnelColor, float givenAngularVelocity, float givenFeatherSize)
        {
            matCameraEffect.SetColor(shaderPropertyColor, givenTunnelColor);
            matCameraEffect.SetFloat(shaderPropertyAV, givenAngularVelocity);
            matCameraEffect.SetFloat(shaderPropertyFeather, givenFeatherSize);
            matCameraEffect.SetFloat(shaderPropertyFarPlaneMultiplier, farPlaneMulti);
            matCameraEffect.SetFloat(shaderPropertyRadiusMultiplier, radiusMultiplier);
        }

        protected virtual void CheckSkyboxTexture()
        {
            if (effectSkybox == null)
            {
                Cubemap cubemap = new Cubemap(1, TextureFormat.ARGB32, false);
                cubemap.SetPixel(CubemapFace.NegativeX, 0, 0, Color.white);
                cubemap.SetPixel(CubemapFace.NegativeY, 0, 0, Color.white);
                cubemap.SetPixel(CubemapFace.NegativeZ, 0, 0, Color.white);
                cubemap.SetPixel(CubemapFace.PositiveX, 0, 0, Color.white);
                cubemap.SetPixel(CubemapFace.PositiveY, 0, 0, Color.white);
                cubemap.SetPixel(CubemapFace.PositiveZ, 0, 0, Color.white);
                effectSkybox = cubemap;
                createEffectSkybox = true;
                return;
            }
            if (effectColor.r < 0.15f && (double)effectColor.g < 0.15 && (double)effectColor.b < 0.15)
            {
                VRTK_Logger.Warn("`VRTK_TunnelOverlay` has an `Effect Skybox` texture but the `Effect Color` is too dark which will tint the texture so it is not visible.");
            }
        }

        protected virtual void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (Settings.TunnelEnabled && lastFov != 1 && Time.deltaTime != 0)
                Graphics.Blit(src, dest, matCameraEffect);
        }

        protected virtual void OnGUI()
        {
            if (!Settings.DebugEnabled) return;
            GUILayout.BeginArea(new Rect(20, 20, 200, 200));
            GUILayout.Label($"ZoomEnabled: {Settings.ZoomEnabled}");
            GUILayout.Label($"zoomFovFactor: {XRDevice.fovZoomFactor}");
            GUILayout.Label($"lastFov: {lastFov}");
            GUILayout.Label($"ZoomFactor: {Settings.ZoomFactor}");
            GUILayout.Label($"targetEffectAmount: {targetEffectAmount}");
            GUILayout.Label($"TunnelEnabled: {Settings.TunnelEnabled}");
            GUILayout.Label($"ComfortTunnelSize: {Settings.ComfortTunnelSize}");
            GUILayout.Label($"ComfortTunnerFeather: {Settings.ComfortTunnerFeather}");
            GUILayout.Label($"ZoomTime: {Settings.ZoomTime}");
            GUILayout.Label($"LeftRight: {Settings.LeftRight}");
            GUILayout.Label($"Button: {Settings.Button}");
            GUILayout.Label($"Axis: {Settings.Axis}");
        }


        private float lastFov = 1;
        private float targetEffectAmount = 0f;
        private float currentTunnelZoomVelocity;
        private const float normalEffectAmount = 0;

        public float farPlaneMulti = 16.7f;
        public float radiusMultiplier = 1f;

        public Color effectColor = Color.black;
        
        public Texture effectSkybox;
        protected Transform headset;
        protected Camera headsetCamera;
        protected Transform playarea;
        protected Material matCameraEffect;
        protected bool createEffectSkybox;

        protected Color originalColor;
        protected float originalAngularVelocity;
        protected float originalFeatherSize;
        protected Texture originalSkyboxTexture;
        protected float originalFarPlaneMulti;
        protected float originalRadiusMultiplier;

        protected int shaderPropertyColor;
        protected int shaderPropertyAV;
        protected int shaderPropertyFeather;
        protected int shaderPropertySkyboxTexture;
        private int shaderPropertyRadiusMultiplier;
        private int shaderPropertyFarPlaneMultiplier;
    }
}