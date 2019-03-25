using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    // Used as key in Dictionary
    public struct MultipassCamera : IEquatable<MultipassCamera>
    {
        private Camera m_Camera;
        private int    m_PassId;

        public MultipassCamera(Camera camera)
        {
            m_Camera = camera;
            m_PassId = -1;
        }

        public MultipassCamera(Camera camera, int passId)
        {
            m_Camera = camera;
            m_PassId = passId;
        }

        public Camera camera { get { return m_Camera; } }
        public int    passId { get { return m_PassId; } }

        public bool Equals(MultipassCamera other)
        {
            return passId == other.passId && camera == other.camera;
        }

        public override bool Equals(object obj)
        {
            if (obj is MultipassCamera)
                return Equals((MultipassCamera)obj);

            return false;
        }

        public static bool operator == (MultipassCamera x, MultipassCamera y)
        {
            return x.Equals(y);
        }

        public static bool operator != (MultipassCamera x, MultipassCamera y)
        {
            return !(x == y);
        }

        public override int GetHashCode()
        {
            int hash = 13;

            unchecked
            {
                hash = hash * 23 + passId;
                if (camera != null)
                    hash = hash * 23 + camera.GetHashCode();
            }

            return hash;
        }
    }
}
