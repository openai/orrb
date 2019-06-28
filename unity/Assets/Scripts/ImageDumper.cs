using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// ImageDumper consumes rendered batches and saves them to disk as
// images. It uses Parallel.ForEach to speed up compute and I/O bound
// operations.
//
// Configurable flags:
//   int max_worker_threads - how many images to process in parallel,
//   int offset - start with this number when naming images,
//   string dir - save dir relative to CWD,
//   enum format - PNG, JPG or EXR,

public class ImageDumper : MonoBehaviour, IImageBatchConsumer {

    public enum Format {
        PNG, JPEG, EXR
    };

    [SerializeField]
    [Flag]
    public int max_worker_threads_ = 10;

    [SerializeField]
    [Flag]
    public int offset_ = 0;

    [SerializeField]
    [Flag]
    public string dir_ = "output";

    [SerializeField]
    [Flag]
    public Format format_ = Format.PNG;

    [HideInInspector]
    public int batch_count_ = 0;

    private Dictionary<string, int> offsets_ = new Dictionary<string, int>();

    // Use this for initialization
    void Start() {
        Flags.InitFlags(this, "image_dumper");
    }

    public bool Initialize() {
        Directory.CreateDirectory(dir_);
        return true;
    }

    private class ImageToSave {
        public Texture2D image_ = null;
        public string path_ = "";

        public ImageToSave(Texture2D image, string path) {
            image_ = image;
            path_ = path;
        }
    }

    public void ConsumeImageBatch(RenderBatch batch) {
        List<ImageToSave> flat_batch = PrepareBatch(batch);

        Parallel.ForEach(
            flat_batch, GetParallelOptions(),
            image_to_save => {
                byte[] bytes = Encode(image_to_save.image_);
                using (FileStream stream = new FileStream(image_to_save.path_, FileMode.Create, FileAccess.Write)) {
                    stream.Write(bytes, 0, bytes.Length);
                }
            });
        batch_count_++;
    }

    private byte[] Encode(Texture2D image) {
        switch (format_) {
        case Format.PNG:
            return image.EncodeToPNG();
        case Format.JPEG:
            return image.EncodeToJPG(95);
        case Format.EXR:
            return image.EncodeToEXR();
        default:
            return new byte[0];
        }
    }

    private ParallelOptions GetParallelOptions() {
        ParallelOptions options = new ParallelOptions();
        options.MaxDegreeOfParallelism = max_worker_threads_;
        return options;
    }

    // Prepare a flat list of work items.
    private List<ImageToSave> PrepareBatch(RenderBatch batch) {
        List<ImageToSave> flat_batch = new List<ImageToSave>();
        foreach (KeyValuePair<string, RenderBatch.CameraBatch> pair in batch.camera_batches_) {
            int offset = GetOffset(pair.Key);
            List<Texture2D> rgb_data = pair.Value.images_[RenderBatch.CameraBatch.RenderType.RGB];
            for (int i = 0; i < rgb_data.Count; ++i) {
                flat_batch.Add(new ImageToSave(rgb_data[i], GetSavePath(pair.Key, i + offset)));
            }
            // Move the offsets, naming for the next batch starts where the
            // previous ended.
            UpdateOffset(pair.Key, offset + rgb_data.Count);
        }
        return flat_batch;
    }

    private int GetOffset(string stream_name) {
        if (offsets_.ContainsKey(stream_name)) {
            return offsets_[stream_name];
        } else {
            return offset_;
        }
    }

    private void UpdateOffset(string stream_name, int offset) {
        offsets_[stream_name] = offset;
    }

    private string GetSavePath(string stream_name, int index) {
        return string.Format("{0}/{1}_{2:D7}.{3}", dir_, stream_name, index, GetFormat());
    }

    private string GetFormat() {
        switch (format_) {
        case Format.PNG:
            return "png";
        case Format.JPEG:
            return "jpeg";
        case Format.EXR:
            return "exr";
        default:
            return "error";
        }
    }
}
