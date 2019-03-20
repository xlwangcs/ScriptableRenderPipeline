using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Experimental.XR;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    public struct XRView
    {
        public RenderTargetIdentifier renderTarget;
        public RenderTextureDescriptor renderTargetDesc;
        
        public Matrix4x4 view;
        public Matrix4x4 proj;
        public Rect viewport;
        public Mesh occlusionMesh;

        public int textureArraySlice;
    }

    public struct XRPass
    {
        public XRPass(int renderPassIndex, XRDisplaySubsystem.XRRenderPass renderPass)
        {
            passIndex = renderPassIndex;
            cullingPassIndex = renderPass.cullingPassIndex;
            shouldFillOutDepth = renderPass.shouldFillOutDepth;
            views = new List<XRView>();
        }

        //public const int k_MaxViewCount = 4;
        //public fixed int viewIndices[k_MaxViewCount];
        //public int viewCount;
        public int passIndex;

        public bool instancingEnabled { get { return views.Count > 1; } }
        public bool multipassEnabled  { get { return passIndex > 0; } }


        // Move to hdCamera?
        public Matrix4x4 GetViewMatrix(int viewIndex = 0)
        {
            // XR SDK version
            //if (xrDisplay.TryGetRenderParam(camera, renderPassIndex, renderParamIndex, out var renderParam))
            //{
            //    enderParam.view;
            //}

            // Legacy version
            //camera.GetStereoViewMatrix(eye);
        }

        public RenderTargetIdentifier renderTarget;
        public RenderTextureDescriptor renderTargetDesc;

        public Matrix4x4 view;
        public Matrix4x4 proj;
        public Rect viewport;
        public Mesh occlusionMesh;

        public int textureArraySlice;

        public int cullingPassIndex;
        public bool shouldFillOutDepth;

        // TODO: use pool ?
        public List<XRView> views;
    }

    public struct XRCamera
    {
        public XRCamera(Camera camera, XRPass xrPass)
        {
            this.camera = camera;
            this.xrPass = xrPass;
        }

        public Camera camera;
        public XRPass xrPass;
    }
}
