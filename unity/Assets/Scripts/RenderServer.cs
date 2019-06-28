using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;

using StreamEntry = Orrb.RenderBatchResponse.Types.StreamEntry;
using BatchResponseEntry = Orrb.RenderBatchResponse.Types.StreamEntry.Types.BatchResponseEntry;
using Google.Protobuf;
using System.Threading;
using System.IO;
using System;

// The RenderServer starts a GRPC service and processes incoming
// RenderBatch and UpdateRenderer requests. The GRPC servers are
// inherently asynchronous and the Unity game loop is embarassingly
// serial. To join those two worlds the GRPC server communicates with
// the game loop over a concurrent queue. The incoming requests wait
// on enqueued workloads. The game loop processes the workloads in
// a serial fashion and then fulfills a Response promise. The queue
// will spin for a little while and then use user space conditional
// variables, in order to reduce context switching and provide highest
// performance in high load scenarios. When idle the queue will wait
// on the conditional variable with high timeout, reducing the idle
// load significantly.
//
// Configurable flags:
//   int queues_count - GRPC completion queue count,
//   int workers_count - GRPC worker threads initial count,
//   int streams_count - GRPC concurrent streams max count,
//   int port - port to bind the render service to,
//   string host - host to bind the render service to.

public class RenderServer : MonoBehaviour, IImageBatchConsumer {

    private interface IRenderServerWorkload {
        void InitializeWorkload();

        // One frame of processing.
        void ProcessWorkload();

        // Has this workload finished?
        bool WorkloadDone();
    }

    private class QueuedWorkloadRequest<Request, Response> where Request : class {
        public RenderServer server_ = null;

        // The GRPC server hangs on this promise, signal it when the work
        // is done and the response can be sent back.
        public TaskCompletionSource<Response> response_promise_ = new TaskCompletionSource<Response>();
        public Request request_ = null;

        public QueuedWorkloadRequest(RenderServer server, Request request) {
            server_ = server;
            request_ = request;
        }
    }

    // This structure gathers auxiliary outputs. It assumes that the per
    // frame output from a given stream will have constant size. It also
    // assumes that all frames generate the same output streams.
    private class BatchOutputContext : RendererComponent.IOutputContext {
        private int batch_size_ = 0;
        private int current_entry_ = 0;

        private class OutputStreams<T> where T : struct {
            private Dictionary<string, T[]> streams_ = new Dictionary<string, T[]>();

            public Dictionary<string, T[]> GetStreams() {
                return streams_;
            }

            private bool EnsureStream(string stream_name, int stream_size, int entry, int entry_length) {
                if (streams_.ContainsKey(stream_name)) {
                    T[] stream = streams_[stream_name];
                    if (stream.Length != stream_size * entry_length) {
                        Logger.Error("RenderServer::BatchOutputContext::EnsureStream::Wrong size, {0} vs. {1}x{2}.",
                                     stream.Length, stream_size, entry_length);
                        return false;
                    }
                    return true;
                } else if (entry == 0) {
                    streams_.Add(stream_name, new T[stream_size * entry_length]);
                    return true;
                } else {
                    Logger.Error("RenderServer::BatchOutputContext::EnsureStream::Entry not 0, in initialize.");
                    return false;
                }
            }

            public bool Output(string stream_name, int stream_size, int entry, T value) {
                if (!EnsureStream(stream_name, stream_size, entry, 1)) {
                    Logger.Error("RenderServer::BatchOutputContext::Output::Cannot output to stream: {0}", stream_name);
                    return false;
                }
                streams_[stream_name][entry] = value;
                return true;
            }

            public bool Output(string stream_name, int stream_size, int entry, T[] values) {
                if (!EnsureStream(stream_name, stream_size, entry, values.Length)) {
                    Logger.Error("RenderServer::BatchOutputContext::Output::Cannot output to stream: {0}", stream_name);
                    return false;
                }
                Array.Copy(values, 0, streams_[stream_name], values.Length * entry, values.Length);
                return true;
            }
        };

        private OutputStreams<int> int_streams_ = null;
        private OutputStreams<float> float_streams_ = null;
        private OutputStreams<bool> bool_streams_ = null;

        public BatchOutputContext(int batch_size) {
            this.batch_size_ = batch_size;
            this.int_streams_ = new OutputStreams<int>();
            this.float_streams_ = new OutputStreams<float>();
            this.bool_streams_ = new OutputStreams<bool>();
        }

        public void Advance() {
            this.current_entry_++;
        }

        public void OutputBool(string output_name, bool value) {
            bool_streams_.Output(output_name, batch_size_, current_entry_, value);
        }

        public void OutputBools(string output_name, bool[] values) {
            bool_streams_.Output(output_name, batch_size_, current_entry_, values);
        }

        public void OutputFloat(string output_name, float value) {
            float_streams_.Output(output_name, batch_size_, current_entry_, value);
        }

        public void OutputFloats(string output_name, float[] values) {
            float_streams_.Output(output_name, batch_size_, current_entry_, values);
        }

        public void OutputInt(string output_name, int value) {
            int_streams_.Output(output_name, batch_size_, current_entry_, value);
        }

        public void OutputInts(string output_name, int[] values) {
            int_streams_.Output(output_name, batch_size_, current_entry_, values);
        }

        public void BuildResponseStreams(Orrb.RenderBatchResponse response) {
            foreach (KeyValuePair<string, bool[]> bool_stream in bool_streams_.GetStreams()) {
                Orrb.RenderBatchResponse.Types.AuxiliaryBoolStreamEntry response_bool_stream =
                        new Orrb.RenderBatchResponse.Types.AuxiliaryBoolStreamEntry();
                response_bool_stream.Name = bool_stream.Key;
                response_bool_stream.Data.AddRange(bool_stream.Value);
                response.AuxiliaryBoolStreams.Add(response_bool_stream);
            }

            foreach (KeyValuePair<string, int[]> int_stream in int_streams_.GetStreams()) {
                Orrb.RenderBatchResponse.Types.AuxiliaryIntStreamEntry response_int_stream =
                        new Orrb.RenderBatchResponse.Types.AuxiliaryIntStreamEntry();
                response_int_stream.Name = int_stream.Key;
                response_int_stream.Data.AddRange(int_stream.Value);
                response.AuxiliaryIntStreams.Add(response_int_stream);
            }

            foreach (KeyValuePair<string, float[]> float_stream in float_streams_.GetStreams()) {
                Orrb.RenderBatchResponse.Types.AuxiliaryFloatStreamEntry response_float_stream =
                        new Orrb.RenderBatchResponse.Types.AuxiliaryFloatStreamEntry();
                response_float_stream.Name = float_stream.Key;
                response_float_stream.Data.AddRange(float_stream.Value);
                response.AuxiliaryFloatStreams.Add(response_float_stream);
            }
        }
    }

    // This workload runs the work triggered by a RenderBatch RPC.
    private class RenderBatchWorkload : QueuedWorkloadRequest<Orrb.RenderBatchRequest, Orrb.RenderBatchResponse>, IRenderServerWorkload, IImageBatchConsumer {

        private float start_time_ = 0.0f;
        private int current_batch_entry_ = 0;
        private BatchOutputContext output_context_ = null;
        private bool done_ = false;

        public RenderBatchWorkload(RenderServer server, Orrb.RenderBatchRequest request) : base(server, request) { }

        public void InitializeWorkload() {
            Logger.Info("RenderBatchWorkload::InitializeWorkload::New render request.");
            start_time_ = Time.realtimeSinceStartup;
            output_context_ = new BatchOutputContext(request_.Entries.Count);
            current_batch_entry_ = 0;

            List<Camera> cameras = server_.scene_instance_.GetCameras(request_.CameraNames);

            // Make sure we can find all the requested cameras in the scene.
            if (cameras.Count != request_.CameraNames.Count) {
                Logger.Error("RenderServer::RenderBatchWorkload::InitializeWorkload::Cannot find all requested cameras.");
                done_ = true;
                return;
            }

            // Prepare the recorder, so that it has buffers ready.
            server_.recorder_.ResetBatch(cameras, request_.Entries.Count, request_.Width, request_.Height,
                                         request_.RenderAlpha, request_.RenderDepth, request_.RenderNormals,
                                         request_.RenderSegmentation);
        }

        // Render one state (frame).
        public void ProcessWorkload() {
            int seed = request_.BatchSeed + current_batch_entry_;
            if (request_.UseEntrySeeds) {
                seed = request_.Entries[current_batch_entry_].Seed;
            }
            UnityEngine.Random.InitState(seed);
            server_.scene_instance_.UpdateState(request_.Entries[current_batch_entry_].Qpos);
            server_.scene_instance_.GetComponentManager().RunComponents(output_context_);
            output_context_.Advance();
            server_.recorder_.Capture();
            current_batch_entry_++;
        }

        public bool WorkloadDone() {
            return done_;
        }

        // The RenderBatchWorkload is also an ImageBatchConsumer, when the
        // Recorder is done it will send the batch here (through the RenderServer).
        public void ConsumeImageBatch(RenderBatch batch) {

            Orrb.RenderBatchResponse response = new Orrb.RenderBatchResponse();

            // Build the GRPC response from the recorded images...
            int frames = 0;
            foreach (KeyValuePair<string, RenderBatch.CameraBatch> pair in batch.camera_batches_) {
                Tuple<int, StreamEntry> stream_info = StreamFromBatch(pair.Key, pair.Value);
                response.Streams.Add(stream_info.Item2);
                frames += stream_info.Item1;
            }

            // ... and the auxiliary outputs.
            output_context_.BuildResponseStreams(response);

            float delta_time = Time.realtimeSinceStartup - start_time_;
            response_promise_.SetResult(response);
            Logger.Info("RenderBatchWorkload::ConsumeImageBatch::Batch finished: {0} images in {1} ({2}).",
                        frames, delta_time, frames / delta_time);

            done_ = true;
        }

        private static Tuple<int, StreamEntry> StreamFromBatch(string name, RenderBatch.CameraBatch batch_stream) {
            StreamEntry stream = new StreamEntry();
            stream.Name = name;

            int count = 0;
            foreach (KeyValuePair<RenderBatch.CameraBatch.RenderType, List<Texture2D>> pair in batch_stream.images_) {
                int i = 0;
                foreach (Texture2D image in pair.Value) {
                    BatchResponseEntry entry;
                    if (i + 1 > stream.Entries.Count) {
                        entry = new BatchResponseEntry();
                        stream.Entries.Add(entry);
                    } else {
                        entry = stream.Entries[i];
                    }
                    ++i;

                    ByteString data = null;
                    switch (pair.Key) {
                        case RenderBatch.CameraBatch.RenderType.RGB:
                            data = ByteString.CopyFrom(image.GetRawTextureData());
                            entry.ImageData = data;
                            break;
                        case RenderBatch.CameraBatch.RenderType.DEPTH:
                            entry.DepthData = ReadDepth(image);
                            break;
                        case RenderBatch.CameraBatch.RenderType.NORMALS:
                            entry.NormalsData = ReadNormals(image);
                            break;
                        case RenderBatch.CameraBatch.RenderType.SEGMENTATION:
                            entry.SegmentationData = ReadSegmentation(image);
                            break;
                        default:
                            Logger.Error("Unsupported Batch.Stream.Type {0}", pair.Key);
                            break;
                    }
                }
                count = i;
            }

            Tuple<int, StreamEntry> stream_info = new Tuple<int, StreamEntry>(count, stream);
            return stream_info;
        }

        private static ByteString ReadDepth(Texture2D texture) {
            // Read depth from RGBAFloat texture where its stored in R channel
            int size = texture.width * texture.height;
            byte[] depth_array = new byte[size * 4];  // float32, so 4 bytes
            byte[] texture_array = texture.GetRawTextureData();
            for (int i = 0; i < size; ++i) {
                for (int j = 0; j < 4; ++j) {
                    depth_array[i * 4 + j] = texture_array[i * 16 + j];
                }
            }
            return ByteString.CopyFrom(depth_array);
        }

        private static ByteString ReadNormals(Texture2D texture) {
            // Read surface normals from RGBAFloat texture where they're stored in GBA channels
            int size = texture.width * texture.height;
            byte[] normals_array = new byte[size * 3 * 4];  // 3 times float32, so 3 * 4 bytes
            byte[] texture_array = texture.GetRawTextureData();
            for (int i = 0; i < size; ++i) {
                for (int j = 0; j < 12; ++j) {
                    // offset by 4 since first channel is depth
                    normals_array[i * 12 + j] = texture_array[i * 16 + 4 + j];
                }
            }
            return ByteString.CopyFrom(normals_array);
        }

        private static ByteString ReadSegmentation(Texture2D texture) {
            int size = texture.width * texture.height;
            byte[] segmentation_array = new byte[size];
            byte[] texture_array = texture.GetRawTextureData();
            for (int i = 0; i < size; ++i) {
                segmentation_array[i] = texture_array[i * 3]; // 3 RGB channels
            }
            return ByteString.CopyFrom(segmentation_array);
        }
    }

    // This instant workload updates the RenderComponentConfigs of
    // Components in the ComponentManager.
    private class UpdateWorkload : QueuedWorkloadRequest<Orrb.UpdateRequest, Orrb.UpdateResponse>, IRenderServerWorkload {

        public UpdateWorkload(RenderServer server, Orrb.UpdateRequest request) : base(server, request) { }

        public void InitializeWorkload() { }

        public void ProcessWorkload() {
            ComponentManager manager = server_.scene_instance_.GetComponentManager();
            foreach (Orrb.RendererComponent config in request_.Components) {
                manager.UpdateComponent(config.Name, config.Config);
            }
            JsonFormatter formatter = new JsonFormatter(JsonFormatter.Settings.Default);
            Logger.Info("UpdateWorkload::ProcessWorkload::New config after update: {0}", formatter.Format(manager.GetConfig()));
            response_promise_.SetResult(new Orrb.UpdateResponse());
        }

        public bool WorkloadDone() {
            return true;
        }
    }

    // GRPC RenderService implementation, just a proxy that that delegates
    // to the parent RenderServer class.
    private class RenderServiceImpl : Orrb.RenderService.RenderServiceBase {

        private RenderServer server_ = null;

        public RenderServiceImpl(RenderServer server) {
            server_ = server;
        }

        public override Task<Orrb.RenderBatchResponse> RenderBatch(Orrb.RenderBatchRequest request, ServerCallContext context) {
            RenderBatchWorkload workload = new RenderBatchWorkload(server_, request);
            server_.EnqueueWorkload(workload);
            return workload.response_promise_.Task;
        }

        public override Task<Orrb.UpdateResponse> Update(Orrb.UpdateRequest request, ServerCallContext context) {
            UpdateWorkload workload = new UpdateWorkload(server_, request);
            server_.EnqueueWorkload(workload);
            return workload.response_promise_.Task;
        }
    }

    [SerializeField]
    [Flag]
    public int queues_count_ = 4;

    [SerializeField]
    [Flag]
    public int workers_count_ = 4;

    [SerializeField]
    [Flag]
    public int streams_count_ = 4;

    [SerializeField]
    [Flag]
    public int port_ = 6666;

    [SerializeField]
    [Flag]
    public string host_ = "[::]";

    private Recorder recorder_ = null;
    private SceneInstance scene_instance_ = null;

    private RenderServiceImpl render_service_ = null;
    private Server server_ = null;
    private IRenderServerWorkload current_workload_ = null;
    private Queue<IRenderServerWorkload> queue_ = new Queue<IRenderServerWorkload>();

    // Use this for initialization
    void Start() {
        Flags.InitFlags(this, "render_server");
    }

    public bool Initialize(Recorder recorder, SceneInstance scene_instance) {
        recorder_ = recorder;
        scene_instance_ = scene_instance;

        GrpcEnvironment.SetThreadPoolSize(workers_count_);
        GrpcEnvironment.SetCompletionQueueCount(queues_count_);

        render_service_ = new RenderServiceImpl(this);

        // Port reuse is turned off, it was a frequent cause of hard
        // to debug complications.
        server_ = new Server(
            new[] {
                new ChannelOption(ChannelOptions.SoReuseport, 0),
                new ChannelOption(ChannelOptions.MaxConcurrentStreams, streams_count_)
            }) {
            Services = { Orrb.RenderService.BindService(render_service_) },
            Ports = { new ServerPort(host_, port_, ServerCredentials.Insecure) }
        };

        try {
            server_.Start();
        } catch (IOException e) {
            Logger.Error("RenderServer::Initialize::Failed to initialize render server on: {0}:{1} ({2})",
                         host_, port_, e.Message);
            return false;
        }

        Logger.Info("RenderServer::Initialize::Initialized render server on: {0}:{1}", host_, port_);
        return true;
    }

    // Main server loop. Process current workload, or if it is done
    // try to get a next one.
    public void ProcessRequests() {
        if (current_workload_ != null) {
            ProcessCurrentWorkload();
        } else {
            IRenderServerWorkload next_workload = GetNextWorkload();
            if (next_workload != null) {
                InitializeNewWorkload(next_workload);
                ProcessCurrentWorkload();
            }
        }
    }

    // The GRPC service will use this to enqueue and notify the main
    // loop of new incoming work.
    private void EnqueueWorkload(IRenderServerWorkload workload) {
        lock (queue_) {
            queue_.Enqueue(workload);
            Monitor.Pulse(queue_);
        }
    }

    // The main loop will use this to pull a new workload from the queue,
    // or wait (in a blocking fashion) till next one comes.
    private IRenderServerWorkload GetNextWorkload() {
        lock (queue_) {
            int retries = 10;
            while (retries-- > 0) {
                if (queue_.Count > 0) {
                    return queue_.Dequeue();
                }
                // Yield on the queue, up to 100ms, this reduces idle
                // load when no work is pending.
                Monitor.Wait(queue_, 100);
            }
        }
        return null;
    }

    private void InitializeNewWorkload(IRenderServerWorkload new_workload) {
        current_workload_ = new_workload;
        current_workload_.InitializeWorkload();
    }

    private void ProcessCurrentWorkload() {
        current_workload_.ProcessWorkload();
        if (current_workload_.WorkloadDone()) {
            current_workload_ = null;
        }
    }

    // The RenderServer is a ImageBatchConsumer, when the Recorder is done
    // it will send a batch here. Pass it to the current workload if it is
    // a ImageBatchConsumer too.
    public void ConsumeImageBatch(RenderBatch batch) {
        if (current_workload_ != null && current_workload_ is IImageBatchConsumer) {
            (current_workload_ as IImageBatchConsumer).ConsumeImageBatch(batch);
            if (current_workload_.WorkloadDone()) {
                current_workload_ = null;
            }
        } else {
            Logger.Warning("RenderServer::ConsumeImageBatch::Unexpected image batch consume call.");
        }
    }
}
