# Renderer Components API

Renderer components are the key elements of ORRB framework. They are responsible for rendering randomizations,
augmentations and other custom scene transformations. Each component is a Unity `MonoBehavior` that inherits the
`RendererComponent` abstract class. The crucial parts of the interface are the following methods:

``` csharp

public abstract class RendererComponent : MonoBehaviour {

    public virtual bool InitializeComponent(Orrb.RendererComponentConfig config) { ... }

    public virtual bool UpdateComponent(Orrb.RendererComponentConfig config) { ... }

    public abstract bool RunComponent(IOutputContext context);

    // Truncated.

}
```

The `InitializeComponent` method is called when the component is configured for the first time.
It is responsible for setting up the newly instantiated object and reading the initial configuration
from the provided `Orrb.RenderComponentConfig` protocol buffer. It is expected that the heavy
initialization happens here. The default implementation just calls `UpdateComponent()` which might
be enough for more simple code.

The `UpdateComponent` method is called everytime the component properties are changed. This happens
on two occasions: when the component is initialized the first time, and everytime when the `Update`
RPC is called by ORRB client. This method might be called multiple times, and possibly quite frequently
in some applications (e.g. adaptation techniques where the renderer parameters are actually optimized),
that's why heavy operations should be added with care. One technique to follow this pattern of actually
veryfying which parameters have changed:

``` csharp
    int old_light_head_count = light_head_count_;

    ConfigUtils.GetProperties(this, config);

    if (old_light_head_count != light_head_count_) {
        // Slow operation that recreates scene lights, should only be executed
	// when the number of requested lights actually changed.
        BuildLightHeads();
    }
```

Finally, the `RunComponent` method is executed everytime a new state is rendered. It is important
that this method is quick and all redundant calculations are cached / precomputed. A `IOutputContext`
object is passed in the call. It can be used to produce auxiliary output streams, e.g. a randomizer
might emit the actual values that were randomly picked or a component might be used to output a side
training objective. One example of such behavior is the `Tracker` component that produces a stream of
screen space positions and bounding boxes:

``` csharp
public override bool RunComponent(IOutputContext context) {
    foreach (Camera tracking_camera in cameras_) {
        for (int i = 0; i < tracked_objects_.Count; ++i) {

            // Truncated.

            // Return the center of the object.
            string stream_name = string.Format("tracker_{0}_X_{1}", tracked_object_name, tracking_camera.name);
            Vector3 viewport_position = tracking_camera.WorldToViewportPoint(tracked_object.transform.position);
            context.OutputFloats(stream_name, new float[] { viewport_position.x, viewport_position.y });

            // Return the bounding box
            string stream_name_bbox = string.Format("tracker_{0}_X_{1}_bbox", tracked_object_name, tracking_camera.name);
            context.OutputFloats(stream_name_bbox, GetBounds2DViewPoint(tracking_camera, tracked_object));
        }
    }
    return true;
}
```

# Component Properties

Component properties are used to control how a renderer component behaves. The tweakable property framework in
ORRB is designed with the goal of reducting the boilerplate code responsible for getting/setting/copying
values from config files to config structures to the actual components. We use reflection and introspection to
automate those tedious pieces of code.

In order to mark a tweakable property use the `ConfigProperty` annotation on your member fields:

``` csharp
    [SerializeField]
    [ConfigProperty]
    public bool enable_color_grading_ = true;

    [SerializeField]
    [ConfigProperty]
    public float cg_min_hue_shift_ = -10.0f;

    [SerializeField]
    [ConfigProperty]
    public float cg_max_hue_shift_ = 10.0f;
```

`bool`, `int`, `float`, `string` and `enum` fields are supported. Then you can extract individual
values from the `Orrb.RendererComponentConfig` protos with specialized static methods provided by
the `ConfigUtils` helper class:

``` csharp
public class ConfigUtils {

    public static float GetProperty(string name, Orrb.RendererComponentConfig config, float default_value);

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, float value);

    public static int GetProperty(string name, Orrb.RendererComponentConfig config, int default_value);

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, int value);

    public static string GetProperty(string name, Orrb.RendererComponentConfig config, string default_value);

    public static void SetProperty(string name, Orrb.RendererComponentConfig config, string value);

    // Truncated.
}
```

or you can extract all the values and populate all the annotated fields at once using:

``` csharp
public class ConfigUtils {
    public static void GetProperties(object subject, Orrb.RendererComponentConfig config);
}
```

where the `subject` is the `RendererComponent` to be populated.

The actual `Orrb.RendererComponentConfig` proto strives to be both human readable and easy to maintain.
It contains `string` to value mappings for different types and named parameters can be added to configurations
without changing the proto. This of course is a little slower than using hardcoded wires for different
parameters but seems a good compromise that gives us enough generality. This is an example of a component
config containing some component properties of different types:

``` json
{
   "config":{
      "enumProperties":{
         "mode":"Jitter"
      },
      "floatProperties":{
         "fov_radius":5,
         "position_radius":0.1,
         "quat_radius":0.1
      },
      "vector3Properties":{
         "orbit_center":{}
      }
   },
   "name":"camera_randomizer_0",
   "type":"CameraRandomizer"
},
```
