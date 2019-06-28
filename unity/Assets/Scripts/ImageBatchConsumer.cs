using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class RenderBatch
{
    public class CameraBatch
    {
        public enum RenderType
        {
            RGB=0, DEPTH, NORMALS, SEGMENTATION
        }
        // mapping from render type to rendered camera images
        public Dictionary<RenderType, List<Texture2D>> images_ = new Dictionary<RenderType, List<Texture2D>>();
    }

    // mapping from camera name to CameraBatch instance
    public Dictionary<string, CameraBatch> camera_batches_ = new Dictionary<string, CameraBatch>();
}

// Interface for all the consumers that receive batches of rendered images.
public interface IImageBatchConsumer {

    void ConsumeImageBatch(RenderBatch batch);
}
