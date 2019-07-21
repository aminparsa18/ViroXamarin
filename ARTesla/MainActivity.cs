using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Util;
using Java.Lang;
using Java.Util;
using ViroCore;
using Vector = ViroCore.Vector;

namespace ARTesla
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ARScene.IListener, ViroViewARCore.IStartupListener, IAsyncObject3DListener
    {
        private static readonly string Tag = typeof(MainActivity).Name;
        private ViroView mViroView;
        private ARScene mScene;
        private Node mCarModelNode;
        private Node mColorChooserGroupNode;
        private Dictionary<string, KeyValuePair<ARImageTarget, Node>> mTargetedNodesMap;
        private readonly Dictionary<CarModel, Texture> mCarColorTextures = new Dictionary<CarModel, Texture>();

        private class CarModel
        {
            public string DiffuseSource { get; private set; }
            public Vector UiPickerColorSource { get; private set; }

            private CarModel(string carSrc, Vector pickerColorSrc)
            {
                DiffuseSource = carSrc;
                UiPickerColorSource = pickerColorSrc;
            }

            public static readonly CarModel White = new CarModel("object_car_main_Base_Color.png",
                new Vector(231, 231, 231));

            public static readonly CarModel Blue = new CarModel("object_car_main_Base_Color_blue.png",
                new Vector(19, 42, 143));

            public static readonly CarModel Grey = new CarModel("object_car_main_Base_Color_grey.png",
                new Vector(75, 76, 79));

            public static readonly CarModel Red = new CarModel("object_car_main_Base_Color_red.png",
                new Vector(168, 0, 0));

            public static readonly CarModel Yellow = new CarModel("object_car_main_Base_Color_yellow.png",
                new Vector(200, 142, 31));

            public static readonly List<CarModel> Values = new List<CarModel>()
            {
                White, Blue, Grey, Red, Yellow
            };
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mTargetedNodesMap = new Dictionary<string, KeyValuePair<ARImageTarget, Node>>();
            mViroView = new ViroViewARCore(this, this);
            SetContentView(mViroView);
        }

        private void OnRenderCreate()
        {
            // Create the base ARScene
            mScene = new ARScene();
            mScene.SetListener(this);
            mViroView.Scene = mScene;

            // Create an ARImageTarget for the Tesla logo
            var teslaLogoTargetBmp = GetBitmapFromAssets("logo.png");
            var teslaTarget = new ARImageTarget(teslaLogoTargetBmp, ARImageTarget.Orientation.Up, 0.188f);
            mScene.AddARImageTarget(teslaTarget);

            // Build the Tesla car Node and add it to the Scene. Set it to invisible: it will be made
            // visible when the ARImageTarget is found.
            var teslaNode = new Node();
            InitCarModel(teslaNode);
            InitColorPickerModels(teslaNode);
            InitSceneLights(teslaNode);
            teslaNode.Visible = false;
            mScene.RootNode.AddChildNode(teslaNode);

            // Link the Node with the ARImageTarget, such that when the image target is
            // found, we'll render the Node.
            LinkTargetWithNode(teslaTarget, teslaNode);
        }

        /*
         Link the given ARImageTarget with the provided Node. When the ARImageTarget is
         found in the scene (by onAnchorFound below), the Node will be made visible and
         the target's transformations will be applied to the Node, thereby rendering the
         Node over the target.
         */
        private void LinkTargetWithNode(ARImageTarget imageToDetect, Node nodeToRender)
        {
            var key = imageToDetect.Id;
            mTargetedNodesMap.Add(key, new KeyValuePair<ARImageTarget, Node>(imageToDetect, nodeToRender));
        }

        public void OnAmbientLightUpdate(float p0, Vector p1)
        {
        }

        public void OnAnchorFound(ARAnchor p0, ARNode p1)
        {
            var anchorId = p0.AnchorId;
            if (!mTargetedNodesMap.ContainsKey(anchorId))
            {
                return;
            }

            var imageTargetNode = mTargetedNodesMap[anchorId].Value;
            var rot = new Vector(0, p0.Rotation.Y, 0);
            imageTargetNode.SetPosition(p0.Position);
            imageTargetNode.SetRotation(rot);
            imageTargetNode.Visible = true;
            AnimateCarVisible(mCarModelNode);

            // Stop the node from moving in place once found
            var imgTarget = mTargetedNodesMap[anchorId].Key;
            mScene.RemoveARImageTarget(imgTarget);
            mTargetedNodesMap.Remove(anchorId);
        }

        public void OnAnchorRemoved(ARAnchor p0, ARNode p1)
        {
            var anchorId = p0.AnchorId;
            if (!mTargetedNodesMap.ContainsKey(anchorId))
            {
                return;
            }

            var imageTargetNode = mTargetedNodesMap[anchorId].Value;
            imageTargetNode.Visible = false;
        }

        public void OnAnchorUpdated(ARAnchor p0, ARNode p1)
        {
            //no op
        }

        public void OnTrackingInitialized()
        {
        }

        public void OnTrackingUpdated(ARScene.TrackingState p0, ARScene.TrackingStateReason p1)
        {
        }

        public void OnFailure(ViroViewARCore.StartupError p0, string p1)
        {
            Log.Error(Tag, "Error initializing AR [" + p1 + "]");
        }

        public void OnSuccess()
        {
            OnRenderCreate();
        }

        private void InitCarModel(Node groupNode)
        {
            // Creation of ObjectJni to the right
            var fbxCarNode = new Object3D();
            fbxCarNode.SetScale(new Vector(0.00f, 0.00f, 0.00f));
            fbxCarNode.LoadModel(mViroView.ViroContext, Uri.Parse("file:///android_asset/object_car.obj"),
                Object3D.Type.Obj, this);
            groupNode.AddChildNode(fbxCarNode);
            mCarModelNode = fbxCarNode;

            // Set click listeners.
            mCarModelNode.Click += (s, e) =>
            {
                var setVisibility = !mColorChooserGroupNode.Visible;
                mColorChooserGroupNode.Visible = setVisibility;
                AnimateColorPickerVisible(setVisibility, mColorChooserGroupNode);
            };
        }

        public void OnObject3DFailed(string p0)
        {
            Log.Error(Tag, "Car Model Failed to load.");
        }

        public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
        {
            PreloadCarColorTextures(p0);
        }

        /*
   Constructs a group of sphere color pickers and attaches them to the passed in group Node.
   These sphere pickers when click will change the diffuse texture of our tesla model.
   */
        private void InitColorPickerModels(Node groupNode)
        {
            mColorChooserGroupNode = new Node {TransformBehaviors = EnumSet.Of(Node.TransformBehavior.BillboardY)};
            mColorChooserGroupNode.SetPosition(new Vector(0, 0.25, 0));
            float[] pickerPositions = {-.2f, -.1f, 0f, .1f, .2f};
            var i = 0;

            // Loop through car color model colors
            foreach (var model in CarModel.Values)
            {
                // Create our sphere picker geometry
                var colorSphereNode = new Node();
                var posX = pickerPositions[i++];
                colorSphereNode.SetPosition(new Vector(posX, 0, 0));
                var colorSphere = new Sphere(0.03f);

                // Create sphere picker color that correlates to the car model's color
                var material = new Material();
                var c = model.UiPickerColorSource;
                material.DiffuseColor = Color.Rgb((int) c.X, (int) c.Y, (int) c.Z);
                material.SetLightingModel(Material.LightingModel.PhysicallyBased);

                // Finally, set the sphere's properties
                colorSphere.Materials = new List<Material>() {material};
                colorSphereNode.Geometry = colorSphere;
                colorSphereNode.ShadowCastingBitMask = 0;
                mColorChooserGroupNode.AddChildNode(colorSphereNode);

                // Set clickListener on spheres
                colorSphereNode.Click += (s, e) =>
                {
                    //mCarModelNode.getGeometry().setMaterials();
                    var texture = mCarColorTextures[model];
                    var mat = mCarModelNode.Geometry.Materials[0];
                    mat.DiffuseTexture = texture;
                    AnimateColorPickerClicked(colorSphereNode);
                };
            }

            mColorChooserGroupNode.SetScale(new Vector(0, 0, 0));
            mColorChooserGroupNode.Visible = false;
            groupNode.AddChildNode(mColorChooserGroupNode);
        }

        private void InitSceneLights(Node groupNode)
        {
            var rootLightNode = new Node();

            // Construct a spot light for shadows
            var spotLight = new Spotlight
            {
                Position = new Vector(0, 5, 0),
                Color = Color.ParseColor("#FFFFFF"),
                Direction = new Vector(0, -1, 0),
                Intensity = 300,
                InnerAngle = 5,
                OuterAngle = 25,
                ShadowMapSize = 2048,
                ShadowNearZ = 2,
                ShadowFarZ = 7,
                ShadowOpacity = .7f,
                CastsShadow = true
            };
            rootLightNode.AddLight(spotLight);

            // Add our shadow planes.
            var material = new Material();
            material.SetShadowMode(Material.ShadowMode.Transparent);
            var surface = new Surface(2, 2) {Materials = new List<Material>() {material}};
            var surfaceShadowNode = new Node();
            surfaceShadowNode.SetRotation(new Vector(Math.ToRadians(-90), 0, 0));
            surfaceShadowNode.Geometry = surface;
            surfaceShadowNode.SetPosition(new Vector(0, 0, -0.7));
            rootLightNode.AddChildNode(surfaceShadowNode);
            groupNode.AddChildNode(rootLightNode);

            var environment = Texture.LoadRadianceHDRTexture(Uri.Parse("file:///android_asset/garage_1k.hdr"));
            mScene.LightingEnvironment = environment;
        }

        private void PreloadCarColorTextures(Node node)
        {
            var metallicTexture = new Texture(GetBitmapFromAssets("object_car_main_Metallic.png"),
                Texture.Format.Rgba8, true, true);
            var roughnessTexture = new Texture(GetBitmapFromAssets("object_car_main_Roughness.png"),
                Texture.Format.Rgba8, true, true);

            var material = new Material();
            material.MetalnessMap = metallicTexture;
            material.RoughnessMap = roughnessTexture;
            material.SetLightingModel(Material.LightingModel.PhysicallyBased);
            node.Geometry.Materials = new List<Material>() {material};

            // Loop through color.
            foreach (var model in CarModel.Values)
            {
                var carBitmap = GetBitmapFromAssets(model.DiffuseSource);
                var carTexture = new Texture(carBitmap, Texture.Format.Rgba8, true, true);
                mCarColorTextures.Add(model, carTexture);

                // Preload our textures into the model
                material.DiffuseTexture = carTexture;
            }

            material.DiffuseTexture = mCarColorTextures[CarModel.White];
            return;
        }

        // +---------------------------------------------------------------------------+
        //  Image Loading
        // +---------------------------------------------------------------------------+

        private Bitmap GetBitmapFromAssets(string assetName)
        {
            Bitmap bitmap;
            try
            {
                var istr = Assets.Open(assetName);
                bitmap = BitmapFactory.DecodeStream(istr);
            }
            catch (IOException)
            {
                throw new IllegalArgumentException("Loading bitmap failed!");
            }

            return bitmap;
        }
        // +---------------------------------------------------------------------------+
        //  Animation Utilities
        // +---------------------------------------------------------------------------+

        private static void AnimateScale(Node node, long duration, Vector targetScale,
            AnimationTimingFunction fcn, Thread thread)
        {
            AnimationTransaction.Begin();
            AnimationTransaction.SetAnimationDuration(duration);
            AnimationTransaction.SetTimingFunction(fcn);
            node.SetScale(targetScale);
            if (thread != null)
            {
                AnimationTransaction.SetListener(new AnimationTransactionListener(thread));
            }

            AnimationTransaction.Commit();
        }

        private class AnimationTransactionListener : Java.Lang.Object, AnimationTransaction.IListener
        {
            private readonly Thread thread;

            public AnimationTransactionListener(Thread thread)
            {
                this.thread = thread;
            }

            public void OnFinish(AnimationTransaction p0)
            {
                thread.Start();
            }
        }

        private static void AnimateColorPickerVisible(bool isVisible, Node groupNode)
        {
            if (isVisible)
            {
                AnimateScale(groupNode, 500, new Vector(1, 1, 1), AnimationTimingFunction.Bounce, null);
            }
            else
            {
                AnimateScale(groupNode, 200, new Vector(0, 0, 0), AnimationTimingFunction.Bounce, null);
            }
        }

        private static void AnimateCarVisible(Node car)
        {
            AnimateScale(car, 500, new Vector(0.09f, 0.09f, 0.09f), AnimationTimingFunction.EaseInEaseOut, null);
        }

        private static void AnimateColorPickerClicked(Node picker)
        {
            AnimateScale(picker, 50, new Vector(0.8f, 0.8f, 0.8f), AnimationTimingFunction.EaseInEaseOut,
                new Thread(() =>
                    AnimateScale(picker, 50, new Vector(1, 1, 1), AnimationTimingFunction.EaseInEaseOut, null)));
        }
    }
}