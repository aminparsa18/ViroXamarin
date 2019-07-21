using System;
using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using ViroCore;
using IOException = Java.IO.IOException;
using Math = Java.Lang.Math;


namespace ARHelloWorld
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ViroViewARCore.IStartupListener
    {
        private static readonly string Tag = typeof(MainActivity).Name;
        private ViroViewARCore mViroView;
        private ARScene mScene;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mViroView = new ViroViewARCore(this, this);
            SetContentView(mViroView);
        }

        private class ClickListener2 : Java.Lang.Object, IClickListener
        {
            private readonly MainActivity act;

            public ClickListener2(MainActivity act)
            {
                this.act = act;
            }

            public void OnClick(int var1, Node var2, Vector var3)
            {
                act.CreateDroidAtPosition(var3);
            }

            public void OnClickState(int p0, Node p1, ClickState p2, Vector p3)
            {
                //No op
            }
        }

        private void DisplayScene()
        {
            // Create the 3D AR scene, and display the point cloud
            mScene = new ARScene();
            mScene.DisplayPointCloud(true);

            // Create a TrackedPlanesController to visually display identified planes.
            var controller = new TrackedPlanesController(this, mViroView);

            // Spawn a 3D Droid on the position where the user has clicked on a tracked plane.
            controller.AddOnPlaneClickListener(new ClickListener2(this));

            mScene.SetListener(controller);

            // Add some lights to the scene; this will give the Android's some nice illumination.
            var rootNode = mScene.RootNode;
            var lightPositions = new List<Vector>
            {
                new Vector(-10, 10, 1), new Vector(10, 10, 1)
            };

            const float intensity = 300;
            var lightColors = new List<Color> {Color.White, Color.White};

            for (var i = 0; i < lightPositions.Count; i++)
            {
                var light = new OmniLight
                {
                    Color = lightColors[i],
                    Position = lightPositions[i],
                    AttenuationStartDistance = 20,
                    AttenuationEndDistance = 30,
                    Intensity = intensity
                };
                rootNode.AddLight(light);
            }

            //Add an HDR environment map to give the Android's more interesting ambient lighting.
            var environment =
                Texture.LoadRadianceHDRTexture(Android.Net.Uri.Parse("file:///android_asset/ibl_newport_loft.hdr"));
            mScene.LightingEnvironment = environment;

            mViroView.Scene = mScene;
        }

        private class AsyncObjectListener : Java.Lang.Object, IAsyncObject3DListener
        {
            private readonly Bitmap bot;

            public AsyncObjectListener(Bitmap bot)
            {
                this.bot = bot;
            }


            public void OnObject3DFailed(string var1)
            {
            }

            public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
            {
                var objectTexture = new Texture(bot, Texture.Format.Rgba8, false, false);
                var material = new Material {DiffuseTexture = objectTexture, Roughness = 0.23f, Metalness = 0.7f};

                // Give the material a more "metallic" appearance, so it reflects the environment map.
                // By setting its lighting model to PHYSICALLY_BASED, we enable PBR rendering on the
                // model.
                material.SetLightingModel(Material.LightingModel.PhysicallyBased);

                p0.Geometry.Materials = new List<Material>() {material};
            }
        }

/**
 * Create an Android object and have it appear at the given location.
 * @param position The location where the Android should appear.
 */
        private void CreateDroidAtPosition(Vector position)
        {
            // Create a droid on the surface
            var bot = GetBitmapFromAsset(this, "andy.png");
            var object3D = new Object3D();
            object3D.SetPosition(position);

            mScene.RootNode.AddChildNode(object3D);

            // Load the Android model asynchronously.
            object3D.LoadModel(mViroView.ViroContext, Android.Net.Uri.Parse("file:///android_asset/andy.obj"),
                Object3D.Type.Obj, new AsyncObjectListener(bot));
            // Make the object draggable.
            object3D.Drag += (s, e) => { };
            object3D.SetDragType(Node.DragType.FixedDistance);
        }

        /**
         * Tracks planes and renders a surface on them so the user can see where we've identified
         * planes.
         */
        public class TrackedPlanesController : Java.Lang.Object, ARScene.IListener
        {
            private readonly WeakReference mCurrentActivityWeak;
            private bool searchingForPlanesLayoutIsVisible = false;
            private readonly Dictionary<string, Node> surfaces = new Dictionary<string, Node>();
            private readonly HashSet<IClickListener> mPlaneClickListeners = new HashSet<IClickListener>();

            public TrackedPlanesController(Activity activity, View rootView)
            {
                mCurrentActivityWeak = new WeakReference(activity);
                // Inflate viro_view_hud.xml layout to display a "Searching for surfaces" text view.
                View.Inflate(activity, Resource.Layout.viro_view_hud, ((ViewGroup) rootView));
            }

            public void AddOnPlaneClickListener(IClickListener listener)
            {
                mPlaneClickListeners.Add(listener);
            }

            public void RemoveOnPlaneClickListener(IClickListener listener)
            {
                if (mPlaneClickListeners.Contains(listener))
                {
                    mPlaneClickListeners.Remove(listener);
                }
            }

/**
 * Once a Tracked plane is found, we can hide the our "Searching for Surfaces" UI.
 */
            private void HideIsTrackingLayoutUi()
            {
                if (searchingForPlanesLayoutIsVisible)
                {
                    return;
                }

                searchingForPlanesLayoutIsVisible = true;

                var activity = (Activity) mCurrentActivityWeak.Target;
                if (activity == null)
                {
                    return;
                }

                var isTrackingFrameLayout = activity.FindViewById(Resource.Id.viro_view_hud);
                isTrackingFrameLayout.Animate().Alpha(0.0f).SetDuration(2000);
            }

            public void OnAnchorFound(ARAnchor arAnchor, ARNode arNode)
            {
                // Spawn a visual plane if a PlaneAnchor was found
                if (arAnchor.GetType() == ARAnchor.Type.Plane)
                {
                    var planeAnchor = (ARPlaneAnchor) arAnchor;

                    // Create the visual geometry representing this plane
                    var dimensions = planeAnchor.Extent;
                    var plane = new ViroCore.Surface(1, 1) {Width = dimensions.X, Height = dimensions.Z};

                    // Set a default material for this plane.
                    var material = new Material {DiffuseColor = Color.ParseColor("#BF000000")};
                    plane.Materials = new List<Material>() {material};

                    // Attach it to the node
                    var planeNode = new Node {Geometry = plane};
                    planeNode.SetRotation(new Vector(-Math.ToRadians(90.0), 0, 0));
                    planeNode.SetPosition(planeAnchor.Center);

                    // Attach this planeNode to the anchor's arNode
                    arNode.AddChildNode(planeNode);
                    surfaces.Add(arAnchor.AnchorId, planeNode);

                    // Attach click listeners to be notified upon a plane onClick.
                    planeNode.Click += (s, e) =>
                    {
                        foreach (var listener in mPlaneClickListeners)
                        {
                            listener.OnClick(e.P0, e.P1, e.P2);
                        }
                    };
                    HideIsTrackingLayoutUi();
                }
            }


// Finally, hide isTracking UI if we haven't done so already.
            public void OnAnchorUpdated(ARAnchor arAnchor, ARNode arNode)
            {
                if (arAnchor.GetType() == ARAnchor.Type.Plane)
                {
                    var planeAnchor = (ARPlaneAnchor) arAnchor;

                    // Update the mesh surface geometry
                    var node = surfaces[arAnchor.AnchorId];
                    var plane = (ViroCore.Surface) node.Geometry;
                    var dimensions = planeAnchor.Extent;
                    plane.Width = dimensions.X;
                    plane.Height = dimensions.Z;
                }
            }

            public void OnAnchorRemoved(ARAnchor arAnchor, ARNode arNode)
            {
                surfaces.Remove(arAnchor.AnchorId);
            }

            public void OnAmbientLightUpdate(float p0, Vector p1)
            {
            }


            public void OnTrackingInitialized()
            {
                //No-op
            }

            public void OnTrackingUpdated(ARScene.TrackingState p0, ARScene.TrackingStateReason p1)
            {
            }
        }

        private static Bitmap GetBitmapFromAsset(Context context, string assetName)
        {
            var assetManager = context.Resources.Assets;
            Stream imageStream;
            try
            {
                imageStream = assetManager.Open(assetName);
            }
            catch (IOException exception)
            {
                Log.Warn(Tag, "Unable to find image [" + assetName + "] in assets! Error: "
                              + exception.Message);
                return null;
            }

            return BitmapFactory.DecodeStream(imageStream);
        }

        protected override void OnStart()
        {
            base.OnStart();
            mViroView.OnActivityStarted(this);
        }

        protected override void OnResume()
        {
            base.OnResume();
            mViroView.OnActivityResumed(this);
        }

        protected override void OnPause()
        {
            base.OnPause();
            mViroView.OnActivityPaused(this);
        }

        protected override void OnStop()
        {
            base.OnStop();
            mViroView.OnActivityStopped(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            mViroView.OnActivityDestroyed(this);
        }

        public void OnFailure(ViroViewARCore.StartupError p0, string p1)
        {
            Log.Error(Tag, "Error initializing AR [" + p1 + "]");
        }

        public void OnSuccess()
        {
            DisplayScene();
        }
    }
}