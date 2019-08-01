﻿using UnityEngine;

namespace XFramework.Resource
{
    public class ResProgress : IProgress
    {
        private AsyncOperation[] m_Operations;

        public ResProgress(AsyncOperation[] asyncOperations)
        {
            m_Operations = asyncOperations;
        }

        public bool IsDone
        {
            get
            {
                foreach (var item in m_Operations)
                {
                    if (!item.isDone)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public float Progress
        {
            get
            {
                float p = 0;
                foreach (var item in m_Operations)
                {
                    p += item.progress;
                }
                return p / m_Operations.Length;
            }
        }
    }
}