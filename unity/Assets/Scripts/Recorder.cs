using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.IO;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering;

// The Recorder works in server / offline mode. It is responsible for
// preparing offscreen buffers that are used for batch rendering, issuing
// the actual render call and finally pulling the images from the GPU.
// To avoid GPU/CPU stalls and unnecessary data dependencies, there is
// a long pipeline of images in flight. The Recorder keeps a large number
// of render targets and destination textures, that are accessed in
// a round robin fashion.
//
// Configurable flags:
//   int capture_width - default width for the captured image,
//   int capture_height - default height for the captured image,
//   bool capture_alpha - should capture transparency by default,
//   int capture_fps - deprecated,
//   float speedup - deprecated,
//   int batch_size - default batch size.

public class Recorder : MonoBehaviour {

    [SerializeField]
    [Flag]
    public int capture_width_ = 512;

    [SerializeField]
    [Flag]
    public int capture_height_ = 512;

    [SerializeField]
    [Flag]
    public bool capture_alpha_ = false;

    [SerializeField]
    [Flag]
    public int capture_fps_ = 30;

    [SerializeField]
    [Flag]
    public float speedup_ = 10.0f;

    [SerializeField]
    [Flag]
    public int batch_size_ = 200;

    [SerializeField]
    public IImageBatchConsumer batch_consumer_ = null;

    public RenderTexture last_render_ = null;

    private int render_texture_rgb_index_ = -1;
    private int render_texture_depth_index_ = -1;
    private int capture_count_ = 0;
    private int capture_total_count_ = 0;
    private bool capture_next_frame_ = false;
    private int batch_count_ = 0;
    private bool capture_depth_ = false;
    private bool capture_normals_ = false;

    // This structure holds the batch textures for a single camera that
    // is capturing.
    [System.Serializable]
    public class CameraSetup { 

        public enum CameraType {
            RGB = 0, DEPTH_NORMALS, SEGMENTATION,
        }

        public Camera camera_ = null;
        public List<Texture2D> captured_images_ = new List<Texture2D>();
        public CameraType camera_type_ = CameraType.RGB;
        public string camera_name_ = null;
        private int width_ = 0;
        private int height_ = 0;
        private int batch_size_ = 0;
        private bool alpha_ = false;

        public CameraSetup(Camera camera, int batch_size, int width, int height,
                           bool alpha, CameraType camera_type) {
            width_ = width;
            height_ = height;
            batch_size_ = batch_size;
            alpha_ = alpha;
            camera_type_ = camera_type;
            camera_name_ = camera.name;
            camera_ = PrepareCamera(camera);

            ResetBatch(camera, batch_size, width, height, alpha, camera_type);
        }

        // Set the current render texture.
        public void UpdateRenderTexture(RenderTexture next_render_texture) {
            camera_.targetTexture = next_render_texture;
        }

        // Issue framebuffer to texture asynchronous blit.
        public RenderTexture CaptureRenderTexture(int index) {
            Texture2D next_capture = captured_images_[index];
            RenderTexture.active = camera_.targetTexture;
            next_capture.ReadPixels(new Rect(0, 0, next_capture.width, next_capture.height), 0, 0);
            return camera_.targetTexture;
        }

        // Commit and apply any not commited textures, this might be blocking,
        // so do it at the end, when the whole batch is ready and data stalls
        // would be least significant.
        public List<Texture2D> ApplyAndGetImages() {
            foreach (Texture2D texture in captured_images_) {
                texture.Apply();
            }

            List<Texture2D> images = new List<Texture2D>();
            for (int i = 0; i < batch_size_; ++i) {
                images.Add(captured_images_[i]);
            }
            return images;
        }

        // Make sure we have enough of big enough render textures for new
        // batch parameters.
        public void ResetBatch(Camera camera, int batch_size, int width, int height,
                               bool alpha, CameraSetup.CameraType camera_type) {
            if (width != width_ || height != height_ || alpha != alpha_) {
                captured_images_.Clear();
                width_ = width;
                height_ = height;
                alpha_ = alpha;
            }

            if (camera_type_ != camera_type || camera.name != camera_name_) {
                camera_type_ = camera_type;
                camera_name_ = camera.name;
                camera_ = PrepareCamera(camera);
            }

            batch_size_ = batch_size;

            if (captured_images_.Count > 2 * batch_size && batch_size >= 32) {
                captured_images_.RemoveRange(batch_size, captured_images_.Count - batch_size);
                return;
            }

            while (captured_images_.Count < batch_size) {
                TextureFormat format;
                if (camera_type_ == CameraType.DEPTH_NORMALS) {
                    format = TextureFormat.RGBAFloat;
                } else {
                    format = alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
                }
                Texture2D capture_image = new Texture2D(width, height, format, false);
                captured_images_.Add(capture_image);
            }
        }

        public void CleanUp() {
            captured_images_.Clear();
            batch_size_ = 0;
        }

        private Camera PrepareCamera(Camera camera) {
            // Save original camera name since it is modified for segmentation
            if (camera_type_ != CameraType.RGB) {
                return CreateSegmentationCamera(camera);
            } else {
                return camera;
            }
        }

        private Camera CreateSegmentationCamera(Camera camera_template) {
            Camera cam = Instantiate(camera_template);

            cam.name = camera_template.name + " (Segmentation)";
            cam.transform.parent = camera_template.transform;
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.transform.localScale = Vector3.one;

            Destroy(cam.GetComponent<PostProcessVolume>());
            Destroy(cam.GetComponent<PostProcessLayer>());

            var cb = new CommandBuffer();
            switch (camera_type_) {
                case CameraType.SEGMENTATION:
                    cb.SetGlobalFloat("_OutputMode", 0);
                    break;
                case CameraType.DEPTH_NORMALS:
                    cb.SetGlobalFloat("_OutputMode", 1);
                    break;
                default:
                    break;
            }
            cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
            cam.AddCommandBuffer(CameraEvent.BeforeFinalPass, cb);
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;

            cam.renderingPath = RenderingPath.Forward;
            cam.SetReplacementShader(Shader.Find("Hidden/LabelShader"), "");

            return cam;
        }
    };

    public Dictionary<CameraSetup.CameraType, List<CameraSetup>> camera_setups_ = new Dictionary<CameraSetup.CameraType, List<CameraSetup>>();
    // RGB (and optionally Alpha, so RGBA) textures used for appearance
    public List<RenderTexture> render_textures_rgb_ = new List<RenderTexture>();
    // Depth textures used for depth and surface normals
    public List<RenderTexture> render_textures_depth_ = new List<RenderTexture>();

    // Use this for initialization
    void Start() {
        Flags.InitFlags(this, "recorder");
    }

    public bool Initialize(IImageBatchConsumer batch_consumer) {
        Screen.SetResolution(capture_width_, capture_height_, false);
        Time.captureFramerate = (int)(speedup_ * capture_fps_);
        Time.timeScale = speedup_;
        batch_consumer_ = batch_consumer;

        PrepareRenderTextures();

        ResetBatch(new List<Camera>(), batch_size_, capture_width_, capture_height_, capture_alpha_,
                   false, false, false);

        StartCoroutine(RenderCaptureHook());

        Logger.Info("Recorder::Initialize::Capture ready.");
        return true;
    }

    public void ResetBatch(IList<Camera> capture_cameras, int batch_size, int width, int height, bool alpha,
                           bool depth, bool normals, bool segmentation) {
        HashSet<Recorder.CameraSetup.CameraType> camera_types = new HashSet<Recorder.CameraSetup.CameraType>();
        camera_types.Add(Recorder.CameraSetup.CameraType.RGB);
        if (depth || normals) {
            camera_types.Add(Recorder.CameraSetup.CameraType.DEPTH_NORMALS);
        } 
        if (segmentation) {
            camera_types.Add(Recorder.CameraSetup.CameraType.SEGMENTATION);
        }

        foreach (CameraSetup.CameraType camera_type in System.Enum.GetValues(typeof(CameraSetup.CameraType))) {
            if (!camera_types.Contains(camera_type)) {
                camera_setups_.Remove(camera_type);
                continue;
            }
            if (!camera_setups_.ContainsKey(camera_type)) {
                camera_setups_.Add(camera_type, new List<CameraSetup>());
            }

            for (int i = 0; i < capture_cameras.Count; ++i) {
                if (camera_setups_[camera_type].Count < i + 1) {
                    camera_setups_[camera_type].Add(new CameraSetup(capture_cameras[i], batch_size,
                                                                    width, height, alpha, camera_type));
                } else {
                    camera_setups_[camera_type][i].ResetBatch(capture_cameras[i], batch_size,
                                                              width, height, alpha, camera_type);
                }
            }
        }

        if (capture_width_ != width || capture_height_ != height || capture_alpha_ != alpha) {
            capture_width_ = width;
            capture_height_ = height;
            capture_alpha_ = alpha;
            PrepareRenderTextures();
        }
        capture_depth_ = depth;
        capture_normals_ = normals;

        batch_size_ = batch_size;
        RoundRobinRenderTextures();

        capture_count_ = 0;
        capture_next_frame_ = false;
    }

    private void PrepareRenderTextures() {
        PrepareRenderTextures(30, render_textures_rgb_, RenderTextureFormat.Default);
        PrepareRenderTextures(30, render_textures_depth_, RenderTextureFormat.ARGBFloat);
    }

    // Prepare a pool of render textures.
    private void PrepareRenderTextures(int rt_count, List<RenderTexture> texture_list, RenderTextureFormat format) {
        texture_list.Clear();

        for (int i = 0; i < rt_count; ++i) {
            RenderTexture texture = new RenderTexture(capture_width_, capture_height_, 24,
                                                      format, RenderTextureReadWrite.sRGB);
            texture.Create();
            texture_list.Add(texture);
        }
    }

    // Cycle through the cameras and the pool of render textures in a round
    // robin fashion (to avoid data stalls).
    private void RoundRobinRenderTextures() {
        foreach (List<CameraSetup> camera_setups in camera_setups_.Values) {
            foreach (CameraSetup camera_setup in camera_setups) {
                camera_setup.UpdateRenderTexture(NextRenderTexture(camera_setup.camera_type_));
            }
        }
    }

    private RenderTexture NextRenderTexture(CameraSetup.CameraType camera_type) {
        if (camera_type == CameraSetup.CameraType.DEPTH_NORMALS) {
            render_texture_depth_index_ = (render_texture_depth_index_ + 1) % render_textures_depth_.Count;
            return render_textures_depth_[render_texture_depth_index_];
        } else {
            render_texture_rgb_index_ = (render_texture_rgb_index_ + 1) % render_textures_rgb_.Count;
            return render_textures_rgb_[render_texture_rgb_index_];
        }
    }

    // Issue render request to all capturing cameras.
    public void Capture() {
        // We will be capturing this frame. Flip a flag to let the coroutine know.
        capture_next_frame_ = true;
        foreach (List<CameraSetup> camera_setups in camera_setups_.Values) {
            foreach (CameraSetup camera_setup in camera_setups) {
                camera_setup.camera_.Render();
            }
        }
    }

    // This coroutine is hooked to the event that notifies when the rendering
    // (which is asynchronous) is done.
    public IEnumerator RenderCaptureHook() {
        while (true) {
            yield return new WaitForEndOfFrame();

            // Are we capturing this frame?
            if (!capture_next_frame_) {
                continue;
            }
            capture_next_frame_ = false;

            CaptureRenderTextures();
            RoundRobinRenderTextures();

            // The whole batch is done, apply, DMA and inform the consumers.
            if (capture_count_ == batch_size_) {
                ProcessBatch();
            }
        }
    }


    private void CaptureRenderTextures() {
        foreach (List<CameraSetup> camera_setups in camera_setups_.Values) {
            foreach (CameraSetup camera_setup in camera_setups) {
                last_render_ = camera_setup.CaptureRenderTexture(capture_count_);
            }
        }
        capture_count_++;
        capture_total_count_++;
    }

    private void ProcessBatch() {
        RenderBatch batch = new RenderBatch();

        foreach (List<CameraSetup> camera_setups in camera_setups_.Values) {
            foreach (CameraSetup camera_setup in camera_setups) {
                RenderBatch.CameraBatch camera_batch;
                if (batch.camera_batches_.ContainsKey(camera_setup.camera_name_)) {
                    camera_batch = batch.camera_batches_[camera_setup.camera_name_];
                }
                else {
                    camera_batch = new RenderBatch.CameraBatch();
                    batch.camera_batches_.Add(camera_setup.camera_name_, camera_batch);
                }

                switch (camera_setup.camera_type_) {
                    case CameraSetup.CameraType.RGB:
                        camera_batch.images_.Add(RenderBatch.CameraBatch.RenderType.RGB, camera_setup.ApplyAndGetImages());
                        break;
                    case CameraSetup.CameraType.SEGMENTATION:
                        camera_batch.images_.Add(RenderBatch.CameraBatch.RenderType.SEGMENTATION, camera_setup.ApplyAndGetImages());
                        break;
                    case CameraSetup.CameraType.DEPTH_NORMALS:
                        List<Texture2D> images = camera_setup.ApplyAndGetImages();
                        if (capture_depth_) {
                            camera_batch.images_.Add(RenderBatch.CameraBatch.RenderType.DEPTH, images);
                        }
                        if (capture_normals_) {
                            camera_batch.images_.Add(RenderBatch.CameraBatch.RenderType.NORMALS, images);
                        }
                        break;
                }
            }
        }

        batch_consumer_.ConsumeImageBatch(batch);

        capture_count_ = 0;
        batch_count_++;
    }
}
