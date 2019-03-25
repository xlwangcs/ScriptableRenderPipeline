// XRSystem is where information about XR views and passes are read from 2 exclusive sources:
// - XRDisplaySubsystem from the XR SDK
// - or the 'legacy' C++ stereo rendering path and XRSettings

// XRTODO(2019.3) Deprecate legacy code
// XRTODO(2020.1) Remove legacy code
#if UNITY_2019_2_OR_NEWER
    #define USE_XR_SDK
#endif

using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
#if USE_XR_SDK
using UnityEngine.Experimental.XR;
#endif

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    // XRTODO: culling from XR SDK
    public class XRPass
    {
        internal bool enabled { get { return views != null; } }
        internal bool xrSdkEnabled;

        // Ability to specify where to render the pass
        internal RenderTargetIdentifier  renderTarget;
        internal RenderTextureDescriptor renderTargetDesc;
        static RenderTargetIdentifier    invalidRT = -1;
        internal bool                    renderTargetValid { get { return renderTarget != invalidRT; } }

        // Access to view information
        internal Matrix4x4 GetProjMatrix(int viewIndex = 0) { return views[viewIndex].projMatrix; }
        internal Matrix4x4 GetViewMatrix(int viewIndex = 0) { return views[viewIndex].viewMatrix; }
        internal Rect GetViewport(int viewIndex = 0)        { return views[viewIndex].viewport; }

        // Instanced views support (instanced draw calls or multiview extension)
        internal List<XRView> views = null;
        internal int viewCount { get { return enabled ? views.Count : 0; } }
        internal bool instancingEnabled { get { return viewCount > 1; } }

        // Legacy multipass support
        internal int  legacyMultipassEye      { get { return (int)views[0].legacyStereoEye; } }
        internal bool legacyMultipassEnabled  { get { return enabled && !instancingEnabled && legacyMultipassEye >= 0; } }

        internal static XRPass Create()
        {
            XRPass passInfo = GenericPool<XRPass>.Get();

            passInfo.views = ListPool<XRView>.Get();
            passInfo.renderTarget = invalidRT;
            passInfo.renderTargetDesc = default;
            passInfo.xrSdkEnabled = false;

            return passInfo;
        }

#if USE_XR_SDK
        internal static XRPass Create(XRDisplaySubsystem.XRRenderPass xrRenderPass)
        {
            XRPass passInfo = GenericPool<XRPass>.Get();

            passInfo.views = ListPool<XRView>.Get();
            passInfo.renderTarget = xrRenderPass.renderTarget;
            passInfo.renderTargetDesc = xrRenderPass.renderTargetDesc;
            passInfo.xrSdkEnabled = true;

            return passInfo;
        }
#endif

        internal static void Release(XRPass xrPass)
        {
            foreach (var xrView in xrPass.views)
            {
                GenericPool<XRView>.Release(xrView);
            }

            ListPool<XRView>.Release(xrPass.views);
            GenericPool<XRPass>.Release(xrPass);
        }

        internal void AddView(XRView xrView)
        {
            views.Add(xrView);

            // Validate memory limitations
            Debug.Assert(views.Count <= TextureXR.kMaxSliceCount);
        }

        internal void StartLegacyStereo(Camera camera, CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            if (enabled && camera.stereoEnabled)
            {
                // Reset scissor and viewport for C++ stereo code
                cmd.DisableScissorRect();
                cmd.SetViewport(camera.pixelRect);

                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (legacyMultipassEnabled)
                    renderContext.StartMultiEye(camera, legacyMultipassEye);
                else
                    renderContext.StartMultiEye(camera);
            }
        }

        internal void StopLegacyStereo(Camera camera, CommandBuffer cmd, ScriptableRenderContext renderContext)
        {
            if (enabled && camera.stereoEnabled)
            {
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                renderContext.StopMultiEye(camera);
            }
        }

        internal void EndCamera(Camera camera, ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            if (!enabled)
                return;

            if (xrSdkEnabled)
            {
                // XRTODO: mirror view
            }
            else
            {
                renderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Pushes to XR headset and/or display mirror
                if (legacyMultipassEnabled)
                    renderContext.StereoEndRender(camera, legacyMultipassEye, legacyMultipassEye == 1);
                else
                    renderContext.StereoEndRender(camera);
            }
        }
    }

    internal class XRView
    {
        internal Matrix4x4 projMatrix;
        internal Matrix4x4 viewMatrix;
        internal Rect viewport;
        internal Mesh occlusionMesh;
        internal Camera.StereoscopicEye legacyStereoEye;

        internal static XRView Create(Camera camera, Camera.StereoscopicEye eye)
        {
            XRView xrView = GenericPool<XRView>.Get();

            xrView.projMatrix = camera.GetStereoProjectionMatrix(eye);
            xrView.viewMatrix = camera.GetStereoViewMatrix(eye);
            xrView.viewport = camera.pixelRect;
            xrView.occlusionMesh = null;
            xrView.legacyStereoEye = eye;

            return xrView;
        }

#if USE_XR_SDK
        internal static XRView Create(XRDisplaySubsystem.XRRenderParameter renderParameter)
        {
            XRView xrView = GenericPool<XRView>.Get();

            xrView.projMatrix = renderParameter.projection;
            xrView.viewMatrix = renderParameter.view;
            xrView.viewport = renderParameter.viewport;
            xrView.occlusionMesh = renderParameter.occlusionMesh;
            xrView.legacyStereoEye = (Camera.StereoscopicEye)(-1);

            return xrView;
        }
#endif
    }

    public static class XRSystem
    {
        static XRPass s_EmptyPass = new XRPass();
        static List<XRPass> s_PassList = new List<XRPass>();

#if USE_XR_SDK
        static List<XRDisplaySubsystem> s_DisplayList = new List<XRDisplaySubsystem>();
#endif

        internal static XRPass GetPass(int passId)
        {
            if (passId < 0)
                return s_EmptyPass;

            return s_PassList[passId];
        }

        internal static void SetupFrame(Camera[] cameras, ref List<MultipassCamera> multipassCameras)
        {
            bool xrSdkActive = false;

#if USE_XR_SDK
            // Refresh XR displays
            SubsystemManager.GetInstances(s_DisplayList);

            // XRTODO: bind cameras to XR displays (only display 0 is used for now)
            XRDisplaySubsystem xrDisplay = null;
            if (s_DisplayList.Count > 0)
            {
                xrDisplay = s_DisplayList[0];
                xrDisplay.disableLegacyRenderer = true;
                xrSdkActive = true;
            }
#endif

            // Validate current state
            Debug.Assert(s_PassList.Count == 0, "XRSystem.ReleaseFrame() was not called!");
            Debug.Assert(!(xrSdkActive && XRGraphics.enabled), "The legacy C++ stereo rendering path must be disabled with XR SDK! Go to Project Settings --> Player --> XR Settings");
            
            foreach (var camera in cameras)
            {
                bool xrEnabled = xrSdkActive || (camera.stereoEnabled && XRGraphics.enabled);

                // XRTODO: support render to texture
                if (camera.cameraType != CameraType.Game || camera.targetTexture != null || !xrEnabled)
                {
                    multipassCameras.Add(new MultipassCamera(camera));
                    continue;
                }

#if USE_XR_SDK
                if (xrSdkActive)
                {
                    for (int renderPassIndex = 0; renderPassIndex < xrDisplay.GetRenderPassCount(); ++renderPassIndex)
                    {
                        xrDisplay.GetRenderPass(renderPassIndex, out var renderPass);

                        if (CanUseInstancing(camera, renderPass))
                        {
                            // XRTODO: instanced views support with XR SDK
                        }
                        else
                        {
                            for (int renderParamIndex = 0; renderParamIndex < renderPass.GetRenderParameterCount(); ++renderParamIndex)
                            {
                                renderPass.GetRenderParameter(camera, renderParamIndex, out var renderParam);

                                var xrPass = XRPass.Create(renderPass);
                                var xrView = XRView.Create(renderParam);
                                xrPass.AddView(xrView);

                                AddPassToFrame(xrPass, camera, ref multipassCameras);
                            }
                        }
                    }
                }
                else 
#endif
                {
                    if (XRGraphics.stereoRenderingMode == XRGraphics.StereoRenderingMode.MultiPass)
                    {
                        for (int passIndex = 0; passIndex < 2; ++passIndex)
                        {
                            var xrPass = XRPass.Create();
                            var xrView = XRView.Create(camera, (Camera.StereoscopicEye)passIndex);
                            xrPass.AddView(xrView);
                            
                            AddPassToFrame(xrPass, camera, ref multipassCameras);
                        }
                    }
                    else
                    {
                        var xrPass = XRPass.Create();

                        for (int viewIndex = 0; viewIndex < 2; ++viewIndex)
                        {
                            var xrView = XRView.Create(camera, (Camera.StereoscopicEye)viewIndex);
                            xrPass.AddView(xrView);
                        }

                        AddPassToFrame(xrPass, camera, ref multipassCameras);
                    }
                }
            }
        }

        internal static void ReleaseFrame()
        {
            foreach (var xrPass in s_PassList)
            {
                XRPass.Release(xrPass);
            }

            s_PassList.Clear();
        }

        internal static void AddPassToFrame(XRPass passInfo, Camera camera, ref List<MultipassCamera> multipassCameras)
        {
            int passIndex = s_PassList.Count;
            s_PassList.Add(passInfo);
            multipassCameras.Add(new MultipassCamera(camera, passIndex));
        }

#if USE_XR_SDK
        internal static bool CanUseInstancing(Camera camera, XRDisplaySubsystem.XRRenderPass renderPass)
        {
            // XRTODO: instanced views support with XR SDK
            return false;

            // check viewCount > 1, valid texture array format and valid slice index
            // limit to 2 for now (until code fully fixed)
        }
#endif
    }
}
