using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.XR;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public struct PassInfo
    {
        public XRDisplaySubsystem xrDisplay;
        public int renderPassIndex;
        public int renderParamIndex;

        public PassInfo(XRDisplaySubsystem xrDisplay, int renderPassIndex, int renderParamIndex)
        {
            this.xrDisplay = xrDisplay;
            this.renderPassIndex = renderPassIndex;
            this.renderParamIndex = renderParamIndex;
        }
    }

    public struct MultipassCamera
    {
        public Camera m_Camera;
        public PassInfo m_PassInfo;

        public MultipassCamera(Camera camera = null, PassInfo passInfo = default)
        {
            m_Camera = camera;
            m_PassInfo = passInfo;
        }

        public Camera camera { get { return m_Camera; } }
        public PassInfo passInfo { get { return m_PassInfo; } }

        static public List<MultipassCamera> SetupFrame(Camera[] cameras, XRDisplaySubsystem xrDisplay)
        {
            // TODO: use pool
            List<MultipassCamera> multipassCameras = new List<MultipassCamera>();
            foreach (var camera in cameras)
            {
                if (xrDisplay != null)
                {
                    for (int renderPassIndex = 0; renderPassIndex < xrDisplay.GetRenderPassCount(); ++renderPassIndex)
                    {
                        if (xrDisplay.TryGetRenderPass(renderPassIndex, out var renderPass))
                        {
                            for (int renderParamIndex = 0; renderParamIndex < xrDisplay.GetRenderParamCount(renderPassIndex); ++renderParamIndex)
                            {
                                if (xrDisplay.TryGetRenderParam(camera, renderPassIndex, renderParamIndex, out var renderParam))
                                {
                                    PassInfo passInfo = new PassInfo(xrDisplay, renderPassIndex, renderParamIndex);
                                    multipassCameras.Add(new MultipassCamera(camera, passInfo));
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (camera.stereoEnabled && XRGraphics.enabled && XRGraphics.stereoRenderingMode == XRGraphics.StereoRenderingMode.MultiPass)
                    {
                        for (int passIndex = 0; passIndex < 2; ++passIndex)
                        {
                            PassInfo passInfo = new PassInfo(xrDisplay, passIndex, -1);
                            multipassCameras.Add(new MultipassCamera(camera, passInfo));
                        }
                    }
                    else
                    {
                        multipassCameras.Add(new MultipassCamera(camera));
                    }
                }
            }

            return multipassCameras;
        }
    }
}
