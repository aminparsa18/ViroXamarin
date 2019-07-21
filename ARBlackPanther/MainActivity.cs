using System.Collections.Generic;
using System.IO;
using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using Java.Lang;
using ViroCore;

namespace ARBlackPanther
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : Activity, ViroViewARCore.IStartupListener, ARScene.IListener, IAsyncObject3DListener, Animation.IListener
    {
        private static string TAG = typeof(MainActivity).Name;
        protected ViroView mViroView;
        private ARScene mScene;
        public ARImageTarget mImageTarget;
        private Node mBlackPantherNode;
        private AssetManager mAssetManager;
        private Object3D mBlackPantherModel;

        private bool mObjLoaded = false;
        private bool mImageTargetFound;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            mViroView = new ViroViewARCore(this, this);
            SetContentView(mViroView);
        }

        public void OnFailure(ViroViewARCore.StartupError p0, string p1)
        {
            // Fail as you wish!
        }

        public void OnSuccess()
        {
            // Override this function to start building your scene here
            OnRenderCreate();
        }
        private void OnRenderCreate()
        {
            // Create the base ARScene
            mScene = new ARScene();

            // Create an ARImageTarget out of the Black Panther poster
            Bitmap blackPantherPoster = getBitmapFromAssets("logo.jpg");
            mImageTarget = new ARImageTarget(blackPantherPoster, ARImageTarget.Orientation.Up, 0.188f);
            mScene.AddARImageTarget(mImageTarget);

            // Create a Node containing the Black Panther model
            mBlackPantherNode = initBlackPantherNode();
            mBlackPantherNode.AddChildNode(initLightingNode());
            mScene.RootNode.AddChildNode(mBlackPantherNode);

            mViroView.Scene=mScene;
            TrackImageNodeTargets();
        }
        private Node initBlackPantherNode()
        {
            Node blackPantherNode = new Node();
            mBlackPantherModel = new Object3D();
            mBlackPantherModel.SetRotation(new Vector(Math.ToRadians(-90), 0, 0));
            mBlackPantherModel.SetScale(new Vector(0.2f, 0.2f, 0.2f));
            mBlackPantherModel.LoadModel(mViroView.ViroContext, Uri.Parse("file:///android_asset/blackpanther/object_bpanther_anim.vrx"), Object3D.Type.Fbx,this);

            mBlackPantherModel.Visible=false;
            blackPantherNode.AddChildNode(mBlackPantherModel);
            return blackPantherNode;
        }
        private Node initLightingNode()
        {
            Vector[] omniLightPositions ={new Vector(-3, 3, 0.3),
                new Vector(3, 3, 1),
                new Vector(-3,-3,1),
                new Vector(3, -3, 1)};

            Node lightingNode = new Node();
            foreach (Vector pos in omniLightPositions)
            {
                OmniLight light = new OmniLight
                {
                    Position = pos,
                    Color = Color.ParseColor("#FFFFFF"),
                    Intensity = 20,
                    AttenuationStartDistance = 6,
                    AttenuationEndDistance = 9
                };

                lightingNode.AddLight(light);
            }

            // The spotlight will cast the shadows
            Spotlight spotLight = new Spotlight();
            spotLight.Position=new Vector(0, 5, -0.5);
            spotLight.Color=(Color.ParseColor("#FFFFFF"));
            spotLight.Direction=(new Vector(0, -1, 0));
            spotLight.Intensity=(50);
            spotLight.ShadowOpacity=(0.4f);
            spotLight.ShadowMapSize=(2048);
            spotLight.ShadowNearZ=(2f);
            spotLight.ShadowFarZ=(7f);
            spotLight.InnerAngle=(5);
            spotLight.OuterAngle=(20);
            spotLight.CastsShadow=(true);

            lightingNode.AddLight(spotLight);

            // Add a lighting environment for realistic PBR rendering
            Texture environment = Texture.LoadRadianceHDRTexture(Uri.Parse("file:///android_asset/wakanda_360.hdr"));
            mScene.LightingEnvironment=(environment);

            // Add shadow planes: these are "invisible" surfaces on which virtual shadows will be cast,
            // simulating real-world shadows
            Material material = new Material();
            material.SetShadowMode(Material.ShadowMode.Transparent);

            Surface surface = new Surface(3, 3);
            surface.Materials=new List<Material>(){material};

            Node surfaceShadowNode = new Node();
            surfaceShadowNode.SetRotation(new Vector(Math.ToRadians(-90), 0, 0));
            surfaceShadowNode.Geometry=surface;
            surfaceShadowNode.SetPosition(new Vector(0, 0, 0.0));
            lightingNode.AddChildNode(surfaceShadowNode);

            lightingNode.SetRotation(new Vector(Math.ToRadians(-90), 0, 0));
            return lightingNode;
        }
        private Bitmap getBitmapFromAssets(string assetName)
        {
            if (mAssetManager == null)
            {
                mAssetManager = Resources.Assets;
            }

            Stream imageStream;
            try
            {
                imageStream = mAssetManager.Open(assetName);
            }
            catch (IOException exception)
            {
                Log.Warn("Viro", "Unable to find image [" + assetName + "] in assets! Error: "
                              + exception.Message);
                return null;
            }
            return BitmapFactory.DecodeStream(imageStream);
        }
        private void TrackImageNodeTargets()
        {
            mScene.SetListener(this);
        }

        public void OnObject3DFailed(string p0)
        {
            Log.Error(TAG, "Black Panther Object Failed to load.");
        }

        public void OnObject3DLoaded(Object3D p0, Object3D.Type p1)
        {
            mObjLoaded = true;
            StartPantherExperience();
        }

        public void OnAmbientLightUpdate(float p0, Vector p1)
        {
        }

        public void OnAnchorFound(ARAnchor p0, ARNode p1)
        {
            var anchorId = p0.AnchorId;
            if (mImageTarget.Id != anchorId)
            {
                return;
            }

            Vector anchorPos = p0.Position;
            Vector pos = new Vector(anchorPos.X, anchorPos.Y - 0.4, anchorPos.Z - 0.15);
            mBlackPantherNode.SetPosition(pos);
            mBlackPantherNode.SetRotation(p0.Rotation);
            mBlackPantherModel.Visible = true;
            mImageTargetFound = true;
            StartPantherExperience();
        }

        public void OnAnchorRemoved(ARAnchor p0, ARNode p1)
        {
            var anchorId = p0.AnchorId;
            if (mImageTarget.Id != anchorId)
            {
                return;
            }

            mBlackPantherNode.Visible = false;
        }

        public void OnAnchorUpdated(ARAnchor p0, ARNode p1)
        {

        }

        public void OnTrackingInitialized()
        {
        }

        public void OnTrackingUpdated(ARScene.TrackingState p0, ARScene.TrackingStateReason p1)
        {
          
        }
        private void StartPantherExperience()
        {
            if (!mObjLoaded || !mImageTargetFound)
            {
                return;
            }

            // Animate the black panther's jump animation
            Animation animationJump = mBlackPantherModel.GetAnimation("01");
            animationJump.Listener = this;
            animationJump.Play();
        }

        public void OnAnimationFinish(Animation p0, bool p1)
        {
            Animation animationIdle = mBlackPantherModel.GetAnimation("02");
            animationIdle.Play();
        }

        public void OnAnimationStart(Animation p0)
        {
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
    }
}