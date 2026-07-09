using System;
using System.Collections;

namespace GH.Loading
{
    public interface ILoadingTask
    {
        string Description { get; }
        float Weight { get; }
        IEnumerator Run(Action<float> reportProgress);
    }
}
